using System.Collections.Generic;
using System.Threading.Tasks;
using VisualEQ.Database.Models;

namespace VisualEQ.Database.Repositories.Interfaces
{
    // Loot-editor read path (Slice 6a). Writes for 6b/6c will land in
    // EditCommitter's own loops against SqlQueries.*LootTableEntry / *LootDropEntry
    // rather than on this interface — same shape as IFactionRepository.
    public interface ILootRepository
    {
        Task<LootTable> GetLootTableAsync(int loottableId);
        Task<IEnumerable<LootTableEntry>> GetLootTableEntriesAsync(int loottableId);
        Task<IEnumerable<LootDropEntry>>  GetLootDropEntriesBatchAsync(IEnumerable<int> lootdropIds);
        Task<int> GetLootTableUsageCountAsync(int loottableId);

        // Slice 6b — SEARCH picker for the "add lootdrop to loottable" flow.
        // Filter is substring-matched against lootdrop.name; empty filter returns
        // the first `limit` alphabetical rows so users can browse when they don't
        // have a specific search term.
        Task<IEnumerable<ReferenceItem>> SearchLootdropsAsync(string filter, int limit);
    }
}
