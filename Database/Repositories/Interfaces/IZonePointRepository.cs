using System.Collections.Generic;
using System.Threading.Tasks;
using VisualEQ.Database.Models;

namespace VisualEQ.Database.Repositories.Interfaces
{
    public interface IZonePointRepository
    {
        Task<IEnumerable<TrilogyZonePoint>> GetZonePointsAsync(string zoneName);
    }
}
