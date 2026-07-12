using System.Collections.Generic;
using System.Threading.Tasks;
using VisualEQ.Database.Models;

namespace VisualEQ.Database.Repositories.Interfaces
{
    public interface IZonePointRepository
    {
        Task<IEnumerable<TrilogyZonePoint>> GetZonePointsAsync(string zoneName);
        // Powers the target-zone dropdown in the inspector. Small table (dozens of rows),
        // fetched once per zone load and cached on the Controller.
        Task<IEnumerable<string>> GetAllZoneShortNamesAsync();
    }
}
