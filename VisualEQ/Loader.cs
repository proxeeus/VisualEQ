using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using ImageLib;
using MoreLinq;
using VisualEQ.Common;
using VisualEQ.Engine;
using VisualEQ.Materials;
using static System.Console;

namespace VisualEQ
{
    internal class Loader
    {
        internal static void LoadZoneFile(string path, EngineCore engine)
        {
            using (var zip = ZipFile.OpenRead(path))
            {
                using (var ms = new MemoryStream())
                {
                    using (var temp = zip.GetEntry("main.oes")?.Open())
                        temp?.CopyTo(ms);
                    ms.Position = 0;
                    var zone = OESFile.Read<OESZone>(ms);
                    WriteLine($"Loading {zone.Name}");

                    engine.Add(FromMeshes(FromSkin(zone.Find<OESSkin>().First(), zip), new[] { Matrix4x4.Identity }, zone.Find<OESStaticMesh>()));

                    var objInstances = zone.Find<OESObject>().ToDictionary(x => x, x => new List<Matrix4x4>());
                    zone.Find<OESInstance>().ForEach(inst =>
                    {
                        objInstances[inst.Object].Add(Matrix4x4.CreateScale(inst.Scale) * Matrix4x4.CreateFromQuaternion(inst.Rotation) * Matrix4x4.CreateTranslation(inst.Position));
                    });
                    foreach (var (obj, instances) in objInstances)
                    {
                        engine.Add(FromMeshes(
                            FromSkin(obj.Find<OESSkin>().First(), zip),
                            instances.ToArray(),
                            obj.Find<OESStaticMesh>()
                        ));
                    }

                    zone.Find<OESLight>().ForEach(light => engine.AddLight(light.Position, light.Radius, light.Attenuation, light.Color));

                    // Liquid regions (water/lava/slime) — DB-space AABBs stamped into the
                    // .oes at convert time by ConverterCore. Consumed by "Snap Z to water"
                    // in the sidebar. Zones converted before this feature landed have zero
                    // OESRegion chunks; the query API just reports "no water here".
                    var regionCount = 0;
                    zone.Find<OESRegion>().ForEach(r =>
                    {
                        engine.AddRegion(r.Name, r.Kind, r.Min, r.Max);
                        regionCount++;
                    });
                    if (regionCount > 0)
                        WriteLine($"[Loader] Loaded {regionCount} region(s) for {zone.Name}");
                }
            }
        }

        static Model FromMeshes(IReadOnlyList<Material> mats, Matrix4x4[] instances, IEnumerable<OESStaticMesh> meshes)
        {
            IEnumerable<float> TransformBuffer(IReadOnlyList<float> buffer, Matrix4x4 mat)
            {
                if (mat == Matrix4x4.Identity)
                {
                    foreach (var elem in buffer)
                        yield return elem;
                    yield break;
                }
                // Hoist the Invert call outside Debug.Assert — Assert is stripped in Release
                // (Conditional("DEBUG")), which would also strip the Invert call and leave imat
                // unassigned by the time it's read below. Fails Release compilation with CS0165.
                var inverted = Matrix4x4.Invert(mat, out var imat);
                Debug.Assert(inverted);
                for (var i = 0; i < buffer.Count; i += 8)
                {
                    var vert = Vector3.Transform(new Vector3(buffer[i + 0], buffer[i + 1], buffer[i + 2]), mat);
                    var normal = Vector3.Transform(new Vector3(buffer[i + 3], buffer[i + 4], buffer[i + 5]), imat);
                    yield return vert.X;
                    yield return vert.Y;
                    yield return vert.Z;
                    yield return normal.X;
                    yield return normal.Y;
                    yield return normal.Z;
                    yield return buffer[i + 6];
                    yield return buffer[i + 7];
                }
            }

            var model = new Model();
            meshes.ForEach((sm, i) => model.Add(new Mesh(
                mats[i],
                instances.AsParallel().Select(instance => TransformBuffer(sm.VertexBuffer, instance)).AsSequential().SelectMany(x => x).ToArray(),
                instances.Select((_, j) => sm.IndexBuffer.Select(v => (uint)(v + j * sm.VertexBuffer.Count / 8))).SelectMany(x => x).ToArray(),
                sm.Collidable)));
            return model;
        }

        // Returns all character model names available in a chr zip, or an empty set on failure.
        internal static HashSet<string> GetAvailableCharacterModels(string path)
        {
            try
            {
                using (var zip = ZipFile.OpenRead(path))
                using (var ms = new MemoryStream())
                {
                    using (var temp = zip.GetEntry("main.oes")?.Open())
                        temp?.CopyTo(ms);
                    ms.Position = 0;
                    var root = OESFile.Read<OESRoot>(ms);
                    return new HashSet<string>(
                        root.Find<OESCharacter>().Select(c => c.Name),
                        StringComparer.OrdinalIgnoreCase);
                }
            }
            catch (Exception ex)
            {
                WriteLine($"[Loader] Could not enumerate models in '{path}': {ex.Message}");
                return new HashSet<string>();
            }
        }

        // Returns (model name → richness score) for a chr zip. Richness = animation set
        // count + 1 (so any model with a bind-pose mesh beats "not present at all").
        // Used by SpawnManager.BuildAvailableModels to pick the chr zip that actually
        // has the model animated — zone-specific chr files often carry empty stubs for
        // playable races that shadow the populated versions in global_chr, and we want
        // the animated version to win.
        internal static Dictionary<string, int> GetCharacterModelRichness(string path)
        {
            var result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            try
            {
                using (var zip = ZipFile.OpenRead(path))
                using (var ms = new MemoryStream())
                {
                    using (var temp = zip.GetEntry("main.oes")?.Open())
                        temp?.CopyTo(ms);
                    ms.Position = 0;
                    var root = OESFile.Read<OESRoot>(ms);
                    foreach (var c in root.Find<OESCharacter>())
                    {
                        var anims = c.Find<OESAnimationSet>().Count();
                        var meshes = c.Find<OESAnimatedMesh>().Count();
                        // Score: prefer animated models heavily; a model with meshes
                        // but no anims (e.g. SHIP static mesh) still beats a zip that
                        // doesn't declare the model at all.
                        var score = anims * 1000 + meshes;
                        if (!result.TryGetValue(c.Name, out var cur) || score > cur)
                            result[c.Name] = score;
                    }
                }
            }
            catch (Exception ex)
            {
                WriteLine($"[Loader] Could not enumerate models in '{path}': {ex.Message}");
            }
            return result;
        }

        // Loads a character model. `animationWhitelist` (if non-null) restricts which named
        // animation sets get uploaded to GL — massive win for spawn rendering, where we only
        // need one idle animation per model. `""` (the mesh's built-in bind pose) is always
        // included regardless of the whitelist.
        //
        // `singleFrame`: if true, keep only frame 0 of every animation. Draw becomes static
        // (frameCount % 1 == 0), interpolation lerps between identical buffers → no motion.
        //
        // `textureIndex` picks an armor variant per LANTERN's approach: keep every mesh and
        // material exactly as-is, only substitute the underlying PNG. Mesh UVs, shader flags,
        // material shell all preserved — the sampled texture just points at a different
        // pixel set. For a mesh whose default material references `FPMCH0001.png`, texture=1
        // makes it sample `FPMCH0002.png` instead. When no matching variant PNG exists in
        // the chr zip, the original PNG is kept (silent fallback).
        internal static AniModel LoadCharacter(string path, string name, HashSet<string> animationWhitelist = null, bool singleFrame = false, int textureIndex = 0)
        {
            using (var zip = ZipFile.OpenRead(path))
            {
                using (var ms = new MemoryStream())
                {
                    using (var temp = zip.GetEntry("main.oes")?.Open())
                        temp?.CopyTo(ms);
                    ms.Position = 0;
                    var root = OESFile.Read<OESRoot>(ms);
                    var model = root.Find<OESCharacter>().First(x => x.Name == name);

                    var materials = FromSkin(model.Find<OESSkin>().First(), zip, textureIndex);

                    var allAniSets = model.Find<OESAnimationSet>().ToList();
                    var anisets = allAniSets
                        .Where(x => animationWhitelist == null || animationWhitelist.Contains(x.Name))
                        .Select(x => (x.Name, x.Find<OESAnimationBuffer>().ToList()))
                        .ToDictionary(t => t.Name, t => t.Item2);

                    WriteLine($"Loading {model.Name}: kept [{string.Join(",", anisets.Keys)}] " +
                              $"of {allAniSets.Count} → [{string.Join(",", allAniSets.Select(a => a.Name))}]" +
                              (singleFrame ? " (single-frame)" : "") +
                              (textureIndex != 0 ? $" (texture={textureIndex})" : ""));

                    var animodel = new AniModel();
                    foreach (var setName in anisets.Keys) animodel.AvailableAnimations.Add(setName);

                    // Truncates an animation's frame list to just frame 0.
                    IReadOnlyList<IReadOnlyList<float>> TruncateToFirst(IReadOnlyList<IReadOnlyList<float>> vbs) =>
                        singleFrame && vbs.Count > 1 ? new[] { vbs[0] } : vbs;

                    model.Find<OESAnimatedMesh>().ForEach((oam, i) =>
                    {
                        var animations = anisets.Select(kv => (kv.Key, kv.Value[i])).ToDictionary(t => t.Key, t => t.Item2);
                        animations[""] = oam.Find<OESAnimationBuffer>().First();
                        animodel.Add(new AnimatedMesh(materials[i], animations.ToDictionary(kv => kv.Key, kv => TruncateToFirst(kv.Value.VertexBuffers)), oam.IndexBuffer.ToArray()));
                    });

                    return animodel;
                }
            }
        }

        // Parses a chr texture filename per LANTERN's MaterialList.ParseCharacterSkin
        // (LanternExtractor/EQ/Wld/Fragments/MaterialList.cs:122). The classic EQ base
        // material name is exactly 9 characters:
        //   chars 0-2 : race code   ("FPM", "HUM", "DAM")
        //   chars 3-4 : body region ("CH", "LG", "HE", "FT", "UA", "HN", "FA")
        //   chars 5-6 : SKIN VARIANT (00 = default cloth, 01 = leather, 02 = chain, ...)
        //   chars 7-8 : region sub-part (01, 02, 03 — different mesh pieces of same body region)
        //
        // Slot key groups meshes that share a texture slot across variants:
        //   race + region + sub-part → e.g. "FPMCH01" (Freeport chest sub-part 01)
        // Alt skin swap looks for the same slot at a DIFFERENT variant, so
        //   FPMCH0001 (variant 00, slot FPMCH01) can be swapped with FPMCH0101
        //   (variant 01, slot FPMCH01) but NOT with FPMCH0002 (variant 00, slot FPMCH02).
        //
        // The previous bug: parsed the last 4 digits as "variant" and swapped
        // FPMCH0001 with FPMCH0002 — different sub-parts of the same variant, so the
        // swap put the wrong mesh piece's texture on the wrong geometry (garbled UVs).
        static (string SlotKey, int Variant) ParseTextureFilename(string filename)
        {
            var nameNoExt = Path.GetFileNameWithoutExtension(filename);
            var baseName = nameNoExt;
            var dash = baseName.IndexOf('-');
            if (dash > 0) baseName = baseName.Substring(0, dash);
            if (baseName.Length != 9) return (null, 0);
            var race    = baseName.Substring(0, 3);
            var region  = baseName.Substring(3, 2);
            var variant = baseName.Substring(5, 2);
            var subpart = baseName.Substring(7, 2);
            if (!int.TryParse(variant, out var v)) return (null, 0);
            return (race + region + subpart, v);
        }

        static List<Material> FromSkin(OESSkin skin, ZipArchive zip, int textureIndex = 0) =>
            FromSkinInner(skin, zip, textureIndex);

        static List<Material> FromSkinInner(OESSkin skin, ZipArchive zip, int textureIndex)
        {
            // Build slotKey → (variant → zip-entry-name) lookup. When textureIndex > 0 we
            // consult this to substitute the PNG loaded for each material — mesh geometry
            // and material shell stay put, only the pixel data behind them changes. Cost
            // is O(materials); done once per model load.
            Dictionary<string, Dictionary<int, string>> variantsBySlot = null;
            if (textureIndex > 0)
            {
                variantsBySlot = new Dictionary<string, Dictionary<int, string>>(StringComparer.OrdinalIgnoreCase);
                foreach (var m in skin.Find<OESMaterial>())
                {
                    foreach (var t in m.Find<OESTexture>())
                    {
                        var (slot, variant) = ParseTextureFilename(t.Filename);
                        if (slot == null) continue;
                        if (!variantsBySlot.TryGetValue(slot, out var byVariant))
                            variantsBySlot[slot] = byVariant = new Dictionary<int, string>();
                        if (!byVariant.ContainsKey(variant)) byVariant[variant] = t.Filename;
                    }
                }
            }

            // Returns the zip-entry filename to load for `original`. If an alt-skin PNG
            // exists at the same slot with variant == textureIndex, use it; otherwise
            // fall back to the original (no visible change for chr files that only ship
            // skin variant 00, which is the case for the classic Trilogy client's
            // playable + NPC races).
            string PickVariantFilename(string original)
            {
                if (variantsBySlot == null) return original;
                var (slot, _) = ParseTextureFilename(original);
                if (slot == null || !variantsBySlot.TryGetValue(slot, out var byVariant)) return original;
                return byVariant.TryGetValue(textureIndex, out var alt) ? alt : original;
            }

            return skin.Find<OESMaterial>().Select(mat =>
            {
                var effect = mat.Find<OESEffect>().FirstOrDefault();
                effect = effect ?? new OESEffect("default");

                var textures = mat.Find<OESTexture>().Select(x =>
                {
                    var loadFilename = PickVariantFilename(x.Filename);
                    using (var tzs = zip.GetEntry(loadFilename)?.Open())
                        return Png.Decode(Path.GetFileName(loadFilename), tzs);
                }).ToArray();

                switch (effect.Name)
                {
                    case "default":
                    case "animated":
                        var aniSpeed = effect.Name == "animated" ? (uint)effect["speed"] / 1000f : 0;
                        if (mat.Transparent)
                            return mat.AlphaMask
                                ? (Material)new ForwardDiffuseMaskedMaterial(textures, aniSpeed)
                                : new ForwardDiffuseMaterial(textures, aniSpeed);
                        else
                            return mat.AlphaMask
                                ? (Material)new DeferredDiffuseMaskedMaterial(textures, aniSpeed)
                                : new DeferredDiffuseMaterial(textures, aniSpeed);
                    case "diffuse+normal":
                        return new DeferredDiffuseNormalMaterial(textures);
                    case "fire":
                        return new FireMaterial();
                    default:
                        throw new NotImplementedException($"Unknown OESEffect name: {effect.Name}");
                }
            }).ToList();
        }
    }
}
