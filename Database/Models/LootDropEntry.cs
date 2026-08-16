namespace VisualEQ.Database.Models
{
    // One row from `lootdrop_entries` — an item that can drop from a lootdrop,
    // with per-item roll parameters (chance, multiplier, charges, equip flag).
    // Composite PK is (lootdrop_id, item_id).
    //
    // `disabled_chance` is a peqphpeditor idiom for "temporarily disable this
    // item without losing its chance value" — server treats it as chance=0.
    // Preserved here so a future editor slice can toggle enable / disable.
    //
    // Denormalizes items.Name via LEFT JOIN so the widget renders "Rusty Sword
    // (#1001)" without a separate item-lookup roundtrip. Null-tolerant when the
    // items row is missing (dangling fk) — UI shows "?" with the id.
    public class LootDropEntry
    {
        public int    LootdropId       { get; set; }
        public int    ItemId           { get; set; }
        public ushort ItemCharges      { get; set; }
        public byte   EquipItem        { get; set; }
        public float  Chance           { get; set; }
        public float  DisabledChance   { get; set; }
        public ushort TrivialMinLevel  { get; set; }
        public ushort TrivialMaxLevel  { get; set; }
        public byte   Multiplier       { get; set; }
        public ushort NpcMinLevel      { get; set; }
        public ushort NpcMaxLevel      { get; set; }
        // Denormalized from items.Name via JOIN. Null when the fk is dangling.
        public string ItemName         { get; set; }
    }
}
