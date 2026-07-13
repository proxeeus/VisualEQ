namespace VisualEQ.Database.Models
{
    // Grid-level metadata from the `grid` table. Waypoints (grid_entries) reference this
    // via (gridid, zoneid).
    //
    // Type — wander behavior:
    //   0=Circular, 1=Random10, 2=Patrol, 3=One-way, 4=Random5
    // Type2 — pause behavior:
    //   0=Half-random pause, 1=Full pause, 2=Full-random pause
    public class Grid
    {
        public int Id { get; set; }
        public int ZoneId { get; set; }
        public int Type { get; set; }
        public int Type2 { get; set; }
    }
}
