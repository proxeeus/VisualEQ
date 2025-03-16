namespace VisualEQ.Database.Models
{
    public class SpawnGroup
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public int SpawnLimit { get; set; }
        public float Distance { get; set; }
        public float MinX { get; set; }
        public float MaxX { get; set; }
        public float MinY { get; set; }
        public float MaxY { get; set; }
        public int Delay { get; set; }
        public int DespawnTimer { get; set; }
        public int MinTime { get; set; }
        public int MaxTime { get; set; }
    }
} 