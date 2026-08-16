using System;
using System.IO;
using System.Text.Json;

namespace VisualEQ.EditSystem
{
    // Static helper for locating and (de)serialising per-zone edit buffers on disk.
    // Files live at %APPDATA%\VisualEQ\pending\<zone>.json. Missing files are treated
    // as "no pending edits". Corrupt files log a warning and are treated the same way.
    public static class EditBufferManager
    {
        static readonly string PendingDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "VisualEQ", "pending");

        static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
        };

        static string PathForZone(string zone) => Path.Combine(PendingDir, $"{zone}.json");

        public static bool ExistsForZone(string zone) =>
            !string.IsNullOrEmpty(zone) && File.Exists(PathForZone(zone));

        // Returns null when the file doesn't exist or fails to parse. Callers should treat
        // both cases as "start with a fresh buffer".
        public static EditBuffer LoadForZone(string zone)
        {
            if (string.IsNullOrEmpty(zone)) return null;
            var path = PathForZone(zone);
            if (!File.Exists(path)) return null;

            try
            {
                var json = File.ReadAllText(path);
                var buffer = JsonSerializer.Deserialize<EditBuffer>(json, JsonOptions);
                if (buffer == null) return null;
                if (buffer.SchemaVersion > 14)
                {
                    Console.WriteLine($"[EditBufferManager] Buffer for '{zone}' has newer schema version {buffer.SchemaVersion}; ignoring.");
                    return null;
                }
                // v1  → v2 : ZonePoints wasn't part of the schema; leave the dict empty.
                // v2  → v3 : ZonePointEdit scalar fields (target/heading/mode/keep*/plane
                //          bounds) default to zero/null on deserialisation;
                //          ScalarOriginalsSeeded is false so ApplyPendingBuffer will seed
                //          Original* from the live row.
                // v3  → v4 : adds ZonePointInserts + ZonePointDeletes.
                // v4  → v5 : adds Centerpoint on GridEntryEdit + Grids / GridEntryInserts /
                //          GridEntryDeletes.
                // v5  → v6 : adds GridInserts for whole-grid creation.
                // v6  → v7 : adds SpawnDeletes for pending spawn2 row removals.
                // v7  → v8 : adds SpawnInserts for pending spawn2 duplicates.
                // v8  → v9 : adds Npcs (sparse NPC field edits).
                // v9  → v10: adds NpcFactionEntries.
                // v10 → v11: adds LootTableEntries + LootDropEntries.
                // v11 → v12: adds LootTables (header-field edits).
                // v12 → v13: adds NpcFactions (header-field edits).
                // v13 → v14: adds SpawnEntries (per-row spawnentry ops on existing
                //          spawngroups). Pending-insert spawngroups keep routing through
                //          SpawnInsert.Entries.
                if (buffer.SchemaVersion < 14) buffer.SchemaVersion = 14;
                if (buffer.ZonePoints == null) buffer.ZonePoints = new System.Collections.Generic.Dictionary<int, ZonePointEdit>();
                if (buffer.ZonePointInserts == null) buffer.ZonePointInserts = new System.Collections.Generic.Dictionary<int, ZonePointInsert>();
                if (buffer.ZonePointDeletes == null) buffer.ZonePointDeletes = new System.Collections.Generic.HashSet<int>();
                if (buffer.Grids == null) buffer.Grids = new System.Collections.Generic.Dictionary<string, GridEdit>();
                if (buffer.GridEntryInserts == null) buffer.GridEntryInserts = new System.Collections.Generic.Dictionary<string, GridEntryInsert>();
                if (buffer.GridEntryDeletes == null) buffer.GridEntryDeletes = new System.Collections.Generic.Dictionary<string, GridEntryDelete>();
                if (buffer.GridInserts == null) buffer.GridInserts = new System.Collections.Generic.Dictionary<int, GridInsert>();
                if (buffer.SpawnDeletes == null) buffer.SpawnDeletes = new System.Collections.Generic.Dictionary<int, SpawnDelete>();
                if (buffer.SpawnInserts == null) buffer.SpawnInserts = new System.Collections.Generic.Dictionary<int, SpawnInsert>();
                // Defensive null-init for v9-v14 collections so older on-disk buffers that
                // lack these keys don't NRE inside TotalPending / EditCommitter.
                if (buffer.Npcs == null) buffer.Npcs = new System.Collections.Generic.Dictionary<int, NpcEdit>();
                if (buffer.NpcFactionEntries == null) buffer.NpcFactionEntries = new System.Collections.Generic.Dictionary<string, NpcFactionEntryOp>();
                if (buffer.LootTableEntries == null) buffer.LootTableEntries = new System.Collections.Generic.Dictionary<string, LootTableEntryOp>();
                if (buffer.LootDropEntries == null) buffer.LootDropEntries = new System.Collections.Generic.Dictionary<string, LootDropEntryOp>();
                if (buffer.LootTables == null) buffer.LootTables = new System.Collections.Generic.Dictionary<int, LootTableEdit>();
                if (buffer.NpcFactions == null) buffer.NpcFactions = new System.Collections.Generic.Dictionary<int, NpcFactionRowEdit>();
                if (buffer.SpawnEntries == null) buffer.SpawnEntries = new System.Collections.Generic.Dictionary<string, SpawnEntryOp>();
                return buffer;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[EditBufferManager] Failed to read buffer for '{zone}': {ex.Message}");
                return null;
            }
        }

        public static void SaveForZone(EditBuffer buffer)
        {
            if (buffer == null || string.IsNullOrEmpty(buffer.Zone)) return;
            try
            {
                Directory.CreateDirectory(PendingDir);
                buffer.LastModifiedAt = DateTime.UtcNow;
                var json = JsonSerializer.Serialize(buffer, JsonOptions);
                File.WriteAllText(PathForZone(buffer.Zone), json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[EditBufferManager] Failed to save buffer for '{buffer.Zone}': {ex.Message}");
            }
        }

        public static void DeleteForZone(string zone)
        {
            if (string.IsNullOrEmpty(zone)) return;
            var path = PathForZone(zone);
            try
            {
                if (File.Exists(path)) File.Delete(path);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[EditBufferManager] Failed to delete buffer for '{zone}': {ex.Message}");
            }
        }
    }
}
