using System;
using System.Linq;
using VisualEQ.SpawnSystem;

namespace VisualEQ.EditSystem
{
    // Marks a spawn2 row for deletion. Apply pulls the SpawnPoint out of the visible
    // scene (SpawnManager.Hide + engine/CharacterModels detach) and records the id in
    // buffer.SpawnDeletes; Revert reattaches. The SpawnPoint itself lives on inside
    // SpawnManager.HiddenSpawnPoints for the whole zone session so no DB round-trip is
    // needed for revert / discard.
    //
    // Session recovery: on zone reload, spawns are freshly loaded from DB — Controller.
    // ApplyPendingBuffer walks buffer.SpawnDeletes and calls Hide() on each matching
    // just-loaded SpawnPoint, so the scene reflects the buffer.
    public sealed class SpawnDeleteAction : IEditAction
    {
        public int SpawnId { get; }
        public string DisplayName { get; }
        public DateTime Timestamp { get; }

        public string Description => $"Deleted '{DisplayName}' (#{SpawnId})";
        public string TargetKey   => $"spawn:{SpawnId}";

        public SpawnDeleteAction(SpawnPoint sp)
        {
            SpawnId     = sp.Record.Spawn.Id;
            DisplayName = PrimaryName(sp);
            Timestamp   = DateTime.UtcNow;
        }

        public void Apply(Controller controller)
        {
            var sp = controller.SpawnManager.SpawnPoints
                .FirstOrDefault(p => p.Record.Spawn.Id == SpawnId);
            if (sp == null) return; // already hidden — idempotent

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
                // If the spawn had any pending field edits (e.g. a move) drop them —
                // the row is going away, no need to UPDATE what we're about to DELETE.
                buffer.Spawns.Remove(SpawnId);
                controller.MarkBufferDirty();
            }
        }

        public void Revert(Controller controller)
        {
            controller.RestoreSpawnToScene(SpawnId);
            var buffer = controller.PendingBuffer;
            if (buffer != null)
            {
                buffer.SpawnDeletes.Remove(SpawnId);
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
}
