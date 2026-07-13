using System.Collections.Generic;

namespace VisualEQ.Database.Models
{
    public class SpawnEntryWithNpc
    {
        public SpawnEntry Entry { get; set; }
        public NpcType Npc { get; set; }
    }

    public class SpawnRecord
    {
        public Spawn2 Spawn { get; set; }
        public List<SpawnEntryWithNpc> Entries { get; set; } = new List<SpawnEntryWithNpc>();
        public List<GridEntry> Waypoints { get; set; } = new List<GridEntry>();
        // Null when the spawn has no path grid (Spawn.PathGrid == 0) or when the grid row
        // was not found. Shared across all spawns that reference the same (gridId, zoneId).
        public Grid Grid { get; set; }
    }
}
