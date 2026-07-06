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

        public event Action<SpawnPoint> SpawnSelected;
        public event Action<SpawnPoint> SpawnMoved;

        // Clears any existing spawn points and builds new ones from the fetched records.
        // availableModels: name → chr zip path (built by BuildAvailableModels).
        // modelCache: shared across calls so the same AniModel is not loaded twice.
        // fallback: used when no model can be resolved; if null, the spawn is skipped.
        public void LoadFromRecords(
            IEnumerable<SpawnRecord> records,
            EngineCore engine,
            Dictionary<string, AniModel> modelCache,
            Dictionary<string, string> availableModels,
            AniModel fallback)
        {
            SpawnPoints.Clear();
            Selected = null;

            int modelled = 0, placeholders = 0, skipped = 0;
            // Per-race placeholder telemetry: race → (count, first example NPC, unmapped, tried codes).
            var placeholderByRace = new Dictionary<int, (int Count, string ExampleName, bool Unmapped, string TriedCodes)>();

            foreach (var record in records)
            {
                var primaryEntry = record.Entries
                    .OrderByDescending(e => e.Entry.Chance)
                    .FirstOrDefault();
                var npc = primaryEntry?.Npc;

                AniModel aniModel = null;
                bool isPlaceholder = false;
                string chosenCode = null;
                List<string> triedCodes = null;

                if (npc != null)
                {
                    triedCodes = RaceModelMapper.ResolveCandidates(npc.Race, npc.Gender).ToList();
                    foreach (var candidate in triedCodes)
                    {
                        if (availableModels.ContainsKey(candidate))
                        {
                            chosenCode = candidate;
                            break;
                        }
                    }

                    if (chosenCode != null)
                    {
                        if (!modelCache.TryGetValue(chosenCode, out aniModel))
                        {
                            try
                            {
                                aniModel = Loader.LoadCharacter(availableModels[chosenCode], chosenCode, SpawnAnimations, singleFrame: true);
                                modelCache[chosenCode] = aniModel;
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"[SpawnManager] Failed to load '{chosenCode}': {ex.Message}");
                            }
                        }
                    }
                }

                if (aniModel == null)
                {
                    aniModel = fallback;
                    isPlaceholder = true;

                    if (npc != null)
                    {
                        var key = npc.Race;
                        if (!placeholderByRace.TryGetValue(key, out var stat))
                            stat = (0, npc.Name ?? "?", triedCodes == null || triedCodes.Count == 0, string.Join(",", triedCodes ?? new List<string>()));
                        placeholderByRace[key] = (stat.Count + 1, stat.ExampleName, stat.Unmapped, stat.TriedCodes);
                    }
                }

                if (aniModel == null)
                {
                    skipped++;
                    continue;
                }

                // EQ DB stores (x=east, y=north, z=up); engine expects (x=east, y=forward, z=up)
                // but the axes are transposed relative to the scene — swap X and Y.
                var pos = new Vector3(record.Spawn.Y, record.Spawn.X, record.Spawn.Z);
                // Pick the first candidate this specific model actually has; empty string is
                // the bind-pose fallback and always resolves in AnimatedMesh.Draw.
                var idle = SpawnAnimationCandidates.FirstOrDefault(a => aniModel.AvailableAnimations.Contains(a)) ?? "";
                var instance = new AniModelInstance(aniModel)
                {
                    Animation = idle,
                    Position  = pos
                };

                engine.Add(instance);

                SpawnPoints.Add(new SpawnPoint(record, instance, isPlaceholder));
                if (isPlaceholder) placeholders++; else modelled++;
            }

            Console.WriteLine($"[SpawnManager] {modelled} modelled, {placeholders} placeholders, {skipped} skipped");

            if (placeholderByRace.Count > 0)
            {
                Console.WriteLine("[SpawnManager] Placeholder breakdown by race (count | race | reason | example):");
                foreach (var kv in placeholderByRace.OrderByDescending(kv => kv.Value.Count))
                {
                    var reason = kv.Value.Unmapped
                        ? "unmapped race"
                        : $"no chr zip has any of [{kv.Value.TriedCodes}]";
                    Console.WriteLine($"  {kv.Value.Count,3}× race={kv.Key,-4} — {reason} (e.g. '{kv.Value.ExampleName}')");
                }
            }
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
                var npcName = sp.Record.Entries
                    .OrderByDescending(e => e.Entry.Chance)
                    .FirstOrDefault()?.Npc?.Name ?? "???";
                Console.WriteLine(
                    $"[SpawnManager] Selected #{sp.Record.Spawn.Id} '{sp.Record.Spawn.SpawnGroupName}' — {npcName}");
            }
        }

        // Builds name → chr zip path map for a zone. Search order (higher priority wins on collision):
        //   1. `{zone}_chr_oes.zip`             — zone-specific art
        //   2. `global*_chr_oes.zip`            — canonical playable races + shared monsters
        //   3. any other `*_chr_oes.zip`        — best-effort fallback (previously-decoded zones)
        internal static Dictionary<string, string> BuildAvailableModels(string zoneName)
        {
            const string dir = "../ConverterApp";
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            if (!Directory.Exists(dir))
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

            int zoneModels = 0, globalModels = 0, otherModels = 0;

            foreach (var path in ordered)
            {
                var models = Loader.GetAvailableCharacterModels(path);
                int added = 0;
                foreach (var name in models)
                {
                    if (result.ContainsKey(name)) continue;
                    result[name] = path;
                    added++;
                }

                if (path == zonePath) zoneModels += added;
                else if (IsGlobal(path)) globalModels += added;
                else otherModels += added;
            }

            Console.WriteLine(
                $"[SpawnManager] '{zoneName}' models: {result.Count} total " +
                $"(zone={zoneModels}, global={globalModels}, other={otherModels})");
            return result;
        }
    }
}
