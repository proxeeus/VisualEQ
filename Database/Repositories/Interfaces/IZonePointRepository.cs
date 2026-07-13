using System.Collections.Generic;
using System.Threading.Tasks;
using VisualEQ.Database.Models;

namespace VisualEQ.Database.Repositories.Interfaces
{
    public interface IZonePointRepository
    {
        Task<IEnumerable<TrilogyZonePoint>> GetZonePointsAsync(string zoneName);
        // Rows targeting the current zone from ELSEWHERE — power the "incoming" arrows
        // that show where players arrive in this zone from other zones.
        Task<IEnumerable<TrilogyZonePoint>> GetIncomingZonePointsAsync(string zoneName);
        // Peer-zone rows: every row IN the given zones. Powers the sandwich detector's
        // "does the landing coord fall inside a destination-zone trigger?" check.
        Task<IEnumerable<TrilogyZonePoint>> GetZonePointsForZonesAsync(IEnumerable<string> zoneNames);
        // Powers the target-zone dropdown in the inspector. Small table (dozens of rows),
        // fetched once per zone load and cached on the Controller.
        Task<IEnumerable<string>> GetAllZoneShortNamesAsync();
    }
}
