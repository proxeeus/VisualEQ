namespace VisualEQ.Database.Models
{
    // One row from `loottable_entries` — links a loottable to a lootdrop, with
    // per-link roll parameters (probability, mindrop, droplimit, multiplier).
    //
    // Composite PK is (loottable_id, lootdrop_id) — a lootdrop can appear in a
    // loottable at most once. Denormalizes the lootdrop's name via LEFT JOIN so
    // the sidebar doesn't need a second lookup / cache warm.
    public class LootTableEntry
    {
        public int   LoottableId { get; set; }
        public int   LootdropId  { get; set; }
        public byte  Multiplier  { get; set; }
        public byte  DropLimit   { get; set; }
        public byte  MinDrop     { get; set; }
        public float Probability { get; set; }
        // Denormalized from lootdrop.name via JOIN. Null when the fk is dangling.
        public string LootdropName { get; set; }
    }
}
