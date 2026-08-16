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
    }
}
