namespace VisualEQ.Database.ViewModels
{
    public class SpawnViewModel
    {
        public int Id { get; set; }
        public string ZoneName { get; set; }
        public Vector3 Position { get; set; }
        public float Heading { get; set; }
        public int SpawnGroupId { get; set; }
        public string SpawnGroupName { get; set; }
        public bool IsEnabled { get; set; }
        public int RespawnTime { get; set; }
    }
} 