namespace VisualEQ.Database.Constants
{
    public static class SqlQueries
    {
        public const string GetZoneSpawns = @"
            SELECT s.*, sg.name as SpawnGroupName 
            FROM spawn2 s
            JOIN spawngroup sg ON s.spawngroupID = sg.id
            WHERE s.zone = @ZoneName";

        public const string GetSpawnById = @"
            SELECT s.*, sg.name as SpawnGroupName 
            FROM spawn2 s
            JOIN spawngroup sg ON s.spawngroupID = sg.id
            WHERE s.id = @SpawnId";

        public const string UpdateSpawnLocation = @"
            UPDATE spawn2 
            SET x = @X, y = @Y, z = @Z, heading = @Heading 
            WHERE id = @SpawnId";

        public const string GetSpawnGroup = @"
            SELECT * FROM spawngroup WHERE id = @GroupId";

        public const string GetGridEntries = @"
            SELECT * FROM grid_entries 
            WHERE gridid = @GridId 
            ORDER BY number";

        public const string GetNpcType = @"
            SELECT * FROM npc_types WHERE id = @NpcId";
    }
} 