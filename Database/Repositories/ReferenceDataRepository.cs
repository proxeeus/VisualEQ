using System.Collections.Generic;
using System.Threading.Tasks;
using Dapper;
using VisualEQ.Database.Configuration;
using VisualEQ.Database.Constants;
using VisualEQ.Database.Models;
using VisualEQ.Database.Repositories.Base;
using VisualEQ.Database.Repositories.Interfaces;

namespace VisualEQ.Database.Repositories
{
    public class ReferenceDataRepository : RepositoryBase, IReferenceDataRepository
    {
        public ReferenceDataRepository(IDbConnectionFactory connectionFactory)
            : base(connectionFactory)
        {
        }

        public async Task<IEnumerable<ReferenceItem>> GetAllLootTablesAsync()
        {
            using (var connection = CreateConnection())
                return await connection.QueryAsync<ReferenceItem>(SqlQueries.GetAllLootTables);
        }

        public async Task<IEnumerable<ReferenceItem>> GetAllNpcFactionsAsync()
        {
            using (var connection = CreateConnection())
                return await connection.QueryAsync<ReferenceItem>(SqlQueries.GetAllNpcFactions);
        }

        public async Task<IEnumerable<ReferenceItem>> GetAllMerchantsAsync()
        {
            using (var connection = CreateConnection())
                return await connection.QueryAsync<ReferenceItem>(SqlQueries.GetAllMerchants);
        }

        public async Task<IEnumerable<ReferenceItem>> GetAllNpcSpellSetsAsync()
        {
            using (var connection = CreateConnection())
                return await connection.QueryAsync<ReferenceItem>(SqlQueries.GetAllNpcSpellSets);
        }

        public async Task<IEnumerable<ReferenceItem>> GetAllNpcSpellEffectSetsAsync()
        {
            using (var connection = CreateConnection())
                return await connection.QueryAsync<ReferenceItem>(SqlQueries.GetAllNpcSpellEffectSets);
        }

        public async Task<IEnumerable<ReferenceItem>> GetAllFactionListAsync()
        {
            using (var connection = CreateConnection())
                return await connection.QueryAsync<ReferenceItem>(SqlQueries.GetAllFactionList);
        }
    }
}
