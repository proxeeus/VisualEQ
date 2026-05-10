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

            foreach (var record in records)
            {
                var primaryEntry = record.Entries
                    .OrderByDescending(e => e.Entry.Chance)
                    .FirstOrDefault();
                var npc = primaryEntry?.Npc;

                AniModel aniModel = null;
                bool isPlaceholder = false;

                if (npc != null)
                {
                    // Try gender-specific name first, then base code.
                    string genderCode = RaceModelMapper.ResolveWithGender(npc.Race, npc.Gender);
                    string baseCode   = RaceModelMapper.Resolve(npc.Race);

                    string chosenCode = null;
                    if (genderCode != null && availableModels.ContainsKey(genderCode))
                        chosenCode = genderCode;
                    else if (baseCode != null && availableModels.ContainsKey(baseCode))
                        chosenCode = baseCode;

                    if (chosenCode != null)
                    {
                        if (!modelCache.TryGetValue(chosenCode, out aniModel))
                        {
                            try
                            {
                                aniModel = Loader.LoadCharacter(availableModels[chosenCode], chosenCode);
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
                }

                if (aniModel == null)
                {
                    skipped++;
                    continue;
                }

                // EQ DB stores (x=east, y=north, z=up); engine expects (x=east, y=forward, z=up)
                // but the axes are transposed relative to the scene — swap X and Y.
                var pos = new Vector3(record.Spawn.Y, record.Spawn.X, record.Spawn.Z);
                var instance = new AniModelInstance(aniModel)
                {
                    Animation = "C05",
                    Position  = pos
                };

                engine.Add(instance);

                SpawnPoints.Add(new SpawnPoint(record, instance, isPlaceholder));
                if (isPlaceholder) placeholders++; else modelled++;
            }

            Console.WriteLine($"[SpawnManager] {modelled} modelled, {placeholders} placeholders, {skipped} skipped");
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

        // Builds name → chr zip path map for a zone. Tries zone-specific chr first, then gfaydark fallback.
        internal static Dictionary<string, string> BuildAvailableModels(string zoneName)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            var candidatePaths = new[]
            {
                $"../ConverterApp/{zoneName}_chr_oes.zip",
                "../ConverterApp/gfaydark_chr_oes.zip"
            };

            foreach (var path in candidatePaths)
            {
                if (!File.Exists(path)) continue;
                foreach (var name in Loader.GetAvailableCharacterModels(path))
                {
                    if (!result.ContainsKey(name))
                        result[name] = path;
                }
            }

            Console.WriteLine($"[SpawnManager] {result.Count} character models available for '{zoneName}'");
            return result;
        }
    }
}
