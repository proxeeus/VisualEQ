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
        //   v1 — spawns + grid entries only
        //   v2 — adds ZonePoints (trilogy_zone_points edits, position + size only)
        //   v3 — adds scalar fields to ZonePointEdit (target/heading/mode/keep*/plane bounds)
        public int SchemaVersion { get; set; } = 3;

        public Dictionary<int, SpawnEdit> Spawns { get; set; } = new Dictionary<int, SpawnEdit>();
        public Dictionary<string, GridEntryEdit> GridEntries { get; set; } = new Dictionary<string, GridEntryEdit>();
        public Dictionary<int, ZonePointEdit> ZonePoints { get; set; } = new Dictionary<int, ZonePointEdit>();

        // Reserved for Phase 5.9+ (npc_types edits).
        public Dictionary<int, NpcEdit> Npcs { get; set; } = new Dictionary<int, NpcEdit>();

        public bool IsEmpty => Spawns.Count == 0 && GridEntries.Count == 0 && ZonePoints.Count == 0 && Npcs.Count == 0;
        public int TotalPending => Spawns.Count + GridEntries.Count + ZonePoints.Count + Npcs.Count;

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

    // One row of pending edits for trilogy_zone_points. Covers every column exposed by
    // the inspector: source position (X/Y/Z), source size (Zrange, MaxZDiff), landing
    // heading, target zone + coords, trigger mode, plane-crossing bounds (MinVert/MaxVert/
    // CenterPoint), and keep-axis flags.
    //
    // v2 buffers written before scalar fields existed load with the scalar Original*/
    // Current* pairs defaulted to 0/null; ApplyPendingBuffer seeds them from the live row
    // so post-load inspector edits still get a valid revert baseline.
    public class ZonePointEdit
    {
        public int Id { get; set; }

        // DB-space coordinates (X = east/west, Y = north/south, Z = up).
        public float OriginalX { get; set; }
        public float OriginalY { get; set; }
        public float OriginalZ { get; set; }
        public int OriginalZrange { get; set; }
        public int OriginalMaxZDiff { get; set; }

        public float CurrentX { get; set; }
        public float CurrentY { get; set; }
        public float CurrentZ { get; set; }
        public int CurrentZrange { get; set; }
        public int CurrentMaxZDiff { get; set; }

        // ─── v3 scalar fields ────────────────────────────────────────────────────────
        public float OriginalHeading { get; set; }
        public float CurrentHeading { get; set; }

        public string OriginalTargetZone { get; set; }
        public string CurrentTargetZone { get; set; }

        public float OriginalTargetX { get; set; }
        public float OriginalTargetY { get; set; }
        public float OriginalTargetZ { get; set; }
        public float CurrentTargetX { get; set; }
        public float CurrentTargetY { get; set; }
        public float CurrentTargetZ { get; set; }

        public byte OriginalUseNewZoning { get; set; }
        public byte CurrentUseNewZoning { get; set; }

        public float OriginalMinVert { get; set; }
        public float OriginalMaxVert { get; set; }
        public float OriginalCenterPoint { get; set; }
        public float CurrentMinVert { get; set; }
        public float CurrentMaxVert { get; set; }
        public float CurrentCenterPoint { get; set; }

        public int OriginalKeepX { get; set; }
        public int OriginalKeepY { get; set; }
        public int OriginalKeepZ { get; set; }
        public int CurrentKeepX { get; set; }
        public int CurrentKeepY { get; set; }
        public int CurrentKeepZ { get; set; }

        // Flip once ApplyPendingBuffer has seeded Original* from the live row for a
        // v2-loaded entry, so subsequent seed passes skip it. Not persisted (fresh runs
        // start with the flag false but the seed is a no-op since Original* was written
        // during the previous run).
        [System.Text.Json.Serialization.JsonIgnore]
        public bool ScalarOriginalsSeeded { get; set; }

        // Human-readable label for the sidebar list ("→ felwithea" etc.). Not authoritative.
        public string DisplayName { get; set; }
        public DateTime LastModifiedAt { get; set; }
    }
}
