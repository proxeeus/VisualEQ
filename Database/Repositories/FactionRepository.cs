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
    }
}
