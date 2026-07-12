using System;
using System.Linq;
using System.Numerics;
using VisualEQ.ZonePointSystem;

namespace VisualEQ.EditSystem
{
    // Center-drag → position edit. Position is stored in scene space by the action but
    // written to the buffer/DB in DB space (X/Y swap at the save boundary, matching
    // SpawnMoveAction's convention).
    public sealed class ZonePointMoveAction : IEditAction
    {
        public int ZonePointId { get; }
        public Vector3 FromScenePos { get; }
        public Vector3 ToScenePos { get; }
        public string DisplayName { get; }
        public DateTime Timestamp { get; }

        public string Description => $"Moved zone_point #{ZonePointId} ({DisplayName})";
        public string TargetKey   => $"zonepoint:{ZonePointId}";

        public ZonePointMoveAction(ZonePoint zp, Vector3 fromScenePos, Vector3 toScenePos)
        {
            ZonePointId  = zp.Row.Id;
            FromScenePos = fromScenePos;
            ToScenePos   = toScenePos;
            DisplayName  = zp.Row.TargetZone ?? "?";
            Timestamp    = DateTime.UtcNow;
        }

        public void Apply(Controller controller)  => Move(controller, ToScenePos);
        public void Revert(Controller controller) => Move(controller, FromScenePos);

        void Move(Controller controller, Vector3 scenePos)
        {
            var zp = Find(controller);
            if (zp == null) return;

            zp.MarkMoved(scenePos);
            UpdateBufferEntry(controller, zp);
            controller.MarkBufferDirty();
        }

        ZonePoint Find(Controller controller) =>
            controller.ZonePointManager.ZonePoints.FirstOrDefault(z => z.Row.Id == ZonePointId);

        static void UpdateBufferEntry(Controller controller, ZonePoint zp)
        {
            var buffer = controller.PendingBuffer;
            if (buffer == null) return;

            if (!buffer.ZonePoints.TryGetValue(zp.Row.Id, out var edit))
            {
                edit = new ZonePointEdit
                {
                    Id               = zp.Row.Id,
                    OriginalX        = zp.OriginalX,
                    OriginalY        = zp.OriginalY,
                    OriginalZ        = zp.OriginalZ,
                    OriginalZrange   = zp.OriginalZrange,
                    OriginalMaxZDiff = zp.OriginalMaxZDiff,
                    CurrentZrange    = zp.Row.Zrange,
                    CurrentMaxZDiff  = zp.Row.MaxZDiff,
                    DisplayName      = zp.Row.TargetZone,
                };
                buffer.ZonePoints[zp.Row.Id] = edit;
            }

            edit.CurrentX        = zp.Row.X;
            edit.CurrentY        = zp.Row.Y;
            edit.CurrentZ        = zp.Row.Z;
            edit.LastModifiedAt  = DateTime.UtcNow;

            MaybeDropIfCleanNow(buffer, edit, zp);
        }

        // If undo/edit walks the row back to its baseline, drop the buffer entry so commit
        // doesn't emit a no-op UPDATE and the sidebar's pending count drops accordingly.
        internal static void MaybeDropIfCleanNow(EditBuffer buffer, ZonePointEdit edit, ZonePoint zp)
        {
            const float eps = 0.001f;
            if (System.MathF.Abs(edit.CurrentX - edit.OriginalX) < eps &&
                System.MathF.Abs(edit.CurrentY - edit.OriginalY) < eps &&
                System.MathF.Abs(edit.CurrentZ - edit.OriginalZ) < eps &&
                edit.CurrentZrange   == edit.OriginalZrange &&
                edit.CurrentMaxZDiff == edit.OriginalMaxZDiff)
            {
                buffer.ZonePoints.Remove(zp.Row.Id);
                zp.Revert();
            }
        }
    }

    // Corner-drag → Zrange, Z-face drag → MaxZDiff. One action covers both because the
    // editor already batches them per drag (only one changes per drag) and coalescing
    // them means fewer undo entries for the user.
    public sealed class ZonePointResizeAction : IEditAction
    {
        public int ZonePointId { get; }
        public int FromZrange { get; }
        public int ToZrange { get; }
        public int FromMaxZDiff { get; }
        public int ToMaxZDiff { get; }
        public string DisplayName { get; }
        public DateTime Timestamp { get; }

        public string Description => $"Resized zone_point #{ZonePointId} ({DisplayName})";
        public string TargetKey   => $"zonepoint:{ZonePointId}";

        public ZonePointResizeAction(ZonePoint zp, int fromZrange, int toZrange, int fromMaxZDiff, int toMaxZDiff)
        {
            ZonePointId  = zp.Row.Id;
            FromZrange   = fromZrange;
            ToZrange     = toZrange;
            FromMaxZDiff = fromMaxZDiff;
            ToMaxZDiff   = toMaxZDiff;
            DisplayName  = zp.Row.TargetZone ?? "?";
            Timestamp    = DateTime.UtcNow;
        }

        public void Apply(Controller controller)  => Apply(controller, ToZrange,   ToMaxZDiff);
        public void Revert(Controller controller) => Apply(controller, FromZrange, FromMaxZDiff);

        void Apply(Controller controller, int zrange, int maxZDiff)
        {
            var zp = controller.ZonePointManager.ZonePoints.FirstOrDefault(z => z.Row.Id == ZonePointId);
            if (zp == null) return;

            zp.MarkResized(zrange, maxZDiff);
            UpdateBufferEntry(controller, zp);
            controller.MarkBufferDirty();
        }

        static void UpdateBufferEntry(Controller controller, ZonePoint zp)
        {
            var buffer = controller.PendingBuffer;
            if (buffer == null) return;

            if (!buffer.ZonePoints.TryGetValue(zp.Row.Id, out var edit))
            {
                edit = new ZonePointEdit
                {
                    Id               = zp.Row.Id,
                    OriginalX        = zp.OriginalX,
                    OriginalY        = zp.OriginalY,
                    OriginalZ        = zp.OriginalZ,
                    OriginalZrange   = zp.OriginalZrange,
                    OriginalMaxZDiff = zp.OriginalMaxZDiff,
                    CurrentX         = zp.Row.X,
                    CurrentY         = zp.Row.Y,
                    CurrentZ         = zp.Row.Z,
                    DisplayName      = zp.Row.TargetZone,
                };
                buffer.ZonePoints[zp.Row.Id] = edit;
            }

            edit.CurrentZrange   = zp.Row.Zrange;
            edit.CurrentMaxZDiff = zp.Row.MaxZDiff;
            edit.LastModifiedAt  = DateTime.UtcNow;

            ZonePointMoveAction.MaybeDropIfCleanNow(buffer, edit, zp);
        }
    }
}
