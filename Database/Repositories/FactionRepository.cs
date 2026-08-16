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
    public class FactionRepository : RepositoryBase, IFactionRepository
    {
        public FactionRepository(IDbConnectionFactory connectionFactory)
            : base(connectionFactory)
        {
        }

        public async Task<IEnumerable<NpcFactionEntry>> GetNpcFactionEntriesAsync(int npcFactionId)
        {
            using (var connection = CreateConnection())
                return await connection.QueryAsync<NpcFactionEntry>(
                    SqlQueries.GetNpcFactionEntries, new { NpcFactionId = npcFactionId });
        }

        public async Task<NpcFactionSet> GetNpcFactionSetAsync(int npcFactionId)
        {
            using (var connection = CreateConnection())
                return await connection.QuerySingleOrDefaultAsync<NpcFactionSet>(
                    SqlQueries.GetNpcFactionById, new { Id = npcFactionId });
        }

        public async Task<int> CreateEmptyNpcFactionAsync(string name, int primaryFaction, int ignorePrimaryAssist)
        {
            using (var connection = CreateConnection())
                return await connection.ExecuteScalarAsync<int>(
                    SqlQueries.CreateEmptyNpcFaction + "; SELECT LAST_INSERT_ID();",
                    new
                    {
                        Name = name ?? "",
                        PrimaryFaction = primaryFaction,
                        IgnorePrimaryAssist = ignorePrimaryAssist,
                    });
        }
    }
}
