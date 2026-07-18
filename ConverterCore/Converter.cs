using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Numerics;
using System.Runtime.Serialization;
using System.Security.Cryptography;
using ImageLib;
using MoreLinq;
using VisualEQ.Common;
using VisualEQ.LegacyFileReader;
using static System.Console;
using Extensions = VisualEQ.Common.Extensions;

namespace VisualEQ.ConverterCore
{
    public enum ConvertedType
    {
        None,
        Zone,
        Characters
    }

    public class Converter
    {
        public string BasePath;

        // Directory where `{name}_oes.zip` outputs are written. Defaults to the process CWD,
        // preserving the original behaviour used by ConverterApp/Program.cs.
        public string OutputPath;

        Dictionary<(string, string), string> TextureMap;

        public Converter(string basePath) : this(basePath, ".") { }

        public Converter(string basePath, string outputPath)
        {
            BasePath = basePath;
            OutputPath = outputPath ?? ".";
        }

        string OutputZip(string name) => Path.Combine(OutputPath, $"{name}_oes.zip");

        public ConvertedType Convert(string name)
        {
            if (name.EndsWith("_chr"))
                return ConvertCharacters(name) ? ConvertedType.Characters : ConvertedType.None;
            if (ConvertWldZone(name) || ConvertEqgZone(name))
                return ConvertedType.Zone;
            return ConvertedType.None;
        }

        bool ConvertWldZone(string name)
        {
            var fns = FindFiles($"{name}.s3d").Concat(FindFiles($"{name}_*.s3d")).Where(fn => !fn.Contains("_chr")).ToList();
            if (!fns.Contains($"{name}_obj.s3d")) return false;

            var s3ds = fns.AsParallel().Select(fn => new S3D(fn, File.OpenRead(Filename(fn)))).ToList();
            var wlds = s3ds.AsParallel().Select(s3d => s3d.Where(fn => fn.EndsWith(".wld")).Select(fn => new Wld(s3d, fn)))
                .SelectMany(x => x).ToList();

            foreach (var wld in wlds)
            {
                WriteLine($"<h1>{wld.Filename}</h1>");
                WriteLine("<il>");
                Debugging.OutputHTML(wld);
                WriteLine("</il>");
            }

            var zn = OutputZip(name);
            if (File.Exists(zn)) File.Delete(zn);
            using (var zip = ZipFile.Open(zn, ZipArchiveMode.Create))
            {
                var texs = wlds
                    .Select(x =>
                        x.GetFragments<Fragment03>().Select(y => y.Fragment.Filenames.Select(z => (x.S3D, z))))
                    .SelectMany(x => x).SelectMany(x => x).Distinct();
                TextureMap = texs.AsParallel().Select(x => ((x.Item1.Filename, x.Item2), ConvertTexture(x.Item1, zip, x.Item2))).ToDictionary(t => t.Item1, t => t.Item2);

                var zone = new OESZone(name);

                foreach (var wld in wlds)
                {
                    if (wld.Filename != name + ".wld") continue;
                    CreateMeshAndSkin(
                        wld, zip, zone,
                        wld.GetFragments<Fragment36>().Select(mesh => new MeshPiece(mesh.Fragment))
                    );
                    break;
                }

                var objMap = new Dictionary<string, OESObject>();
                foreach (var wld in wlds)
                {
                    if (wld.Filename == name + ".wld") continue;

                    // Walk Fragment14 (ACTORDEFs), not raw Fragment36s. Fragment15 instances
                    // reference actors by name — for static Fragment2D actors (BALLISTAE,
                    // CBTENT101, ...) that used to map 1:1 to a Fragment36 name, but Kunark+
                    // introduced skeletal-static actors (TREE102, WARDPINE101, ...) where a
                    // single ACTORDEF composes a Fragment11 skeleton with several bone-
                    // attached Fragment36 branch/trunk meshes. The old converter, keyed by
                    // Fragment36 name (TR102BR10, TR102TNK1, ...), never matched the
                    // TREE102_ACTORDEF the instance pointed at — so every Kunark tree was
                    // silently skipped. LANTERN resolves this the same way via Actor.cs +
                    // ObjectInstance.cs (see reference-lantern-extractor memory).
                    foreach (var (actorName, actor) in wld.GetFragments<Fragment14>())
                    {
                        var objname = actorName;
                        if (objname.EndsWith("_ACTORDEF"))
                            objname = objname.Substring(0, objname.Length - "_ACTORDEF".Length);
                        if (string.IsNullOrEmpty(objname) || objMap.ContainsKey(objname)) continue;

                        var pieces = new List<MeshPiece>();
                        foreach (var r in actor.References)
                        {
                            if (r.Value is Fragment2D f2d)
                            {
                                var mesh36 = f2d.Reference.Value;
                                if (mesh36?.TextureListReference.Value != null)
                                    pieces.Add(new MeshPiece(mesh36));
                            }
                            else if (r.Value is Fragment11 f11)
                            {
                                pieces.AddRange(BakeSkeletalObjectPieces(f11));
                            }
                        }
                        if (pieces.Count == 0) continue;

                        var obj = new OESObject(objname);
                        CreateMeshAndSkin(wld, zip, obj, pieces);
                        objMap[objname] = obj;
                        zone.Add(obj);
                    }
                }

                foreach (var wld in wlds)
                {
                    if (wld.Filename == name + ".wld") continue;

                    foreach (var (instname, instance) in wld.GetFragments<Fragment15>())
                    {
                        var objname = instance.Reference.Value?.Replace("_ACTORDEF", "");
                        if (objname == null || !objMap.ContainsKey(objname)) continue;
                        var rot = Quaternion.CreateFromAxisAngle(new Vector3(0, 0, 1), instance.Rotation.Z) *
                                  Quaternion.CreateFromAxisAngle(new Vector3(0, 1, 0), instance.Rotation.Y) *
                                  Quaternion.CreateFromAxisAngle(new Vector3(1, 0, 0), instance.Rotation.X);
                        zone.Add(new OESInstance(objMap[objname], instance.Position, instance.Scale, rot));

                        // Transform each region defined in the object's local space into
                        // world space for this instance, and emit onto the zone. Rotation
                        // + scale + translation, then re-AABB the 8 corners so the world
                        // AABB is still axis-aligned. Query API only cares about world XY.
                        var obj = objMap[objname];
                        var xform = Matrix4x4.CreateScale(instance.Scale) *
                                    Matrix4x4.CreateFromQuaternion(rot) *
                                    Matrix4x4.CreateTranslation(instance.Position);
                        foreach (var lr in obj.Find<OESRegion>())
                        {
                            var (wMin, wMax) = TransformAabb(lr.Min, lr.Max, xform);
                            zone.Add(new OESRegion(lr.Name, lr.Kind, wMin, wMax));
                        }
                    }
                }

                var lights = wlds.FirstOrDefault(x => x.Filename == "lights.wld");
                if (lights != null)
                {
                    var fragments = lights.GetFragments<Fragment28>();
                    if (fragments != null && fragments.Count() > 0)
                    {
                        foreach (var lf in fragments)
                        {
                            var light = lf.Fragment;
                            var sl = light.Reference.Value.Reference.Value;
                            zone.Add(new OESLight(light.Pos, sl.Color, light.Radius, sl.Attenuation ?? 200));
                        }
                    }
                }



                OESFile.Write(zip.CreateEntry("main.oes", CompressionLevel.Optimal).Open(), zone);
            }

            return true;
        }

        bool ConvertCharacters(string name)
        {
            var fns = FindFiles($"{name}*.s3d").ToList();
            if (fns.Count == 0) return false;

            var s3ds = fns.AsParallel().Select(fn => new S3D(fn, File.OpenRead(Filename(fn)))).ToList();
            var wlds = s3ds.AsParallel().Select(s3d => s3d.Where(fn => fn.EndsWith(".wld")).Select(fn => new Wld(s3d, fn)))
                .SelectMany(x => x).ToList();

            foreach (var wld in wlds)
            {
                WriteLine($"<h1>{wld.Filename}</h1>");
                WriteLine("<il>");
                Debugging.OutputHTML(wld);
                WriteLine("</il>");
            }

            var zn = OutputZip(name);
            if (File.Exists(zn)) File.Delete(zn);
            using (var zip = ZipFile.Open(zn, ZipArchiveMode.Create))
            {
                var root = new OESRoot();
                foreach (var wld in wlds)
                    foreach (var (aname, actor) in wld.GetFragments<Fragment14>())
                    {
                        var model = new OESCharacter(aname.Substring(0, aname.Length - "_ACTORDEF".Length));
                        root.Add(model);
                        var skin = new OESSkin();
                        model.Add(skin);
                        foreach (var elem in actor.References)
                            switch (elem.Value)
                            {
                                case Fragment11 f11:
                                    GenerateAnimatedMeshes(wld, zip, model, skin, f11.Reference.Value);
                                    break;
                                case Fragment2D f2d:
                                    // Non-skeletal actor (BOAT, SHIP, EYE, ...). The Fragment14
                                    // references a Fragment2D directly, which points at a
                                    // Fragment36 static mesh. Emit it as a single-frame animated
                                    // mesh so the runtime loader still finds a bind pose under
                                    // `animations[""]`.
                                    if (f2d.Reference.Value is Fragment36 f36)
                                        GenerateStaticMesh(wld, zip, model, skin, f36);
                                    else
                                        WriteLine($"[Converter] Fragment2D on '{aname}' resolved to null Fragment36");
                                    break;
                                default:
                                    WriteLine($"Unknown reference from 0x14 fragment on '{aname}' to {elem.Value}");
                                    break;
                            }
                    }
                OESFile.Write(zip.CreateEntry("main.oes", CompressionLevel.Optimal).Open(), root);
            }

            return true;
        }

        class AniTreePrecursor
        {
            public uint Index;
            public (Quaternion Rotate, Vector3 Translate)[] Frames;
            public AniTreePrecursor[] Children;
        }

        class AniTreeFrame
        {
            public uint Index;
            public (Quaternion Rotate, Vector3 Translate) Transform;
            public AniTreeFrame[] Children;
        }

        // LANTERN's `ClientData/animationsources.txt` — maps a character model
        // code to the model whose animation tracks it should inherit. Classic
        // EQ's global_chr.s3d only stores animated pose tracks for a handful
        // of "donor" models (ELM/ELF for humans, DWM/DWF for stubby races,
        // OGF for large humanoids, etc.). Every other playable race + a lot
        // of NPC races carry only a bind pose in their own tracks; the client
        // resolves this at load time via this same mapping. If we don't apply
        // it here, HAM/DAM/HIM/etc. render as T-poses because the OES emits
        // zero OESAnimationSets for them. Kept as a static table (~90 entries,
        // stable over ~20 years) to avoid runtime file I/O.
        static readonly Dictionary<string, string> AnimationSources = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "HUM", "ELM" }, { "HUF", "ELF" },
            { "BAM", "ELM" }, { "BAF", "ELF" },
            { "ERM", "ELM" }, { "ERF", "ELF" },
            { "HIM", "ELM" }, { "HIF", "ELF" },
            { "DAM", "ELM" }, { "DAF", "ELF" },
            { "HAM", "ELM" }, { "HAF", "ELF" },
            { "TRM", "OGF" }, { "TRF", "OGF" },
            { "OGM", "OGF" },
            { "HOM", "DWM" }, { "HOF", "DWF" },
            { "GNM", "DWM" }, { "GNF", "DWF" },
            { "BRM", "ELM" }, { "BRF", "ELF" },
            { "GOM", "GIA" }, { "GOL", "GIA" },
            { "BET", "SPI" },
            { "CPF", "CPM" },
            { "FRG", "FRO" },
            { "GAM", "GAR" },
            { "GHU", "GOB" },
            { "FPM", "ELM" },
            { "IMP", "GAR" },
            { "GRI", "DRK" },
            { "KOB", "WER" },
            { "LIF", "LIM" },
            { "MIN", "GNN" },
            { "BGM", "ELM" },
            { "PIF", "FAF" },
            { "BGG", "KGO" },
            { "SKE", "ELM" },
            { "TIG", "LIM" },
            { "HHM", "ELM" },
            { "ZOM", "ELM" }, { "ZOF", "ELF" },
            { "QCM", "ELM" }, { "QCF", "ELF" },
            { "PUM", "LIM" },
            { "NGM", "ELM" },
            { "EGM", "ELM" },
            { "RIM", "DWM" }, { "RIF", "DWF" },
            { "SKU", "RAT" },
            { "SPH", "DRK" },
            { "ARM", "RAT" },
            { "CLM", "DWM" }, { "CLF", "DWF" }, { "CL", "DWM" },
            { "HLM", "ELM" }, { "HLF", "ELF" },
            { "GRM", "OGF" }, { "GRF", "OGF" },
            { "OKM", "OGF" }, { "OKF", "OGF" },
            { "KAM", "DWM" }, { "KAF", "DWF" }, { "KA",  "DWM" },
            { "FEM", "ELM" }, { "FEF", "ELF" },
            { "GFM", "ELM" }, { "GFF", "ELF" },
            { "STC", "LIM" },
            { "IKF", "IKM" },
            { "ICM", "IKM" }, { "ICF", "IKM" }, { "ICN", "IKM" },
            { "ERO", "ELF" },
            { "TRI", "ELM" },
            { "BRI", "DWM" },
            { "FDF", "FDR" },
            { "SSK", "SRW" },
            { "VRF", "VRM" },
            { "WUR", "DRA" },
            { "IKS", "IKM" },
            { "IKH", "REA" },
            { "FMO", "DRK" },
            { "BTM", "RHI" },
            { "SDE", "DML" },
            { "SPC", "SPE" },
            { "ENA", "ELM" },
            { "YAK", "GNN" },
            { "COM", "DWM" }, { "COF", "DWF" }, { "COK", "DWM" },
            { "DR2", "TRK" },
            { "HAG", "ELF" },
            { "SIR", "ELF" },
            { "STG", "FSG" },
            { "CCD", "TRK" },
            { "ABH", "ELF" },
            { "BWD", "TRK" },
            { "GDR", "DRA" },
            { "PRI", "TRK" },
        };

        // Animation prefixes VisualEQ actually renders at runtime — the spawn
        // editor is idle-only, so we drop combat/social/damage/etc. animations
        // at bake time. Each anim we keep means ~one OESAnimationBuffer per
        // mesh (vertex counts × float per anim frame); pulling in the ~35 anims
        // LANTERN's animationsources gives us was inflating global_chr's OES
        // from ~100 MB to ~260 MB, tanking zone-load time and BuildAvailable-
        // Models. If you need combat animations for a preview / debug tool,
        // widen this set and re-decode. `""` is the bind pose and is always
        // emitted separately.
        static readonly HashSet<string> BakedAnimationPrefixes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "P01", "P02", "P03",
            "L01", "L02", "L03",
            "O01",
            "STA", "POS",
        };

        void GenerateAnimatedMeshes(Wld wld, ZipArchive zip, OESCharacter model, OESSkin skin, Fragment10 f10)
        {
            var prefixes = new List<string> { "" };
            var rootName = f10.Tracks[0].PieceTrack.Name;

            // Meshes we'll process this call. Starts with `f10.Meshes` (skeleton's
            // primary meshes: base body + base head), then scans the WLD for
            // ORPHAN Fragment36s named `{race}HE##_DMSPRITEDEF` with ## > 00 —
            // those are helmet variants the classic client renders as secondaries
            // (LANTERN's `WldFileCharacters.FindAdditionalAnimationsAndMeshes`
            // scans them the same way). They aren't referenced by the skeleton's
            // Fragment10.Meshes list but share the same bones, so the same anim
            // frames apply. Every downstream loop (vertex bake, poly emit, helmet
            // classifier, OESMeshGroups) iterates this combined list.
            var meshesToProcess = new List<(Fragment36 Mesh, string Name)>();
            for (var i = 0; i < f10.Meshes.Length; i++)
            {
                meshesToProcess.Add((f10.Meshes[i].Value?.Reference.Value, f10.Meshes[i].Value?.Reference.Name ?? ""));
            }
            {
                var alreadyIn = new HashSet<string>(meshesToProcess.Select(m => m.Name), StringComparer.OrdinalIgnoreCase);
                var assetUpper = model.Name.ToUpperInvariant();
                foreach (var (fragName, f36) in wld.GetFragments<Fragment36>())
                {
                    if (alreadyIn.Contains(fragName)) continue;
                    var stripped = fragName;
                    if (stripped.EndsWith("_DMSPRITEDEF")) stripped = stripped.Substring(0, stripped.Length - "_DMSPRITEDEF".Length);
                    stripped = stripped.ToUpperInvariant();
                    if (stripped.Length != assetUpper.Length + 4) continue;
                    if (!stripped.StartsWith(assetUpper + "HE")) continue;
                    var suffix = stripped.Substring(assetUpper.Length + 2, 2);
                    if (!int.TryParse(suffix, out var num) || num == 0) continue;
                    meshesToProcess.Add((f36, fragName));
                }
            }

            // Pull LANTERN's alternate animation-source for this model. When
            // set, we also look for prefixed tracks that end with the
            // alternate's rootName and substitute the model code in per-bone
            // lookups. This is how the classic client synthesises anims for
            // races whose own chr tracks are bind-pose only.
            AnimationSources.TryGetValue(model.Name, out var altModel);
            string altRootName = null;
            if (altModel != null && rootName.Contains(model.Name))
                altRootName = rootName.Replace(model.Name, altModel);

            foreach (var f13 in wld.GetFragments<Fragment13>())
            {
                if (f13.Name == rootName) continue;
                string candidate = null;
                if (f13.Name.EndsWith(rootName))
                    candidate = f13.Name.Substring(0, f13.Name.Length - rootName.Length);
                else if (altRootName != null && f13.Name.EndsWith(altRootName))
                    candidate = f13.Name.Substring(0, f13.Name.Length - altRootName.Length);
                if (candidate != null && BakedAnimationPrefixes.Contains(candidate))
                    prefixes.Add(candidate);
            }
            prefixes = prefixes.Distinct().ToList();

            AniTreePrecursor BuildAniTreePrecursor(string prefix, uint index)
            {
                var track = f10.Tracks[index];
                var ptref = track.PieceTrack;
                var piecetrack = ptref.Value;
                if (prefix != "")
                {
                    // Prefer the model's own prefixed track when available;
                    // fall back to the alternate model's equivalent track
                    // (bone name with `model.Name` swapped to `altModel`).
                    if (wld.GetFragment<Fragment13>(prefix + ptref.Name) is Fragment13 own)
                        piecetrack = own;
                    else if (altModel != null && ptref.Name.Contains(model.Name))
                    {
                        var altPieceName = prefix + ptref.Name.Replace(model.Name, altModel);
                        if (wld.GetFragment<Fragment13>(altPieceName) is Fragment13 alt)
                            piecetrack = alt;
                    }
                }

                return new AniTreePrecursor { Index = index, Frames = piecetrack.Reference.Value.Frames, Children = track.Children.Select(i => BuildAniTreePrecursor(prefix, (uint)i)).ToArray() };
            }

            AniTreeFrame[] BuildFrameTree(AniTreePrecursor pc)
            {
                int GetMaxFrames(AniTreePrecursor pct) =>
                    pct.Children.Select(GetMaxFrames).Concat(new[] { pct.Frames.Length }).Max();

                AniTreeFrame BuildFrame(AniTreePrecursor pct, int frame) =>
                    new AniTreeFrame { Index = pct.Index, Transform = pct.Frames[pct.Frames.Length > 1 ? frame : 0], Children = pct.Children.Select(x => BuildFrame(x, frame)).ToArray() };

                return GetMaxFrames(pc).Times(i => BuildFrame(pc, i)).ToArray();
            }

            var trees = prefixes.Select(x => (x, BuildFrameTree(BuildAniTreePrecursor(x, 0)))).ToDictionary(t => t.Item1, t => t.Item2);

            var animationBuffers = new Dictionary<string, List<List<List<float>>>>();
            foreach (var (name, frames) in trees)
            {
                var frameBuffers = animationBuffers[name] = meshesToProcess.Count.Times(() => new List<List<float>>()).ToList();
                foreach (var frame in frames)
                {
                    var matrices = new Dictionary<uint, Matrix4x4>();

                    void BuildBoneMatrices(AniTreeFrame cur, Matrix4x4 mat)
                    {
                        mat = Matrix4x4.CreateTranslation(cur.Transform.Translate) * mat;
                        mat = Matrix4x4.CreateFromQuaternion(cur.Transform.Rotate) * mat;
                        matrices[cur.Index] = mat;
                        cur.Children.ForEach(x => BuildBoneMatrices(x, mat));
                    }
                    BuildBoneMatrices(frame, Matrix4x4.Identity);

                    for (var i = 0; i < meshesToProcess.Count; i++)
                    {
                        var curBuffer = new List<float>();
                        // Some WLDs (notably classic Trilogy global_chr.s3d) have Fragment2D→Fragment36
                        // references that don't resolve. Skip those meshes so one bad reference doesn't
                        // sink the whole model; an empty buffer keeps `frameBuffers[i]` index-aligned
                        // with the sibling loop below (which also null-checks).
                        var mesh = meshesToProcess[i].Mesh;
                        if (mesh == null)
                        {
                            frameBuffers[i].Add(curBuffer);
                            continue;
                        }
                        var offset = 0U;
                        foreach (var (count, index) in mesh.VertBones)
                        {
                            // Orphan helmet meshes might reference a bone that isn't in this
                            // Fragment10's track table (they use their own indexing). Skip
                            // gracefully — the mesh's frames will just be its bind pose,
                            // which is what LANTERN's `SetActiveMeshFromGroup` uses too.
                            if (!matrices.TryGetValue(index, out var mat)) mat = Matrix4x4.Identity;
                            for (var j = 0; j < count; ++j)
                            {
                                curBuffer.AddRange(Vector3.Transform(mesh.Vertices[offset + j], mat).AsArray());
                                curBuffer.AddRange(Vector3.Transform(mesh.Normals[offset + j], mat).AsArray());
                                curBuffer.AddRange(mesh.TexCoords[offset + j].AsArray());
                            }
                            offset += count;
                        }
                        frameBuffers[i].Add(curBuffer);
                    }
                }
            }

            (List<uint>, Dictionary<string, OESAnimationBuffer>) RewriteBuffers(List<uint> indices, Dictionary<string, List<List<float>>> vertices)
            {
                var oinds = new List<uint>();
                var indmap = new Dictionary<uint, uint>();
                foreach (var index in indices)
                {
                    if (!indmap.ContainsKey(index))
                        indmap[index] = (uint)indmap.Count;
                    oinds.Add(indmap[index]);
                }
                var indexorder = indmap.OrderBy(x => x.Value).Select(x => x.Key).ToList();

                IReadOnlyList<float> Remap(List<float> verts) =>
                    indexorder.Select(i => verts.Skip((int)i * 8).Take(8)).SelectMany(x => x).ToList();

                for (var i = 0; i < oinds.Count; i += 3)
                {
                    var temp = oinds[i + 1];
                    oinds[i + 1] = oinds[i + 2];
                    oinds[i + 2] = temp;
                }

                return (oinds, vertices.Select(kv => (kv.Key, new OESAnimationBuffer(kv.Value.Select(Remap).ToList()))).ToDictionary(t => t.Key, t => t.Item2));
            }

            // Classify each source Fragment36 into a helmet group per LANTERN's
            // SkeletonImporter (mesh name == asset name OR ends in "00" → primary,
            // else → secondary in insertion order). Base body + HE00 (base head)
            // become groups 0/1. Helmet variants HE01, HE02, HE03 become groups
            // 2, 3, 4 (rendered one at a time based on npc.HelmTexture).
            //
            // We track group per meshesToProcess index; poly-groups within one
            // Fragment36 all share the same helmet group. Extra suffixes we
            // haven't classified explicitly (e.g. HUM01) get group 0 = always
            // render — safest default until we understand what they are.
            var f36HelmetGroup = new uint[meshesToProcess.Count];
            {
                var assetName = model.Name.ToUpperInvariant();
                var nextSecondary = 2u; // groups 2, 3, 4… allocated in encounter order
                for (var mi = 0; mi < meshesToProcess.Count; mi++)
                {
                    var fragName = meshesToProcess[mi].Name;
                    var stripped = fragName;
                    if (stripped.EndsWith("_DMSPRITEDEF")) stripped = stripped.Substring(0, stripped.Length - "_DMSPRITEDEF".Length);
                    stripped = stripped.ToUpperInvariant();
                    if (stripped == assetName) { f36HelmetGroup[mi] = 0; continue; }
                    // {race}HE00 pattern → base head (group 1)
                    // {race}HE## for ## > 00 → helmet secondary (group nextSecondary)
                    if (stripped.Length == assetName.Length + 4 && stripped.StartsWith(assetName + "HE"))
                    {
                        var suffix = stripped.Substring(assetName.Length + 2, 2);
                        if (int.TryParse(suffix, out var num))
                        {
                            f36HelmetGroup[mi] = num == 0 ? 1u : nextSecondary++;
                            continue;
                        }
                    }
                    // Unrecognised suffix — treat as always-render (group 0). May
                    // over-render meshes like HUM01 but keeps the model visually
                    // intact; iterate if a specific NPC shows stacked geometry.
                    f36HelmetGroup[mi] = 0;
                }
            }

            var meshGroups = new List<uint>();

            var asets = prefixes.Where(x => x != "").Select(x => (x, new OESAnimationSet(x, 0f))).ToDictionary(t => t.Item1, t => t.Item2);
            meshesToProcess.Count.Times(i =>
            {
                var meshf = meshesToProcess[i].Mesh;
                if (meshf == null) return; // Skip unresolved mesh — matches the null-guard in the animationBuffers loop above.
                var omats = meshf.TextureListReference.Value.References.Select(matref =>
                {
                    var tfn = matref.Value.Reference.Value.Reference.Value.References[0].Value.Filenames[0];
                    var tf = matref.Value.Flags;
                    // Character materials: mask bits (2, 8, 16) route through the
                    // Deferred masked shader (alpha-discard). We deliberately DO NOT
                    // set `transparent` — the zone-path formula `(tf & (4|8))` would
                    // flip skin materials with bit 3 (value 8) to
                    // AlphaMask=True + Transparent=True, which routes them to
                    // ForwardDiffuseMasked (intensity-as-alpha, no discard). That
                    // ships NPCs as semi-transparent and tanks FPS due to forward
                    // overdraw. Sail cloth (also bit 3) still renders correctly:
                    // Deferred masked reads alpha from the BMP chroma-key PNG and
                    // discards palette-index-0 pixels, which is what "transparent
                    // sail" actually means for classic character models.
                    var masked = (tf & (2 | 8 | 16)) != 0;
                    return new OESMaterial(masked, false, false) { new OESTexture(ConvertTexture(wld.S3D, zip, tfn)) };
                }).ToList();
                var offset = 0U;
                meshf.PolyTexs.ForEach(v =>
                {
                    var polys = meshf.Polygons.Skip((int)offset).Take((int)v.Count).Select(x => new[] { x.A, x.B, x.C }).SelectMany(x => x).ToList();
                    offset += v.Count;
                    skin.Add(omats[(int)v.Index]);
                    var (ibuffer, vbuffers) = RewriteBuffers(
                        polys,
                        animationBuffers.Select(kv => (kv.Key, kv.Value[i])).ToDictionary(t => t.Key, t => t.Item2)
                    );
                    var amesh = new OESAnimatedMesh(true, ibuffer, (uint)vbuffers[""].VertexBuffers[0].Count);
                    model.Add(amesh);
                    meshGroups.Add(f36HelmetGroup[i]);
                    foreach (var prefix in prefixes)
                    {
                        if (prefix == "")
                            amesh.Add(vbuffers[""]);
                        else
                            asets[prefix].Add(vbuffers[prefix]);
                    }
                });
            });
            asets.ForEach(kv => model.Add(kv.Value));
            model.Add(new OESMeshGroups(meshGroups));

            // LANTERN's `WldFileCharacters.FindMaterialVariants` +
            // `MaterialList.AddVariant`: the mesh's TextureListReference (Fragment31)
            // only lists the base outfit's Fragment30 materials (skin variant 00,
            // subpart-tens digit 0). Higher-tier armor and face variants live as
            // UNREFERENCED Fragment30s in the WLD with the SAME character+region
            // prefix — the LoadCharacter PNG-swap needs them present in the OES to
            // find them per npc.Texture / npc.Face.
            //
            //   variant > "00"  →  armor tier (npc.Texture)   e.g. BATCH0001 → BATCH0101
            //   subpart-tens > 0 →  face variant (npc.Face)    e.g. HUMHE0001 → HUMHE0011
            {
                var baseCharRegions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var alreadyInSkin = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var mat in skin.Find<OESMaterial>())
                {
                    foreach (var tex in mat.Find<OESTexture>())
                    {
                        var fn = Path.GetFileNameWithoutExtension(tex.Filename);
                        var dash = fn.IndexOf('-');
                        if (dash > 0) fn = fn.Substring(0, dash);
                        if (fn.Length != 9) continue;
                        alreadyInSkin.Add(fn);
                        baseCharRegions.Add(fn.Substring(0, 5)); // race(3) + region(2)
                    }
                }

                var added = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var (fragName, f30) in wld.GetFragments<Fragment30>())
                {
                    var matName = fragName;
                    if (matName.EndsWith("_MDF")) matName = matName.Substring(0, matName.Length - 4);
                    if (matName.Length != 9) continue;
                    if (alreadyInSkin.Contains(matName)) continue;
                    var charRegion = matName.Substring(0, 5);
                    if (!baseCharRegions.Contains(charRegion)) continue;
                    if (!added.Add(matName)) continue;

                    if (f30.Reference.Value?.Reference.Value?.References == null ||
                        f30.Reference.Value.Reference.Value.References.Length == 0) continue;
                    var tfn = f30.Reference.Value.Reference.Value.References[0].Value.Filenames[0];
                    var tf = f30.Flags;
                    var masked = (tf & (2 | 8 | 16)) != 0;
                    skin.Add(new OESMaterial(masked, false, false) { new OESTexture(ConvertTexture(wld.S3D, zip, tfn)) });
                }
            }
        }

        // Non-skeletal actors (BOAT, SHIP, EYE_OF_ZOMM, ...) — the Fragment14
        // ActorDef references a Fragment2D → Fragment36 mesh directly, with no
        // Fragment11 skeleton track set. Emit each Fragment36 poly group as an
        // OESAnimatedMesh with a single "" (bind-pose) frame so the runtime
        // loader treats it the same as a zero-animation character. All vertices
        // are baked in local space (identity transform) since there's no
        // skeleton to animate.
        void GenerateStaticMesh(Wld wld, ZipArchive zip, OESCharacter model, OESSkin skin, Fragment36 mesh)
        {
            if (mesh == null || mesh.TextureListReference.Value == null) return;

            var omats = mesh.TextureListReference.Value.References.Select(matref =>
            {
                var tfn = matref.Value.Reference.Value.Reference.Value.References[0].Value.Filenames[0];
                var tf = matref.Value.Flags;
                var transparent = (tf & 7) == 7;
                return new OESMaterial(transparent, transparent, false) { new OESTexture(ConvertTexture(wld.S3D, zip, tfn)) };
            }).ToList();

            // Flatten every vertex to the interleaved (pos, normal, uv) format the loader
            // expects. No bone matrix — static meshes bake their world-space verts as-is.
            var vertsFlat = new List<float>(mesh.Vertices.Length * 8);
            for (var i = 0; i < mesh.Vertices.Length; i++)
            {
                var v = mesh.Vertices[i];
                var n = i < mesh.Normals.Length ? mesh.Normals[i] : Vector3.UnitZ;
                var uv = i < mesh.TexCoords.Length ? mesh.TexCoords[i] : Vector2.Zero;
                vertsFlat.Add(v.X); vertsFlat.Add(v.Y); vertsFlat.Add(v.Z);
                vertsFlat.Add(n.X); vertsFlat.Add(n.Y); vertsFlat.Add(n.Z);
                vertsFlat.Add(uv.X); vertsFlat.Add(uv.Y);
            }

            var offset = 0U;
            foreach (var (count, texIndex) in mesh.PolyTexs)
            {
                var polys = mesh.Polygons.Skip((int)offset).Take((int)count)
                    .SelectMany(x => new[] { x.A, x.B, x.C })
                    .ToList();
                offset += count;
                skin.Add(omats[(int)texIndex]);

                // Compact indices to only the verts this poly group uses, remap
                // triangles into the compacted space, and swap winding order to
                // match the animated-mesh path (which flips [i+1]<->[i+2]).
                var indmap = new Dictionary<uint, uint>();
                var oinds = new List<uint>(polys.Count);
                foreach (var index in polys)
                {
                    if (!indmap.ContainsKey(index)) indmap[index] = (uint)indmap.Count;
                    oinds.Add(indmap[index]);
                }
                for (var i = 0; i < oinds.Count; i += 3)
                {
                    var tmp = oinds[i + 1];
                    oinds[i + 1] = oinds[i + 2];
                    oinds[i + 2] = tmp;
                }
                var indexOrder = indmap.OrderBy(kv => kv.Value).Select(kv => kv.Key).ToList();
                var compactVerts = new List<float>(indexOrder.Count * 8);
                foreach (var srcIdx in indexOrder)
                    for (var k = 0; k < 8; k++)
                        compactVerts.Add(vertsFlat[(int)srcIdx * 8 + k]);

                var amesh = new OESAnimatedMesh(true, oinds, (uint)(compactVerts.Count / 8));
                model.Add(amesh);
                amesh.Add(new OESAnimationBuffer(new IReadOnlyList<float>[] { compactVerts }));
            }
        }

        bool ConvertEqgZone(string name)
        {
            var ename = $"{name}.eqg";
            if (!Exists(ename)) return false;

            var eqg = new S3D(ename, File.OpenRead(Filename(ename)));
            Zon zon;
            var zname = $"{name}.zon";
            if (eqg.Contains(zname))
                zon = new Zon(eqg, eqg.Open(zname));
            else if (Exists(zname))
                zon = new Zon(eqg, File.OpenRead(zname));
            else
                return false;

            var zn = OutputZip(name);
            if (File.Exists(zn)) File.Delete(zn);
            using (var zip = ZipFile.Open(zn, ZipArchiveMode.Create))
            {
                var texs = zon.Objects.Select(x => x.Materials.Values.Select(y =>
                    y.Properties.Values.Where(z => z is string w && w.ToLower().EndsWith(".dds")).Select(z =>
                        (string)z)).SelectMany(y => y)).SelectMany(x => x).OrderBy(x => x).Distinct();
                TextureMap = texs.AsParallel()
                    .Select(x => ((ename, x), ConvertTexture(eqg, zip, x))).ToDictionary(t => t.Item1, t => t.Item2);

                var zone = new OESZone(name);
                var objs = zon.Objects.Select((obj, i) =>
                {
                    var root = obj.IsTer ? (OESChunk)zone : new OESObject();
                    if (root != zone)
                        zone.Add(root);
                    var skin = new OESSkin();
                    root.Add(skin);
                    obj.Meshes.ForEach(mesh =>
                    {
                        if (!obj.Materials.ContainsKey(mesh.Key.MatIndex)) return;
                        var mat = obj.Materials[mesh.Key.MatIndex];
                        if (mat.Properties.ContainsKey("e_TextureNormal0") && (string)mat.Properties["e_TextureNormal0"] != "None")
                        {
                            skin.Add(new OESMaterial(false, false, false) {
                                new OESEffect("diffuse+normal"),
                                new OESTexture(TextureMap[(ename, (string) mat.Properties["e_TextureDiffuse0"])]),
                                new OESTexture(TextureMap[(ename, (string) mat.Properties["e_TextureNormal0"])])
                            });
                        }
                        else
                            skin.Add(new OESMaterial(false, false, false) { new OESTexture(TextureMap[(ename, (string)mat.Properties["e_TextureDiffuse0"])]) });
                        var (vb, ib) = OptimizeBuffers(obj.VertexBuffer, mesh.Value);
                        root.Add(new OESStaticMesh(mesh.Key.Collidable, ib, vb));
                    });
                    return obj.IsTer ? null : root;
                }).ToList();

                zon.Placeables.ForEach(instance =>
                {
                    if (objs.Count <= instance.ObjId || objs[instance.ObjId] == null) return;

                    zone.Add(new OESInstance(
                        (OESObject)objs[instance.ObjId],
                        instance.Position,
                        new Vector3(instance.Scale),
                        Quaternion.CreateFromAxisAngle(new Vector3(0, 0, 1), instance.Rotation.X) *
                        Quaternion.CreateFromAxisAngle(new Vector3(0, 1, 0), instance.Rotation.Y) *
                        Quaternion.CreateFromAxisAngle(new Vector3(1, 0, 0), instance.Rotation.Z)));
                });

                zon.Lights.ForEach(light => zone.Add(new OESLight(light.Position, light.Color, light.Radius, 200)));

                OESFile.Write(zip.CreateEntry("main.oes", CompressionLevel.Optimal).Open(), zone);
            }

            return true;
        }

        (float[] VertexBuffer, uint[] IndexBuffer) OptimizeBuffers(IReadOnlyList<float> vb, IReadOnlyList<uint> ib)
        {
            var pvb = (0, vb.Count, 8).Range().Select(i => (
                new Vector3(vb[i++], vb[i++], vb[i++]),
                new Vector3(vb[i++], vb[i++], vb[i++]),
                new Vector2(vb[i++], vb[i])
            )).ToList();

            var ovb = new List<float>();
            var vertMap = new Dictionary<(Vector3, Vector3, Vector2), uint>();

            uint Add((Vector3, Vector3, Vector2) vert)
            {
                if (vertMap.ContainsKey(vert)) return vertMap[vert];

                var ind = vertMap[vert] = (uint)vertMap.Count;
                ovb.AddRange(vert.Item1.AsArray());
                ovb.AddRange(vert.Item2.AsArray());
                ovb.AddRange(vert.Item3.AsArray());
                return ind;
            }

            var oib = ib.Select(x => Add(pvb[(int)x])).ToArray();
            return (ovb.ToArray(), oib);
        }

        // Transform the 8 corners of a local-space AABB through a world matrix and
        // re-fit an axis-aligned AABB in world space. Necessary because the query API
        // only checks a world-XY point against min/max — a rotated instance without
        // this pass would have the wrong footprint.
        static (Vector3 Min, Vector3 Max) TransformAabb(Vector3 lMin, Vector3 lMax, Matrix4x4 xform)
        {
            Span<Vector3> corners = stackalloc Vector3[8];
            corners[0] = new Vector3(lMin.X, lMin.Y, lMin.Z);
            corners[1] = new Vector3(lMax.X, lMin.Y, lMin.Z);
            corners[2] = new Vector3(lMin.X, lMax.Y, lMin.Z);
            corners[3] = new Vector3(lMax.X, lMax.Y, lMin.Z);
            corners[4] = new Vector3(lMin.X, lMin.Y, lMax.Z);
            corners[5] = new Vector3(lMax.X, lMin.Y, lMax.Z);
            corners[6] = new Vector3(lMin.X, lMax.Y, lMax.Z);
            corners[7] = new Vector3(lMax.X, lMax.Y, lMax.Z);
            var min = Vector3.Transform(corners[0], xform);
            var max = min;
            for (var i = 1; i < 8; i++)
            {
                var w = Vector3.Transform(corners[i], xform);
                min = Vector3.Min(min, w);
                max = Vector3.Max(max, w);
            }
            return (min, max);
        }

        static string KindLabel(byte kind)
        {
            switch (kind)
            {
                case OESRegion.KindWater: return "water";
                case OESRegion.KindLava:  return "lava";
                case OESRegion.KindSlime: return "slime";
                default: return "other";
            }
        }

        // Skeletal-static zone actors (Kunark trees, Warslik pines, cabilis pillars):
        // Fragment14 → Fragment11 → Fragment10 with N bone-attached Fragment36 meshes
        // sharing one skeleton. There's no animation — we bake the bind pose (frame 0
        // of every Fragment13 piece track) into the mesh vertices and hand the caller
        // a set of MeshPieces ready to feed into the existing static-object pipeline.
        // Mirrors GenerateAnimatedMeshes' bone-matrix walk but uses only frame 0 and
        // pre-transforms verts in place instead of emitting animation buffers.
        IEnumerable<MeshPiece> BakeSkeletalObjectPieces(Fragment11 f11)
        {
            var f10 = f11?.Reference.Value;
            if (f10 == null) yield break;

            var boneMats = new Dictionary<uint, Matrix4x4>();
            void Walk(uint idx, Matrix4x4 parent)
            {
                if (idx >= f10.Tracks.Length) return;
                var track = f10.Tracks[idx];
                var frames = track.PieceTrack?.Value?.Reference.Value?.Frames;
                var rot = Quaternion.Identity;
                var trans = Vector3.Zero;
                if (frames != null && frames.Length > 0)
                {
                    rot = frames[0].Rotation;
                    trans = frames[0].Shift;
                }
                // Match GenerateAnimatedMeshes' order: translate first, then rotate,
                // then apply parent transform. Row-vector convention (System.Numerics)
                // applies these left-to-right when computing v * M, which reproduces the
                // classic-client "local translate → local rotate → parent" chain.
                var mat = Matrix4x4.CreateTranslation(trans) * parent;
                mat = Matrix4x4.CreateFromQuaternion(rot) * mat;
                boneMats[idx] = mat;
                foreach (var ch in track.Children) Walk((uint)ch, mat);
            }
            Walk(0, Matrix4x4.Identity);

            for (var mi = 0; mi < f10.Meshes.Length; mi++)
            {
                var f36 = f10.Meshes[mi]?.Value?.Reference.Value;
                if (f36 == null || f36.TextureListReference.Value == null) continue;

                var piece = new MeshPiece(f36);

                // Bone assignment has two paths in classic Trilogy WLDs:
                //
                //   1. VertBones populated → per-range weighted like character models. Each
                //      (count, boneIdx) binds a contiguous vertex range to one bone. We use
                //      LANTERN's formula `bone_matrix * raw_vertex + center` — the parser
                //      baked `center` into `piece.Vertices` at read time, so we subtract it,
                //      apply the bone matrix, then add it back.
                //
                //   2. VertBones empty → the mesh is attached wholly to Tracks[mi]'s bone.
                //      Fragment10 built its Meshes list from `tracks.Select(x => x.Item4)`
                //      (see Read10), so Meshes[i] is Tracks[i].Mesh and the implied bone is
                //      bone i. Every Kunark tree/rock/pillar hits this path.
                //
                // For path 2 we deliberately DROP mesh.Center. Kunark tree trunks have a
                // large-negative Center.Z (e.g. TREE102 trunk Center Z = -39.3) that with
                // LANTERN's `bone*raw + center` plants the trunk 39 units underground and
                // matches nothing on the ground. Dropping the center gives Z = raw + bone
                // (trunk from 0 upward, branches lifted by their bone's Z translation) —
                // which is what the classic client actually renders. Data trumps the OBJ-
                // exporter formula: LANTERN's export writes centers back for tooling
                // compatibility, but in-game rendering does not.
                if (f36.VertBones.Length > 0)
                {
                    var offset = 0U;
                    foreach (var (count, boneIdx) in f36.VertBones)
                    {
                        if (!boneMats.TryGetValue(boneIdx, out var mat)) mat = Matrix4x4.Identity;
                        for (var j = 0U; j < count; j++)
                        {
                            var vi = (int)(offset + j);
                            var raw = piece.Vertices[vi] - f36.Center;
                            piece.Vertices[vi] = Vector3.Transform(raw, mat) + f36.Center;
                            piece.Normals[vi]  = Vector3.TransformNormal(piece.Normals[vi], mat);
                        }
                        offset += count;
                    }
                }
                else
                {
                    if (!boneMats.TryGetValue((uint)mi, out var mat)) mat = Matrix4x4.Identity;
                    for (var vi = 0; vi < piece.Vertices.Count; vi++)
                    {
                        var raw = piece.Vertices[vi] - f36.Center;
                        piece.Vertices[vi] = Vector3.Transform(raw, mat);
                        piece.Normals[vi]  = Vector3.TransformNormal(piece.Normals[vi], mat);
                    }
                }

                yield return piece;
            }
        }

        void CreateMeshAndSkin(Wld wld, ZipArchive zip, OESChunk target, IEnumerable<MeshPiece> pieces)
        {
            var mesh = new Mesh();
            pieces.ForEach(mesh.Add);

            // Liquid region detection runs before Bake so it sees the original per-material
            // polygon groups (Bake dedupes materials by texture properties, which would fold
            // multiple named liquid regions using the same texture into one AABB).
            //
            // Emit onto zone AND object targets. Some zones place water inside an object
            // mesh (a "water pool" prop instanced across the zone), and skipping objects
            // meant the ocean visible in freporte's harbor was invisible to snap-to-water.
            // Object regions are stamped in the object's LOCAL space; a later pass in the
            // zone loop transforms them into world space for each OESInstance.
            foreach (var (name, kind, min, max) in mesh.DetectLiquidRegions())
            {
                WriteLine($"[Converter] Detected {KindLabel(kind)} region '{name}' " +
                          $"on {(target is OESZone ? "zone" : "object")} " +
                          $"min=({min.X:F1},{min.Y:F1},{min.Z:F1}) " +
                          $"max=({max.X:F1},{max.Y:F1},{max.Z:F1})");
                target.Add(new OESRegion(name, kind, min, max));
            }

            var baked = mesh.Bake();
            var skin = new OESSkin();
            target.Add(skin);
            foreach (var (vb, ib, collidable, texture) in baked)
            {
                if (texture.Flags == 0) continue; // TODO: Bake this in, but non-renderable. Collision mesh type?
                target.Add(new OESStaticMesh(collidable, ib, vb));
                var tf = texture.Flags;
                var masked = (tf & (2 | 8 | 16)) != 0;
                var transparent = (tf & (4 | 8)) != 0;
                if ((tf & 0xFFFF) == 0x14) // TODO: Remove hack. Fixes tiger head in Halas
                    masked = transparent = false;
                var isFire = texture.Filenames[0].ToLower() == "fire1.bmp";
                var mat = new OESMaterial(masked, transparent, isFire);
                if (isFire)
                    mat.Add(new OESEffect("fire"));
                else
                {
                    if (texture.Filenames.Count > 1)
                        mat.Add(new OESEffect("animated") { ["speed"] = texture.AnimSpeed });
                    texture.Filenames.ForEach(fn => mat.Add(new OESTexture(TextureMap[(wld.S3D.Filename, fn)])));
                }

                skin.Add(mat);
            }
        }

        string ConvertTexture(S3D s3d, ZipArchive zip, string fn)
        {
            fn = fn.Substring(0, fn.IndexOf('.') + 4);

            // Some WLDs reference textures the S3D doesn't actually contain — fearplane's
            // `maywall.bmp` is a known case. Without this guard, `s3d[fn]` throws
            // KeyNotFoundException, which kills the whole AsParallel().ToDictionary() up in
            // ConvertWldZone. The enclosing `using(zip)` still runs Dispose, flushing a
            // partial output zip with no main.oes to disk — silent load failure at runtime.
            // Emit a bright placeholder instead so the zone is still usable with visible
            // holes where the texture was.
            if (!s3d.Contains(fn))
            {
                WriteLine($"[Converter] Texture '{fn}' missing from {s3d.Filename} — writing placeholder");
                return WriteMissingTexturePlaceholder(s3d, zip, fn);
            }

            byte[] data;
            lock (s3d) data = s3d[fn];

            var md5 = string.Join("", MD5.Create().ComputeHash(data).Select(x => $"{x:X02}")).Substring(0, 10);

            var ofn = $"{fn.Split('.', 2)[0]}-{md5}.png";
            Image image;
            try
            {
                image = data[0] == 'B' && data[1] == 'M'
                    ? Bmp.Load(fn, data).FlipY()
                    : Dds.Load(fn, data).Images[0];
            }
            catch (Exception)
            {
                image = new Image(ColorMode.Rgb, (1, 1), new byte[] { 0xff, 0xff, 0 });
            }

            lock (zip)
            {
                var entry = zip.CreateEntry(ofn, CompressionLevel.Optimal).Open();
                Png.Encode(image, entry);
                entry.Close();
            }

            return ofn;
        }

        // 1x1 yellow placeholder for a texture the S3D didn't have. Same tint ConvertTexture
        // falls back to when a BMP/DDS payload fails to decode. Named per (S3D, texture) so
        // parallel writes don't collide and so the two-S3Ds-share-a-missing-name case doesn't
        // clobber. Note: the zip is in ZipArchiveMode.Create, so `GetEntry` isn't available —
        // we rely on the caller's `.Distinct()` upstream to guarantee we're invoked at most
        // once per (S3D, texture) pair.
        static string WriteMissingTexturePlaceholder(S3D s3d, ZipArchive zip, string fn)
        {
            var s3dSlug = System.IO.Path.GetFileNameWithoutExtension(s3d.Filename);
            var ofn = $"{fn.Split('.', 2)[0]}-{s3dSlug}-MISSING.png";
            lock (zip)
            {
                var image = new Image(ColorMode.Rgb, (1, 1), new byte[] { 0xff, 0xff, 0 });
                using (var entry = zip.CreateEntry(ofn, CompressionLevel.Optimal).Open())
                    Png.Encode(image, entry);
            }
            return ofn;
        }

        List<string> FindFiles(string pattern) => Directory.GetFiles(BasePath, pattern).Select(Path.GetFileName).ToList();

        string Filename(string name) => Path.Join(BasePath, name);
        bool Exists(string name) => File.Exists(Filename(name));
    }
}
