using System;
using System.Collections.Generic;
using System.Linq;
using VisualEQ.Database.Models;
using VisualEQ.SpawnSystem;

namespace VisualEQ.EditSystem
{
    // Marks a spawn2 row for deletion. Two paths:
    //
    //   Persisted row (Spawn.Id > 0):
    //     Apply  → SpawnManager.Hide + engine/CharacterModels detach + add SpawnDelete
    //              entry. SpawnPoint parked in SpawnManager.HiddenSpawnPoints for revert.
    //     Revert → SpawnManager.Restore + engine/CharacterModels re-attach + drop entry.
    //
    //   Pending-insert row (Spawn.Id < 0):
    //     Apply  → engine/CharacterModels detach + SpawnManager.SpawnPoints.Remove +
    //              drop the SpawnInsert entry from the buffer. Never lands in
    //              HiddenSpawnPoints — the row was never in the DB to begin with.
    //     Revert → re-attach the snapshot SpawnPoint to scene + put SpawnInsert back
    //              in the buffer.
    //
    // Session recovery: on zone reload, persisted deletes replay via ApplyPendingBuffer
    // (hides matching just-loaded SpawnPoints). Pending-insert deletes don't need
    // recovery — the buffer.SpawnInserts entry was dropped at Apply time, so the
    // reloaded scene simply won't contain the row.
    public sealed class SpawnDeleteAction : IEditAction
    {
        public int SpawnId { get; }
        public string DisplayName { get; }
        public DateTime Timestamp { get; }
        public bool WasPendingInsert => _pendingInsertSnapshot != null;

        // Held for the pending-insert Revert path — must re-add both the scene node and
        // the buffer entry. For persisted rows this is null; revert flows through
        // SpawnManager.HiddenSpawnPoints instead.
        readonly SpawnPoint _snapshot;
        readonly SpawnInsert _pendingInsertSnapshot;

        public string Description => WasPendingInsert
            ? $"Deleted new '{DisplayName}' (temp #{SpawnId})"
            : $"Deleted '{DisplayName}' (#{SpawnId})";
        public string TargetKey   => $"spawn:{SpawnId}";

        // pendingInsertSnapshot: pass the current buffer.SpawnInserts entry when
        // sp.Record.Spawn.Id < 0. Controller.DeleteSelectedSpawn handles the lookup so
        // callers don't have to know about the buffer.
        public SpawnDeleteAction(SpawnPoint sp, SpawnInsert pendingInsertSnapshot = null)
        {
            SpawnId                = sp.Record.Spawn.Id;
            DisplayName            = PrimaryName(sp);
            Timestamp              = DateTime.UtcNow;
            _snapshot              = sp;
            _pendingInsertSnapshot = pendingInsertSnapshot;
        }

        public void Apply(Controller controller)
        {
            var sp = controller.SpawnManager.SpawnPoints
                .FirstOrDefault(p => p.Record.Spawn.Id == SpawnId);
            if (sp == null) return; // already removed — idempotent

            if (WasPendingInsert)
            {
                controller.DetachPendingInsertSpawn(sp);
                var buffer = controller.PendingBuffer;
                if (buffer != null)
                {
                    buffer.SpawnInserts.Remove(SpawnId);
                    buffer.Spawns.Remove(SpawnId);
                    controller.MarkBufferDirty();
                }
                return;
            }

            controller.HideSpawnFromScene(sp);

            var buf = controller.PendingBuffer;
            if (buf != null)
            {
                buf.SpawnDeletes[SpawnId] = new SpawnDelete
                {
                    SpawnId     = SpawnId,
                    DisplayName = DisplayName,
                    DeletedAt   = DateTime.UtcNow,
                };
                buf.Spawns.Remove(SpawnId);
                controller.MarkBufferDirty();
            }
        }

        public void Revert(Controller controller)
        {
            if (WasPendingInsert)
            {
                // Re-attach the snapshot node + restore the buffer entry so the temp
                // row shows up in Pending Changes again and can be committed on the
                // next Save. Snapshot node is safe to re-Add because we never disposed
                // its AniModelInstance (Engine.Remove doesn't drop the ref).
                controller.ReattachPendingInsertSpawn(_snapshot);
                var buffer = controller.PendingBuffer;
                if (buffer != null)
                {
                    buffer.SpawnInserts[SpawnId] = _pendingInsertSnapshot;
                    controller.MarkBufferDirty();
                }
                return;
            }

            controller.RestoreSpawnToScene(SpawnId);
            var buf = controller.PendingBuffer;
            if (buf != null)
            {
                buf.SpawnDeletes.Remove(SpawnId);
                controller.MarkBufferDirty();
            }
        }

        static string PrimaryName(SpawnPoint sp) =>
            sp.Record.Entries
                .OrderByDescending(e => e.Entry.Chance)
                .FirstOrDefault()?.Npc?.Name ?? "?";
    }

    // Sidebar "Revert" affordance for a pending spawn delete. Symmetric with
    // SpawnDeleteAction — Apply un-hides the SpawnPoint, Revert re-hides. Undo history
    // stays coherent: after clicking Revert, Ctrl+Z re-deletes; a second Ctrl+Z restores.
    //
    // Only used for persisted-row deletes (id > 0). Pending-insert deletes are undone
    // by SpawnDeleteAction.Revert (Ctrl+Z) since the snapshot lives on the action.
    public sealed class SpawnRestoreAction : IEditAction
    {
        public int SpawnId { get; }
        public string DisplayName { get; }
        public DateTime Timestamp { get; }

        public string Description => $"Restored '{DisplayName}' (#{SpawnId})";
        public string TargetKey   => $"spawn:{SpawnId}";

        public SpawnRestoreAction(int spawnId, string displayName)
        {
            SpawnId     = spawnId;
            DisplayName = displayName ?? "?";
            Timestamp   = DateTime.UtcNow;
        }

        public void Apply(Controller controller)
        {
            controller.RestoreSpawnToScene(SpawnId);
            var buffer = controller.PendingBuffer;
            if (buffer != null)
            {
                buffer.SpawnDeletes.Remove(SpawnId);
                controller.MarkBufferDirty();
            }
        }

        public void Revert(Controller controller)
        {
            var sp = controller.SpawnManager.SpawnPoints
                .FirstOrDefault(p => p.Record.Spawn.Id == SpawnId);
            if (sp == null) return;

            controller.HideSpawnFromScene(sp);
            var buffer = controller.PendingBuffer;
            if (buffer != null)
            {
                buffer.SpawnDeletes[SpawnId] = new SpawnDelete
                {
                    SpawnId     = SpawnId,
                    DisplayName = DisplayName,
                    DeletedAt   = DateTime.UtcNow,
                };
                controller.MarkBufferDirty();
            }
        }
    }

    // Creates a fresh spawn2 row + spawngroup + spawnentries from a cloned SpawnRecord.
    // Apply builds the in-scene SpawnPoint via SpawnManager.LoadSingle (same model
    // pipeline as initial zone load) and adds a SpawnInsert to the buffer; Revert
    // detaches + drops the buffer entry.
    //
    // The in-memory SpawnPoint uses a negative temp id (SpawnManager.NextTempSpawnId)
    // until commit lands a real spawn2.id via Controller.OnCommitSucceeded's remap.
    //
    // On session recovery ApplyPendingBuffer replays each SpawnInsert by rebuilding
    // the SpawnRecord from stored fields + walking npc_types for the primary NPC's
    // race/size/textures — the same lookups the initial load performs. Slice 2
    // deliberately relies on the fresh session having a DB connection (no offline
    // recovery of pending inserts).
    public sealed class SpawnInsertAction : IEditAction
    {
        public int TempSpawnId { get; }
        public string DisplayName { get; }
        public DateTime Timestamp { get; }

        // Snapshot: the fully-cloned SpawnRecord (spawn2 + entries) + the SpawnInsert
        // buffer entry to write on Apply. Both frozen at duplicate time so undo/redo
        // stays consistent even if the user later moves / rotates the temp spawn (those
        // edits mirror into buffer.SpawnInserts directly via the move/rotate actions;
        // the snapshot here is only used for Revert-and-Redo-of-the-original-Apply).
        readonly SpawnRecord _record;
        readonly SpawnInsert _initialInsert;

        public string Description => $"Duplicated '{DisplayName}' (temp #{TempSpawnId})";
        public string TargetKey   => $"spawn:{TempSpawnId}";

        public SpawnInsertAction(SpawnRecord record, SpawnInsert initialInsert)
        {
            TempSpawnId    = record.Spawn.Id;
            DisplayName    = record.Entries.OrderByDescending(e => e.Entry.Chance).FirstOrDefault()?.Npc?.Name ?? "?";
            Timestamp      = DateTime.UtcNow;
            _record        = record;
            _initialInsert = initialInsert;
        }

        public void Apply(Controller controller)
        {
            // Idempotent: skip if the temp SpawnPoint is already attached (Ctrl+Y after
            // Ctrl+Z, or ApplyPendingBuffer replay after session recovery).
            if (controller.SpawnManager.SpawnPoints.Any(p => p.Record.Spawn.Id == TempSpawnId))
                return;

            controller.SpawnPendingInsertFromSnapshot(_record);

            var buffer = controller.PendingBuffer;
            if (buffer != null)
            {
                buffer.SpawnInserts[TempSpawnId] = _initialInsert;
                controller.MarkBufferDirty();
            }
        }

        public void Revert(Controller controller)
        {
            var sp = controller.SpawnManager.SpawnPoints
                .FirstOrDefault(p => p.Record.Spawn.Id == TempSpawnId);
            if (sp != null) controller.DetachPendingInsertSpawn(sp);

            var buffer = controller.PendingBuffer;
            if (buffer != null)
            {
                buffer.SpawnInserts.Remove(TempSpawnId);
                // Drop any post-duplicate field edits too — the row is gone.
                buffer.Spawns.Remove(TempSpawnId);
                controller.MarkBufferDirty();
            }
        }
    }
}
