using System.Collections.Generic;
using System.Threading.Tasks;
using VisualEQ.Database.Models;
using VisualEQ.Database.ViewModels;

namespace VisualEQ.Database.Repositories.Interfaces
{
    public interface ISpawnRepository
    {
        Task<IEnumerable<SpawnViewModel>> GetZoneSpawnsAsync(string zoneName);
        Task<SpawnViewModel> GetSpawnByIdAsync(int spawnId);
        Task<bool> UpdateSpawnLocationAsync(int spawnId, Vector3 position, float heading);
    }
} 