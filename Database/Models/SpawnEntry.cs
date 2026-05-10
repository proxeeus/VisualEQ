namespace VisualEQ.Database.Models
{
    public class SpawnEntry
    {
        public int SpawnGroupId { get; set; }  // DB: spawngroupID
        public int NpcId { get; set; }         // DB: npcID
        public int Chance { get; set; }
    }
}
