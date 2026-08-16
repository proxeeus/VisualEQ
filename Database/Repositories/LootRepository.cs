using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using VisualEQ.Database.Configuration;
using VisualEQ.Database.Constants;
using VisualEQ.Database.Models;
using VisualEQ.Database.Repositories.Base;
using VisualEQ.Database.Repositories.Interfaces;

namespace VisualEQ.Database.Repositories
{
    public class LootRepository : RepositoryBase, ILootRepository
    {
        public LootRepository(IDbConnectionFactory connectionFactory)
            : base(connectionFactory)
        {
        }

        public async Task<LootTable> GetLootTableAsync(int loottableId)
        {
            using (var connection = CreateConnection())
                return await connection.QuerySingleOrDefaultAsync<LootTable>(
                    SqlQueries.GetLootTableById, new { Id = loottableId });
        }

        public async Task<IEnumerable<LootTableEntry>> GetLootTableEntriesAsync(int loottableId)
        {
            using (var connection = CreateConnection())
                return await connection.QueryAsync<LootTableEntry>(
                    SqlQueries.GetLootTableEntries, new { LoottableId = loottableId });
        }

        public async Task<IEnumerable<LootDropEntry>> GetLootDropEntriesBatchAsync(IEnumerable<int> lootdropIds)
        {
            var ids = lootdropIds?.ToList();
            if (ids == null || ids.Count == 0)
                return System.Linq.Enumerable.Empty<LootDropEntry>();

            using (var connection = CreateConnection())
                return await connection.QueryAsync<LootDropEntry>(
                    SqlQueries.GetLootDropEntriesBatch, new { Ids = ids });
        }

        public async Task<int> GetLootTableUsageCountAsync(int loottableId)
        {
            using (var connection = CreateConnection())
                return await connection.ExecuteScalarAsync<int>(
                    SqlQueries.GetLootTableUsageCount, new { LoottableId = loottableId });
        }

        public async Task<IEnumerable<ReferenceItem>> SearchLootdropsAsync(string filter, int limit)
        {
            var like = "%" + (filter ?? "") + "%";
            using (var connection = CreateConnection())
                return await connection.QueryAsync<ReferenceItem>(
                    SqlQueries.SearchLootdrops, new { Filter = like, Limit = limit });
        }

        public async Task<Dictionary<int, int>> GetLootDropUsageCountsAsync(IEnumerable<int> lootdropIds)
        {
            var ids = lootdropIds?.ToList();
            if (ids == null || ids.Count == 0) return new Dictionary<int, int>();

            using (var connection = CreateConnection())
            {
                var rows = await connection.QueryAsync<(int LootdropId, int Count)>(
                    SqlQueries.GetLootDropUsageCountBatch, new { Ids = ids });
                var dict = new Dictionary<int, int>(ids.Count);
                foreach (var (LootdropId, Count) in rows) dict[LootdropId] = Count;
                return dict;
            }
        }

        public async Task<int> CloneLootTableAsync(int sourceId)
        {
            using (var connection = CreateConnection())
            {
                await ((System.Data.Common.DbConnection)connection).OpenAsync();
                using (var tx = ((System.Data.Common.DbConnection)connection).BeginTransaction())
                {
                    await connection.ExecuteAsync(SqlQueries.CloneLootTableRow,
                        new { SourceId = sourceId }, tx);
                    var newId = await connection.ExecuteScalarAsync<int>(
                        "SELECT LAST_INSERT_ID()", transaction: tx);
                    await connection.ExecuteAsync(SqlQueries.CloneLootTableEntries,
                        new { NewId = newId, SourceId = sourceId }, tx);
                    tx.Commit();
                    return newId;
                }
            }
        }

        public async Task<int> CloneLootDropAsync(int sourceId)
        {
            using (var connection = CreateConnection())
            {
                await ((System.Data.Common.DbConnection)connection).OpenAsync();
                using (var tx = ((System.Data.Common.DbConnection)connection).BeginTransaction())
                {
                    await connection.ExecuteAsync(SqlQueries.CloneLootDropRow,
                        new { SourceId = sourceId }, tx);
                    var newId = await connection.ExecuteScalarAsync<int>(
                        "SELECT LAST_INSERT_ID()", transaction: tx);
                    await connection.ExecuteAsync(SqlQueries.CloneLootDropEntries,
                        new { NewId = newId, SourceId = sourceId }, tx);
                    tx.Commit();
                    return newId;
                }
            }
        }

        public async Task<int> CreateEmptyLootTableAsync(string name, int minCash, int maxCash, int avgCoin)
        {
            // LAST_INSERT_ID() is per-connection in MySQL, and Dapper closes
            // auto-opened connections after each call. Splitting INSERT + SELECT
            // across two calls means the second one lands on a different pooled
            // connection whose LAST_INSERT_ID() is 0 → the caller wires the NPC
            // to loottable 0 = "no loottable". Bundle both statements into one
            // ExecuteScalarAsync so they share the physical connection.
            using (var connection = CreateConnection())
                return await connection.ExecuteScalarAsync<int>(
                    SqlQueries.CreateEmptyLootTable + "; SELECT LAST_INSERT_ID();",
                    new { Name = name ?? "", MinCash = minCash, MaxCash = maxCash, AvgCoin = avgCoin });
        }

        public async Task<int> CreateEmptyLootDropAsync(string name)
        {
            // See CreateEmptyLootTableAsync for the LAST_INSERT_ID reasoning.
            using (var connection = CreateConnection())
                return await connection.ExecuteScalarAsync<int>(
                    SqlQueries.CreateEmptyLootDrop + "; SELECT LAST_INSERT_ID();",
                    new { Name = name ?? "" });
        }

        public async Task RepointLootTableEntryLootdropAsync(int loottableId, int oldLootdropId, int newLootdropId)
        {
            using (var connection = CreateConnection())
                await connection.ExecuteAsync(SqlQueries.RepointLootTableEntryLootdrop,
                    new { LoottableId = loottableId, OldLootdropId = oldLootdropId, NewLootdropId = newLootdropId });
        }
    }
}
