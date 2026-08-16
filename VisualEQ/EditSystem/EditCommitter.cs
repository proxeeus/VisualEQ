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
            public int SpawnDeletesWritten;
            public int SpawnInsertsWritten;
            public int GridRowsWritten;
            public int GridInsertsWritten;
            public int GridEntryInsertsWritten;
            public int GridEntryDeletesWritten;
            public int GridMetaRowsWritten;
            public int ZonePointRowsWritten;      // UPDATEs on existing rows
            public int ZonePointInsertsWritten;
            public int ZonePointDeletesWritten;
            public int NpcRowsWritten;            // UPDATEs on npc_types
            public int NpcFactionEntryInserts;    // INSERTs on npc_faction_entries
            public int NpcFactionEntryUpdates;    // UPDATEs on npc_faction_entries
            public int NpcFactionEntryDeletes;    // DELETEs on npc_faction_entries
            public int LootTableEntryInserts;     // INSERTs on loottable_entries
            public int LootTableEntryUpdates;     // UPDATEs on loottable_entries
            public int LootTableEntryDeletes;     // DELETEs on loottable_entries
            public int LootDropEntryInserts;      // INSERTs on lootdrop_entries
            public int LootDropEntryUpdates;      // UPDATEs on lootdrop_entries
            public int LootDropEntryDeletes;      // DELETEs on lootdrop_entries
            public int LootTableRowsWritten;      // UPDATEs on loottable (header fields)

            // Maps pending-insert temp ids (negative) to their assigned AUTO_INCREMENT ids
            // (positive) after a successful INSERT. Consumers (OnCommitSucceeded) apply this
            // to the in-memory ZonePoint list so subsequent edits refer to the persisted row.
            public System.Collections.Generic.Dictionary<int, int> InsertedIdMap;

            // Spawn-insert equivalent: maps (temp spawn2 id) → (real spawn2 id + resolved
            // spawngroup id) so post-commit the in-memory SpawnPoint's Record.Spawn.Id
            // and Record.Spawn.SpawnGroupId reflect the persisted rows. Populated by the
            // pending-insert loop in CommitAsync.
            public System.Collections.Generic.Dictionary<int, SpawnInsertResult> SpawnInsertedIdMap;

            public struct SpawnInsertResult
            {
                public int SpawnId;
                public int SpawnGroupId;
            }
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
                            // DELETE spawn2 rows first — matches the "delete before update"
                            // convention used by grid_entries / trilogy_zone_points, so a
                            // hypothetical "delete id 42, then move id 42" in the same buffer
                            // (which the delete-action already prevents by dropping any prior
                            // Spawns entry) can't accidentally UPDATE a row we're deleting.
                            int spawnDeletes = 0;
                            foreach (var kv in buffer.SpawnDeletes)
                            {
                                spawnDeletes += await connection.ExecuteAsync(
                                    SqlQueries.DeleteSpawn2,
                                    new { Id = kv.Key },
                                    tx);
                            }

                            // Spawn INSERTs — three steps per pending insert, all inside the
                            // same tx: spawngroup → each spawnentry → spawn2. The auto-
                            // increment ids returned by LAST_INSERT_ID() flow through so
                            // (a) spawnentry rows reference the fresh spawngroup id,
                            // (b) spawn2 references the fresh spawngroup id, and
                            // (c) the SpawnInsertedIdMap gives Controller enough to remap
                            //     the negative temp ids on the in-memory SpawnPoints.
                            //
                            // Ordering is BEFORE the UPDATE loop below so any post-duplicate
                            // move / rotate edits (which would sit in buffer.Spawns keyed
                            // on the temp id) would UPDATE a non-existent row. That's why
                            // SpawnMoveAction / SpawnRotateAction mirror into SpawnInserts
                            // for temp ids instead — they never hit buffer.Spawns.
                            int spawnInserts = 0;
                            var spawnInsertedIdMap = new System.Collections.Generic.Dictionary<int, Result.SpawnInsertResult>();
                            foreach (var kv in buffer.SpawnInserts)
                            {
                                var ins = kv.Value;

                                var newGroupId = await connection.ExecuteScalarAsync<int>(
                                    SqlQueries.InsertSpawnGroup,
                                    new { Name = ins.SpawnGroupName },
                                    tx);

                                foreach (var e in ins.Entries)
                                {
                                    await connection.ExecuteAsync(
                                        SqlQueries.InsertSpawnEntry,
                                        new { SpawnGroupId = newGroupId, NpcId = e.NpcId, Chance = e.Chance },
                                        tx);
                                }

                                var newSpawnId = await connection.ExecuteScalarAsync<int>(
                                    SqlQueries.InsertSpawn2,
                                    new
                                    {
                                        SpawnGroupId = newGroupId,
                                        Zone         = ins.Zone,
                                        Version      = ins.Version,
                                        X            = ins.X,
                                        Y            = ins.Y,
                                        Z            = ins.Z,
                                        Heading      = ins.Heading,
                                        RespawnTime  = ins.RespawnTime,
                                        Variance     = ins.Variance,
                                        PathGrid     = ins.PathGrid,
                                        Animation    = ins.Animation,
                                    },
                                    tx);

                                spawnInsertedIdMap[ins.TempSpawnId] = new Result.SpawnInsertResult
                                {
                                    SpawnId      = newSpawnId,
                                    SpawnGroupId = newGroupId,
                                };
                                spawnInserts++;
                            }

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

                            // NPC field edits — sparse per-field updates on npc_types. Each
                            // NpcEdit's CurrentValues dict holds only the columns the user
                            // actually touched, keyed by SQL column name; the SET clause is
                            // built dynamically so a single-field edit doesn't rewrite every
                            // column and MySQL's row-write log stays tight.
                            int npcRows = 0;
                            foreach (var kv in buffer.Npcs)
                            {
                                var edit = kv.Value;
                                if (edit.CurrentValues == null || edit.CurrentValues.Count == 0)
                                    continue;

                                var setClauses = new System.Collections.Generic.List<string>();
                                var parameters = new DynamicParameters();
                                parameters.Add("Id", edit.NpcId);
                                int p = 0;
                                foreach (var fv in edit.CurrentValues)
                                {
                                    var def = NpcFieldCatalog.Get(fv.Key);
                                    if (def == null) continue; // unknown field — skip rather than fail the whole commit
                                    var paramName = $"p{p++}";
                                    setClauses.Add($"{def.ForSet} = @{paramName}");
                                    parameters.Add(paramName, NpcFieldCatalog.ParseValue(fv.Value, def.Kind));
                                }
                                if (setClauses.Count == 0) continue;

                                var sql = $"UPDATE npc_types SET {string.Join(", ", setClauses)} WHERE id = @Id";
                                npcRows += await connection.ExecuteAsync(sql, parameters, tx);
                            }

                            // NPC faction entries — Insert / Update / Delete driven by the
                            // (Original, Current) pair on each NpcFactionEntryOp. Deletes
                            // fire first so a same-frame "delete X then re-add X with a
                            // different value" (rare but possible via Ctrl+Z sequences)
                            // wouldn't hit the PK uniqueness on npc_faction_entries.
                            int nfeDeletes = 0, nfeInserts = 0, nfeUpdates = 0;
                            foreach (var kv in buffer.NpcFactionEntries)
                            {
                                var op = kv.Value;
                                if (op.Original != null && op.Current == null)
                                {
                                    nfeDeletes += await connection.ExecuteAsync(
                                        SqlQueries.DeleteNpcFactionEntry,
                                        new { NpcFactionId = op.NpcFactionId, FactionId = op.FactionId },
                                        tx);
                                }
                            }
                            foreach (var kv in buffer.NpcFactionEntries)
                            {
                                var op = kv.Value;
                                if (op.Original == null && op.Current != null)
                                {
                                    nfeInserts += await connection.ExecuteAsync(
                                        SqlQueries.InsertNpcFactionEntry,
                                        new
                                        {
                                            NpcFactionId = op.NpcFactionId,
                                            FactionId    = op.FactionId,
                                            Value        = op.Current.Value,
                                            NpcValue     = op.Current.NpcValue,
                                            Temp         = op.Current.Temp,
                                        },
                                        tx);
                                }
                                else if (op.Original != null && op.Current != null)
                                {
                                    nfeUpdates += await connection.ExecuteAsync(
                                        SqlQueries.UpdateNpcFactionEntry,
                                        new
                                        {
                                            NpcFactionId = op.NpcFactionId,
                                            FactionId    = op.FactionId,
                                            Value        = op.Current.Value,
                                            NpcValue     = op.Current.NpcValue,
                                            Temp         = op.Current.Temp,
                                        },
                                        tx);
                                }
                            }

                            // ── Loot editor commits (Slice 6b) ─────────────────────
                            // Same DELETE→INSERT→UPDATE ordering as the faction loop
                            // so a delete+re-insert with the same composite PK in
                            // one commit doesn't collide.

                            int lteDeletes = 0, lteInserts = 0, lteUpdates = 0;
                            foreach (var kv in buffer.LootTableEntries)
                            {
                                var op = kv.Value;
                                if (op.Original != null && op.Current == null)
                                {
                                    lteDeletes += await connection.ExecuteAsync(
                                        SqlQueries.DeleteLootTableEntry,
                                        new { LoottableId = op.LoottableId, LootdropId = op.LootdropId },
                                        tx);
                                }
                            }
                            foreach (var kv in buffer.LootTableEntries)
                            {
                                var op = kv.Value;
                                if (op.Original == null && op.Current != null)
                                {
                                    lteInserts += await connection.ExecuteAsync(
                                        SqlQueries.InsertLootTableEntry,
                                        new
                                        {
                                            LoottableId = op.LoottableId,
                                            LootdropId  = op.LootdropId,
                                            Multiplier  = op.Current.Multiplier,
                                            DropLimit   = op.Current.DropLimit,
                                            MinDrop     = op.Current.MinDrop,
                                            Probability = op.Current.Probability,
                                        },
                                        tx);
                                }
                                else if (op.Original != null && op.Current != null)
                                {
                                    lteUpdates += await connection.ExecuteAsync(
                                        SqlQueries.UpdateLootTableEntry,
                                        new
                                        {
                                            LoottableId = op.LoottableId,
                                            LootdropId  = op.LootdropId,
                                            Multiplier  = op.Current.Multiplier,
                                            DropLimit   = op.Current.DropLimit,
                                            MinDrop     = op.Current.MinDrop,
                                            Probability = op.Current.Probability,
                                        },
                                        tx);
                                }
                            }

                            int ldeDeletes = 0, ldeInserts = 0, ldeUpdates = 0;
                            foreach (var kv in buffer.LootDropEntries)
                            {
                                var op = kv.Value;
                                if (op.Original != null && op.Current == null)
                                {
                                    ldeDeletes += await connection.ExecuteAsync(
                                        SqlQueries.DeleteLootDropEntry,
                                        new { LootdropId = op.LootdropId, ItemId = op.ItemId },
                                        tx);
                                }
                            }
                            foreach (var kv in buffer.LootDropEntries)
                            {
                                var op = kv.Value;
                                if (op.Original == null && op.Current != null)
                                {
                                    ldeInserts += await connection.ExecuteAsync(
                                        SqlQueries.InsertLootDropEntry,
                                        new
                                        {
                                            LootdropId      = op.LootdropId,
                                            ItemId          = op.ItemId,
                                            ItemCharges     = op.Current.ItemCharges,
                                            EquipItem       = op.Current.EquipItem,
                                            Chance          = op.Current.Chance,
                                            DisabledChance  = op.Current.DisabledChance,
                                            TrivialMinLevel = op.Current.TrivialMinLevel,
                                            TrivialMaxLevel = op.Current.TrivialMaxLevel,
                                            Multiplier      = op.Current.Multiplier,
                                            NpcMinLevel     = op.Current.NpcMinLevel,
                                            NpcMaxLevel     = op.Current.NpcMaxLevel,
                                        },
                                        tx);
                                }
                                else if (op.Original != null && op.Current != null)
                                {
                                    ldeUpdates += await connection.ExecuteAsync(
                                        SqlQueries.UpdateLootDropEntry,
                                        new
                                        {
                                            LootdropId      = op.LootdropId,
                                            ItemId          = op.ItemId,
                                            ItemCharges     = op.Current.ItemCharges,
                                            EquipItem       = op.Current.EquipItem,
                                            Chance          = op.Current.Chance,
                                            DisabledChance  = op.Current.DisabledChance,
                                            TrivialMinLevel = op.Current.TrivialMinLevel,
                                            TrivialMaxLevel = op.Current.TrivialMaxLevel,
                                            Multiplier      = op.Current.Multiplier,
                                            NpcMinLevel     = op.Current.NpcMinLevel,
                                            NpcMaxLevel     = op.Current.NpcMaxLevel,
                                        },
                                        tx);
                                }
                            }

                            // Loottable header commits (Slice 6c follow-up) —
                            // dynamic UPDATE SET pattern lifted from NpcEdit.
                            // Only the fields the user touched appear in the SET
                            // clause so a name-only edit doesn't rewrite cash.
                            int lootTableRows = 0;
                            foreach (var kv in buffer.LootTables)
                            {
                                var edit = kv.Value;
                                if (edit.CurrentValues == null || edit.CurrentValues.Count == 0)
                                    continue;

                                var setClauses = new System.Collections.Generic.List<string>();
                                var parameters = new DynamicParameters();
                                parameters.Add("Id", edit.LoottableId);
                                int p = 0;
                                foreach (var fv in edit.CurrentValues)
                                {
                                    var def = LootTableFieldCatalog.Get(fv.Key);
                                    if (def == null) continue;
                                    var paramName = $"p{p++}";
                                    setClauses.Add($"{def.ColumnName} = @{paramName}");
                                    parameters.Add(paramName, LootTableFieldCatalog.Parse(fv.Value, def.Kind));
                                }
                                if (setClauses.Count == 0) continue;

                                var sql = $"UPDATE loottable SET {string.Join(", ", setClauses)} WHERE id = @Id";
                                lootTableRows += await connection.ExecuteAsync(sql, parameters, tx);
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
                                // Normalize plane-crossing MinVert/MaxVert at the DB boundary.
                                // The server-side crossing check (trilogy_client.cpp:2005) does
                                // `y >= MinVert && y <= MaxVert` with the raw values — no sort.
                                // If a plane end-cap drag swapped them, the check is impossible
                                // for any Y and the plane silently never fires. Sort here so
                                // no matter how the editor state got there, the DB row is
                                // always in the canonical MinVert <= MaxVert order. Left = 0/0
                                // untouched (server treats as unbounded).
                                var (insMinVert, insMaxVert) = SortPlaneBounds(ins.UseNewZoning, ins.MinVert, ins.MaxVert);
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
                                        MinVert      = insMinVert, MaxVert = insMaxVert, CenterPoint = ins.CenterPoint,
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
                                // Same MinVert/MaxVert normalization as the insert path — see
                                // the SortPlaneBounds comment above. An end-cap drag that ends
                                // with MinVert > MaxVert would otherwise persist to the DB and
                                // silently break server-side crossing detection.
                                var (editMinVert, editMaxVert) = SortPlaneBounds(edit.CurrentUseNewZoning, edit.CurrentMinVert, edit.CurrentMaxVert);
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
                                        MinVert      = editMinVert,
                                        MaxVert      = editMaxVert,
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
                                SpawnDeletesWritten      = spawnDeletes,
                                SpawnInsertsWritten      = spawnInserts,
                                SpawnInsertedIdMap       = spawnInsertedIdMap,
                                GridRowsWritten          = gridRows,
                                GridInsertsWritten       = gridInserts,
                                GridEntryInsertsWritten  = gridEntryInserts,
                                GridEntryDeletesWritten  = gridEntryDeletes,
                                GridMetaRowsWritten      = gridMetaRows,
                                ZonePointRowsWritten     = zonePointRows,
                                ZonePointInsertsWritten  = zonePointInserts,
                                ZonePointDeletesWritten  = zonePointDeletes,
                                NpcRowsWritten           = npcRows,
                                NpcFactionEntryInserts   = nfeInserts,
                                NpcFactionEntryUpdates   = nfeUpdates,
                                NpcFactionEntryDeletes   = nfeDeletes,
                                LootTableEntryInserts    = lteInserts,
                                LootTableEntryUpdates    = lteUpdates,
                                LootTableEntryDeletes    = lteDeletes,
                                LootDropEntryInserts     = ldeInserts,
                                LootDropEntryUpdates     = ldeUpdates,
                                LootDropEntryDeletes     = ldeDeletes,
                                LootTableRowsWritten     = lootTableRows,
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

        // Sort MinVert/MaxVert so the DB row stores them in the canonical
        // MinVert <= MaxVert order that the server's plane-crossing check
        // (trilogy_client.cpp:2005, 2010, 2059, 2064) reads. Only touch plane
        // modes (1 = X-plane, 2 = Y-plane); box mode ignores these columns.
        // 0/0 stays 0/0 — the server treats it as "unbounded" and swapping
        // wouldn't change semantics but would tag the row as "edited" in
        // diffs, so leave the sentinel alone.
        static (float min, float max) SortPlaneBounds(byte useNewZoning, float minVert, float maxVert)
        {
            if (useNewZoning != 1 && useNewZoning != 2) return (minVert, maxVert);
            if (minVert == 0f && maxVert == 0f) return (minVert, maxVert);
            return (System.MathF.Min(minVert, maxVert), System.MathF.Max(minVert, maxVert));
        }
    }
}
