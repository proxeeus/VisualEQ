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
        //   v4 — adds ZonePointInserts + ZonePointDeletes for new-row / delete-row support
        //   v5 — adds Centerpoint on GridEntryEdit, plus Grids + GridEntryInserts +
        //        GridEntryDeletes for grid metadata edits and per-waypoint add/delete
        //   v6 — adds GridInserts for whole-grid creation (temp id → real id at commit)
        //   v7 — adds SpawnDeletes for pending spawn2 row removals (delete-key + commit)
        //   v8 — adds SpawnInserts for pending spawn2 duplicates. Each carries the cloned
        //        spawngroup name + spawnentries so commit can INSERT spawngroup → spawnentries
        //        → spawn2 in one go, then remap the negative TempSpawnId to the real
        //        AUTO_INCREMENT id on the in-memory SpawnPoint.
        public int SchemaVersion { get; set; } = 8;

        public Dictionary<int, SpawnEdit> Spawns { get; set; } = new Dictionary<int, SpawnEdit>();

        // Pending spawn2 row deletes — DB ids of rows the user removed via Delete key or
        // sidebar. The removed SpawnPoint object is held live in SpawnManager.HiddenSpawnPoints
        // so Revert / Discard can re-attach it to the scene without a DB round-trip; a fresh
        // session load re-fetches from DB and hides again by matching this set.
        public Dictionary<int, SpawnDelete> SpawnDeletes { get; set; } = new Dictionary<int, SpawnDelete>();

        // Pending spawn2 row inserts — keyed by their negative temp id (see
        // SpawnManager.NextTempSpawnId). On commit each becomes a three-step INSERT
        // (spawngroup → spawnentries → spawn2) and its returned AUTO_INCREMENT spawn2 id
        // replaces the temp id on the in-memory SpawnPoint via Controller.OnCommitSucceeded.
        public Dictionary<int, SpawnInsert> SpawnInserts { get; set; } = new Dictionary<int, SpawnInsert>();
        public Dictionary<string, GridEntryEdit> GridEntries { get; set; } = new Dictionary<string, GridEntryEdit>();
        public Dictionary<int, ZonePointEdit> ZonePoints { get; set; } = new Dictionary<int, ZonePointEdit>();

        // Pending inserts — keyed by their negative temp id (see ZonePointManager.NextTempId).
        // On commit each becomes an INSERT and its returned AUTO_INCREMENT id replaces the
        // temp id on the in-memory ZonePoint.
        public Dictionary<int, ZonePointInsert> ZonePointInserts { get; set; } = new Dictionary<int, ZonePointInsert>();

        // Pending deletes — DB ids of rows the user removed. Excludes pending-insert rows
        // (those are dropped straight from ZonePointInserts instead of being tracked here).
        public HashSet<int> ZonePointDeletes { get; set; } = new HashSet<int>();

        // Grid metadata edits (grid.type / grid.type2). Key = "gridId:zoneId".
        public Dictionary<string, GridEdit> Grids { get; set; } = new Dictionary<string, GridEdit>();

        // Pending waypoint inserts. Key = "gridId:number". Number for a pending insert is
        // determined at edit time (append at max+1 within the grid).
        public Dictionary<string, GridEntryInsert> GridEntryInserts { get; set; } = new Dictionary<string, GridEntryInsert>();

        // Pending waypoint deletes. Key = "gridId:number". Row snapshot is preserved so
        // Discard/Revert can restore the waypoint into the scene.
        public Dictionary<string, GridEntryDelete> GridEntryDeletes { get; set; } = new Dictionary<string, GridEntryDelete>();

        // Pending whole-grid inserts. Key = negative temp id (see Controller.NextTempGridId).
        // On commit each becomes an INSERT into `grid` with a MAX(id)+1 resolution; the real
        // id then replaces the temp id via post-commit ZoneGrids reload. The seed waypoint
        // for a new grid is a normal GridEntryInsert entry with GridId = temp id.
        public Dictionary<int, GridInsert> GridInserts { get; set; } = new Dictionary<int, GridInsert>();

        // Reserved for Phase 5.9+ (npc_types edits).
        public Dictionary<int, NpcEdit> Npcs { get; set; } = new Dictionary<int, NpcEdit>();

        public bool IsEmpty =>
            Spawns.Count == 0 && SpawnDeletes.Count == 0 && SpawnInserts.Count == 0 && GridEntries.Count == 0 &&
            ZonePoints.Count == 0 && ZonePointInserts.Count == 0 && ZonePointDeletes.Count == 0 &&
            Grids.Count == 0 && GridInserts.Count == 0 &&
            GridEntryInserts.Count == 0 && GridEntryDeletes.Count == 0 &&
            Npcs.Count == 0;
        public int TotalPending =>
            Spawns.Count + SpawnDeletes.Count + SpawnInserts.Count + GridEntries.Count +
            ZonePoints.Count + ZonePointInserts.Count + ZonePointDeletes.Count +
            Grids.Count + GridInserts.Count +
            GridEntryInserts.Count + GridEntryDeletes.Count +
            Npcs.Count;

        // Composite key helper for grid entries: (gridId, number).
        public static string GridEntryKey(int gridId, int number) => $"{gridId}:{number}";

        // Composite key helper for grid metadata: (gridId, zoneId).
        public static string GridKey(int gridId, int zoneId) => $"{gridId}:{zoneId}";
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

    // Pending spawn2 row delete. Persisted fields are only what the sidebar + commit need:
    // the id (PK for the DELETE statement) and a display label. The live SpawnPoint that
    // was removed from the scene is held in SpawnManager.HiddenSpawnPoints so Revert /
    // Discard can splice it back without reloading from DB.
    public class SpawnDelete
    {
        public int SpawnId { get; set; }
        public string DisplayName { get; set; }
        public DateTime DeletedAt { get; set; }
    }

    // Pending spawn2 duplicate. Carries every field the three-step commit INSERT chain
    // needs: spawngroup metadata (name — UNIQUE in DB, so generated at duplicate time),
    // per-entry NPC rotation, and the spawn2 row itself. The in-scene SpawnPoint is
    // built at duplicate time and re-hydrated on session recovery via ApplyPendingBuffer
    // (which re-runs the same model-resolution pipeline used by the initial zone load).
    //
    // TempSpawnId is a negative sentinel from SpawnManager.NextTempSpawnId(); real
    // spawn2 ids are always positive AUTO_INCREMENT so a negative id unambiguously
    // marks a pre-commit row throughout scene + buffer.
    public class SpawnInsert
    {
        public int TempSpawnId { get; set; }

        // For sidebar display + revert (which restores the source spawn's DB record shape).
        public int SourceSpawnId { get; set; }
        public string DisplayName { get; set; }

        // spawngroup row — cloned from source. Name is generated unique at duplicate time
        // (spawngroup.name has a UNIQUE constraint); everything else stays default.
        public string SpawnGroupName { get; set; }

        // spawn2 row fields — DB-space X/Y/Z (X = east/west, Y = north/south, Z = up).
        public string Zone { get; set; }
        public short Version { get; set; }
        public float X { get; set; }
        public float Y { get; set; }
        public float Z { get; set; }
        public float Heading { get; set; }
        public int RespawnTime { get; set; }
        public int Variance { get; set; }
        public int PathGrid { get; set; }
        public byte Animation { get; set; }

        // NPC rotation — cloned from source's spawnentries. Chances preserved verbatim.
        public List<SpawnInsertEntry> Entries { get; set; } = new List<SpawnInsertEntry>();

        public DateTime CreatedAt { get; set; }
    }

    public class SpawnInsertEntry
    {
        public int NpcId { get; set; }
        public int Chance { get; set; }
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
        public byte OriginalCenterpoint { get; set; }

        public float CurrentX { get; set; }
        public float CurrentY { get; set; }
        public float CurrentZ { get; set; }
        public float CurrentHeading { get; set; }
        public int CurrentPause { get; set; }
        public byte CurrentCenterpoint { get; set; }

        public DateTime LastModifiedAt { get; set; }
    }

    // Grid-level metadata edits (grid.type / grid.type2). ZoneId is stored so the commit
    // WHERE clause can key on the composite PK; the runtime grid revert uses (gridId, zoneId).
    public class GridEdit
    {
        public int Id { get; set; }
        public int ZoneId { get; set; }
        public int OriginalType { get; set; }
        public int OriginalType2 { get; set; }
        public int CurrentType { get; set; }
        public int CurrentType2 { get; set; }
        public DateTime LastModifiedAt { get; set; }
    }

    // Pending waypoint insert. Number is chosen at edit time (append at max+1). ZoneId is
    // carried so the commit doesn't need to re-resolve it from the zone lookup.
    public class GridEntryInsert
    {
        public int GridId { get; set; }
        public int ZoneId { get; set; }
        public int Number { get; set; }
        public float X { get; set; }
        public float Y { get; set; }
        public float Z { get; set; }
        public float Heading { get; set; }
        public int Pause { get; set; }
        public byte Centerpoint { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    // Pending whole-grid insert. Real (id, zoneid) PK gets assigned at commit time via
    // MAX(id)+1 within the transaction; TempId is the negative sentinel used in-scene
    // and as the GridEntryInsert.GridId of the seed waypoint until commit remaps it.
    public class GridInsert
    {
        public int TempId { get; set; }
        public int ZoneId { get; set; }
        public int Type { get; set; }
        public int Type2 { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    // Pending waypoint delete. Snapshot of the removed row so Discard/Revert can restore it.
    public class GridEntryDelete
    {
        public int GridId { get; set; }
        public int ZoneId { get; set; }
        public int Number { get; set; }
        public float X { get; set; }
        public float Y { get; set; }
        public float Z { get; set; }
        public float Heading { get; set; }
        public int Pause { get; set; }
        public byte Centerpoint { get; set; }
        public DateTime DeletedAt { get; set; }
    }

    public class NpcEdit
    {
        public int NpcId { get; set; }
        // Populated when Phase 5.9+ NPC editing lands. Kept as a placeholder so the JSON
        // shape is stable across releases.
        public DateTime LastModifiedAt { get; set; }
    }

    // A brand-new trilogy_zone_points row waiting to be INSERTed on commit. Holds every
    // schema column plus a negative temp id that the in-memory ZonePoint uses as its
    // Row.Id until the INSERT lands and the real AUTO_INCREMENT id is stitched back in.
    public class ZonePointInsert
    {
        public int TempId { get; set; }
        public string Zone { get; set; }
        public float X { get; set; }
        public float Y { get; set; }
        public float Z { get; set; }
        public float Heading { get; set; }
        public string TargetZone { get; set; }
        public float TargetX { get; set; }
        public float TargetY { get; set; }
        public float TargetZ { get; set; }
        public int Zrange { get; set; }
        public int MaxZDiff { get; set; }
        public byte UseNewZoning { get; set; }
        public float MinVert { get; set; }
        public float MaxVert { get; set; }
        public float CenterPoint { get; set; }
        public int KeepX { get; set; }
        public int KeepY { get; set; }
        public int KeepZ { get; set; }
        public DateTime CreatedAt { get; set; }
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
