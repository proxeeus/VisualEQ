using System;
using System.Linq;
using System.Numerics;
using VisualEQ.SpawnSystem;

namespace VisualEQ.EditSystem
{
    // One undoable, redoable change. Actions live on the undo stack; Apply reproduces the
    // change (buffer + scene state), Revert undoes it. Both must be idempotent given a
    // valid controller state.
    //
    // Actions do NOT own their target references — they hold IDs and look up the current
    // SpawnPoint / GridEntry at Apply/Revert time. Zone re-loads therefore invalidate
    // outstanding actions gracefully (lookup returns null → no-op).
    public interface IEditAction
    {
        // Human-readable label used by the Pending Changes list ("Moved 'a_guard' (#4501)").
        string Description { get; }

        // Composite ID of the affected item — "spawn:<id>" or "grid:<gridId>:<number>" — used
        // for coalescing (per-item revert) and undo-stack UI.
        string TargetKey { get; }

        DateTime Timestamp { get; }

        void Apply(Controller controller);
        void Revert(Controller controller);
    }

    // Records a single heading change on a spawn. Kept separate from SpawnMoveAction so
    // a user can undo rotation independently of drag moves.
    public sealed class SpawnRotateAction : IEditAction
    {
        public int SpawnId { get; }
        public float FromHeading { get; }
        public float ToHeading { get; }
        public string DisplayName { get; }
        public DateTime Timestamp { get; }

        public string Description => $"Rotated '{DisplayName}' (#{SpawnId})";
        public string TargetKey   => $"spawn:{SpawnId}";

        public SpawnRotateAction(SpawnPoint sp, float fromHeading, float toHeading)
        {
            SpawnId     = sp.Record.Spawn.Id;
            FromHeading = fromHeading;
            ToHeading   = toHeading;
            DisplayName = PrimaryNameOf(sp);
            Timestamp   = DateTime.UtcNow;
        }

        public void Apply(Controller controller)  => Rotate(controller, ToHeading);
        public void Revert(Controller controller) => Rotate(controller, FromHeading);

        void Rotate(Controller controller, float heading)
        {
            var sp = controller.SpawnManager.SpawnPoints.FirstOrDefault(p => p.Record.Spawn.Id == SpawnId);
            if (sp == null) return;

            sp.MarkMoved(sp.Model.Position, heading);
            UpdateBufferEntry(controller, sp, heading);
            controller.MarkBufferDirty();
        }

        static void UpdateBufferEntry(Controller controller, SpawnPoint sp, float heading)
        {
            var buffer = controller.PendingBuffer;
            if (buffer == null) return;

            if (!buffer.Spawns.TryGetValue(sp.Record.Spawn.Id, out var edit))
            {
                edit = new SpawnEdit
                {
                    SpawnId         = sp.Record.Spawn.Id,
                    OriginalX       = sp.Record.Spawn.X,
                    OriginalY       = sp.Record.Spawn.Y,
                    OriginalZ       = sp.Record.Spawn.Z,
                    OriginalHeading = sp.OriginalHeading,
                    // Position hasn't been touched — Current* = Original* for x/y/z.
                    CurrentX        = sp.Record.Spawn.X,
                    CurrentY        = sp.Record.Spawn.Y,
                    CurrentZ        = sp.Record.Spawn.Z,
                    DisplayName     = PrimaryNameOf(sp),
                };
                buffer.Spawns[sp.Record.Spawn.Id] = edit;
            }

            edit.CurrentHeading = heading;
            edit.LastModifiedAt = DateTime.UtcNow;

            // Back to original in every field → drop entry so commit doesn't emit a no-op.
            const float eps = 0.001f;
            if (Math.Abs(edit.CurrentX - edit.OriginalX) < eps &&
                Math.Abs(edit.CurrentY - edit.OriginalY) < eps &&
                Math.Abs(edit.CurrentZ - edit.OriginalZ) < eps &&
                Math.Abs(edit.CurrentHeading - edit.OriginalHeading) < eps)
            {
                buffer.Spawns.Remove(sp.Record.Spawn.Id);
                sp.Revert();
            }
        }

        static string PrimaryNameOf(SpawnPoint sp) =>
            sp.Record.Entries
                .OrderByDescending(e => e.Entry.Chance)
                .FirstOrDefault()?.Npc?.Name ?? "?";
    }

    // Records a single position change on a spawn. Rotation is handled separately by
    // SpawnRotateAction so a user can undo one dimension without the other.
    public sealed class SpawnMoveAction : IEditAction
    {
        public int SpawnId { get; }
        public Vector3 FromScenePos { get; }   // Scene-space (X = DB Y, Y = DB X, Z = DB Z).
        public Vector3 ToScenePos { get; }
        public float FromHeading { get; }      // Preserved verbatim — position-only edits leave heading untouched.
        public float ToHeading { get; }
        public string DisplayName { get; }
        public DateTime Timestamp { get; }

        public string Description => $"Moved '{DisplayName}' (#{SpawnId})";
        public string TargetKey   => $"spawn:{SpawnId}";

        public SpawnMoveAction(SpawnPoint sp, Vector3 fromScenePos, Vector3 toScenePos)
        {
            SpawnId       = sp.Record.Spawn.Id;
            FromScenePos  = fromScenePos;
            ToScenePos    = toScenePos;
            FromHeading   = sp.CurrentHeading;
            ToHeading     = sp.CurrentHeading;
            DisplayName   = PrimaryName(sp);
            Timestamp     = DateTime.UtcNow;
        }

        public void Apply(Controller controller)  => Move(controller, ToScenePos,   ToHeading);
        public void Revert(Controller controller) => Move(controller, FromScenePos, FromHeading);

        void Move(Controller controller, Vector3 scenePos, float heading)
        {
            var sp = FindSpawn(controller);
            if (sp == null) return;

            sp.MarkMoved(scenePos, heading);
            UpdateBufferEntry(controller, sp, scenePos, heading);
            controller.MarkBufferDirty();
        }

        SpawnPoint FindSpawn(Controller controller) =>
            controller.SpawnManager.SpawnPoints.FirstOrDefault(p => p.Record.Spawn.Id == SpawnId);

        // Insert-or-update the buffer entry for this spawn. Original values come from the
        // SpawnPoint (which cached them at load); Current values are DB-space un-swapped
        // from scene coords.
        static void UpdateBufferEntry(Controller controller, SpawnPoint sp, Vector3 scenePos, float heading)
        {
            var buffer = controller.PendingBuffer;
            if (buffer == null) return;

            if (!buffer.Spawns.TryGetValue(sp.Record.Spawn.Id, out var edit))
            {
                edit = new SpawnEdit
                {
                    SpawnId         = sp.Record.Spawn.Id,
                    OriginalX       = sp.Record.Spawn.X,
                    OriginalY       = sp.Record.Spawn.Y,
                    OriginalZ       = sp.Record.Spawn.Z,
                    OriginalHeading = sp.OriginalHeading,
                    DisplayName     = PrimaryName(sp),
                };
                buffer.Spawns[sp.Record.Spawn.Id] = edit;
            }

            // Scene → DB coord swap: DB_X = scene.Y, DB_Y = scene.X, DB_Z = scene.Z.
            edit.CurrentX        = scenePos.Y;
            edit.CurrentY        = scenePos.X;
            edit.CurrentZ        = scenePos.Z;
            edit.CurrentHeading  = heading;
            edit.LastModifiedAt  = DateTime.UtcNow;

            // If undo has walked us back to the original DB state, drop the buffer entry
            // entirely so a commit doesn't emit a no-op UPDATE and the sidebar's pending
            // list doesn't linger on unchanged spawns.
            const float eps = 0.001f;
            if (Math.Abs(edit.CurrentX       - edit.OriginalX)       < eps &&
                Math.Abs(edit.CurrentY       - edit.OriginalY)       < eps &&
                Math.Abs(edit.CurrentZ       - edit.OriginalZ)       < eps &&
                Math.Abs(edit.CurrentHeading - edit.OriginalHeading) < eps)
            {
                buffer.Spawns.Remove(sp.Record.Spawn.Id);
                sp.Revert(); // Clears IsDirty so the orange marker disappears.
            }
        }

        static string PrimaryName(SpawnPoint sp) =>
            sp.Record.Entries
                .OrderByDescending(e => e.Entry.Chance)
                .FirstOrDefault()?.Npc?.Name ?? "?";
    }

    // Records a single position change to one waypoint (grid entry). Note: multiple spawns
    // can share a grid; Apply/Revert updates every SpawnRecord.Waypoints copy in-scene.
    public sealed class GridWaypointMoveAction : IEditAction
    {
        public int GridId { get; }
        public int Number { get; }
        public Vector3 FromScenePos { get; }
        public Vector3 ToScenePos { get; }
        public DateTime Timestamp { get; }

        public string Description => $"Moved waypoint {Number} of grid {GridId}";
        public string TargetKey   => $"grid:{GridId}:{Number}";

        public GridWaypointMoveAction(int gridId, int number, Vector3 fromScenePos, Vector3 toScenePos)
        {
            GridId       = gridId;
            Number       = number;
            FromScenePos = fromScenePos;
            ToScenePos   = toScenePos;
            Timestamp    = DateTime.UtcNow;
        }

        public void Apply(Controller controller)  => Move(controller, ToScenePos);
        public void Revert(Controller controller) => Move(controller, FromScenePos);

        void Move(Controller controller, Vector3 targetScenePos)
        {
            // Snapshot the pre-mutation waypoint state so, if this is the FIRST edit for
            // (GridId, Number), the buffer entry's Original* reflects the true baseline.
            var snapshot = FindWaypoint(controller);
            if (snapshot == null) return;
            var pre = GridActionHelpers.SnapshotWaypoint(snapshot);

            // scenePos → DB: DB_X = scene.Y, DB_Y = scene.X, DB_Z = scene.Z.
            var newDbX = targetScenePos.Y;
            var newDbY = targetScenePos.X;
            var newDbZ = targetScenePos.Z;

            GridActionHelpers.MutateEveryWaypoint(controller, GridId, Number, wp =>
            {
                wp.X = newDbX;
                wp.Y = newDbY;
                wp.Z = newDbZ;
            });

            var post = FindWaypoint(controller);
            if (post == null) return;
            GridActionHelpers.SyncBufferForWaypoint(controller, GridId, Number, pre, post);
        }

        Database.Models.GridEntry FindWaypoint(Controller controller) =>
            GridActionHelpers.FindWaypoint(controller, GridId, Number);
    }
}
