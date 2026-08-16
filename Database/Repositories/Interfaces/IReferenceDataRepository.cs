using System.Collections.Generic;
using System.Threading.Tasks;
using VisualEQ.Database.Models;

namespace VisualEQ.Database.Repositories.Interfaces
{
    // Reference-table lookups for the NPC editor's typeahead pickers. Every method returns
    // {id, name} rows so callers (specifically ReferenceDataCache) can hold one dict per
    // table and let the sidebar resolve any FK int to a human label in O(1) after warmup.
    public interface IReferenceDataRepository
    {
        Task<IEnumerable<ReferenceItem>> GetAllLootTablesAsync();
        Task<IEnumerable<ReferenceItem>> GetAllNpcFactionsAsync();
        Task<IEnumerable<ReferenceItem>> GetAllMerchantsAsync();
        Task<IEnumerable<ReferenceItem>> GetAllNpcSpellSetsAsync();
        Task<IEnumerable<ReferenceItem>> GetAllNpcSpellEffectSetsAsync();
        Task<IEnumerable<ReferenceItem>> GetAllFactionListAsync();
    }
}
