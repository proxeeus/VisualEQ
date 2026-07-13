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
    public class ZonePointRepository : RepositoryBase, IZonePointRepository
    {
        public ZonePointRepository(IDbConnectionFactory connectionFactory)
            : base(connectionFactory)
        {
        }

        public async Task<IEnumerable<TrilogyZonePoint>> GetZonePointsAsync(string zoneName)
        {
            using (var connection = CreateConnection())
            {
                return await connection.QueryAsync<TrilogyZonePoint>(
                    SqlQueries.GetTrilogyZonePoints, new { ZoneName = zoneName });
            }
        }

        public async Task<IEnumerable<TrilogyZonePoint>> GetIncomingZonePointsAsync(string zoneName)
        {
            using (var connection = CreateConnection())
            {
                return await connection.QueryAsync<TrilogyZonePoint>(
                    SqlQueries.GetIncomingZonePoints, new { ZoneName = zoneName });
            }
        }

        public async Task<IEnumerable<string>> GetAllZoneShortNamesAsync()
        {
            using (var connection = CreateConnection())
            {
                return await connection.QueryAsync<string>(SqlQueries.GetAllZoneShortNames);
            }
        }
    }
}
