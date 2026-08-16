namespace VisualEQ.Database.Models
{
    // One row of npc_faction_entries. Composite PK is (NpcFactionId, FactionId) —
    // uniqueness enforced DB-side, so the sidebar's per-faction row must dedupe
    // before allowing an "add" of a faction that's already present in the set.
    //
    // Value / NpcValue: client-visible vs NPC-side aggro hit magnitudes. Both
    // signed — negative values mean killing this NPC hurts your standing with
    // that faction. Temp is a legacy flag; kept editable but rarely non-zero.
    public class NpcFactionEntry
    {
        public int    NpcFactionId { get; set; }
        public int    FactionId    { get; set; }
        public int    Value        { get; set; }
        public sbyte  NpcValue     { get; set; }
        public sbyte  Temp         { get; set; }
        // Denormalized from faction_list.name via LEFT JOIN so the editor never
        // has to round-trip a separate lookup / hope the reference-data cache
        // has loaded. Null when faction_list has no row for FactionId (dangling
        // fk — treat as "?" in the UI).
        public string FactionName { get; set; }
    }
}
