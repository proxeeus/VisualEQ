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
                var sr1 = x.Value;
                if (sr1?.Reference.Value == null) continue;
                var sr2 = sr1.Reference.Value;
                var sr = sr2.Reference.Value;
                var fns = sr.References.Select(y => y.Value.Filenames.ToList()).SelectMany(y => y).ToList();
                textures.Add((x.Value.Flags, sr.FrameTime, fns));
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
                    var kind = ClassifyLiquid(matName);
                    if (kind == 0xFF) { pi += (int)ptc; continue; }

                    foreach (var (_collidable, a, b, c) in piece.Polygons.Skip(pi).Take((int)ptc))
                    {
                        AccumulateVertex(boxes, matName, kind, piece.Vertices[(int)a]);
                        AccumulateVertex(boxes, matName, kind, piece.Vertices[(int)b]);
                        AccumulateVertex(boxes, matName, kind, piece.Vertices[(int)c]);
                    }
                    pi += (int)ptc;
                }
            }

            var result = new List<(string, byte, Vector3, Vector3)>();
            foreach (var kv in boxes)
                result.Add((kv.Key, kv.Value.Kind, kv.Value.Min, kv.Value.Max));
            return result;
        }

        // Returns OESRegion.KindWater/KindLava/KindSlime for a matching name, or
        // 0xFF (sentinel) when the name doesn't look like a liquid region.
        //
        // Naming patterns come from the SOE/Verant material convention observed in classic
        // EQ Trilogy WLDs (verified against erudsxing, oasis, freporte, soldunga):
        //   Water: W<n>_MDF (rivers, lakes), OW<n>_MDF (ocean water), UW<n>_MDF (rarely,
        //          underwater surfaces from a swim-cam perspective)
        //   Lava:  LAVA<nnn>_MDF (Nagafen's, Sol A, etc.). LAVAFALL* is a vertical mesh
        //          (a lava fall), which we skip — it's not a horizontal surface to snap to.
        //          UNDERLAVA* is the mesh you see when swimming through lava; also skipped.
        //   Slime: SLIME<nnn>_MDF. Uncommon in classic zones — this pattern is a guess and
        //          may need widening as we see real slime materials.
        //
        // The regex-lite check is deliberate: startsWith prefix + digit at the split point
        // catches the naming without pulling in a full Regex allocation for every material.
        static byte ClassifyLiquid(string materialName)
        {
            if (string.IsNullOrEmpty(materialName)) return 0xFF;
            var n = materialName;
            if (IsWaterName(n))  return OESRegion.KindWater;
            if (IsLavaName(n))   return OESRegion.KindLava;
            if (IsSlimeName(n))  return OESRegion.KindSlime;
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
            // "SLIME" followed by digit. Best-guess convention; widen if we see counterexamples.
            n.Length >= 6 &&
            (n[0] == 'S' || n[0] == 's') &&
            (n[1] == 'L' || n[1] == 'l') &&
            (n[2] == 'I' || n[2] == 'i') &&
            (n[3] == 'M' || n[3] == 'm') &&
            (n[4] == 'E' || n[4] == 'e') &&
            char.IsDigit(n[5]);

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
