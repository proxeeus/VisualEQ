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
    public class ItemRepository : RepositoryBase, IItemRepository
    {
        public ItemRepository(IDbConnectionFactory connectionFactory)
            : base(connectionFactory)
        {
        }

        public async Task<IEnumerable<ReferenceItem>> SearchItemsAsync(string filter, int limit)
        {
            var like = "%" + (filter ?? "") + "%";
            using (var connection = CreateConnection())
                return await connection.QueryAsync<ReferenceItem>(
                    SqlQueries.SearchItems, new { Filter = like, Limit = limit });
        }
    }
}
