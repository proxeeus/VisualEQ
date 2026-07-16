namespace VisualEQ.Database.Constants
{
    public static class SqlQueries
    {
        // Explicit aliases ensure Dapper maps correctly regardless of DB driver case behaviour.
        // spawn2 columns: id, spawngroupID, zone, version, x, y, z, heading,
        //                 respawntime, variance, pathgrid, animation
        // No 'enabled' column in this schema version.
        public const string GetZoneSpawns = @"
            SELECT
                s.id,
                s.spawngroupID  AS SpawnGroupId,
                s.zone,
                s.version,
                s.x,
                s.y,
                s.z,
                s.heading,
                s.respawntime   AS RespawnTime,
                s.variance,
                s.pathgrid      AS PathGrid,
                s.animation,
                sg.name         AS SpawnGroupName
            FROM spawn2 s
            JOIN spawngroup sg ON s.spawngroupID = sg.id
            WHERE s.zone = @ZoneName";

        public const string GetSpawnById = @"
            SELECT
                s.id,
                s.spawngroupID  AS SpawnGroupId,
                s.zone,
                s.version,
                s.x,
                s.y,
                s.z,
                s.heading,
                s.respawntime   AS RespawnTime,
                s.variance,
                s.pathgrid      AS PathGrid,
                s.animation,
                sg.name         AS SpawnGroupName
            FROM spawn2 s
            JOIN spawngroup sg ON s.spawngroupID = sg.id
            WHERE s.id = @SpawnId";

        public const string UpdateSpawnLocation = @"
            UPDATE spawn2
            SET x = @X, y = @Y, z = @Z, heading = @Heading
            WHERE id = @SpawnId";

        // Removes a spawn2 row by id. Does not touch spawngroup / spawnentry — those may
        // be referenced by other spawn2 rows in this or other zones; a "cleanup orphan
        // spawngroups" tool is a separate concern.
        public const string DeleteSpawn2 = @"
            DELETE FROM spawn2 WHERE id = @Id";

        // Duplicate-spawn commit chain (used by EditCommitter for buffer.SpawnInserts).
        // Every non-required column relies on its DB default (spawn_limit=0, delay,
        // mindelay, min_expansion=-1, etc.). LAST_INSERT_ID() is session-scoped so it
        // stays coherent inside a Dapper transaction — must be read AT ONCE via
        // ExecuteScalar<int> before the next INSERT overwrites it.
        //
        // spawngroup.name is UNIQUE — callers must pre-generate a unique name (see
        // Controller.DuplicateSelectedSpawn — source name + 4-hex Guid suffix, truncated
        // to varchar(30)). A collision throws inside the transaction → whole commit
        // rolls back → user retries (< 1-in-65k on random inserts, so acceptable).
        public const string InsertSpawnGroup = @"
            INSERT INTO spawngroup (name) VALUES (@Name);
            SELECT LAST_INSERT_ID();";

        // Minimal spawnentry insert. Composite PK is (spawngroupID, npcID); duplicates
        // within the same call are impossible because SpawnInsertAction dedupes on the
        // client side. Other cols (condition_value_filter, min_time/max_time,
        // min/max_expansion, content_flags*) all default in-DB.
        public const string InsertSpawnEntry = @"
            INSERT INTO spawnentry (spawngroupID, npcID, chance)
            VALUES (@SpawnGroupId, @NpcId, @Chance)";

        // spawn2 row insert. Columns that VisualEQ tracks in the SpawnInsert record are
        // named explicitly; DB-defaulted columns (path_when_zone_idle, _condition,
        // cond_value, min/max_expansion, content_flags*) inherit their schema defaults.
        public const string InsertSpawn2 = @"
            INSERT INTO spawn2
                (spawngroupID, zone, version, x, y, z, heading,
                 respawntime, variance, pathgrid, animation)
            VALUES
                (@SpawnGroupId, @Zone, @Version, @X, @Y, @Z, @Heading,
                 @RespawnTime, @Variance, @PathGrid, @Animation);
            SELECT LAST_INSERT_ID();";

        // Used by the Phase 5 commit path to write waypoint drags back to the DB. Key is
        // (gridid, number, zoneid) — grid_entries has no primary key beyond that composite.
        public const string UpdateGridEntry = @"
            UPDATE grid_entries
            SET x = @X, y = @Y, z = @Z, heading = @Heading, pause = @Pause, centerpoint = @Centerpoint
            WHERE gridid = @GridId AND number = @Number AND zoneid = @ZoneId";

        // Waypoint INSERT/DELETE for the add/delete affordance. Composite PK is
        // (gridid, number, zoneid) so the WHERE is deterministic.
        public const string InsertGridEntry = @"
            INSERT INTO grid_entries (gridid, zoneid, number, x, y, z, heading, pause, centerpoint)
            VALUES (@GridId, @ZoneId, @Number, @X, @Y, @Z, @Heading, @Pause, @Centerpoint)";

        public const string DeleteGridEntry = @"
            DELETE FROM grid_entries
            WHERE gridid = @GridId AND number = @Number AND zoneid = @ZoneId";

        // Grid-level metadata (wander / pause behavior). PK is (id, zoneid).
        public const string GetGridsBatch = @"
            SELECT id AS Id, zoneid AS ZoneId, type AS Type, type2 AS Type2
            FROM grid
            WHERE id IN @GridIds AND zoneid = @ZoneId";

        public const string UpdateGrid = @"
            UPDATE grid
            SET type = @Type, type2 = @Type2
            WHERE id = @Id AND zoneid = @ZoneId";

        // Look up the numeric zone ID — required for filtering grid/grid_entries by zone.
        public const string GetZoneId = @"
            SELECT zoneidnumber FROM zone WHERE short_name = @ZoneName";

        // Batch-load all spawnentry rows for a set of spawn groups (avoids N+1).
        public const string GetSpawnEntriesBatch = @"
            SELECT spawngroupID AS SpawnGroupId, npcID AS NpcId, chance AS Chance
            FROM spawnentry
            WHERE spawngroupID IN @GroupIds";

        // Batch-load npc_types for a set of NPC IDs.
        // Only selects the columns needed for model resolution in Phase 2.
        // Cosmetic columns (hair/beard/eye) are omitted — they vary by EQEmu version.
        public const string GetNpcTypesBatch = @"
            SELECT id, name, lastname AS LastName, level, race,
                   `class` AS Class, bodytype AS BodyType, size, gender,
                   texture, helmtexture AS HelmTexture, face
            FROM npc_types
            WHERE id IN @NpcIds";

        // Batch-load grid_entries for multiple grids in one zone.
        public const string GetGridEntriesBatch = @"
            SELECT gridid AS GridId, number, x, y, z, heading, pause
            FROM grid_entries
            WHERE gridid IN @GridIds AND zoneid = @ZoneId
            ORDER BY gridid, number";

        // Full sweep of every grid in a zone — used by the Grid List sidebar section so
        // orphan grids (no spawn2 references them; quest scripts spawn NPCs onto them at
        // runtime) become visible/editable. GetGridsBatch above is spawn-driven and would
        // miss these entirely.
        public const string GetAllZoneGrids = @"
            SELECT id AS Id, zoneid AS ZoneId, type AS Type, type2 AS Type2
            FROM grid
            WHERE zoneid = @ZoneId
            ORDER BY id";

        public const string GetAllZoneGridEntries = @"
            SELECT gridid AS GridId, number, x, y, z, heading, pause, centerpoint
            FROM grid_entries
            WHERE zoneid = @ZoneId
            ORDER BY gridid, number";

        // grid.id isn't AUTO_INCREMENT — it's a user-assigned composite PK with zoneid.
        // On commit we compute the next id inside the same transaction: FOR UPDATE locks
        // the peer rows for this zone so two concurrent commits get sequential ids
        // instead of colliding on the PK.
        public const string NextGridIdForZone = @"
            SELECT COALESCE(MAX(id), 0) + 1
            FROM grid
            WHERE zoneid = @ZoneId
            FOR UPDATE";

        public const string InsertGrid = @"
            INSERT INTO grid (id, zoneid, type, type2)
            VALUES (@Id, @ZoneId, @Type, @Type2)";

        // Trilogy client's server-side zone-crossing triggers. Columns aliased so Dapper
        // maps deterministically regardless of MySQL's platform-dependent case handling.
        public const string GetTrilogyZonePoints = @"
            SELECT
                id,
                zone,
                x, y, z, heading,
                target_zone     AS TargetZone,
                target_x        AS TargetX,
                target_y        AS TargetY,
                target_z        AS TargetZ,
                Zrange,
                maxZDiff        AS MaxZDiff,
                UseNewZoning,
                MinVert, MaxVert, CenterPoint,
                keepX           AS KeepX,
                keepY           AS KeepY,
                keepZ           AS KeepZ,
                ToZoneID        AS ToZoneId
            FROM trilogy_zone_points
            WHERE zone = @ZoneName";

        public const string UpdateTrilogyZonePoint = @"
            UPDATE trilogy_zone_points
            SET x = @X, y = @Y, z = @Z, heading = @Heading,
                target_zone = @TargetZone,
                target_x = @TargetX, target_y = @TargetY, target_z = @TargetZ,
                Zrange = @Zrange, maxZDiff = @MaxZDiff, UseNewZoning = @UseNewZoning,
                MinVert = @MinVert, MaxVert = @MaxVert, CenterPoint = @CenterPoint,
                keepX = @KeepX, keepY = @KeepY, keepZ = @KeepZ,
                ToZoneID = @ToZoneId
            WHERE id = @Id";

        // INSERT returns the AUTO_INCREMENT id via LAST_INSERT_ID() (session-scoped, safe
        // inside a transaction). Committer runs this then remaps the in-memory temp id
        // to the returned real id.
        public const string InsertTrilogyZonePoint = @"
            INSERT INTO trilogy_zone_points
                (zone, x, y, z, heading,
                 target_zone, target_x, target_y, target_z,
                 Zrange, maxZDiff, UseNewZoning,
                 MinVert, MaxVert, CenterPoint,
                 keepX, keepY, keepZ, ToZoneID)
            VALUES
                (@Zone, @X, @Y, @Z, @Heading,
                 @TargetZone, @TargetX, @TargetY, @TargetZ,
                 @Zrange, @MaxZDiff, @UseNewZoning,
                 @MinVert, @MaxVert, @CenterPoint,
                 @KeepX, @KeepY, @KeepZ, @ToZoneId);
            SELECT LAST_INSERT_ID();";

        public const string DeleteTrilogyZonePoint = @"
            DELETE FROM trilogy_zone_points WHERE id = @Id";

        // Populates the target-zone dropdown in the inspector. Sorted alphabetically so
        // the Combo is easy to scan; ORDER BY short_name is the natural human order for
        // dozens of Trilogy-era zones.
        public const string GetAllZoneShortNames = @"
            SELECT short_name
            FROM zone
            ORDER BY short_name";

        // Rows in other zones that land inside the currently-viewed zone — used to render
        // "incoming" arrows at the target_x/y/z coord with a heading indicator. The row's
        // `zone` field stays as the SOURCE (foreign) zone; edits still UPDATE by id so
        // the cross-zone commit path is identical to the normal case.
        public const string GetIncomingZonePoints = @"
            SELECT
                id,
                zone,
                x, y, z, heading,
                target_zone     AS TargetZone,
                target_x        AS TargetX,
                target_y        AS TargetY,
                target_z        AS TargetZ,
                Zrange,
                maxZDiff        AS MaxZDiff,
                UseNewZoning,
                MinVert, MaxVert, CenterPoint,
                keepX           AS KeepX,
                keepY           AS KeepY,
                keepZ           AS KeepZ,
                ToZoneID        AS ToZoneId
            FROM trilogy_zone_points
            WHERE target_zone = @ZoneName AND zone <> @ZoneName";

        // Peer-zone rows for the sandwich detector: for each destination zone reached from
        // the current zone's owned rows, load every row IN that destination zone so we
        // can check whether an outgoing landing coord falls inside one of that zone's
        // fire regions. Kept as a single IN-list query so N destinations = 1 round trip.
        public const string GetZonePointsForZones = @"
            SELECT
                id,
                zone,
                x, y, z, heading,
                target_zone     AS TargetZone,
                target_x        AS TargetX,
                target_y        AS TargetY,
                target_z        AS TargetZ,
                Zrange,
                maxZDiff        AS MaxZDiff,
                UseNewZoning,
                MinVert, MaxVert, CenterPoint,
                keepX           AS KeepX,
                keepY           AS KeepY,
                keepZ           AS KeepZ,
                ToZoneID        AS ToZoneId
            FROM trilogy_zone_points
            WHERE zone IN @ZoneNames";
    }
}