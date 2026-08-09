using System.Threading.Tasks;
using VisualEQ.Database.Models;

namespace VisualEQ.Database.Repositories.Interfaces
{
    // Read/write surface for the NPC editor. Slice 1 exposes only the read path;
    // Slice 2 adds UpdateNpcAsync, Slice 7 adds DuplicateNpcAsync + RepointSpawnEntryAsync.
    public interface INpcRepository
    {
        Task<NpcTypeFull> GetNpcByIdAsync(int npcId);
    }
}
