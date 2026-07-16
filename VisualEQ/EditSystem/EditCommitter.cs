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
            public int GridInsertsWritten;
            public int GridEntryInsertsWritten;
            public int GridEntryDeletesWritten;
            public int GridMetaRowsWritten;
            public int ZonePointRowsWritten;      // UPDATEs on existing rows
            public int ZonePointInsertsWritten;
            public int ZonePointDeletesWritten;

            // Maps pending-insert temp ids (negative) to their assigned AUTO_INCREMENT ids
            // (positive) after a successful INSERT. Consumers (OnCommitSucceeded) apply this
            // to the in-memory ZonePoint list so subsequent edits refer to the persisted row.
            public System.Collections.Generic.Dictionary<int, int> InsertedIdMap;
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
                    if (buffer.GridEntries.Count > 0 ||
                        buffer.GridEntryInserts.Count > 0 ||
                        buffer.GridEntryDeletes.Count > 0 ||
                        buffer.Grids.Count > 0 ||
                        buffer.GridInserts.Count > 0)
                    {
                        zoneId = await connection.QueryFirstOrDefaultAsync<int?>(
                            SqlQueries.GetZoneId, new { ZoneName = zoneName });
                        if (zoneId == null)
                            return new Result { Success = false, Error = $"Zone '{zoneName}' not found in DB (needed to write grid/grid_entries)." };
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

                            // Waypoint DELETE first so a delete + re-insert with the same
                            // (gridid, number) doesn't briefly duplicate a PK during the tx.
                            int gridEntryDeletes = 0;
                            foreach (var kv in buffer.GridEntryDeletes)
                            {
                                var del = kv.Value;
                                gridEntryDeletes += await connection.ExecuteAsync(
                                    SqlQueries.DeleteGridEntry,
                                    new { GridId = del.GridId, Number = del.Number, ZoneId = zoneId },
                                    tx);
                            }

                            // Whole-grid INSERTs — assign a real (positive) id via
                            // MAX(id)+1 FOR UPDATE, then INSERT the grid row. Keep the
                            // temp→real mapping so the GridEntryInserts loop below can
                            // remap each seed waypoint's GridId before writing.
                            int gridInserts = 0;
                            var insertedGridIdMap = new System.Collections.Generic.Dictionary<int, int>();
                            foreach (var kv in buffer.GridInserts)
                            {
                                var ins = kv.Value;
                                var newId = await connection.ExecuteScalarAsync<int>(
                                    SqlQueries.NextGridIdForZone,
                                    new { ZoneId = ins.ZoneId },
                                    tx);
                                await connection.ExecuteAsync(
                                    SqlQueries.InsertGrid,
                                    new { Id = newId, ZoneId = ins.ZoneId, Type = ins.Type, Type2 = ins.Type2 },
                                    tx);
                                insertedGridIdMap[ins.TempId] = newId;
                                gridInserts++;
                            }

                            int gridEntryInserts = 0;
                            foreach (var kv in buffer.GridEntryInserts)
                            {
                                var ins = kv.Value;
                                // Remap negative temp GridId to the freshly-assigned real
                                // id from the GridInserts loop above. Non-temp GridIds
                                // (waypoint additions to existing grids) pass through
                                // unchanged.
                                var effectiveGridId = insertedGridIdMap.TryGetValue(ins.GridId, out var realId)
                                    ? realId
                                    : ins.GridId;
                                gridEntryInserts += await connection.ExecuteAsync(
                                    SqlQueries.InsertGridEntry,
                                    new
                                    {
                                        GridId      = effectiveGridId,
                                        ZoneId      = zoneId,
                                        Number      = ins.Number,
                                        X           = ins.X,
                                        Y           = ins.Y,
                                        Z           = ins.Z,
                                        Heading     = ins.Heading,
                                        Pause       = ins.Pause,
                                        Centerpoint = ins.Centerpoint,
                                    },
                                    tx);
                            }

                            int gridRows = 0;
                            foreach (var kv in buffer.GridEntries)
                            {
                                var edit = kv.Value;
                                // Trace log for diagnosing "editor stored X, DB has Y" reports.
                                // Prints the exact float bound to the UPDATE — a hidden
                                // conversion elsewhere would show up as a mismatch between
                                // the pre-commit value the user saw in the inspector and
                                // this line.
                                Console.WriteLine(
                                    $"[EditCommitter] grid_entries: gid={edit.GridId} #{edit.Number} " +
                                    $"heading orig={edit.OriginalHeading:F4} → cur={edit.CurrentHeading:F4} " +
                                    $"(pause={edit.CurrentPause}s, centerpoint={edit.CurrentCenterpoint})");
                                gridRows += await connection.ExecuteAsync(
                                    SqlQueries.UpdateGridEntry,
                                    new
                                    {
                                        GridId      = edit.GridId,
                                        Number      = edit.Number,
                                        ZoneId      = zoneId,
                                        X           = edit.CurrentX,
                                        Y           = edit.CurrentY,
                                        Z           = edit.CurrentZ,
                                        Heading     = edit.CurrentHeading,
                                        Pause       = edit.CurrentPause,
                                        Centerpoint = edit.CurrentCenterpoint,
                                    },
                                    tx);
                            }

                            int gridMetaRows = 0;
                            foreach (var kv in buffer.Grids)
                            {
                                var edit = kv.Value;
                                gridMetaRows += await connection.ExecuteAsync(
                                    SqlQueries.UpdateGrid,
                                    new
                                    {
                                        Id     = edit.Id,
                                        ZoneId = edit.ZoneId,
                                        Type   = edit.CurrentType,
                                        Type2  = edit.CurrentType2,
                                    },
                                    tx);
                            }

                            // Zone-point commits: DELETE first (so a delete+re-insert with
                            // the same target coord doesn't briefly duplicate a row), then
                            // INSERT (returns AUTO_INCREMENT ids we map back to the temp
                            // ids), then UPDATE the pre-existing rows.
                            //
                            // ToZoneID is re-derived from the current target_zone shortname
                            // for every INSERT/UPDATE (cached per shortname) so it never
                            // drifts out of sync with target_zone across renames.
                            var toZoneIdCache = new System.Collections.Generic.Dictionary<string, int>(System.StringComparer.OrdinalIgnoreCase);
                            async Task<int> ResolveToZoneIdAsync(string shortName)
                            {
                                var s = shortName ?? "";
                                if (toZoneIdCache.TryGetValue(s, out var cached)) return cached;
                                var resolved = await connection.QueryFirstOrDefaultAsync<int?>(
                                    SqlQueries.GetZoneId, new { ZoneName = s }, tx) ?? 0;
                                toZoneIdCache[s] = resolved;
                                return resolved;
                            }

                            int zonePointDeletes = 0;
                            foreach (var deleteId in buffer.ZonePointDeletes)
                            {
                                zonePointDeletes += await connection.ExecuteAsync(
                                    SqlQueries.DeleteTrilogyZonePoint, new { Id = deleteId }, tx);
                            }

                            int zonePointInserts = 0;
                            var insertedIdMap = new System.Collections.Generic.Dictionary<int, int>();
                            foreach (var kv in buffer.ZonePointInserts)
                            {
                                var ins = kv.Value;
                                var toZoneId = await ResolveToZoneIdAsync(ins.TargetZone);
                                var newId = await connection.ExecuteScalarAsync<int>(
                                    SqlQueries.InsertTrilogyZonePoint,
                                    new
                                    {
                                        Zone         = ins.Zone,
                                        X            = ins.X, Y = ins.Y, Z = ins.Z, Heading = ins.Heading,
                                        TargetZone   = ins.TargetZone,
                                        TargetX      = ins.TargetX, TargetY = ins.TargetY, TargetZ = ins.TargetZ,
                                        Zrange       = ins.Zrange, MaxZDiff = ins.MaxZDiff,
                                        UseNewZoning = ins.UseNewZoning,
                                        MinVert      = ins.MinVert, MaxVert = ins.MaxVert, CenterPoint = ins.CenterPoint,
                                        KeepX        = ins.KeepX, KeepY = ins.KeepY, KeepZ = ins.KeepZ,
                                        ToZoneId     = toZoneId,
                                    },
                                    tx);
                                insertedIdMap[ins.TempId] = newId;
                                zonePointInserts++;
                            }

                            int zonePointRows = 0;
                            foreach (var kv in buffer.ZonePoints)
                            {
                                var edit = kv.Value;
                                var toZoneId = await ResolveToZoneIdAsync(edit.CurrentTargetZone);
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
                                Success                  = true,
                                SpawnRowsWritten         = spawnRows,
                                GridRowsWritten          = gridRows,
                                GridInsertsWritten       = gridInserts,
                                GridEntryInsertsWritten  = gridEntryInserts,
                                GridEntryDeletesWritten  = gridEntryDeletes,
                                GridMetaRowsWritten      = gridMetaRows,
                                ZonePointRowsWritten     = zonePointRows,
                                ZonePointInsertsWritten  = zonePointInserts,
                                ZonePointDeletesWritten  = zonePointDeletes,
                                InsertedIdMap            = insertedIdMap,
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
