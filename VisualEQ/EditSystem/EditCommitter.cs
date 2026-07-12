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

                            // Zone-point edits: buffer holds every editable field, so we
                            // write from the buffer directly. ToZoneID is re-derived from
                            // the current target_zone shortname (cheap SELECT per unique
                            // target zone) so it never drifts out of sync with target_zone.
                            int zonePointRows = 0;
                            var toZoneIdCache = new System.Collections.Generic.Dictionary<string, int>(System.StringComparer.OrdinalIgnoreCase);
                            foreach (var kv in buffer.ZonePoints)
                            {
                                var edit = kv.Value;
                                var targetShort = edit.CurrentTargetZone ?? "";
                                if (!toZoneIdCache.TryGetValue(targetShort, out var toZoneId))
                                {
                                    toZoneId = await connection.QueryFirstOrDefaultAsync<int?>(
                                        SqlQueries.GetZoneId, new { ZoneName = targetShort }, tx) ?? 0;
                                    toZoneIdCache[targetShort] = toZoneId;
                                }
                                zonePointRows += await connection.ExecuteAsync(
                                    SqlQueries.UpdateTrilogyZonePoint,
                                    new
                                    {
                                        Id           = edit.Id,
                                        X            = edit.CurrentX,
                                        Y            = edit.CurrentY,
                                        Z            = edit.CurrentZ,
                                        Heading      = edit.CurrentHeading,
                                        TargetZone   = edit.CurrentTargetZone,
                                        TargetX      = edit.CurrentTargetX,
                                        TargetY      = edit.CurrentTargetY,
                                        TargetZ      = edit.CurrentTargetZ,
                                        Zrange       = edit.CurrentZrange,
                                        MaxZDiff     = edit.CurrentMaxZDiff,
                                        UseNewZoning = edit.CurrentUseNewZoning,
                                        MinVert      = edit.CurrentMinVert,
                                        MaxVert      = edit.CurrentMaxVert,
                                        CenterPoint  = edit.CurrentCenterPoint,
                                        KeepX        = edit.CurrentKeepX,
                                        KeepY        = edit.CurrentKeepY,
                                        KeepZ        = edit.CurrentKeepZ,
                                        ToZoneId     = toZoneId,
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
