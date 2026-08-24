using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using VisualEQ.Database.Models;
using VisualEQ.Engine;

namespace VisualEQ.SpawnSystem
{
    public class SpawnManager
    {
        // EQEmu heading: full circle = 512. Values outside 0..512 wrap naturally through the
        // trig conversion. Direction convention (CCW/CW, meshes' default facing) is a best-
        // guess for the user's schema — expect to negate or add π/2 if things face wrong.
        const float HeadingFullCircle = 512f;

        // Per-race authored mesh height in EQ "world units". `npc_types.size` divided
        // by this yields the render scale — most humanoids/creatures use 6.
        // Dwarves (DWM) and halflings (HOM) share the DWM anim-source and are
        // authored to a shorter absolute height, so `size/6` under-scales them.
        // Halas / Grobb / Oggok / Kaladim / Coldain citizens use city-specific
        // codes with the same physique, so they use the same divisors as their
        // ancestor race.
        //
        // Public so the sidebar's camera-framing code can compute head/torso
        // world offsets that match the actual rendered mesh height (a halfling's
        // head sits at pos + 4*scale, not pos + 6*scale — hardcoding 6 leaves
        // the camera pointing above the head for short-mesh races).
        public static float MeshHeightForRace(int race) => MeshAuthoredHeightForRace(race);

        static float MeshAuthoredHeightForRace(int race)
        {
            switch (race)
            {
                case 8:                             // Dwarf
                case 11:                            // Halfling (HOM — short-mesh code)
                case 81:                            // Rivervale Citizen (RIM/RIF, halfling stock)
                case 94:                            // Kaladim Citizen (KAM/KAF, dwarf stock)
                case 183:                           // Coldain (COM/COF, dwarf stock)
                    return 4f;
                default:
                    return 6f;
            }
        }

        // Scale = npc.Size / per-race divisor. Purely visual — DB.size is calibrated
        // for this formula, don't touch. Overloads exist for NpcType (spawn load) and
        // NpcTypeFull (mid-load model swap on edit).
        static float ComputeScale(VisualEQ.Database.Models.NpcType npc) =>
            npc == null || npc.Size <= 0f ? 1f : npc.Size / MeshAuthoredHeightForRace(npc.Race);
        static float ComputeScale(VisualEQ.Database.Models.NpcTypeFull npc) =>
            npc == null || npc.Size <= 0f ? 1f : npc.Size / MeshAuthoredHeightForRace(npc.Race);

        // Foot-align render bias. `spawn.z` is not a reliable ground reference —
        // varies per-NPC (some placed at belly, some at feet, some floating).
        // Instead raycast straight down from just above spawn.z, find actual
        // walkable terrain via the collision octree, and shift the render so
        // the mesh's lowest vertex lands at hit-Z. That way dragons/wurms/humans
        // all stand ON ground regardless of what `spawn.z` was set to.
        //
        // No bias when Globals.Collider isn't ready yet (falls back to raw
        // spawn.z placement — the pre-fix behavior). No bias when the ray
        // misses (spawn placed in the sky, over water, off-map).
        static float ComputeRenderZBias(Vector3 scenePos, AniModel aniModel, float scale)
        {
            if (aniModel == null || Globals.Collider == null) return 0f;
            // Cast down from spawn.z itself, not from way above. Casting from
            // +200 hit the CEILING in tall chambers (Temple of Veeshan, guild
            // halls) — placed dragons on the ceiling.
            var origin = new Vector3(scenePos.X, scenePos.Y, scenePos.Z + 1f);
            var hit = Globals.Collider.FindIntersection(origin, new Vector3(0, 0, -1));
            if (!hit.HasValue) return 0f;
            var groundZ = hit.Value.Item2.Z;
            // Compute where the mesh's lowest scaled vertex would land WITHOUT
            // any bias — i.e. respecting the DB's spawn.z as-is.
            var feetZ = scenePos.Z + aniModel.AuthoredMinZ * scale;
            // Only lift when the mesh would sink INTO the terrain. If spawn.z
            // is intentionally elevated (drakes, wyverns and other flying NPCs
            // hover above ground), the DB placement is already correct — leave
            // it alone. This keeps big ground-dwellers (dragons, wurms) on the
            // floor while flying spawns stay at their intended altitude.
            return feetZ < groundZ ? groundZ - feetZ : 0f;
        }

        public static Quaternion HeadingToRotation(float heading)
        {
            var angle = heading * ((float)Math.PI * 2f / HeadingFullCircle);
            return Quaternion.CreateFromAxisAngle(new Vector3(0, 0, 1), angle);
        }

        // Candidate idle-stance animation names, tried in order per-model. Classic EQ:
        //   P01/P02/P03 — pose/passive (primary idle)
        //   L01/L02/L03 — locomotion (walk/run) — falls through here so shared models like
        //                 HOM/GNM (only L03) don't T-pose
        //   O01         — idle emote (wave etc.) — some models have only this
        //   STA/POS     — theoretical fallbacks, rarely present in classic
        static readonly string[] SpawnAnimationCandidates = { "P01", "P02", "P03", "L01", "L02", "L03", "O01", "STA", "POS" };
        static readonly HashSet<string> SpawnAnimations = new HashSet<string>(SpawnAnimationCandidates);

        public List<SpawnPoint> SpawnPoints { get; } = new List<SpawnPoint>();
        public SpawnPoint Selected { get; private set; }
        public int DirtyCount => SpawnPoints.Count(sp => sp.IsDirty);

        // Spawn2 rows that were marked pending-delete but whose SpawnPoint objects are
        // kept alive so Revert / Discard can splice them back into SpawnPoints without a
        // DB round-trip. Keyed by spawn2.id. Cleared on zone unload alongside SpawnPoints.
        public Dictionary<int, SpawnPoint> HiddenSpawnPoints { get; } = new Dictionary<int, SpawnPoint>();

        // Negative temp-id counter for pending spawn2 inserts. Real spawn2 ids are always
        // positive AUTO_INCREMENT so a negative sentinel unambiguously marks a pre-commit
        // row throughout scene + buffer. Reset by PrepareForLoad so a zone unload also
        // resets the temp-id sequence.
        int _nextTempSpawnId = -1;
        public int NextTempSpawnId() => _nextTempSpawnId--;

        public event Action<SpawnPoint> SpawnSelected;
        public event Action<SpawnPoint> SpawnMoved;

        // Running counters + telemetry for a step-based load (see PrepareForLoad / LoadBatch /
        // FinishLoad). LoadFromRecords is now a thin wrapper that calls all three.
        private int _loadModelled, _loadPlaceholders, _loadSkipped;
        private Dictionary<int, (int Count, string ExampleName, bool Unmapped, string TriedCodes)> _loadPlaceholderByRace;

        // Clears prior state so a fresh incremental load can begin. Call once before batches.
        public void PrepareForLoad()
        {
            SpawnPoints.Clear();
            HiddenSpawnPoints.Clear();
            _nextTempSpawnId = -1;
            Selected = null;
            _loadModelled = _loadPlaceholders = _loadSkipped = 0;
            _loadPlaceholderByRace = new Dictionary<int, (int, string, bool, string)>();
        }

        // Removes `sp` from the visible SpawnPoints list and parks it in HiddenSpawnPoints.
        // Callers (SpawnDeleteAction / session recovery) also strip the model from
        // Engine.AniModels and Controller.CharacterModels so it stops rendering + being
        // pickable. Clears selection when `sp` was selected.
        public void Hide(SpawnPoint sp)
        {
            if (sp == null) return;
            var id = sp.Record.Spawn.Id;
            if (Selected == sp)
            {
                Selected = null;
                SpawnSelected?.Invoke(null);
            }
            SpawnPoints.Remove(sp);
            HiddenSpawnPoints[id] = sp;
        }

        // Pulls a previously-hidden SpawnPoint back into SpawnPoints. Returns null when the
        // id isn't hidden (idempotent — no-op on double-restore). Caller handles engine +
        // CharacterModels re-attach so the ordering matches the delete path in reverse.
        public SpawnPoint Restore(int spawnId)
        {
            if (!HiddenSpawnPoints.TryGetValue(spawnId, out var sp)) return null;
            HiddenSpawnPoints.Remove(spawnId);
            SpawnPoints.Add(sp);
            return sp;
        }

        // Processes a slice of records. Call between PrepareForLoad and FinishLoad. Safe to
        // call repeatedly — counters accumulate across batches so FinishLoad's log reflects
        // the total. `zoneName` is used to prefer zone-local chr models when a race resolves
        // to multiple candidates present across chr zips (e.g. race 42 → WOL classic vs
        // WOF Kunark scaled wolf).
        public void LoadBatch(
            IEnumerable<SpawnRecord> records,
            EngineCore engine,
            Dictionary<string, AniModel> modelCache,
            Dictionary<string, string> availableModels,
            AniModel fallback,
            string zoneName)
        {
            foreach (var record in records)
                LoadOne(record, engine, modelCache, availableModels, fallback, zoneName);
        }

        void LoadOne(
            SpawnRecord record,
            EngineCore engine,
            Dictionary<string, AniModel> modelCache,
            Dictionary<string, string> availableModels,
            AniModel fallback,
            string zoneName)
        {
            var (sp, isPlaceholder, resolved) = BuildAndAdd(record, engine, modelCache, availableModels, fallback, zoneName);
            if (sp == null)
            {
                _loadSkipped++;
                return;
            }
            if (isPlaceholder)
            {
                _loadPlaceholders++;
                var npc = record.Entries.OrderByDescending(e => e.Entry.Chance).FirstOrDefault()?.Npc;
                if (npc != null)
                {
                    var key = npc.Race;
                    if (!_loadPlaceholderByRace.TryGetValue(key, out var stat))
                        stat = (0, npc.Name ?? "?", resolved == null || resolved.Count == 0, string.Join(",", resolved ?? new List<string>()));
                    _loadPlaceholderByRace[key] = (stat.Count + 1, stat.ExampleName, stat.Unmapped, stat.TriedCodes);
                }
            }
            else
            {
                _loadModelled++;
            }
        }

        // Public single-record load — used by Controller.DuplicateSelectedSpawn (and any
        // future add-spawn action) to build a fresh SpawnPoint at runtime with the same
        // model-resolution pipeline the initial zone load uses. Returns null when even
        // the fallback isn't available (very rare — only if the zone loaded with no
        // default character at all). Bypasses the placeholder-telemetry bookkeeping.
        public SpawnPoint LoadSingle(
            SpawnRecord record,
            EngineCore engine,
            Dictionary<string, AniModel> modelCache,
            Dictionary<string, string> availableModels,
            AniModel fallback,
            string zoneName)
        {
            var (sp, _, _) = BuildAndAdd(record, engine, modelCache, availableModels, fallback, zoneName);
            return sp;
        }

        // Shared core of LoadOne / LoadSingle: resolves the model, builds the
        // AniModelInstance, wraps in a SpawnPoint, and appends to SpawnPoints. Returns
        // (null, false, null) when even fallback is unavailable. `resolvedCodes` is
        // returned so LoadOne can log a useful placeholder reason.
        (SpawnPoint sp, bool isPlaceholder, List<string> resolvedCodes) BuildAndAdd(
            SpawnRecord record,
            EngineCore engine,
            Dictionary<string, AniModel> modelCache,
            Dictionary<string, string> availableModels,
            AniModel fallback,
            string zoneName)
        {
            // Initial focus = the index of the highest-Chance entry. Ties break to the
            // first entry encountered (List order — usually the DB's row order). Stored on
            // the SpawnPoint below so the sidebar cycler can shift focus to a lower-chance
            // sibling without changing the load-time visual.
            var primaryIndex = 0;
            if (record.Entries != null && record.Entries.Count > 0)
            {
                float bestChance = float.NegativeInfinity;
                for (int i = 0; i < record.Entries.Count; i++)
                {
                    var c = record.Entries[i]?.Entry?.Chance ?? 0f;
                    if (c > bestChance) { bestChance = c; primaryIndex = i; }
                }
            }
            var primaryEntry = (record.Entries != null && record.Entries.Count > primaryIndex)
                ? record.Entries[primaryIndex]
                : null;
            var npc = primaryEntry?.Npc;

            AniModel aniModel = null;
            bool isPlaceholder = false;
            string chosenCode = null;
            List<string> triedCodes = null;

            if (npc != null)
            {
                triedCodes = RaceModelMapper.ResolveCandidates(npc.Race, npc.Gender).ToList();
                chosenCode = ResolveChosenCode(triedCodes, availableModels, zoneName);

                if (chosenCode != null)
                {
                    var textureIdx = npc.Texture;
                    var helmIdx    = npc.HelmTexture;
                    var faceIdx    = npc.Face;
                    var cacheKey = (textureIdx | helmIdx | faceIdx) == 0
                        ? chosenCode
                        : $"{chosenCode}#{textureIdx}#{helmIdx}#{faceIdx}";
                    if (!modelCache.TryGetValue(cacheKey, out aniModel))
                    {
                        try
                        {
                            aniModel = Loader.LoadCharacter(
                                availableModels[chosenCode], chosenCode, SpawnAnimations,
                                singleFrame: true,
                                textureIndex: textureIdx,
                                helmTextureIndex: helmIdx,
                                faceIndex: faceIdx);
                            modelCache[cacheKey] = aniModel;
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[SpawnManager] Failed to load '{chosenCode}' (t={textureIdx} h={helmIdx} f={faceIdx}): {ex.Message}");
                        }
                    }
                }
            }

            if (aniModel == null)
            {
                aniModel = fallback;
                isPlaceholder = true;
            }
            if (aniModel == null) return (null, false, triedCodes);

            var pos = new Vector3(record.Spawn.Y, record.Spawn.X, record.Spawn.Z);
            var idle = SpawnAnimationCandidates.FirstOrDefault(a => aniModel.AvailableAnimations.Contains(a)) ?? "";
            var sizeScale = ComputeScale(npc);
            var instance = new AniModelInstance(aniModel)
            {
                Animation   = idle,
                Rotation    = HeadingToRotation(record.Spawn.Heading),
                Position    = pos,
                Scale       = sizeScale,
                RenderZBias = ComputeRenderZBias(pos, aniModel, sizeScale),
            };

            engine.Add(instance);
            var sp = new SpawnPoint(record, instance, isPlaceholder);
            sp.FocusedEntryIndex = primaryIndex;
            SpawnPoints.Add(sp);
            return (sp, isPlaceholder, triedCodes);
        }

        // Groups spawn2 rows sharing the exact same DB (x, y, z) — the EQEmu respawn
        // rotation pattern that causes multiple spawn models to visually stack in
        // VisualEQ (see project memory: spawn-stacking-not-a-bug). Populates each
        // stacked SpawnPoint's StackSiblings so the Spawn Info sidebar can offer a
        // cycler. Runs once at end-of-load; a spawn move edit doesn't invalidate the
        // stack list (small quirk — accept for now).
        void ComputeStacks()
        {
            var groups = SpawnPoints
                .GroupBy(sp => (
                    (long)Math.Round(sp.Record.Spawn.X * 1000f),
                    (long)Math.Round(sp.Record.Spawn.Y * 1000f),
                    (long)Math.Round(sp.Record.Spawn.Z * 1000f)))
                .Where(g => g.Count() > 1)
                .ToList();

            var stackedSpawnCount = 0;
            foreach (var g in groups)
            {
                var members = g.OrderBy(sp => sp.Record.Spawn.Id).ToList();
                foreach (var sp in members)
                    sp.StackSiblings = members;
                stackedSpawnCount += members.Count;
            }

            if (groups.Count > 0)
                Console.WriteLine($"[SpawnManager] {groups.Count} stacked coord(s) covering {stackedSpawnCount} spawn2 row(s)");
        }

        // Emits the summary + placeholder breakdown log. Call once after all batches.
        public void FinishLoad()
        {
            ComputeStacks();
            Console.WriteLine($"[SpawnManager] {_loadModelled} modelled, {_loadPlaceholders} placeholders, {_loadSkipped} skipped");

            if (_loadPlaceholderByRace != null && _loadPlaceholderByRace.Count > 0)
            {
                Console.WriteLine("[SpawnManager] Placeholder breakdown by race (count | race | reason | example):");
                foreach (var kv in _loadPlaceholderByRace.OrderByDescending(kv => kv.Value.Count))
                {
                    var reason = kv.Value.Unmapped
                        ? "unmapped race"
                        : $"no chr zip has any of [{kv.Value.TriedCodes}]";
                    Console.WriteLine($"  {kv.Value.Count,3}× race={kv.Key,-4} — {reason} (e.g. '{kv.Value.ExampleName}')");
                }
            }
        }

        // Backwards-compatible: one-shot load of every record + telemetry log.
        public void LoadFromRecords(
            IEnumerable<SpawnRecord> records,
            EngineCore engine,
            Dictionary<string, AniModel> modelCache,
            Dictionary<string, string> availableModels,
            AniModel fallback,
            string zoneName)
        {
            PrepareForLoad();
            LoadBatch(records, engine, modelCache, availableModels, fallback, zoneName);
            FinishLoad();
        }

        // Resolves a race's candidate list to a single chr code. Two-pass:
        //   1) Prefer a candidate whose availableModels path is the zone's own chr zip.
        //   2) Fall back to the first candidate present in availableModels from any source.
        // Pass 1 disambiguates one-race-two-meshes cases (race 42 → WOL classic vs WOF
        // Kunark scaled wolf) where BuildAvailableModels's cross-zone merge would otherwise
        // leak the wrong-region mesh into a zone that has its own variant.
        static string ResolveChosenCode(
            List<string> triedCodes,
            Dictionary<string, string> availableModels,
            string zoneName)
        {
            if (triedCodes == null || triedCodes.Count == 0) return null;
            if (!string.IsNullOrEmpty(zoneName))
            {
                var zoneChr = $"{zoneName}_chr_oes.zip";
                foreach (var candidate in triedCodes)
                {
                    if (availableModels.TryGetValue(candidate, out var path)
                        && string.Equals(Path.GetFileName(path), zoneChr, StringComparison.OrdinalIgnoreCase))
                    {
                        return candidate;
                    }
                }
            }
            foreach (var candidate in triedCodes)
            {
                if (availableModels.ContainsKey(candidate)) return candidate;
            }
            return null;
        }

        // Rebuild the AniModelInstance backing a SpawnPoint after a visual-affecting edit
        // (race / gender / size / texture / helm / face). Re-runs the same resolution
        // pipeline used at load time — RaceModelMapper.ResolveCandidates → availableModels
        // filter → modelCache lookup or Loader.LoadCharacter — and swaps the whole
        // AniModelInstance on the SpawnPoint (position, rotation, and now-recomputed scale
        // carry over). Also updates the engine's live instance list so the new model
        // actually renders. Idempotent per (sp, effective) — safe to call on every edit
        // even when the field didn't change race/gender/etc., though the caller should
        // filter to avoid the unnecessary cache lookup.
        //
        // MUST be called on the GL thread (Loader.LoadCharacter builds meshes + textures).
        // See CLAUDE.md §7.
        //
        // Returns true if the visual actually changed (new AniModel or new scale), false
        // if we ended up with the same instance the SpawnPoint already had — lets callers
        // skip camera re-framing when nothing moved.
        public bool RebuildInstanceForNpc(
            SpawnPoint sp,
            VisualEQ.Database.Models.NpcTypeFull effective,
            EngineCore engine,
            List<AniModelInstance> characterModels,
            Dictionary<string, AniModel> modelCache,
            Dictionary<string, string> availableModels,
            AniModel fallback,
            string zoneName)
        {
            if (sp == null || effective == null) return false;

            // Re-resolve chr code from the (possibly-changed) race + gender.
            var triedCodes = RaceModelMapper.ResolveCandidates(effective.Race, effective.Gender).ToList();
            string chosenCode = ResolveChosenCode(triedCodes, availableModels, zoneName);

            AniModel newAniModel = null;
            bool isPlaceholder = false;

            if (chosenCode != null)
            {
                var textureIdx = effective.Texture;
                var helmIdx    = effective.HelmTexture;
                var faceIdx    = effective.Face;
                var cacheKey = (textureIdx | helmIdx | faceIdx) == 0
                    ? chosenCode
                    : $"{chosenCode}#{textureIdx}#{helmIdx}#{faceIdx}";
                if (!modelCache.TryGetValue(cacheKey, out newAniModel))
                {
                    try
                    {
                        newAniModel = Loader.LoadCharacter(
                            availableModels[chosenCode], chosenCode, SpawnAnimations,
                            singleFrame: true,
                            textureIndex: textureIdx,
                            helmTextureIndex: helmIdx,
                            faceIndex: faceIdx);
                        modelCache[cacheKey] = newAniModel;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[SpawnManager] Refresh failed for '{chosenCode}' (t={textureIdx} h={helmIdx} f={faceIdx}): {ex.Message}");
                    }
                }
            }

            if (newAniModel == null)
            {
                newAniModel = fallback;
                isPlaceholder = true;
            }
            if (newAniModel == null) return false; // nothing to render — keep the old instance visible

            var newScale = ComputeScale(effective);

            // Short-circuit when the resolved instance is functionally the same (same
            // AniModel, same scale) — happens if the edit only touched a field the model
            // resolution ignores. Caller uses this to skip camera reframe.
            var oldModel = sp.Model;
            var oldAniModel = oldModel?.Model;
            var oldScale = oldModel?.Scale ?? 1f;
            if (ReferenceEquals(oldAniModel, newAniModel) && Math.Abs(oldScale - newScale) < 0.0001f)
                return false;

            // Build the replacement instance carrying over position + rotation (and the
            // dirty-move state on SpawnPoint is unaffected since we don't touch Record).
            var idle = SpawnAnimationCandidates.FirstOrDefault(a => newAniModel.AvailableAnimations.Contains(a)) ?? "";
            var newInstance = new AniModelInstance(newAniModel)
            {
                Animation   = idle,
                Rotation    = oldModel?.Rotation ?? Quaternion.Identity,
                Position    = oldModel?.Position ?? new Vector3(0, 0, 0),
                Scale       = newScale,
                RenderZBias = ComputeRenderZBias(oldModel?.Position ?? Vector3.Zero, newAniModel, newScale),
            };

            // Swap in the scene: remove old, add new. Two lists have to stay in sync —
            // engine.AniModels drives rendering, and Controller.CharacterModels drives
            // ModelSelector's ray-cast picking (see Controller ctor). Skipping the second
            // one leaves the picker holding a stale AniModelInstance whose SpawnPoint's
            // Model no longer matches, so clicks silently fail SpawnManager.Select's
            // FirstOrDefault(p.Model == model) lookup.
            var wasSelected = Selected == sp;
            if (oldModel != null)
            {
                engine.Remove(oldModel);
                characterModels?.Remove(oldModel);
            }
            engine.Add(newInstance);
            characterModels?.Add(newInstance);
            sp.Model = newInstance;
            sp.IsPlaceholder = isPlaceholder;
            if (wasSelected) Selected = sp; // no-op assignment, kept for clarity

            return true;
        }

        // Called by ModelSelector.OnSelectionChanged — maps the raw instance to a SpawnPoint.
        public void Select(AniModelInstance model)
        {
            if (model == null)
            {
                Selected = null;
                SpawnSelected?.Invoke(null);
                return;
            }

            var sp = SpawnPoints.FirstOrDefault(p => p.Model == model);
            Selected = sp;
            SpawnSelected?.Invoke(sp);

            if (sp != null)
            {
                var npcName = sp.FocusedNpc?.Name ?? "???";
                Console.WriteLine(
                    $"[SpawnManager] Selected #{sp.Record.Spawn.Id} '{sp.Record.Spawn.SpawnGroupName}' — {npcName}");
            }
        }

        // Builds name → chr zip path map for a zone. For each model name we pick the
        // chr zip where it has the most animation content (anims × 1000 + meshes), so
        // that empty stubs in zone-specific chr zips don't shadow the fully-animated
        // versions in global_chr. Ties are broken by scan order (zone first, then
        // global*, then other), which lets zone-only models (DER/GHU/FPM/SHIP) still
        // pick up the zone-specific mesh even if a decoded global zip has a same-name
        // stub. Zone models with meshes-but-no-anims (SHIP, BOAT) win against any
        // other zip that doesn't have the model at all.
        internal static Dictionary<string, string> BuildAvailableModels(string zoneName, string dir)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir))
            {
                Console.WriteLine($"[SpawnManager] Directory '{dir}' does not exist.");
                return result;
            }

            var allChrZips = Directory.EnumerateFiles(dir, "*_chr_oes.zip").ToList();

            string ZonePath()
            {
                var target = $"{zoneName}_chr_oes.zip";
                return allChrZips.FirstOrDefault(p =>
                    string.Equals(Path.GetFileName(p), target, StringComparison.OrdinalIgnoreCase));
            }

            bool IsGlobal(string p) => Path.GetFileName(p)
                .StartsWith("global", StringComparison.OrdinalIgnoreCase);

            var ordered = new List<string>();
            var zonePath = ZonePath();
            if (zonePath != null) ordered.Add(zonePath);
            ordered.AddRange(allChrZips.Where(IsGlobal).OrderBy(p => p, StringComparer.OrdinalIgnoreCase));
            ordered.AddRange(allChrZips
                .Where(p => p != zonePath && !IsGlobal(p))
                .OrderBy(p => p, StringComparer.OrdinalIgnoreCase));

            // Zone-first, then richness. Zone chr wins for anything it declares —
            // freporte's PRE (SirensBane) must not be overridden by overthere's PRE
            // (Bloated Belly, higher anim count but a different visual variant of
            // the same skeleton family). For models the zone does NOT declare,
            // richness-wins across the remaining chr zips.
            var bestScore = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var zoneLocked = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            int zoneModels = 0, globalModels = 0, otherModels = 0;

            foreach (var path in ordered)
            {
                var richness = Loader.GetCharacterModelRichness(path);
                var isZone = path == zonePath;
                int added = 0;
                foreach (var kv in richness)
                {
                    // Zone-declared names are locked to the zone chr regardless of
                    // any richer version another chr zip might carry.
                    if (zoneLocked.Contains(kv.Key)) continue;
                    if (bestScore.TryGetValue(kv.Key, out var cur) && cur >= kv.Value) continue;
                    bestScore[kv.Key] = kv.Value;
                    var wasNew = !result.ContainsKey(kv.Key);
                    result[kv.Key] = path;
                    if (isZone) zoneLocked.Add(kv.Key);
                    if (wasNew) added++;
                }

                if (path == zonePath) zoneModels += added;
                else if (IsGlobal(path)) globalModels += added;
                else otherModels += added;
            }

            Console.WriteLine(
                $"[SpawnManager] '{zoneName}' models: {result.Count} total " +
                $"(zone-new={zoneModels}, global-new={globalModels}, other-new={otherModels})");
            return result;
        }
    }
}
