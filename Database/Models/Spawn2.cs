namespace VisualEQ.Database.Models
{
    public class Spawn2
    {
        public int Id { get; set; }
        public int SpawnGroupId { get; set; }
        public string Zone { get; set; }
        public float X { get; set; }
        public float Y { get; set; }
        public float Z { get; set; }
        public float Heading { get; set; }
        public int RespawnTime { get; set; }
        public int PathGrid { get; set; }
        public int Variance { get; set; }
        public int Version { get; set; }
        public bool Enabled { get; set; }
    }
} 