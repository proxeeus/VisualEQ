namespace VisualEQ.Database.Models
{
    public class GridEntry
    {
        public int GridId { get; set; }
        public int Number { get; set; }
        public float X { get; set; }
        public float Y { get; set; }
        public float Z { get; set; }
        public float Heading { get; set; }
        public int Pause { get; set; }
        // grid_entries.centerpoint (tinyint 0/1). EQEmu uses this to mark a waypoint that
        // should be treated as the grid's center for movement modes that need one.
        public byte Centerpoint { get; set; }
    }
} 