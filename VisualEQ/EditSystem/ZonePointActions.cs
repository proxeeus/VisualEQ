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
            var zp = ZonePointActionHelpers.Find(controller, ZonePointId);
            if (zp == null) return;

            zp.MarkMoved(scenePos);
            var edit = ZonePointActionHelpers.EnsureBufferEntry(controller, zp);
            if (edit == null) return;
            edit.CurrentX       = zp.Row.X;
            edit.CurrentY       = zp.Row.Y;
            edit.CurrentZ       = zp.Row.Z;
            edit.LastModifiedAt = DateTime.UtcNow;
            ZonePointActionHelpers.MaybeDropIfCleanNow(controller.PendingBuffer, edit, zp);
            controller.MarkBufferDirty();
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
            var zp = ZonePointActionHelpers.Find(controller, ZonePointId);
            if (zp == null) return;

            zp.MarkResized(zrange, maxZDiff);
            var edit = ZonePointActionHelpers.EnsureBufferEntry(controller, zp);
            if (edit == null) return;
            edit.CurrentZrange   = zp.Row.Zrange;
            edit.CurrentMaxZDiff = zp.Row.MaxZDiff;
            edit.LastModifiedAt  = DateTime.UtcNow;
            ZonePointActionHelpers.MaybeDropIfCleanNow(controller.PendingBuffer, edit, zp);
            controller.MarkBufferDirty();
        }
    }

    // Inspector field-edit action. One instance per field-commit boundary (fires when an
    // ImGui field transitions from active to inactive after a value change — same trigger
    // the heading slider on SpawnRotateAction uses). Serialisable state is enough to
    // Apply/Revert without keeping delegates alive across zone reloads.
    public sealed class ZonePointFieldEditAction : IEditAction
    {
        public enum Field
        {
            Heading,
            TargetZone,
            TargetX,
            TargetY,
            TargetZ,
            UseNewZoning,
            MinVert,
            MaxVert,
            CenterPoint,
            KeepX,
            KeepY,
            KeepZ,
        }

        public int ZonePointId { get; }
        public Field Which { get; }
        public object FromValue { get; }
        public object ToValue { get; }
        public string DisplayName { get; }
        public DateTime Timestamp { get; }

        public string Description => $"Edited {Which} on zone_point #{ZonePointId} ({DisplayName})";
        public string TargetKey   => $"zonepoint:{ZonePointId}:{Which}";

        public ZonePointFieldEditAction(ZonePoint zp, Field which, object fromValue, object toValue)
        {
            ZonePointId = zp.Row.Id;
            Which       = which;
            FromValue   = fromValue;
            ToValue     = toValue;
            DisplayName = zp.Row.TargetZone ?? "?";
            Timestamp   = DateTime.UtcNow;
        }

        public void Apply(Controller controller)  => Apply(controller, ToValue);
        public void Revert(Controller controller) => Apply(controller, FromValue);

        void Apply(Controller controller, object value)
        {
            var zp = ZonePointActionHelpers.Find(controller, ZonePointId);
            if (zp == null) return;

            ApplyToZonePoint(zp, value);
            var edit = ZonePointActionHelpers.EnsureBufferEntry(controller, zp);
            if (edit == null) return;
            ApplyToBufferEntry(edit, value);
            edit.LastModifiedAt = DateTime.UtcNow;
            ZonePointActionHelpers.MaybeDropIfCleanNow(controller.PendingBuffer, edit, zp);
            controller.MarkBufferDirty();
        }

        void ApplyToZonePoint(ZonePoint zp, object v)
        {
            switch (Which)
            {
                case Field.Heading:      zp.SetHeading(Convert.ToSingle(v)); break;
                case Field.TargetZone:   zp.SetTargetZone((string)v); break;
                case Field.TargetX:      zp.SetTargetX(Convert.ToSingle(v)); break;
                case Field.TargetY:      zp.SetTargetY(Convert.ToSingle(v)); break;
                case Field.TargetZ:      zp.SetTargetZ(Convert.ToSingle(v)); break;
                case Field.UseNewZoning: zp.SetUseNewZoning(Convert.ToByte(v)); break;
                case Field.MinVert:      zp.SetMinVert(Convert.ToSingle(v)); break;
                case Field.MaxVert:      zp.SetMaxVert(Convert.ToSingle(v)); break;
                case Field.CenterPoint:  zp.SetCenterPoint(Convert.ToSingle(v)); break;
                case Field.KeepX:        zp.SetKeepX(Convert.ToInt32(v)); break;
                case Field.KeepY:        zp.SetKeepY(Convert.ToInt32(v)); break;
                case Field.KeepZ:        zp.SetKeepZ(Convert.ToInt32(v)); break;
            }
        }

        void ApplyToBufferEntry(ZonePointEdit edit, object v)
        {
            switch (Which)
            {
                case Field.Heading:      edit.CurrentHeading      = Convert.ToSingle(v); break;
                case Field.TargetZone:   edit.CurrentTargetZone   = (string)v; break;
                case Field.TargetX:      edit.CurrentTargetX      = Convert.ToSingle(v); break;
                case Field.TargetY:      edit.CurrentTargetY      = Convert.ToSingle(v); break;
                case Field.TargetZ:      edit.CurrentTargetZ      = Convert.ToSingle(v); break;
                case Field.UseNewZoning: edit.CurrentUseNewZoning = Convert.ToByte(v); break;
                case Field.MinVert:      edit.CurrentMinVert      = Convert.ToSingle(v); break;
                case Field.MaxVert:      edit.CurrentMaxVert      = Convert.ToSingle(v); break;
                case Field.CenterPoint:  edit.CurrentCenterPoint  = Convert.ToSingle(v); break;
                case Field.KeepX:        edit.CurrentKeepX        = Convert.ToInt32(v); break;
                case Field.KeepY:        edit.CurrentKeepY        = Convert.ToInt32(v); break;
                case Field.KeepZ:        edit.CurrentKeepZ        = Convert.ToInt32(v); break;
            }
        }
    }

    // Shared helpers used by every ZonePoint*Action to keep buffer-entry seeding and the
    // "back to baseline → drop" check in one place. Any new mutator only touches its own
    // Current* fields.
    internal static class ZonePointActionHelpers
    {
        public static ZonePoint Find(Controller controller, int id) =>
            controller.ZonePointManager.ZonePoints.FirstOrDefault(z => z.Row.Id == id);

        // Ensures a ZonePointEdit exists for zp and its Original* fields reflect the true
        // baseline. First-edit path seeds every Original* from the ZonePoint (which cached
        // them at load). Returns null when there's no active buffer.
        public static ZonePointEdit EnsureBufferEntry(Controller controller, ZonePoint zp)
        {
            var buffer = controller.PendingBuffer;
            if (buffer == null) return null;

            if (!buffer.ZonePoints.TryGetValue(zp.Row.Id, out var edit))
            {
                edit = SeedFullEdit(zp);
                buffer.ZonePoints[zp.Row.Id] = edit;
            }
            return edit;
        }

        // Builds a ZonePointEdit whose Original* and Current* fields both mirror the
        // baseline captured on the ZonePoint at load. Callers then overwrite Current*
        // for the field they're actually editing.
        public static ZonePointEdit SeedFullEdit(ZonePoint zp)
        {
            return new ZonePointEdit
            {
                Id                    = zp.Row.Id,
                DisplayName           = zp.Row.TargetZone,
                ScalarOriginalsSeeded = true,

                OriginalX             = zp.OriginalX,
                OriginalY             = zp.OriginalY,
                OriginalZ             = zp.OriginalZ,
                OriginalZrange        = zp.OriginalZrange,
                OriginalMaxZDiff      = zp.OriginalMaxZDiff,
                OriginalHeading       = zp.OriginalHeading,
                OriginalTargetZone    = zp.OriginalTargetZone,
                OriginalTargetX       = zp.OriginalTargetX,
                OriginalTargetY       = zp.OriginalTargetY,
                OriginalTargetZ       = zp.OriginalTargetZ,
                OriginalUseNewZoning  = zp.OriginalUseNewZoning,
                OriginalMinVert       = zp.OriginalMinVert,
                OriginalMaxVert       = zp.OriginalMaxVert,
                OriginalCenterPoint   = zp.OriginalCenterPoint,
                OriginalKeepX         = zp.OriginalKeepX,
                OriginalKeepY         = zp.OriginalKeepY,
                OriginalKeepZ         = zp.OriginalKeepZ,

                CurrentX              = zp.Row.X,
                CurrentY              = zp.Row.Y,
                CurrentZ              = zp.Row.Z,
                CurrentZrange         = zp.Row.Zrange,
                CurrentMaxZDiff       = zp.Row.MaxZDiff,
                CurrentHeading        = zp.Row.Heading,
                CurrentTargetZone     = zp.Row.TargetZone,
                CurrentTargetX        = zp.Row.TargetX,
                CurrentTargetY        = zp.Row.TargetY,
                CurrentTargetZ        = zp.Row.TargetZ,
                CurrentUseNewZoning   = zp.Row.UseNewZoning,
                CurrentMinVert        = zp.Row.MinVert,
                CurrentMaxVert        = zp.Row.MaxVert,
                CurrentCenterPoint    = zp.Row.CenterPoint,
                CurrentKeepX          = zp.Row.KeepX,
                CurrentKeepY          = zp.Row.KeepY,
                CurrentKeepZ          = zp.Row.KeepZ,
            };
        }

        // If the row has walked back to its original state on every editable field, drop
        // the buffer entry so commit doesn't emit a no-op UPDATE and the sidebar's pending
        // count decrements.
        public static void MaybeDropIfCleanNow(EditBuffer buffer, ZonePointEdit edit, ZonePoint zp)
        {
            const float eps = 0.001f;

            bool floatEq(float a, float b) => System.MathF.Abs(a - b) < eps;
            bool stringEq(string a, string b) => string.Equals(a ?? "", b ?? "", StringComparison.Ordinal);

            if (!floatEq(edit.CurrentX,           edit.OriginalX))           return;
            if (!floatEq(edit.CurrentY,           edit.OriginalY))           return;
            if (!floatEq(edit.CurrentZ,           edit.OriginalZ))           return;
            if (edit.CurrentZrange              != edit.OriginalZrange)      return;
            if (edit.CurrentMaxZDiff            != edit.OriginalMaxZDiff)    return;
            if (!floatEq(edit.CurrentHeading,     edit.OriginalHeading))     return;
            if (!stringEq(edit.CurrentTargetZone, edit.OriginalTargetZone))  return;
            if (!floatEq(edit.CurrentTargetX,     edit.OriginalTargetX))     return;
            if (!floatEq(edit.CurrentTargetY,     edit.OriginalTargetY))     return;
            if (!floatEq(edit.CurrentTargetZ,     edit.OriginalTargetZ))     return;
            if (edit.CurrentUseNewZoning        != edit.OriginalUseNewZoning) return;
            if (!floatEq(edit.CurrentMinVert,     edit.OriginalMinVert))     return;
            if (!floatEq(edit.CurrentMaxVert,     edit.OriginalMaxVert))     return;
            if (!floatEq(edit.CurrentCenterPoint, edit.OriginalCenterPoint)) return;
            if (edit.CurrentKeepX               != edit.OriginalKeepX)       return;
            if (edit.CurrentKeepY               != edit.OriginalKeepY)       return;
            if (edit.CurrentKeepZ               != edit.OriginalKeepZ)       return;

            buffer.ZonePoints.Remove(zp.Row.Id);
            zp.Revert();
        }
    }
}
