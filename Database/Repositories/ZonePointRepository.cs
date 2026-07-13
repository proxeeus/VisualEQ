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

        public async Task<IEnumerable<TrilogyZonePoint>> GetZonePointsForZonesAsync(IEnumerable<string> zoneNames)
        {
            var names = zoneNames.Where(n => !string.IsNullOrEmpty(n)).Distinct(System.StringComparer.OrdinalIgnoreCase).ToArray();
            if (names.Length == 0) return System.Linq.Enumerable.Empty<TrilogyZonePoint>();
            using (var connection = CreateConnection())
            {
                return await connection.QueryAsync<TrilogyZonePoint>(
                    SqlQueries.GetZonePointsForZones, new { ZoneNames = names });
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
