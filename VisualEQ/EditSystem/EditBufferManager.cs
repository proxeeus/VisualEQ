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
                if (buffer.SchemaVersion > 5)
                {
                    Console.WriteLine($"[EditBufferManager] Buffer for '{zone}' has newer schema version {buffer.SchemaVersion}; ignoring.");
                    return null;
                }
                // v1 → v2: ZonePoints wasn't part of the schema; leave the dict empty.
                // v2 → v3: ZonePointEdit scalar fields (target/heading/mode/keep*/plane
                //         bounds) default to zero/null on deserialisation; ScalarOriginalsSeeded
                //         is false so ApplyPendingBuffer will seed Original* from the live row.
                // v3 → v4: adds ZonePointInserts + ZonePointDeletes for new-row/delete-row
                //         support. Both default to empty collections on load.
                // v4 → v5: adds Centerpoint on GridEntryEdit (defaults to 0, matching the
                //         DB default), plus Grids / GridEntryInserts / GridEntryDeletes
                //         collections. New collections default to empty.
                // v5 → v6: adds GridInserts for whole-grid creation. Defaults to empty
                //         (pre-v6 buffers never carried pending grid inserts).
                // v6 → v7: adds GridDeletes for whole-grid deletion. Defaults to empty.
                if (buffer.SchemaVersion < 7) buffer.SchemaVersion = 7;
                if (buffer.ZonePoints == null) buffer.ZonePoints = new System.Collections.Generic.Dictionary<int, ZonePointEdit>();
                if (buffer.ZonePointInserts == null) buffer.ZonePointInserts = new System.Collections.Generic.Dictionary<int, ZonePointInsert>();
                if (buffer.ZonePointDeletes == null) buffer.ZonePointDeletes = new System.Collections.Generic.HashSet<int>();
                if (buffer.Grids == null) buffer.Grids = new System.Collections.Generic.Dictionary<string, GridEdit>();
                if (buffer.GridEntryInserts == null) buffer.GridEntryInserts = new System.Collections.Generic.Dictionary<string, GridEntryInsert>();
                if (buffer.GridEntryDeletes == null) buffer.GridEntryDeletes = new System.Collections.Generic.Dictionary<string, GridEntryDelete>();
                if (buffer.GridInserts == null) buffer.GridInserts = new System.Collections.Generic.Dictionary<int, GridInsert>();
                if (buffer.GridDeletes == null) buffer.GridDeletes = new System.Collections.Generic.Dictionary<string, GridDelete>();
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
