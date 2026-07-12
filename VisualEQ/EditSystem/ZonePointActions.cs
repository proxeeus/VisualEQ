using System;
using System.Linq;
using System.Numerics;
using VisualEQ.Database.Models;
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
            ZonePointActionHelpers.SyncBufferForCurrentState(controller, zp);
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
            ZonePointActionHelpers.SyncBufferForCurrentState(controller, zp);
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
            ZonePointActionHelpers.SyncBufferForCurrentState(controller, zp);
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

        // Old ApplyToBufferEntry removed — SyncBufferForCurrentState now handles the
        // buffer mirror in one place, dispatching on IsPendingInsert.
    }

    // Creates a brand-new trilogy_zone_points row. Apply installs a ZonePoint in the
    // manager (with a negative temp id) and a ZonePointInsert in the buffer; Revert
    // removes both. Undo-safe and idempotent — re-Apply is a no-op when the row is
    // already present.
    public sealed class ZonePointInsertAction : IEditAction
    {
        public int TempId { get; }
        public TrilogyZonePoint InitialRow { get; }
        public DateTime Timestamp { get; }

        public string Description => $"Created zone_point (→ {InitialRow.TargetZone ?? "?"})";
        public string TargetKey   => $"zonepoint:{TempId}";

        public ZonePointInsertAction(TrilogyZonePoint initialRow)
        {
            InitialRow = initialRow;
            TempId     = initialRow.Id;
            Timestamp  = DateTime.UtcNow;
        }

        public void Apply(Controller controller)
        {
            if (controller.ZonePointManager.ZonePoints.Any(z => z.Row.Id == TempId)) return;

            // Clone the initial row so undo/redo can restore original state even if the
            // scene mutates the ZonePoint's Row after creation.
            var row = CloneRow(InitialRow);
            row.Id = TempId;
            var zp = new ZonePoint(row);
            controller.ZonePointManager.Add(zp);

            var buffer = controller.PendingBuffer;
            if (buffer != null)
            {
                buffer.ZonePointInserts[TempId] = ToInsert(row);
                controller.MarkBufferDirty();
            }
        }

        public void Revert(Controller controller)
        {
            controller.ZonePointManager.Remove(TempId);
            var buffer = controller.PendingBuffer;
            if (buffer != null)
            {
                buffer.ZonePointInserts.Remove(TempId);
                controller.MarkBufferDirty();
            }
        }

        static TrilogyZonePoint CloneRow(TrilogyZonePoint src) => new TrilogyZonePoint
        {
            Id           = src.Id,
            Zone         = src.Zone,
            X            = src.X,
            Y            = src.Y,
            Z            = src.Z,
            Heading      = src.Heading,
            TargetZone   = src.TargetZone,
            TargetX      = src.TargetX,
            TargetY      = src.TargetY,
            TargetZ      = src.TargetZ,
            Zrange       = src.Zrange,
            MaxZDiff     = src.MaxZDiff,
            UseNewZoning = src.UseNewZoning,
            MinVert      = src.MinVert,
            MaxVert      = src.MaxVert,
            CenterPoint  = src.CenterPoint,
            KeepX        = src.KeepX,
            KeepY        = src.KeepY,
            KeepZ        = src.KeepZ,
            ToZoneId     = src.ToZoneId,
        };

        static ZonePointInsert ToInsert(TrilogyZonePoint row) => new ZonePointInsert
        {
            TempId       = row.Id,
            Zone         = row.Zone,
            X            = row.X,
            Y            = row.Y,
            Z            = row.Z,
            Heading      = row.Heading,
            TargetZone   = row.TargetZone,
            TargetX      = row.TargetX,
            TargetY      = row.TargetY,
            TargetZ      = row.TargetZ,
            Zrange       = row.Zrange,
            MaxZDiff     = row.MaxZDiff,
            UseNewZoning = row.UseNewZoning,
            MinVert      = row.MinVert,
            MaxVert      = row.MaxVert,
            CenterPoint  = row.CenterPoint,
            KeepX        = row.KeepX,
            KeepY        = row.KeepY,
            KeepZ        = row.KeepZ,
            CreatedAt    = DateTime.UtcNow,
        };
    }

    // Removes a zone_point. Handles both persisted rows (adds to buffer.ZonePointDeletes
    // for eventual DB DELETE) and pending-insert rows (just drops the ZonePointInsert
    // entry — no DB touch needed). Revert restores the ZonePoint from a captured snapshot
    // so field values survive undo intact.
    public sealed class ZonePointDeleteAction : IEditAction
    {
        public int ZonePointId { get; }
        public bool WasPendingInsert { get; }
        public TrilogyZonePoint Snapshot { get; }
        public string DisplayName { get; }
        public DateTime Timestamp { get; }

        public string Description => $"Deleted zone_point #{ZonePointId} (→ {DisplayName})";
        public string TargetKey   => $"zonepoint:{ZonePointId}";

        public ZonePointDeleteAction(ZonePoint zp)
        {
            ZonePointId      = zp.Row.Id;
            WasPendingInsert = zp.IsPendingInsert;
            Snapshot         = ZonePointInsertAction_CloneRow(zp.Row);
            DisplayName      = zp.Row.TargetZone ?? "?";
            Timestamp        = DateTime.UtcNow;
        }

        public void Apply(Controller controller)
        {
            controller.ZonePointManager.Remove(ZonePointId);
            var buffer = controller.PendingBuffer;
            if (buffer == null) return;

            if (WasPendingInsert)
            {
                buffer.ZonePointInserts.Remove(ZonePointId);
            }
            else
            {
                buffer.ZonePointDeletes.Add(ZonePointId);
                // Drop any prior field-edit entry for this id — the row is going away.
                buffer.ZonePoints.Remove(ZonePointId);
            }
            controller.MarkBufferDirty();
        }

        public void Revert(Controller controller)
        {
            if (controller.ZonePointManager.ZonePoints.Any(z => z.Row.Id == ZonePointId)) return;

            var row = ZonePointInsertAction_CloneRow(Snapshot);
            var zp = new ZonePoint(row);
            controller.ZonePointManager.Add(zp);

            var buffer = controller.PendingBuffer;
            if (buffer == null) return;

            if (WasPendingInsert)
            {
                buffer.ZonePointInserts[ZonePointId] = new ZonePointInsert
                {
                    TempId       = row.Id,
                    Zone         = row.Zone,
                    X            = row.X, Y = row.Y, Z = row.Z, Heading = row.Heading,
                    TargetZone   = row.TargetZone,
                    TargetX      = row.TargetX, TargetY = row.TargetY, TargetZ = row.TargetZ,
                    Zrange       = row.Zrange, MaxZDiff = row.MaxZDiff,
                    UseNewZoning = row.UseNewZoning,
                    MinVert      = row.MinVert, MaxVert = row.MaxVert, CenterPoint = row.CenterPoint,
                    KeepX        = row.KeepX, KeepY = row.KeepY, KeepZ = row.KeepZ,
                    CreatedAt    = DateTime.UtcNow,
                };
            }
            else
            {
                buffer.ZonePointDeletes.Remove(ZonePointId);
            }
            controller.MarkBufferDirty();
        }

        // Duplicate of ZonePointInsertAction.CloneRow — kept file-local to avoid making it
        // public; a follow-up refactor can extract if a third callsite appears.
        static TrilogyZonePoint ZonePointInsertAction_CloneRow(TrilogyZonePoint src) => new TrilogyZonePoint
        {
            Id           = src.Id,
            Zone         = src.Zone,
            X            = src.X, Y = src.Y, Z = src.Z, Heading = src.Heading,
            TargetZone   = src.TargetZone,
            TargetX      = src.TargetX, TargetY = src.TargetY, TargetZ = src.TargetZ,
            Zrange       = src.Zrange, MaxZDiff = src.MaxZDiff,
            UseNewZoning = src.UseNewZoning,
            MinVert      = src.MinVert, MaxVert = src.MaxVert, CenterPoint = src.CenterPoint,
            KeepX        = src.KeepX, KeepY = src.KeepY, KeepZ = src.KeepZ,
            ToZoneId     = src.ToZoneId,
        };
    }

    // Shared helpers used by every ZonePoint*Action to keep buffer-entry seeding and the
    // "back to baseline → drop" check in one place. Any new mutator just calls
    // SyncBufferForCurrentState after mutating zp.Row — the helper picks the right buffer
    // slot (Inserts vs Points) and mirrors every field.
    internal static class ZonePointActionHelpers
    {
        public static ZonePoint Find(Controller controller, int id) =>
            controller.ZonePointManager.ZonePoints.FirstOrDefault(z => z.Row.Id == id);

        // Post-mutation buffer sync. For persisted rows: ensures a ZonePointEdit exists,
        // mirrors every Current* from zp.Row, runs MaybeDropIfCleanNow. For pending-insert
        // rows: mirrors every field into the ZonePointInsert entry so the eventual INSERT
        // reflects the latest inspector edits (was a silent-data-loss bug — the UPDATE
        // path uses the temp id which matches no persisted row).
        //
        // Always calls MarkBufferDirty so the sidebar's pending count and the on-disk
        // flush stay in sync.
        public static void SyncBufferForCurrentState(Controller controller, ZonePoint zp)
        {
            var buffer = controller.PendingBuffer;
            if (buffer == null) return;

            if (zp.IsPendingInsert)
            {
                if (buffer.ZonePointInserts.TryGetValue(zp.Row.Id, out var insert))
                    MirrorRowIntoInsert(insert, zp.Row);
                controller.MarkBufferDirty();
                return;
            }

            if (!buffer.ZonePoints.TryGetValue(zp.Row.Id, out var edit))
            {
                edit = SeedFullEdit(zp);
                buffer.ZonePoints[zp.Row.Id] = edit;
            }
            MirrorRowIntoEdit(edit, zp.Row);
            edit.LastModifiedAt = DateTime.UtcNow;
            MaybeDropIfCleanNow(buffer, edit, zp);
            controller.MarkBufferDirty();
        }

        static void MirrorRowIntoInsert(ZonePointInsert insert, VisualEQ.Database.Models.TrilogyZonePoint row)
        {
            insert.X            = row.X;
            insert.Y            = row.Y;
            insert.Z            = row.Z;
            insert.Heading      = row.Heading;
            insert.TargetZone   = row.TargetZone;
            insert.TargetX      = row.TargetX;
            insert.TargetY      = row.TargetY;
            insert.TargetZ      = row.TargetZ;
            insert.Zrange       = row.Zrange;
            insert.MaxZDiff     = row.MaxZDiff;
            insert.UseNewZoning = row.UseNewZoning;
            insert.MinVert      = row.MinVert;
            insert.MaxVert      = row.MaxVert;
            insert.CenterPoint  = row.CenterPoint;
            insert.KeepX        = row.KeepX;
            insert.KeepY        = row.KeepY;
            insert.KeepZ        = row.KeepZ;
        }

        static void MirrorRowIntoEdit(ZonePointEdit edit, VisualEQ.Database.Models.TrilogyZonePoint row)
        {
            edit.CurrentX            = row.X;
            edit.CurrentY            = row.Y;
            edit.CurrentZ            = row.Z;
            edit.CurrentZrange       = row.Zrange;
            edit.CurrentMaxZDiff     = row.MaxZDiff;
            edit.CurrentHeading      = row.Heading;
            edit.CurrentTargetZone   = row.TargetZone;
            edit.CurrentTargetX      = row.TargetX;
            edit.CurrentTargetY      = row.TargetY;
            edit.CurrentTargetZ      = row.TargetZ;
            edit.CurrentUseNewZoning = row.UseNewZoning;
            edit.CurrentMinVert      = row.MinVert;
            edit.CurrentMaxVert      = row.MaxVert;
            edit.CurrentCenterPoint  = row.CenterPoint;
            edit.CurrentKeepX        = row.KeepX;
            edit.CurrentKeepY        = row.KeepY;
            edit.CurrentKeepZ        = row.KeepZ;
        }

        // Kept as an obsolete shim so anywhere still calling this doesn't break — new code
        // should use SyncBufferForCurrentState.
        public static ZonePointEdit EnsureBufferEntry(Controller controller, ZonePoint zp)
        {
            var buffer = controller.PendingBuffer;
            if (buffer == null) return null;
            if (zp.IsPendingInsert) return null;  // pending inserts use ZonePointInsert, not ZonePointEdit
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
