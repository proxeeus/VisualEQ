using System.Threading.Tasks;
using VisualEQ.Database.Models;

namespace VisualEQ.Database.Repositories.Interfaces
{
    // Read/write surface for the NPC editor. Slice 1 exposes only the read path;
    // Slice 2 adds UpdateNpcAsync, Slice 7 adds DuplicateNpcAsync + RepointSpawnEntryAsync.
    public interface INpcRepository
    {
        Task<NpcTypeFull> GetNpcByIdAsync(int npcId);

        // Slice 7a — how many spawnentry rows reference this npc_types row.
        // Small query used by the sidebar's shared-record indicator.
        Task<int> GetUsageCountAsync(int npcId);

        // Slice 7a — clone an npc_types row (all columns copied verbatim; name
        // gets a " (clone)" suffix) and return the new AUTO_INCREMENT id.
        // Column list is discovered from INFORMATION_SCHEMA at run-time so
        // fork-specific columns don't get silently dropped.
        Task<int> DuplicateAsync(int sourceNpcId);

        // Slice 7a — swap the npcID on a single spawnentry row. Immediate write.
        // Called right after a successful DuplicateAsync so THIS spawn's
        // spawnentry points at the clone.
        Task RepointSpawnEntryAsync(int spawnGroupId, int oldNpcId, int newNpcId);
    }
}
