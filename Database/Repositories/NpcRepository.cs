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

        public async Task<int> GetUsageCountAsync(int npcId)
        {
            using (var connection = CreateConnection())
                return await connection.ExecuteScalarAsync<int>(
                    SqlQueries.GetNpcTypeUsageCount, new { NpcId = npcId });
        }

        public async Task<int> DuplicateAsync(int sourceNpcId)
        {
            // Column list is discovered per-call from INFORMATION_SCHEMA so a
            // fork with extra columns clones them too. Skips `id` (AUTO_INCREMENT
            // assigns a fresh one on INSERT). Wraps in an explicit transaction
            // so INSERT + LAST_INSERT_ID share the same physical connection
            // (see LAST_INSERT_ID gotcha documented in LootRepository).
            using (var connection = CreateConnection())
            {
                await ((System.Data.Common.DbConnection)connection).OpenAsync();
                using (var tx = ((System.Data.Common.DbConnection)connection).BeginTransaction())
                {
                    var cols = (await connection.QueryAsync<string>(
                        "SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS " +
                        "WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'npc_types' AND COLUMN_NAME <> 'id' " +
                        "ORDER BY ORDINAL_POSITION",
                        transaction: tx)).ToList();

                    var colList    = string.Join(", ", cols.Select(c => "`" + c + "`"));
                    var selectList = string.Join(", ", cols.Select(c =>
                        c == "name" ? "CONCAT(`name`, ' (clone)')" : "`" + c + "`"));

                    var sql = "INSERT INTO npc_types (" + colList + ") " +
                              "SELECT " + selectList + " FROM npc_types WHERE id = @Id";
                    await connection.ExecuteAsync(sql, new { Id = sourceNpcId }, tx);

                    var newId = await connection.ExecuteScalarAsync<int>(
                        "SELECT LAST_INSERT_ID()", transaction: tx);
                    tx.Commit();
                    return newId;
                }
            }
        }

        public async Task RepointSpawnEntryAsync(int spawnGroupId, int oldNpcId, int newNpcId)
        {
            using (var connection = CreateConnection())
                await connection.ExecuteAsync(SqlQueries.RepointSpawnEntry,
                    new { SpawnGroupId = spawnGroupId, OldNpcId = oldNpcId, NewNpcId = newNpcId });
        }
    }
}
