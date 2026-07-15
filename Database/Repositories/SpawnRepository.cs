using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using VisualEQ.Database.Configuration;
using VisualEQ.Database.Constants;
using VisualEQ.Database.Exceptions;
using VisualEQ.Database.Models;
using VisualEQ.Database.ViewModels;
using VisualEQ.Database.Repositories.Base;
using VisualEQ.Database.Repositories.Interfaces;
using System.Data;

namespace VisualEQ.Database.Repositories
{
    public class SpawnRepository : RepositoryBase, ISpawnRepository
    {
        public SpawnRepository(IDbConnectionFactory connectionFactory) 
            : base(connectionFactory)
        {
        }

        public async Task<IEnumerable<SpawnViewModel>> GetZoneSpawnsAsync(string zoneName)
        {
            using (var connection = CreateConnection())
            {
                var spawns = await connection.QueryAsync<Spawn2>(SqlQueries.GetZoneSpawns, new { ZoneName = zoneName });
                return spawns.Select(ToViewModel);
            }
        }

        public async Task<IEnumerable<SpawnRecord>> GetZoneSpawnsFullAsync(string zoneName)
        {
            using (var connection = CreateConnection())
            {
                connection.Open();

                // 1. All spawn2 rows + spawngroup name for this zone.
                var spawns = (await connection.QueryAsync<Spawn2>(
                    SqlQueries.GetZoneSpawns, new { ZoneName = zoneName })).ToList();

                if (!spawns.Any())
                    return Enumerable.Empty<SpawnRecord>();

                Console.WriteLine($"[DB] {spawns.Count} spawn2 rows for zone '{zoneName}'");

                // 2. Numeric zone ID (needed for grid_entries zone filter).
                var zoneId = await connection.QueryFirstOrDefaultAsync<int>(
                    SqlQueries.GetZoneId, new { ZoneName = zoneName });

                // 3. Batch spawnentry for all spawn groups.
                var groupIds = spawns.Select(s => s.SpawnGroupId).Distinct().ToArray();
                var entries = (await connection.QueryAsync<SpawnEntry>(
                    SqlQueries.GetSpawnEntriesBatch, new { GroupIds = groupIds })).ToList();

                // 4. Batch npc_types for all NPC IDs referenced by those entries.
                var npcIds = entries.Select(e => e.NpcId).Distinct().ToArray();
                var npcs = npcIds.Length > 0
                    ? (await connection.QueryAsync<NpcType>(
                        SqlQueries.GetNpcTypesBatch, new { NpcIds = npcIds })).ToList()
                    : new List<NpcType>();
                var npcById = npcs.ToDictionary(n => n.Id);

                // 5. Batch grid_entries for all non-zero pathgrid values.
                var gridIds = spawns.Where(s => s.PathGrid > 0)
                                    .Select(s => s.PathGrid).Distinct().ToArray();
                var gridEntries = gridIds.Length > 0
                    ? (await connection.QueryAsync<GridEntry>(
                        SqlQueries.GetGridEntriesBatch, new { GridIds = gridIds, ZoneId = zoneId })).ToList()
                    : new List<GridEntry>();

                // 5b. Batch grid metadata for the same set of grid ids.
                var grids = gridIds.Length > 0
                    ? (await connection.QueryAsync<Grid>(
                        SqlQueries.GetGridsBatch, new { GridIds = gridIds, ZoneId = zoneId })).ToList()
                    : new List<Grid>();
                var gridById = grids.ToDictionary(g => g.Id);

                // 6. Index for O(1) assembly.
                var entriesByGroup = entries
                    .GroupBy(e => e.SpawnGroupId)
                    .ToDictionary(g => g.Key, g => g.ToList());
                var waypointsByGrid = gridEntries
                    .GroupBy(ge => ge.GridId)
                    .ToDictionary(g => g.Key, g => g.OrderBy(ge => ge.Number).ToList());

                // 7. Assemble SpawnRecord objects.
                return spawns.Select(s => new SpawnRecord
                {
                    Spawn = s,
                    Entries = entriesByGroup.TryGetValue(s.SpawnGroupId, out var groupEntries)
                        ? groupEntries
                            .Select(e => new SpawnEntryWithNpc
                            {
                                Entry = e,
                                Npc = npcById.TryGetValue(e.NpcId, out var npc) ? npc : null
                            })
                            .Where(en => en.Npc != null)
                            .ToList()
                        : new List<SpawnEntryWithNpc>(),
                    Waypoints = s.PathGrid > 0 && waypointsByGrid.TryGetValue(s.PathGrid, out var wps)
                        ? wps
                        : new List<GridEntry>(),
                    Grid = s.PathGrid > 0 && gridById.TryGetValue(s.PathGrid, out var gr) ? gr : null
                }).ToList();
            }
        }

        public async Task<IEnumerable<ZoneGridRecord>> GetZoneGridsAsync(string zoneName)
        {
            using (var connection = CreateConnection())
            {
                connection.Open();

                var zoneId = await connection.QueryFirstOrDefaultAsync<int>(
                    SqlQueries.GetZoneId, new { ZoneName = zoneName });
                if (zoneId == 0)
                    return Enumerable.Empty<ZoneGridRecord>();

                var grids = (await connection.QueryAsync<Grid>(
                    SqlQueries.GetAllZoneGrids, new { ZoneId = zoneId })).ToList();
                if (grids.Count == 0)
                    return Enumerable.Empty<ZoneGridRecord>();

                var entries = (await connection.QueryAsync<GridEntry>(
                    SqlQueries.GetAllZoneGridEntries, new { ZoneId = zoneId })).ToList();

                var entriesByGrid = entries
                    .GroupBy(e => e.GridId)
                    .ToDictionary(g => g.Key, g => g.OrderBy(e => e.Number).ToList());

                Console.WriteLine($"[DB] {grids.Count} grid(s), {entries.Count} grid_entries row(s) for zone '{zoneName}'");

                return grids.Select(g => new ZoneGridRecord
                {
                    Grid       = g,
                    Waypoints  = entriesByGrid.TryGetValue(g.Id, out var wps) ? wps : new List<GridEntry>(),
                    SpawnCount = 0
                }).ToList();
            }
        }

        public async Task<SpawnViewModel> GetSpawnByIdAsync(int spawnId)
        {
            using (var connection = CreateConnection())
            {
                var spawn = await connection.QueryFirstOrDefaultAsync<Spawn2>(
                    SqlQueries.GetSpawnById,
                    new { SpawnId = spawnId }
                );

                if (spawn == null)
                    throw new SpawnNotFoundException(spawnId);

                return ToViewModel(spawn);
            }
        }

        private static SpawnViewModel ToViewModel(Spawn2 s) => new SpawnViewModel
        {
            Id = s.Id,
            ZoneName = s.Zone,
            Position = new Vector3(s.X, s.Y, s.Z),
            Heading = s.Heading,
            SpawnGroupId = s.SpawnGroupId,
            SpawnGroupName = s.SpawnGroupName,
            IsEnabled = true,   // no 'enabled' column in this schema version
            RespawnTime = s.RespawnTime
        };

        public async Task<bool> UpdateSpawnLocationAsync(int spawnId, Vector3 position, float heading)
        {
            using (var connection = CreateConnection())
            {
                var rowsAffected = await connection.ExecuteAsync(
                    SqlQueries.UpdateSpawnLocation,
                    new 
                    { 
                        SpawnId = spawnId,
                        X = position.X,
                        Y = position.Y,
                        Z = position.Z,
                        Heading = heading
                    }
                );

                return rowsAffected > 0;
            }
        }
    }
} 