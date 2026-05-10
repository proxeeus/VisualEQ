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

        public const string GetSpawnGroup = @"
            SELECT * FROM spawngroup WHERE id = @GroupId";

        // grid is keyed on (zoneid, id); always filter by both to avoid cross-zone collisions.
        public const string GetGridEntries = @"
            SELECT * FROM grid_entries
            WHERE gridid = @GridId AND zoneid = @ZoneId
            ORDER BY number";

        public const string GetNpcType = @"
            SELECT * FROM npc_types WHERE id = @NpcId";

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
    }
} 