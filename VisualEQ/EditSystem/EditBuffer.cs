using System;
using System.Collections.Generic;

namespace VisualEQ.EditSystem
{
    // Per-zone pending edits, serialized to JSON at %APPDATA%\VisualEQ\pending\<zone>.json.
    // Only mutated items appear — an unmodified spawn is not present in Spawns.
    //
    // Every edit records both the original DB values (for revert-to-source) and the current
    // in-progress values (for commit + session recovery). Commit writes Current* to DB and
    // clears the buffer.
    public class EditBuffer
    {
        public string Zone { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime LastModifiedAt { get; set; }

        // Bump when the on-disk shape changes. EditBufferManager will refuse to load
        // buffers whose version is higher than the current code understands.
        public int SchemaVersion { get; set; } = 1;

        public Dictionary<int, SpawnEdit> Spawns { get; set; } = new Dictionary<int, SpawnEdit>();
        public Dictionary<string, GridEntryEdit> GridEntries { get; set; } = new Dictionary<string, GridEntryEdit>();

        // Reserved for Phase 5.9+ (npc_types edits).
        public Dictionary<int, NpcEdit> Npcs { get; set; } = new Dictionary<int, NpcEdit>();

        public bool IsEmpty => Spawns.Count == 0 && GridEntries.Count == 0 && Npcs.Count == 0;
        public int TotalPending => Spawns.Count + GridEntries.Count + Npcs.Count;

        // Composite key helper for grid entries: (gridId, number).
        public static string GridEntryKey(int gridId, int number) => $"{gridId}:{number}";
    }

    public class SpawnEdit
    {
        public int SpawnId { get; set; }

        // DB-space coordinates (X = east/west, Y = north/south, Z = up).
        public float OriginalX { get; set; }
        public float OriginalY { get; set; }
        public float OriginalZ { get; set; }
        public float OriginalHeading { get; set; }

        public float CurrentX { get; set; }
        public float CurrentY { get; set; }
        public float CurrentZ { get; set; }
        public float CurrentHeading { get; set; }

        // Human-readable label for the sidebar list (populated at edit time from the primary
        // NPC's name). Not authoritative — DB is source of truth on commit.
        public string DisplayName { get; set; }
        public DateTime LastModifiedAt { get; set; }
    }

    public class GridEntryEdit
    {
        public int GridId { get; set; }
        public int Number { get; set; }

        public float OriginalX { get; set; }
        public float OriginalY { get; set; }
        public float OriginalZ { get; set; }
        public float OriginalHeading { get; set; }
        public int OriginalPause { get; set; }

        public float CurrentX { get; set; }
        public float CurrentY { get; set; }
        public float CurrentZ { get; set; }
        public float CurrentHeading { get; set; }
        public int CurrentPause { get; set; }

        public DateTime LastModifiedAt { get; set; }
    }

    public class NpcEdit
    {
        public int NpcId { get; set; }
        // Populated when Phase 5.9+ NPC editing lands. Kept as a placeholder so the JSON
        // shape is stable across releases.
        public DateTime LastModifiedAt { get; set; }
    }
}
