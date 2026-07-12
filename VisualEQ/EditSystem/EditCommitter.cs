using System;
using System.Threading.Tasks;
using Dapper;
using VisualEQ.Database.Configuration;
using VisualEQ.Database.Constants;

namespace VisualEQ.EditSystem
{
    // Writes a full EditBuffer to the DB inside a single transaction. Either every row
    // lands or none of them do — the "atomicity" answer to the "transaction commit" plan.
    public static class EditCommitter
    {
        public class Result
        {
            public bool Success;
            public string Error;
            public int SpawnRowsWritten;
            public int GridRowsWritten;
            public int ZonePointRowsWritten;
        }

        public static async Task<Result> CommitAsync(
            EditBuffer buffer,
            string zoneName,
            MySqlConnectionFactory factory)
        {
            if (buffer == null || buffer.IsEmpty)
                return new Result { Success = true };
            if (factory == null)
                return new Result { Success = false, Error = "No database connection is configured." };

            try
            {
                using (var connection = factory.CreateConnection())
                {
                    connection.Open();

                    int? zoneId = null;
                    if (buffer.GridEntries.Count > 0)
                    {
                        zoneId = await connection.QueryFirstOrDefaultAsync<int?>(
                            SqlQueries.GetZoneId, new { ZoneName = zoneName });
                        if (zoneId == null)
                            return new Result { Success = false, Error = $"Zone '{zoneName}' not found in DB (needed to write grid_entries)." };
                    }

                    using (var tx = connection.BeginTransaction())
                    {
                        try
                        {
                            int spawnRows = 0;
                            foreach (var kv in buffer.Spawns)
                            {
                                var edit = kv.Value;
                                spawnRows += await connection.ExecuteAsync(
                                    SqlQueries.UpdateSpawnLocation,
                                    new
                                    {
                                        SpawnId = edit.SpawnId,
                                        X       = edit.CurrentX,
                                        Y       = edit.CurrentY,
                                        Z       = edit.CurrentZ,
                                        Heading = edit.CurrentHeading,
                                    },
                                    tx);
                            }

                            int gridRows = 0;
                            foreach (var kv in buffer.GridEntries)
                            {
                                var edit = kv.Value;
                                gridRows += await connection.ExecuteAsync(
                                    SqlQueries.UpdateGridEntry,
                                    new
                                    {
                                        GridId  = edit.GridId,
                                        Number  = edit.Number,
                                        ZoneId  = zoneId,
                                        X       = edit.CurrentX,
                                        Y       = edit.CurrentY,
                                        Z       = edit.CurrentZ,
                                        Heading = edit.CurrentHeading,
                                        Pause   = edit.CurrentPause,
                                    },
                                    tx);
                            }

                            // Zone-point edits: read the current row first so unchanged
                            // fields (target coords, keep*, MinVert etc.) round-trip
                            // untouched — the v1 drag editor only mutates X/Y/Z/Zrange/MaxZDiff.
                            int zonePointRows = 0;
                            foreach (var kv in buffer.ZonePoints)
                            {
                                var edit = kv.Value;
                                var existing = await connection.QueryFirstOrDefaultAsync<VisualEQ.Database.Models.TrilogyZonePoint>(
                                    SqlQueries.GetTrilogyZonePoints + " AND id = @Id",
                                    new { ZoneName = zoneName, Id = edit.Id },
                                    tx);
                                if (existing == null)
                                {
                                    Console.WriteLine($"[EditCommitter] Skipping missing zone_point id={edit.Id}");
                                    continue;
                                }
                                zonePointRows += await connection.ExecuteAsync(
                                    SqlQueries.UpdateTrilogyZonePoint,
                                    new
                                    {
                                        Id           = edit.Id,
                                        X            = edit.CurrentX,
                                        Y            = edit.CurrentY,
                                        Z            = edit.CurrentZ,
                                        Heading      = existing.Heading,
                                        TargetZone   = existing.TargetZone,
                                        TargetX      = existing.TargetX,
                                        TargetY      = existing.TargetY,
                                        TargetZ      = existing.TargetZ,
                                        Zrange       = edit.CurrentZrange,
                                        MaxZDiff     = edit.CurrentMaxZDiff,
                                        UseNewZoning = existing.UseNewZoning,
                                        MinVert      = existing.MinVert,
                                        MaxVert      = existing.MaxVert,
                                        CenterPoint  = existing.CenterPoint,
                                        KeepX        = existing.KeepX,
                                        KeepY        = existing.KeepY,
                                        KeepZ        = existing.KeepZ,
                                        ToZoneId     = existing.ToZoneId,
                                    },
                                    tx);
                            }

                            tx.Commit();
                            return new Result
                            {
                                Success          = true,
                                SpawnRowsWritten = spawnRows,
                                GridRowsWritten  = gridRows,
                                ZonePointRowsWritten = zonePointRows,
                            };
                        }
                        catch (Exception ex)
                        {
                            try { tx.Rollback(); }
                            catch (Exception rbEx) { Console.WriteLine($"[EditCommitter] Rollback failed: {rbEx.Message}"); }
                            return new Result { Success = false, Error = ex.Message };
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                return new Result { Success = false, Error = ex.Message };
            }
        }
    }
}
