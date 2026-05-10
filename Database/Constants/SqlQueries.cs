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
    }
} 