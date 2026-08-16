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

        // Slice 6c — per-lootdrop usage counts. Returns a dict {lootdropId → count}
        // covering every id passed in; ids with no rows in loottable_entries are
        // absent from the result (caller defaults them to 0). Batched via IN so a
        // single query covers the whole current view.
        Task<System.Collections.Generic.Dictionary<int, int>> GetLootDropUsageCountsAsync(
            System.Collections.Generic.IEnumerable<int> lootdropIds);

        // Slice 6c — clone / create. All return the new AUTO_INCREMENT id.
        // Clone flows copy the row + all children in a single transaction so
        // the new record is complete or nothing lands. Create flows insert a
        // bare row with just a name.
        Task<int> CloneLootTableAsync(int sourceId);
        Task<int> CloneLootDropAsync(int sourceId);
        Task<int> CreateEmptyLootTableAsync(string name, int minCash, int maxCash, int avgCoin);
        Task<int> CreateEmptyLootDropAsync(string name);

        // Post-clone: swap the loottable_entries row's lootdrop_id from old →
        // new. Immediate write; scoped to a single (loottableId, lootdropId)
        // pair so other loottables sharing the source lootdrop are untouched.
        Task RepointLootTableEntryLootdropAsync(int loottableId, int oldLootdropId, int newLootdropId);
    }
}
