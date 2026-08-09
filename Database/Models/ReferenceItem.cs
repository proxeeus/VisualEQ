namespace VisualEQ.Database.Models
{
    // Generic {id, name} lookup row for reference-data typeahead pickers. Used by every
    // FK-id-with-a-name-column table exposed to the NPC editor: loottable, npc_faction,
    // npc_spells, npc_spells_effects. Merchantlist is a special case (no name column) —
    // its aggregated row is built in the repository with a synthetic "N items" label.
    public class ReferenceItem
    {
        public int Id { get; set; }
        public string Name { get; set; }
    }
}
