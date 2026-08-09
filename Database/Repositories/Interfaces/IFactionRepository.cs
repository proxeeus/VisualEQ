using System.Collections.Generic;
using System.Threading.Tasks;
using VisualEQ.Database.Models;

namespace VisualEQ.Database.Repositories.Interfaces
{
    // Faction-editor read path. Writes go through EditCommitter's own INSERT/UPDATE/
    // DELETE loops against SqlQueries.*NpcFactionEntry, not through this repo.
    public interface IFactionRepository
    {
        Task<IEnumerable<NpcFactionEntry>> GetNpcFactionEntriesAsync(int npcFactionId);
        Task<NpcFactionSet> GetNpcFactionSetAsync(int npcFactionId);
    }
}
