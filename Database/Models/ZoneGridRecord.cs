using System.Collections.Generic;

namespace VisualEQ.Database.Models
{
    // Zone-wide grid record used by the Grid List sidebar. Independent of SpawnRecord —
    // orphan grids (no spawn2.pathgrid references them) still show up. SpawnCount is
    // filled by the controller after the spawn records land so the sidebar can tag
    // each row as attached vs orphan without a second DB round trip.
    public class ZoneGridRecord
    {
        public Grid Grid;
        public List<GridEntry> Waypoints = new List<GridEntry>();
        public int SpawnCount;
    }
}
