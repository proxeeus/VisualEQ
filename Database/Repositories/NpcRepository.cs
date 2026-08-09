using System.Threading.Tasks;
using Dapper;
using VisualEQ.Database.Configuration;
using VisualEQ.Database.Constants;
using VisualEQ.Database.Models;
using VisualEQ.Database.Repositories.Base;
using VisualEQ.Database.Repositories.Interfaces;

namespace VisualEQ.Database.Repositories
{
    public class NpcRepository : RepositoryBase, INpcRepository
    {
        public NpcRepository(IDbConnectionFactory connectionFactory)
            : base(connectionFactory)
        {
        }

        public async Task<NpcTypeFull> GetNpcByIdAsync(int npcId)
        {
            using (var connection = CreateConnection())
            {
                return await connection.QueryFirstOrDefaultAsync<NpcTypeFull>(
                    SqlQueries.GetNpcTypeById, new { NpcId = npcId });
            }
        }
    }
}
