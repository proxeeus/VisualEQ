using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using VisualEQ.Database.Configuration;
using VisualEQ.Database.Models;
using VisualEQ.Database.Repositories;

namespace VisualEQ.SpawnSystem
{
    // Session-lifetime cache for the NPC editor's reference tables (loottable, npc_faction,
    // merchant, npc_spells, npc_spells_effects). Each table lazily fetches on first request
    // and the loaded {id → name} dict is held for the app lifetime — sizes are ~50k rows
    // total, memory is trivial, and staleness in a running session is acceptable (server
    // OPs rarely add loottables mid-edit and can restart the app to refresh).
    //
    // States per table: NotLoaded → Loading → Loaded / Error. Callers should ask for the
    // dict; if null, kick a warm-up and re-check next frame. Same in-flight-task pattern
    // as the NPC picker in SidebarView so the UI thread never blocks on DB IO.
    public class ReferenceDataCache
    {
        public enum LoadState { NotLoaded, Loading, Loaded, Error }

        public enum Table
        {
            LootTable,
            NpcFaction,        // faction SETS from npc_faction
            Merchant,
            NpcSpellSet,
            NpcSpellEffectSet,
            FactionList,       // individual factions from faction_list (Slice 5 — used
                               //   by the per-entry faction picker inside a faction set)
        }

        sealed class TableState
        {
            public LoadState State = LoadState.NotLoaded;
            public Dictionary<int, string> ByIdName = new Dictionary<int, string>();
            public List<ReferenceItem> Items = new List<ReferenceItem>();
            public string Error;
            public Task LoadTask;
        }

        readonly Dictionary<Table, TableState> _tables = new Dictionary<Table, TableState>
        {
            { Table.LootTable,         new TableState() },
            { Table.NpcFaction,        new TableState() },
            { Table.Merchant,          new TableState() },
            { Table.NpcSpellSet,       new TableState() },
            { Table.NpcSpellEffectSet, new TableState() },
            { Table.FactionList,       new TableState() },
        };

        readonly MySqlConnectionFactory _factory;

        public ReferenceDataCache(MySqlConnectionFactory factory)
        {
            _factory = factory;
        }

        public LoadState GetState(Table table) => _tables[table].State;

        // Returns null when NotLoaded (kicks off a background load) or Loading. Loaded and
        // Error states return the last-loaded dict (empty for Error, so callers can fall
        // back to "id N" text without null-checking).
        public IReadOnlyDictionary<int, string> GetNameLookup(Table table)
        {
            EnsureLoading(table);
            var t = _tables[table];
            return t.State == LoadState.Loaded ? t.ByIdName : null;
        }

        // For pickers — returns the full item list (id-sorted or name-sorted per SQL ORDER BY).
        // Empty when not-yet-loaded.
        public IReadOnlyList<ReferenceItem> GetItems(Table table)
        {
            EnsureLoading(table);
            var t = _tables[table];
            return t.State == LoadState.Loaded ? (IReadOnlyList<ReferenceItem>)t.Items : System.Array.Empty<ReferenceItem>();
        }

        // Resolves a foreign-key id to "N — name" for display, or just "N" if unresolved
        // (not-loaded, missing row, or DB unavailable). Zero is treated as "(none)" per
        // EQEmu convention (0 = no loottable / faction / merchant assigned).
        public string ResolveLabel(Table table, int id)
        {
            if (id == 0) return "(none)";
            var lookup = GetNameLookup(table);
            if (lookup != null && lookup.TryGetValue(id, out var name))
                return $"{id} — {name}";
            return id.ToString();
        }

        void EnsureLoading(Table table)
        {
            var t = _tables[table];
            if (t.State != LoadState.NotLoaded) return;
            if (_factory == null)
            {
                t.State = LoadState.Error;
                t.Error = "No database connection";
                return;
            }

            t.State = LoadState.Loading;
            var repo = new ReferenceDataRepository(_factory);
            t.LoadTask = Task.Run(async () =>
            {
                try
                {
                    IEnumerable<ReferenceItem> rows;
                    switch (table)
                    {
                        case Table.LootTable:         rows = await repo.GetAllLootTablesAsync(); break;
                        case Table.NpcFaction:        rows = await repo.GetAllNpcFactionsAsync(); break;
                        case Table.Merchant:          rows = await repo.GetAllMerchantsAsync(); break;
                        case Table.NpcSpellSet:       rows = await repo.GetAllNpcSpellSetsAsync(); break;
                        case Table.NpcSpellEffectSet: rows = await repo.GetAllNpcSpellEffectSetsAsync(); break;
                        case Table.FactionList:       rows = await repo.GetAllFactionListAsync(); break;
                        default: throw new ArgumentOutOfRangeException(nameof(table));
                    }
                    var list = rows.ToList();
                    var dict = new Dictionary<int, string>(list.Count);
                    foreach (var r in list)
                        dict[r.Id] = r.Name ?? "";
                    // Publish atomically to avoid the UI thread seeing partial state
                    lock (t)
                    {
                        t.Items = list;
                        t.ByIdName = dict;
                        t.State = LoadState.Loaded;
                    }
                }
                catch (Exception ex)
                {
                    lock (t)
                    {
                        t.Error = ex.Message;
                        t.State = LoadState.Error;
                    }
                }
            });
        }
    }
}
