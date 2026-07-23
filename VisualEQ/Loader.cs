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

                    engine.Add(FromMeshes(FromSkin(zone.Find<OESSkin>().First(), path, zip), new[] { Matrix4x4.Identity }, zone.Find<OESStaticMesh>()));

                    var objInstances = zone.Find<OESObject>().ToDictionary(x => x, x => new List<Matrix4x4>());
                    zone.Find<OESInstance>().ForEach(inst =>
                    {
                        objInstances[inst.Object].Add(Matrix4x4.CreateScale(inst.Scale) * Matrix4x4.CreateFromQuaternion(inst.Rotation) * Matrix4x4.CreateTranslation(inst.Position));
                    });
                    foreach (var (obj, instances) in objInstances)
                    {
                        var model = FromMeshes(
                            FromSkin(obj.Find<OESSkin>().First(), path, zip),
                            instances.ToArray(),
                            obj.Find<OESStaticMesh>()
                        );
                        model.IsFoliage = IsFoliageName(obj.Name);
                        engine.Add(model);
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

        // Classic-EQ vegetation ACTORDEFs follow "<theme><type><number>" naming:
        //   TREE102, PINETREE103, SWAMPTREE101, WARDPINE100         (classic/Faydark)
        //   JNTREE103, JNGRASS101, JNPLANT101, JNLOG104             (Kunark jungle)
        //   CMPLANT105B, DRGRASS101                                 (Kunark marsh/drylands)
        //
        // Substring-match the type tokens rather than an ever-growing prefix list —
        // new theme prefixes appear in every expansion and a curated list keeps
        // leaving zones out (trakanon/emeraldjungle were entirely missed by the
        // initial TREE*/PINE*/BUSH* set). Tokens are chosen so the false-positive
        // surface is essentially zero on classic EQ object names: no in-game object
        // contains "TREE"/"PINE"/"PALM"/"BUSH"/"GRASS"/"PLANT" that isn't
        // actually vegetation. LOG is deliberately excluded (JNLOG is minor and
        // "log" could match unrelated names like "DIALOG" in future data).
        //
        // Rocks/pillars/props/tents stay visible: WARROCK, STALAG, KUPEDAST,
        // CAMPFIRE, TENT, TABLE, CHAIR, BARREL, BALLISTAE, none of which contain
        // any of the vegetation tokens.
        static readonly string[] FoliageTokens =
        {
            "TREE", "PINE", "PALM", "BUSH", "GRASS", "PLANT",
        };
        static bool IsFoliageName(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            foreach (var token in FoliageTokens)
                if (name.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            return false;
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
        //
        // Caches by chr-zip file mtime so subsequent zone loads skip the whole
        // enumerate-79-zips-and-parse-each-OES walk. Sharing GetOrLoadRoot means
        // the OES tree is parsed at most once per file per session even if it's
        // fetched here first and then again by LoadCharacter.
        static readonly Dictionary<string, (long Mtime, Dictionary<string, int> Richness)> _richnessCache
            = new Dictionary<string, (long, Dictionary<string, int>)>(StringComparer.OrdinalIgnoreCase);
        static readonly object _richnessCacheLock = new object();
        static bool _richnessDiskCacheLoaded;

        // Disk-persisted richness cache. Path/mtime-keyed so it self-invalidates when
        // any chr zip is re-decoded. First zone load with 79 chr zips would otherwise
        // re-parse ~500 MB of OES data every time; loading this small file up front
        // (populated by a prior session, or written after this session's parses) skips
        // it entirely. Format is a plain-text `path|mtime|name=score,name=score…` per
        // line — trivial to serialize, easy to inspect.
        static string RichnessCachePath() =>
            System.IO.Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData),
                "VisualEQ", "richness-cache.txt");

        static void LoadRichnessCacheFromDisk()
        {
            if (_richnessDiskCacheLoaded) return;
            _richnessDiskCacheLoaded = true;
            var path = RichnessCachePath();
            if (!System.IO.File.Exists(path)) return;
            try
            {
                foreach (var line in System.IO.File.ReadAllLines(path))
                {
                    var parts = line.Split('|');
                    if (parts.Length != 3) continue;
                    if (!long.TryParse(parts[1], out var mt)) continue;
                    var richness = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                    foreach (var pair in parts[2].Split(','))
                    {
                        if (string.IsNullOrEmpty(pair)) continue;
                        var kv = pair.Split('=');
                        if (kv.Length != 2 || !int.TryParse(kv[1], out var score)) continue;
                        richness[kv[0]] = score;
                    }
                    _richnessCache[parts[0]] = (mt, richness);
                }
            }
            catch (Exception ex) { WriteLine($"[Loader] richness-cache read failed: {ex.Message}"); }
        }

        static void PersistRichnessEntry(string path, long mtime, Dictionary<string, int> richness)
        {
            try
            {
                var cachePath = RichnessCachePath();
                System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(cachePath));
                var line = $"{path}|{mtime}|{string.Join(",", richness.Select(kv => $"{kv.Key}={kv.Value}"))}";
                // Append-with-dedupe: read existing, replace matching path, write back.
                var lines = System.IO.File.Exists(cachePath)
                    ? new List<string>(System.IO.File.ReadAllLines(cachePath))
                    : new List<string>();
                lines.RemoveAll(l => l.StartsWith(path + "|", StringComparison.OrdinalIgnoreCase));
                lines.Add(line);
                System.IO.File.WriteAllLines(cachePath, lines);
            }
            catch (Exception ex) { WriteLine($"[Loader] richness-cache write failed: {ex.Message}"); }
        }

        internal static Dictionary<string, int> GetCharacterModelRichness(string path)
        {
            long mtime = 0;
            try { mtime = System.IO.File.GetLastWriteTimeUtc(path).Ticks; } catch { }

            lock (_richnessCacheLock)
            {
                LoadRichnessCacheFromDisk();
                if (_richnessCache.TryGetValue(path, out var cached) && cached.Mtime == mtime)
                    return cached.Richness;
            }

            var result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            try
            {
                // Shallow scan — walks OES chunk tree by header only, skipping the
                // OESAnimationBuffer vertex-data blocks that dominate parse time.
                // Enough for the richness score (character name + anim/mesh child
                // counts). LoadCharacter still uses GetOrLoadRoot's full parse when
                // it actually needs to render a model.
                using (var zip = ZipFile.OpenRead(path))
                using (var ms = new MemoryStream())
                {
                    using (var temp = zip.GetEntry("main.oes")?.Open()) temp?.CopyTo(ms);
                    ms.Position = 0;
                    foreach (var (charName, anims, meshes) in OESFile.ShallowScanCharacters(ms))
                    {
                        var score = anims * 1000 + meshes;
                        if (!result.TryGetValue(charName, out var cur) || score > cur)
                            result[charName] = score;
                    }
                }
            }
            catch (Exception ex)
            {
                WriteLine($"[Loader] Could not enumerate models in '{path}': {ex.Message}");
            }

            lock (_richnessCacheLock) _richnessCache[path] = (mtime, result);
            PersistRichnessEntry(path, mtime, result);
            return result;
        }

        // Loads a character model. `animationWhitelist` (if non-null) restricts which named
        // animation sets get uploaded to GL — massive win for spawn rendering, where we only
        // need one idle animation per model. `""` (the mesh's built-in bind pose) is always
        // included regardless of the whitelist.
        //
        // `singleFrame`: if true, keep only frame 0 of every animation.
        //
        // `textureIndex` (npc.Texture): armor tier — chars 5-6 of the material name.
        // `helmTextureIndex` (npc.HelmTexture): helmet variant — selects which HE-region
        //   Fragment36 mesh renders. Uses the OESMeshGroups chunk emitted by the converter.
        // `faceIndex` (npc.Face): face variant — char 7 of the material name (HUMHE0001 →
        //   HUMHE0011 for face 1).
        //
        // Missing variants fall back silently to the base PNG so races that don't ship the
        // requested combo just render their default look.
        // Parsed OES roots keyed by chr-zip path. Chr archives run 8–60 MB after the
        // alt-scanner puts every skin/face variant into the skin; re-parsing per
        // unique (code, texture, helm, face) combo blew up zone-load and per-spawn
        // caching. The root itself is treated as read-only by LoadCharacter, so
        // sharing the parsed tree across every combo is safe. Never cleared during
        // a session — a single zone's chr set is small in total memory (few hundred
        // MB max) and the alternative is re-parsing gigabytes per zone load.
        static readonly Dictionary<string, OESRoot> _oesRootCache = new Dictionary<string, OESRoot>(StringComparer.OrdinalIgnoreCase);
        static readonly object _oesRootCacheLock = new object();

        // Shared mesh geometry (VAOs + VBOs + index buffer) keyed by
        // (chr-zip path, model code, mesh index, singleFrame). Every AniModel variant
        // that differs only in material re-uses these — freporte's 24 human-male
        // (texture, helm, face) combos now share 13 VAO sets total instead of 312.
        // Cache is session-lifetime; GL resources aren't freed until app exit.
        // MUST be accessed from the GL thread (matches AnimatedMesh construction).
        static readonly Dictionary<(string Path, string Name, int Mesh, bool SingleFrame), MeshGeometry> _meshGeometryCache
            = new Dictionary<(string, string, int, bool), MeshGeometry>();

        // Shared textures keyed by (zip path, PNG entry name). Every FromSkin call
        // routes through here so the same PNG uploads to GL once per session, not once
        // per zone visit and once per (texture, helm, face) combo. Before this cache,
        // ClearCurrentZone dropped _modelCache references, but the underlying Texture
        // GL handles never got deleted (finalizer thread can't safely GL.DeleteTexture
        // without a current context), so every zone swap orphaned a wave of textures
        // on the GPU — FPS crashed after a few swaps and shutdown finalizer walks got
        // slower and slower. Session-lifetime; MUST be accessed from the GL thread.
        static readonly Dictionary<(string ZipPath, string Entry), Texture> _textureCache
            = new Dictionary<(string, string), Texture>(TextureCacheKeyComparer.Instance);

        sealed class TextureCacheKeyComparer : IEqualityComparer<(string ZipPath, string Entry)>
        {
            public static readonly TextureCacheKeyComparer Instance = new TextureCacheKeyComparer();
            public bool Equals((string ZipPath, string Entry) x, (string ZipPath, string Entry) y) =>
                StringComparer.OrdinalIgnoreCase.Equals(x.ZipPath, y.ZipPath) &&
                StringComparer.OrdinalIgnoreCase.Equals(x.Entry, y.Entry);
            public int GetHashCode((string ZipPath, string Entry) o) =>
                StringComparer.OrdinalIgnoreCase.GetHashCode(o.ZipPath ?? "") ^
                (StringComparer.OrdinalIgnoreCase.GetHashCode(o.Entry ?? "") * 397);
        }

        // Decode + upload the PNG at `entryName` inside `zip`, or return the cached
        // Texture if we've already uploaded it in this session. All character/zone
        // texture creation goes through here.
        static Texture GetOrUploadTexture(string zipPath, string entryName, ZipArchive zip)
        {
            var key = (zipPath, entryName);
            if (_textureCache.TryGetValue(key, out var cached)) return cached;
            using (var s = zip.GetEntry(entryName)?.Open())
            {
                var img = Png.Decode(Path.GetFileName(entryName), s);
                var tex = new Texture(img, false);
                _textureCache[key] = tex;
                return tex;
            }
        }

        static OESRoot GetOrLoadRoot(string path)
        {
            lock (_oesRootCacheLock)
                if (_oesRootCache.TryGetValue(path, out var cached)) return cached;
            using (var zip = ZipFile.OpenRead(path))
            using (var ms = new MemoryStream())
            {
                using (var temp = zip.GetEntry("main.oes")?.Open()) temp?.CopyTo(ms);
                ms.Position = 0;
                var root = OESFile.Read<OESRoot>(ms);
                lock (_oesRootCacheLock) _oesRootCache[path] = root;
                return root;
            }
        }

        // Shutdown drop. Called from Controller.Shutdown before Process.Kill so the OS
        // reclaim path doesn't have to walk these dictionaries. Deliberately does NOT
        // call GL.Delete* on the cached buffers/textures — Parallels/ARM64 GL emulation
        // makes each GL.Delete call expensive, and TerminateProcess reclaims the GL
        // context (and everything behind it) instantly regardless.
        internal static void ClearAllCaches()
        {
            lock (_oesRootCacheLock)
            {
                _meshGeometryCache.Clear();
                _oesRootCache.Clear();
                _textureCache.Clear();
            }
        }

        internal static AniModel LoadCharacter(string path, string name, HashSet<string> animationWhitelist = null, bool singleFrame = false, int textureIndex = 0, int helmTextureIndex = 0, int faceIndex = 0)
        {
            var root = GetOrLoadRoot(path);
            using (var zip = ZipFile.OpenRead(path))
            {
                {
                    var model = root.Find<OESCharacter>().First(x => x.Name == name);
                    var oams = model.Find<OESAnimatedMesh>().ToList();

                    var materials = FromSkin(model.Find<OESSkin>().First(), path, zip, textureIndex, faceIndex, oams.Count);

                    var allAniSets = model.Find<OESAnimationSet>().ToList();
                    var anisets = allAniSets
                        .Where(x => animationWhitelist == null || animationWhitelist.Contains(x.Name))
                        .Select(x => (x.Name, x.Find<OESAnimationBuffer>().ToList()))
                        .ToDictionary(t => t.Name, t => t.Item2);

                    var extras = "";
                    if (textureIndex != 0) extras += $" (texture={textureIndex})";
                    if (helmTextureIndex != 0) extras += $" (helm={helmTextureIndex})";
                    if (faceIndex != 0) extras += $" (face={faceIndex})";
                    WriteLine($"Loading {model.Name}: kept [{string.Join(",", anisets.Keys)}] " +
                              $"of {allAniSets.Count} → [{string.Join(",", allAniSets.Select(a => a.Name))}]" +
                              (singleFrame ? " (single-frame)" : "") + extras);

                    var animodel = new AniModel();
                    foreach (var setName in anisets.Keys) animodel.AvailableAnimations.Add(setName);

                    // Helmet mesh group tags from the converter. Rule per LANTERN's
                    // SkeletonImporter + NonPlayableVariantHandler:
                    //   Group 0 → always render (base body, unrecognised sub-meshes)
                    //   Group 1 → base head + hair
                    //   Group N ≥ 2 → helmet variant N - 1
                    //
                    // If any secondary helmet group matches helmTextureIndex, render that
                    // secondary and hide the base head (LANTERN's HandleMainMeshes hides
                    // the last main mesh when a helmet is active). Otherwise fall back to
                    // rendering the base head — races whose helmet variants aren't in
                    // this OES yet (e.g. FPM whose FPMHE01 is an orphan Fragment36
                    // outside f10.Meshes) still show a head for helmTexture > 0.
                    var mgrp = model.Find<OESMeshGroups>().FirstOrDefault();
                    var groups = mgrp?.Groups ?? System.Array.Empty<uint>();
                    var wantedHelmetGroup = (uint)(helmTextureIndex + 1); // helmTexture=1 → group 2
                    var hasHelmetForRequest = helmTextureIndex > 0 && groups.Any(g => g == wantedHelmetGroup);
                    bool ShouldRender(int meshIdx)
                    {
                        var g = meshIdx < groups.Length ? groups[meshIdx] : 0u;
                        if (g == 0) return true;
                        if (g == 1) return !hasHelmetForRequest; // base head unless a helmet replaces it
                        return g == wantedHelmetGroup;
                    }

                    IReadOnlyList<IReadOnlyList<float>> TruncateToFirst(IReadOnlyList<IReadOnlyList<float>> vbs) =>
                        singleFrame && vbs.Count > 1 ? new[] { vbs[0] } : vbs;

                    oams.ForEach((oam, i) =>
                    {
                        if (!ShouldRender(i)) return;
                        var geomKey = (path, name, i, singleFrame);
                        if (!_meshGeometryCache.TryGetValue(geomKey, out var geom))
                        {
                            var animations = anisets.Select(kv => (kv.Key, kv.Value[i])).ToDictionary(t => t.Key, t => t.Item2);
                            animations[""] = oam.Find<OESAnimationBuffer>().First();
                            var built = animations.ToDictionary(kv => kv.Key, kv => TruncateToFirst(kv.Value.VertexBuffers));
                            geom = new MeshGeometry(oam.IndexBuffer.ToArray(), built);
                            _meshGeometryCache[geomKey] = geom;
                        }
                        animodel.Add(new AnimatedMesh(materials[i], geom));
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

        static List<Material> FromSkin(OESSkin skin, string zipPath, ZipArchive zip, int textureIndex = 0, int faceIndex = 0, int materialsToBuild = -1) =>
            FromSkinInner(skin, zipPath, zip, textureIndex, faceIndex, materialsToBuild);

        // `materialsToBuild` caps how many Material objects (with GL texture uploads)
        // are actually built. Character skins carry many extra OESMaterials the
        // converter emits for variant/face lookup — we don't want to allocate GL
        // textures for those. -1 = build every material (used by zone/object skins).
        // `zipPath` is the on-disk path of `zip` — used as the zip half of the
        // (zipPath, entryName) key for the session-lifetime `_textureCache`. Two
        // Materials picking the same PNG will end up sharing the same Texture,
        // even across zone swaps and (texture, helm, face) combos of the same chr.
        static List<Material> FromSkinInner(OESSkin skin, string zipPath, ZipArchive zip, int textureIndex, int faceIndex, int materialsToBuild)
        {
            // Build 9-char basename → zip-entry-name lookup. All alt materials the
            // converter extracted (skin variants + face variants) show up here. Look-up
            // is by exact combined-transform basename computed per original.
            Dictionary<string, string> byBaseName = null;
            if (textureIndex > 0 || faceIndex > 0)
            {
                byBaseName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var m in skin.Find<OESMaterial>())
                {
                    foreach (var t in m.Find<OESTexture>())
                    {
                        var fn = Path.GetFileNameWithoutExtension(t.Filename);
                        var dash = fn.IndexOf('-');
                        if (dash > 0) fn = fn.Substring(0, dash);
                        if (fn.Length != 9) continue;
                        if (!byBaseName.ContainsKey(fn)) byBaseName[fn] = t.Filename;
                    }
                }
            }

            // For an original texture "HUMHE0001" applies both transforms per LANTERN:
            //   armor tier (npc.Texture): replace chars 5-6 with textureIndex.ToString("00")
            //   face      (npc.Face):    replace char 7 with faceIndex.ToString()[0]
            // Falls back progressively to just skin, just face, or the original if the
            // combined variant PNG isn't present in the zip.
            string PickVariantFilename(string original)
            {
                if (byBaseName == null) return original;
                var origBase = Path.GetFileNameWithoutExtension(original);
                var dash = origBase.IndexOf('-');
                if (dash > 0) origBase = origBase.Substring(0, dash);
                if (origBase.Length != 9) return original;
                var race         = origBase.Substring(0, 3);
                var region       = origBase.Substring(3, 2);
                var origVariant  = origBase.Substring(5, 2);
                var origSubPart  = origBase.Substring(7, 2); // e.g. "01"
                var origSubTens  = origSubPart[0];
                var origSubOnes  = origSubPart[1];

                var newVariant = textureIndex > 0 ? textureIndex.ToString("00") : origVariant;
                var newSubTens = faceIndex > 0 ? faceIndex.ToString()[0] : origSubTens;

                string TryName(string variant, char subTens)
                {
                    var candidate = race + region + variant + subTens + origSubOnes;
                    return byBaseName.TryGetValue(candidate, out var full) ? full : null;
                }

                // Combined skin+face first, then just skin, then just face, then original.
                return TryName(newVariant,  newSubTens)
                    ?? TryName(newVariant,  origSubTens)
                    ?? TryName(origVariant, newSubTens)
                    ?? original;
            }

            var allMats = skin.Find<OESMaterial>().ToList();
            var effectiveCount = materialsToBuild < 0 ? allMats.Count : System.Math.Min(materialsToBuild, allMats.Count);
            return allMats.Take(effectiveCount).Select(mat =>
            {
                var effect = mat.Find<OESEffect>().FirstOrDefault();
                effect = effect ?? new OESEffect("default");

                var textures = mat.Find<OESTexture>().Select(x =>
                {
                    var loadFilename = PickVariantFilename(x.Filename);
                    return GetOrUploadTexture(zipPath, loadFilename, zip);
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
