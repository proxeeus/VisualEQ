using System.Collections.Generic;
using System.Threading.Tasks;
using VisualEQ.Database.Models;

namespace VisualEQ.Database.Repositories.Interfaces
{
    // Slice 6b — item picker only. `items` has ~80k rows so nothing gets
    // preloaded; every keystroke in the picker fires SearchItemsAsync with a
    // LIMIT-capped LIKE query. Full item editing is deferred to a later
    // milestone (per the plan's "no item-table editing" scope carve-out).
    public interface IItemRepository
    {
        Task<IEnumerable<ReferenceItem>> SearchItemsAsync(string filter, int limit);
    }
}
