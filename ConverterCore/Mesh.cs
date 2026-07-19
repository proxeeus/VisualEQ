using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using MoreLinq;
using VisualEQ.Common;
using VisualEQ.LegacyFileReader;
using static System.Console;

namespace VisualEQ.ConverterCore
{
    public class MeshPiece
    {
        public readonly List<Vector3> Vertices, Normals;
        public readonly List<Vector2> TexCoords;
        public readonly List<(bool, uint, uint, uint)> Polygons;
        public readonly List<(uint Flags, uint AnimSpeed, List<string> Filenames)> Textures;
        // Parallel to Textures — the Fragment30 name for each material, used to detect
        // water/lava/slime regions by classic EQ convention (e.g. names starting with "WT_").
        // Empty string when the material has no resolvable name. Kept separate from the main
        // Textures tuple so extending the tuple doesn't ripple through the mesh dedup key
        // and cause a bake regression on unrelated materials.
        public readonly List<string> MaterialNames;
        public readonly List<(uint Count, uint Index)> PolyTexs;

        public MeshPiece(Fragment36 meshfrag)
        {
            Vertices = meshfrag.Vertices.ToList();
            Normals = meshfrag.Normals.ToList();
            TexCoords = meshfrag.TexCoords.ToList();
            Polygons = meshfrag.Polygons.ToList();
            var textureRefs = meshfrag.TextureListReference.Value.References;
            var textures = new List<(uint Flags, uint AnimSpeed, List<string> Filenames)>();
            var materialNames = new List<string>();
            foreach (var x in textureRefs)
            {
                // Fragment30 → Fragment05 → Fragment04 → Fragment03 chain. Any link
                // can fail: freportn (and other zones) reference Fragment35 in the
                // texture list, which the WLD reader treats as ignored, and downstream
                // dereferences resolve to null. If we `continue` here the Textures list
                // shrinks but PolyTexs keeps the original texture indices — Mesh.Bake
                // then either grabs the wrong material or throws KeyNotFoundException
                // when the stale index falls past the end of the shortened list.
                // Instead, inject a placeholder so texture indices stay aligned.
                var sr1 = x.Value;
                var sr2 = sr1?.Reference.Value;
                var sr  = sr2?.Reference.Value;
                if (sr == null || sr.References == null)
                {
                    textures.Add((0u, 0u, new List<string>()));
                    materialNames.Add(x.Name ?? "");
                    continue;
                }
                var fns = sr.References
                    .Where(y => y?.Value?.Filenames != null)
                    .Select(y => y.Value.Filenames.ToList())
                    .SelectMany(y => y)
                    .ToList();
                textures.Add((sr1.Flags, sr.FrameTime, fns));
                materialNames.Add(x.Name ?? "");
            }
            Textures = textures;
            MaterialNames = materialNames;
            PolyTexs = meshfrag.PolyTexs.ToList();
        }
    }

    public class Mesh
    {
        readonly List<MeshPiece> Pieces = new List<MeshPiece>();

        public void Add(MeshPiece piece) => Pieces.Add(piece);

        // Scan pieces for materials whose names match classic EQ liquid-region naming
        // conventions ("WT_" water, "LA_" lava, "SL_" slime — case insensitive) and
        // aggregate an axis-aligned bounding box per unique region name across every
        // polygon that uses that material. Names are returned in DB-space coords —
        // mesh vertices come out of the WLD in EQ world space, no swap needed.
        //
        // Returns a list of (regionName, kind, min, max). Empty when the zone has no
        // liquid materials. Multiple pieces (e.g. big zone mesh + object meshes) can
        // contribute to the same region name — their vertices are merged into a single
        // AABB.
        //
        // Heuristic is deliberately name-based: WLD material flags carry translucency
        // info but no reliable water bit across client versions. A texture-filename
        // fallback (e.g. "wat*.bmp") would fire on non-water uses of the same texture
        // (fountains, splashes, effects), so we don't do that here — a name-prefix
        // convention is the tightest signal we can get without full BSP-region parsing.
        public List<(string Name, byte Kind, Vector3 Min, Vector3 Max)> DetectLiquidRegions()
        {
            var boxes = new Dictionary<string, (byte Kind, Vector3 Min, Vector3 Max)>(
                System.StringComparer.OrdinalIgnoreCase);

            foreach (var piece in Pieces)
            {
                var pi = 0;
                foreach (var (ptc, ti) in piece.PolyTexs)
                {
                    var idx = (int)ti;
                    var matName = idx < piece.MaterialNames.Count ? piece.MaterialNames[idx] : "";
                    var texName = idx < piece.Textures.Count
                                  && piece.Textures[idx].Filenames != null
                                  && piece.Textures[idx].Filenames.Count > 0
                                    ? piece.Textures[idx].Filenames[0]
                                    : "";
                    var kind = ClassifyLiquid(matName, texName);
                    if (kind == 0xFF) { pi += (int)ptc; continue; }

                    // Aggregate under the material name if we have one, otherwise the
                    // texture filename — auto-named "M####" materials that share a water
                    // texture with a named one should keep their AABBs separate (they can
                    // be different physical water bodies), but still get grouped stably.
                    var regionKey = !string.IsNullOrEmpty(matName) ? matName : texName;
                    foreach (var (_collidable, a, b, c) in piece.Polygons.Skip(pi).Take((int)ptc))
                    {
                        AccumulateVertex(boxes, regionKey, kind, piece.Vertices[(int)a]);
                        AccumulateVertex(boxes, regionKey, kind, piece.Vertices[(int)b]);
                        AccumulateVertex(boxes, regionKey, kind, piece.Vertices[(int)c]);
                    }
                    pi += (int)ptc;
                }
            }

            var result = new List<(string, byte, Vector3, Vector3)>();
            foreach (var kv in boxes)
                result.Add((kv.Key, kv.Value.Kind, kv.Value.Min, kv.Value.Max));
            return result;
        }

        // Returns OESRegion.KindWater/KindLava/KindSlime for a matching material, or
        // 0xFF (sentinel) when neither name nor texture look like a liquid.
        //
        // Detection now runs TWO passes:
        //   1. Material name — SOE/Verant convention: W<n>_..., OW<n>_..., UW<n>_...,
        //      LAVA<n>_..., SLIME<n>_...
        //   2. Texture filename — same-texture-different-name is common: classic EQ zones
        //      often have named water like "OW1_MDF" alongside auto-generated "M0007_MDF"
        //      that share the same "ow1.bmp" texture. The name-only check misses the
        //      auto-generated one, leaving half the ocean undetected (spotted in freporte).
        //      Texture patterns: ow*.bmp / uw*.bmp / w<n>.bmp / wat*.bmp for water,
        //      lava*.bmp for lava, slime*.bmp for slime.
        //
        // Naming patterns verified against erudsxing (ocean), oasis (river),
        // freporte (harbor), and soldunga (lava pool).
        static byte ClassifyLiquid(string materialName, string textureName)
        {
            if (!string.IsNullOrEmpty(materialName))
            {
                if (IsWaterName(materialName)) return OESRegion.KindWater;
                if (IsLavaName(materialName))  return OESRegion.KindLava;
                if (IsSlimeName(materialName)) return OESRegion.KindSlime;
            }
            if (!string.IsNullOrEmpty(textureName))
            {
                if (IsWaterTexture(textureName)) return OESRegion.KindWater;
                if (IsLavaTexture(textureName))  return OESRegion.KindLava;
                if (IsSlimeTexture(textureName)) return OESRegion.KindSlime;
            }
            return 0xFF;
        }

        static bool IsWaterName(string n)
        {
            // W<digit>_..., OW<digit>_..., UW<digit>_...
            int i = 0;
            if (n.Length > i + 1 && (n[i] == 'O' || n[i] == 'o' || n[i] == 'U' || n[i] == 'u')) i++;
            if (n.Length <= i + 1 || (n[i] != 'W' && n[i] != 'w')) return false;
            i++;
            if (!char.IsDigit(n[i])) return false;
            return true;
        }

        static bool IsLavaName(string n) =>
            // "LAVA" followed by a digit — excludes LAVAFALL, UNDERLAVA (LAVAFALL starts LAVAF,
            // UNDERLAVA doesn't start with LAVA at all).
            n.Length >= 5 &&
            (n[0] == 'L' || n[0] == 'l') &&
            (n[1] == 'A' || n[1] == 'a') &&
            (n[2] == 'V' || n[2] == 'v') &&
            (n[3] == 'A' || n[3] == 'a') &&
            char.IsDigit(n[4]);

        static bool IsSlimeName(string n) =>
            n.Length >= 6 &&
            (n[0] == 'S' || n[0] == 's') &&
            (n[1] == 'L' || n[1] == 'l') &&
            (n[2] == 'I' || n[2] == 'i') &&
            (n[3] == 'M' || n[3] == 'm') &&
            (n[4] == 'E' || n[4] == 'e') &&
            char.IsDigit(n[5]);

        // Extract the leading identifier chunk before the first dot to sidestep
        // extension case (.BMP vs .bmp) and directory prefixes.
        static string TexStem(string texName)
        {
            var dot = texName.IndexOf('.');
            return dot < 0 ? texName : texName.Substring(0, dot);
        }

        static bool IsWaterTexture(string texName)
        {
            var stem = TexStem(texName);
            if (stem.Length == 0) return false;
            // ow<digits>, uw<digits>, w<digits>, wat<anything>
            if (StartsWithI(stem, "ow") || StartsWithI(stem, "uw"))
                return stem.Length > 2 && char.IsDigit(stem[2]);
            if (StartsWithI(stem, "wat")) return true;
            if ((stem[0] == 'w' || stem[0] == 'W') && stem.Length > 1 && char.IsDigit(stem[1]))
                return true;
            return false;
        }

        // Require a digit right after "lava" / "slime" in texture names too — otherwise
        // "lavafall1.bmp" (vertical falls, not a horizontal snap target) matches lava.
        static bool IsLavaTexture(string texName)
        {
            var stem = TexStem(texName);
            return stem.Length > 4 && StartsWithI(stem, "lava") && char.IsDigit(stem[4]);
        }

        static bool IsSlimeTexture(string texName)
        {
            var stem = TexStem(texName);
            return stem.Length > 5 && StartsWithI(stem, "slime") && char.IsDigit(stem[5]);
        }

        static bool StartsWithI(string s, string prefix) =>
            s.StartsWith(prefix, System.StringComparison.OrdinalIgnoreCase);

        static void AccumulateVertex(
            Dictionary<string, (byte Kind, Vector3 Min, Vector3 Max)> boxes,
            string name, byte kind, Vector3 v)
        {
            if (!boxes.TryGetValue(name, out var box))
            {
                boxes[name] = (kind, v, v);
                return;
            }
            box.Min = new Vector3(
                System.MathF.Min(box.Min.X, v.X),
                System.MathF.Min(box.Min.Y, v.Y),
                System.MathF.Min(box.Min.Z, v.Z));
            box.Max = new Vector3(
                System.MathF.Max(box.Max.X, v.X),
                System.MathF.Max(box.Max.Y, v.Y),
                System.MathF.Max(box.Max.Z, v.Z));
            boxes[name] = box;
        }

        public List<(float[] VertexBuffer, uint[] IndexBuffer, bool Collidable, (uint Flags, uint AnimSpeed, List<string> Filenames) Texture)>
            Bake()
        {
            var verts = new List<Vector3>();
            var normals = new List<Vector3>();
            var texCoords = new List<Vector2>();
            var textures = new List<(uint Flags, uint AnimSpeed, List<string> Filenames)>();
            var polygons = new Dictionary<(int TextureIndex, bool Collidable), List<(uint, uint, uint)>>();

            foreach (var piece in Pieces)
            {
                var vertoff = (uint)verts.Count;
                var texoff = textures.Count;
                verts.AddRange(piece.Vertices);
                normals.AddRange(piece.Normals);
                texCoords.AddRange(piece.TexCoords);
                textures.AddRange(piece.Textures);
                var pi = 0;
                foreach (var (ptc, ti) in piece.PolyTexs)
                {
                    // Guard against out-of-range PolyTex indices from corrupt/unusual
                    // WLDs (freportn hits this). Silently drop the offending polygon
                    // group rather than crashing the entire decode — the resulting
                    // hole is visible but the rest of the zone still loads.
                    if (ti >= (uint)piece.Textures.Count)
                    {
                        pi += (int)ptc;
                        continue;
                    }
                    foreach (var (collidable, a, b, c) in piece.Polygons.Skip(pi).Take((int)ptc))
                    {
                        var index = ((int)ti + texoff, collidable);
                        if (!polygons.ContainsKey(index))
                            polygons[index] = new List<(uint, uint, uint)>();
                        polygons[index].Add((a + vertoff, b + vertoff, c + vertoff));
                    }
                    pi += (int)ptc;
                }
            }

            var optTextures = new List<(uint Flags, uint AnimSpeed, string Filenames)>();
            var texIndex = new Dictionary<(uint Flags, uint AnimSpeed, string Filenames), int>();
            var texMap = new Dictionary<int, int>();
            textures.ForEach((texture, i) =>
            {
                var index = (texture.Flags, texture.AnimSpeed, string.Join(',', texture.Filenames));
                if (!texIndex.ContainsKey(index))
                {
                    texIndex[index] = optTextures.Count;
                    optTextures.Add(index);
                }
                texMap[i] = texIndex[index];
            });
            var optPolygons = new Dictionary<(int TextureIndex, bool Collidable), List<(uint, uint, uint)>>();
            foreach (var ((ti, c), polys) in polygons)
            {
                var index = (texMap[ti], c);
                if (!optPolygons.ContainsKey(index))
                    optPolygons[index] = polys;
                else
                    optPolygons[index].AddRange(polys);
            }

            var meshes = new List<(float[], uint[], bool, (uint, uint, List<string>))>();
            foreach (var ((ti, c), polys) in optPolygons)
            {
                var (pvb, pib) = SplitPolyMesh(verts, normals, texCoords, polys);
                var (flags, ani, fns) = optTextures[ti];
                meshes.Add((pvb, pib, c, (flags, ani, fns.Split(',').ToList())));
            }
            return meshes;
        }

        (float[], uint[]) SplitPolyMesh(List<Vector3> vb, List<Vector3> nb, List<Vector2> tcb, List<(uint, uint, uint)> polys)
        {
            var ovb = new List<float>();
            var vmap = new Dictionary<(Vector3, Vector3, Vector2), uint>();
            var oib = new List<uint>();

            uint Add(uint i)
            {
                var (v, n, t) = (vb[(int)i], nb[(int)i], tcb[(int)i]);
                var key = (v, n, t);
                if (vmap.ContainsKey(key))
                    return vmap[key];
                var ind = vmap[key] = (uint)(ovb.Count / 8);
                ovb.AddRange(v.AsArray());
                ovb.AddRange(n.AsArray());
                ovb.AddRange(t.AsArray());
                return ind;
            }

            foreach (var (a, b, c) in polys)
            {
                oib.Add(Add(a));
                oib.Add(Add(c));
                oib.Add(Add(b));
            }

            return (ovb.ToArray(), oib.ToArray());
        }
    }
}
