namespace VisualEQ.Database.Models
{
    // One row from the `loottable` table — the top-level loot definition an NPC
    // is assigned to via npc_types.loottable_id. Holds cash-range fields and
    // expansion / content flags. Lootdrops attached to this loottable live in
    // `loottable_entries` (see LootTableEntry).
    //
    // Slice 6a: read-only. Editing arrives in 6b (cash fields, name) and 6c
    // (create-new / clone flows).
    public class LootTable
    {
        public int    Id                    { get; set; }
        public string Name                  { get; set; }
        public int    MinCash               { get; set; }
        public int    MaxCash               { get; set; }
        public int    AvgCoin               { get; set; }
        public sbyte  MinExpansion          { get; set; }
        public sbyte  MaxExpansion          { get; set; }
        public string ContentFlags          { get; set; }
        public string ContentFlagsDisabled  { get; set; }
    }
}
