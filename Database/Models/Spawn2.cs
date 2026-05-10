namespace VisualEQ.Database.Models
{
    // Columns mapped from spawn2 + spawngroup JOIN.
    // DB column names: spawngroupID→SpawnGroupId, respawntime→RespawnTime, pathgrid→PathGrid.
    // No 'enabled' column exists in this schema version.
    public class Spawn2
    {
        public int Id { get; set; }
        public int SpawnGroupId { get; set; }
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
        // Populated from JOIN with spawngroup
        public string SpawnGroupName { get; set; }
    }
} 