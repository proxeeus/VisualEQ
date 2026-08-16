namespace VisualEQ.Database.Models
{
    // One row from npc_faction — the "faction set" an NPC is assigned to via
    // npc_types.npc_faction_id. Holds set-wide metadata (name, primary faction,
    // whether primary counts for assist aggro) that lives OUTSIDE the per-entry
    // hits list (npc_faction_entries). Slice 5 fetches this alongside the entries
    // so the editor header can show "who this NPC belongs to" instead of just a
    // bare id.
    public class NpcFactionSet
    {
        public int    Id                    { get; set; }
        public string Name                  { get; set; }
        public int    PrimaryFaction        { get; set; }
        public sbyte  IgnorePrimaryAssist   { get; set; }
        // Denormalized from faction_list.name via LEFT JOIN — see NpcFactionEntry
        // for the rationale. Null when PrimaryFaction=0 or the fk is dangling.
        public string PrimaryFactionName    { get; set; }
    }
}
