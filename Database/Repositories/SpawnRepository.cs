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
                
                return spawns.Select(s => new SpawnViewModel
                {
                    Id = s.Id,
                    ZoneName = s.Zone,
                    Position = new Vector3(s.X, s.Y, s.Z),
                    Heading = s.Heading,
                    SpawnGroupId = s.SpawnGroupId,
                    IsEnabled = s.Enabled,
                    RespawnTime = s.RespawnTime
                });
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

                return new SpawnViewModel
                {
                    Id = spawn.Id,
                    ZoneName = spawn.Zone,
                    Position = new Vector3(spawn.X, spawn.Y, spawn.Z),
                    Heading = spawn.Heading,
                    SpawnGroupId = spawn.SpawnGroupId,
                    IsEnabled = spawn.Enabled,
                    RespawnTime = spawn.RespawnTime
                };
            }
        }

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