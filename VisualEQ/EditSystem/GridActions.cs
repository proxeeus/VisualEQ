using System;
using System.Collections.Generic;
using System.Linq;
using VisualEQ.Database.Models;

namespace VisualEQ.EditSystem
{
    // Inspector field-edit action for a single scalar on a waypoint (grid_entries row).
    // Kept separate from GridWaypointMoveAction so a user can undo one scalar edit at a
    // time without rewinding a whole position drag.
    //
    // Position fields (X, Y, Z) are supported here too — the inspector uses this action
    // when the user types coordinates directly, while world drags continue to go through
    // GridWaypointMoveAction. The mutation contract is the same (write to every matching
    // SpawnRecord.Waypoints entry) so both paths stay consistent.
    public sealed class GridEntryFieldEditAction : IEditAction
    {
        public enum Field
        {
            X,
            Y,
            Z,
            Heading,
            Pause,
            Centerpoint,
        }

        public int GridId { get; }
        public int Number { get; }
        public Field Which { get; }
        public object FromValue { get; }
        public object ToValue { get; }
        public DateTime Timestamp { get; }

        public string Description => $"Edited {Which} on grid {GridId} waypoint #{Number}";
        public string TargetKey   => $"grid:{GridId}:{Number}";

        public GridEntryFieldEditAction(int gridId, int number, Field which, object fromValue, object toValue)
        {
            GridId    = gridId;
            Number    = number;
            Which     = which;
            FromValue = fromValue;
            ToValue   = toValue;
            Timestamp = DateTime.UtcNow;
        }

        public void Apply(Controller controller)  => ApplyValue(controller, ToValue);
        public void Revert(Controller controller) => ApplyValue(controller, FromValue);

        void ApplyValue(Controller controller, object value)
        {
            var live = GridActionHelpers.FindWaypoint(controller, GridId, Number);
            if (live == null) return;

            // Build the pre-mutation snapshot used to seed a first-touch GridEntryEdit.
            // The inspector's DragFloat write-callback has already mutated the live wp for
            // this field during the drag (that's how the polyline/arrow track the drag in
            // real time). So reading it back here yields the post-mutation value — we'd
            // record OriginalField == CurrentField, the entry would be immediately
            // dropped as "clean", and the buffer would never show a pending change.
            //
            // Override the specific field with FromValue (this action's authoritative
            // baseline). Other fields have not been touched by THIS action; their live
            // values are their pre-mutation values by definition.
            var pre = GridActionHelpers.SnapshotWaypoint(live);
            switch (Which)
            {
                case Field.X:           pre.X           = Convert.ToSingle(FromValue); break;
                case Field.Y:           pre.Y           = Convert.ToSingle(FromValue); break;
                case Field.Z:           pre.Z           = Convert.ToSingle(FromValue); break;
                case Field.Heading:     pre.Heading     = Convert.ToSingle(FromValue); break;
                case Field.Pause:       pre.Pause       = Convert.ToInt32(FromValue); break;
                case Field.Centerpoint: pre.Centerpoint = Convert.ToByte(FromValue); break;
            }

            GridActionHelpers.MutateEveryWaypoint(controller, GridId, Number, wp =>
            {
                switch (Which)
                {
                    case Field.X:           wp.X           = Convert.ToSingle(value); break;
                    case Field.Y:           wp.Y           = Convert.ToSingle(value); break;
                    case Field.Z:           wp.Z           = Convert.ToSingle(value); break;
                    case Field.Heading:     wp.Heading     = Convert.ToSingle(value); break;
                    case Field.Pause:       wp.Pause       = Convert.ToInt32(value); break;
                    case Field.Centerpoint: wp.Centerpoint = Convert.ToByte(value); break;
                }
            });

            var post = GridActionHelpers.FindWaypoint(controller, GridId, Number);
            if (post == null) return;
            GridActionHelpers.SyncBufferForWaypoint(controller, GridId, Number, pre, post);
        }
    }

    // Inspector edit for grid.type / grid.type2. Grid metadata isn't tied to a specific
    // waypoint, so the target key uses (gridId, zoneId).
    public sealed class GridFieldEditAction : IEditAction
    {
        public enum Field
        {
            Type,
            Type2,
        }

        public int GridId { get; }
        public int ZoneId { get; }
        public Field Which { get; }
        public int FromValue { get; }
        public int ToValue { get; }
        public DateTime Timestamp { get; }

        public string Description => $"Edited grid {GridId} {Which}";
        public string TargetKey   => $"grid:{GridId}:meta";

        public GridFieldEditAction(int gridId, int zoneId, Field which, int fromValue, int toValue)
        {
            GridId    = gridId;
            ZoneId    = zoneId;
            Which     = which;
            FromValue = fromValue;
            ToValue   = toValue;
            Timestamp = DateTime.UtcNow;
        }

        public void Apply(Controller controller)  => ApplyValue(controller, ToValue);
        public void Revert(Controller controller) => ApplyValue(controller, FromValue);

        void ApplyValue(Controller controller, int value)
        {
            var snapshot = GridActionHelpers.FindGrid(controller, GridId, ZoneId);
            if (snapshot == null) return;
            var origType  = snapshot.Type;
            var origType2 = snapshot.Type2;

            GridActionHelpers.MutateEveryGrid(controller, GridId, ZoneId, g =>
            {
                switch (Which)
                {
                    case Field.Type:  g.Type  = value; break;
                    case Field.Type2: g.Type2 = value; break;
                }
            });

            GridActionHelpers.SyncBufferForGrid(controller, GridId, ZoneId, origType, origType2);
        }
    }

    // Adds a new waypoint at the end of the grid (Number = max+1). Snapshot fields let
    // Revert restore the exact row that was inserted (position, heading, pause,
    // centerpoint).
    public sealed class GridEntryInsertAction : IEditAction
    {
        public int GridId { get; }
        public int ZoneId { get; }
        public int Number { get; }
        public float X { get; }
        public float Y { get; }
        public float Z { get; }
        public float Heading { get; }
        public int Pause { get; }
        public byte Centerpoint { get; }
        public DateTime Timestamp { get; }

        public string Description => $"Added grid {GridId} waypoint #{Number}";
        public string TargetKey   => $"grid:{GridId}:{Number}";

        public GridEntryInsertAction(int gridId, int zoneId, int number,
            float x, float y, float z, float heading, int pause, byte centerpoint)
        {
            GridId      = gridId;
            ZoneId      = zoneId;
            Number      = number;
            X           = x;
            Y           = y;
            Z           = z;
            Heading     = heading;
            Pause       = pause;
            Centerpoint = centerpoint;
            Timestamp   = DateTime.UtcNow;
        }

        public void Apply(Controller controller)
        {
            var already = GridActionHelpers.FindWaypoint(controller, GridId, Number);
            if (already == null)
            {
                foreach (var sp in controller.SpawnManager.SpawnPoints)
                {
                    if (sp.Record.Spawn.PathGrid != GridId) continue;
                    sp.Record.Waypoints.Add(new GridEntry
                    {
                        GridId      = GridId,
                        Number      = Number,
                        X           = X,
                        Y           = Y,
                        Z           = Z,
                        Heading     = Heading,
                        Pause       = Pause,
                        Centerpoint = Centerpoint,
                    });
                }
            }

            var buffer = controller.PendingBuffer;
            if (buffer != null)
            {
                buffer.GridEntryInserts[EditBuffer.GridEntryKey(GridId, Number)] = new GridEntryInsert
                {
                    GridId      = GridId,
                    ZoneId      = ZoneId,
                    Number      = Number,
                    X           = X,
                    Y           = Y,
                    Z           = Z,
                    Heading     = Heading,
                    Pause       = Pause,
                    Centerpoint = Centerpoint,
                    CreatedAt   = DateTime.UtcNow,
                };
                controller.MarkBufferDirty();
            }
        }

        public void Revert(Controller controller)
        {
            foreach (var sp in controller.SpawnManager.SpawnPoints)
            {
                sp.Record.Waypoints.RemoveAll(w => w.GridId == GridId && w.Number == Number);
            }

            var buffer = controller.PendingBuffer;
            if (buffer != null)
            {
                buffer.GridEntryInserts.Remove(EditBuffer.GridEntryKey(GridId, Number));
                // Also purge any field-edit entry that may have accumulated on top of the
                // insert — the row no longer exists.
                buffer.GridEntries.Remove(EditBuffer.GridEntryKey(GridId, Number));
                controller.MarkBufferDirty();
            }

            // Clear waypoint selection if we just removed the selected one.
            var sel = controller.Engine.WaypointEditor.Selected;
            if (sel.HasValue && sel.Value.GridId == GridId && sel.Value.Number == Number)
                controller.Engine.WaypointEditor.ClearSelection();
        }
    }

    // Removes a waypoint. Handles both persisted rows (adds to buffer.GridEntryDeletes
    // for eventual DB DELETE) and pending-insert rows (just drops the GridEntryInsert
    // entry — no DB touch). Revert restores from a captured snapshot.
    public sealed class GridEntryDeleteAction : IEditAction
    {
        public int GridId { get; }
        public int ZoneId { get; }
        public int Number { get; }
        public bool WasPendingInsert { get; }
        public float X { get; }
        public float Y { get; }
        public float Z { get; }
        public float Heading { get; }
        public int Pause { get; }
        public byte Centerpoint { get; }
        public DateTime Timestamp { get; }

        public string Description => $"Deleted grid {GridId} waypoint #{Number}";
        public string TargetKey   => $"grid:{GridId}:{Number}";

        public GridEntryDeleteAction(int gridId, int zoneId, GridEntry snapshot, bool wasPendingInsert)
        {
            GridId           = gridId;
            ZoneId           = zoneId;
            Number           = snapshot.Number;
            X                = snapshot.X;
            Y                = snapshot.Y;
            Z                = snapshot.Z;
            Heading          = snapshot.Heading;
            Pause            = snapshot.Pause;
            Centerpoint      = snapshot.Centerpoint;
            WasPendingInsert = wasPendingInsert;
            Timestamp        = DateTime.UtcNow;
        }

        public void Apply(Controller controller)
        {
            foreach (var sp in controller.SpawnManager.SpawnPoints)
            {
                sp.Record.Waypoints.RemoveAll(w => w.GridId == GridId && w.Number == Number);
            }

            var buffer = controller.PendingBuffer;
            var key = EditBuffer.GridEntryKey(GridId, Number);
            if (buffer != null)
            {
                if (WasPendingInsert)
                {
                    buffer.GridEntryInserts.Remove(key);
                }
                else
                {
                    buffer.GridEntryDeletes[key] = new GridEntryDelete
                    {
                        GridId      = GridId,
                        ZoneId      = ZoneId,
                        Number      = Number,
                        X           = X,
                        Y           = Y,
                        Z           = Z,
                        Heading     = Heading,
                        Pause       = Pause,
                        Centerpoint = Centerpoint,
                        DeletedAt   = DateTime.UtcNow,
                    };
                }
                // Drop any field-edit entry for this row — the row is going away.
                buffer.GridEntries.Remove(key);
                controller.MarkBufferDirty();
            }

            var sel = controller.Engine.WaypointEditor.Selected;
            if (sel.HasValue && sel.Value.GridId == GridId && sel.Value.Number == Number)
                controller.Engine.WaypointEditor.ClearSelection();
        }

        public void Revert(Controller controller)
        {
            var already = GridActionHelpers.FindWaypoint(controller, GridId, Number);
            if (already == null)
            {
                foreach (var sp in controller.SpawnManager.SpawnPoints)
                {
                    if (sp.Record.Spawn.PathGrid != GridId) continue;
                    sp.Record.Waypoints.Add(new GridEntry
                    {
                        GridId      = GridId,
                        Number      = Number,
                        X           = X,
                        Y           = Y,
                        Z           = Z,
                        Heading     = Heading,
                        Pause       = Pause,
                        Centerpoint = Centerpoint,
                    });
                }
            }

            var buffer = controller.PendingBuffer;
            var key = EditBuffer.GridEntryKey(GridId, Number);
            if (buffer != null)
            {
                if (WasPendingInsert)
                {
                    buffer.GridEntryInserts[key] = new GridEntryInsert
                    {
                        GridId      = GridId,
                        ZoneId      = ZoneId,
                        Number      = Number,
                        X           = X,
                        Y           = Y,
                        Z           = Z,
                        Heading     = Heading,
                        Pause       = Pause,
                        Centerpoint = Centerpoint,
                        CreatedAt   = DateTime.UtcNow,
                    };
                }
                else
                {
                    buffer.GridEntryDeletes.Remove(key);
                }
                controller.MarkBufferDirty();
            }
        }
    }

    internal static class GridActionHelpers
    {
        internal struct WaypointSnapshot
        {
            public float X;
            public float Y;
            public float Z;
            public float Heading;
            public int Pause;
            public byte Centerpoint;
        }

        public static GridEntry FindWaypoint(Controller controller, int gridId, int number)
        {
            foreach (var sp in controller.SpawnManager.SpawnPoints)
                foreach (var wp in sp.Record.Waypoints)
                    if (wp.GridId == gridId && wp.Number == number)
                        return wp;
            // Fallback to the zone-wide grid list so orphan grids (no spawn2 references
            // them) resolve for the Waypoint Info panel.
            foreach (var zg in controller.ZoneGrids)
                foreach (var wp in zg.Waypoints)
                    if (wp.GridId == gridId && wp.Number == number)
                        return wp;
            return null;
        }

        public static Grid FindGrid(Controller controller, int gridId, int zoneId)
        {
            foreach (var sp in controller.SpawnManager.SpawnPoints)
            {
                var g = sp.Record.Grid;
                if (g != null && g.Id == gridId && g.ZoneId == zoneId) return g;
            }
            foreach (var zg in controller.ZoneGrids)
            {
                if (zg.Grid != null && zg.Grid.Id == gridId && zg.Grid.ZoneId == zoneId)
                    return zg.Grid;
            }
            return null;
        }

        public static WaypointSnapshot SnapshotWaypoint(GridEntry wp) => new WaypointSnapshot
        {
            X           = wp.X,
            Y           = wp.Y,
            Z           = wp.Z,
            Heading     = wp.Heading,
            Pause       = wp.Pause,
            Centerpoint = wp.Centerpoint,
        };

        public static void MutateEveryWaypoint(Controller controller, int gridId, int number, Action<GridEntry> mutate)
        {
            foreach (var sp in controller.SpawnManager.SpawnPoints)
                foreach (var wp in sp.Record.Waypoints)
                    if (wp.GridId == gridId && wp.Number == number)
                        mutate(wp);
            // Zone-wide copies (independent GridEntry instances loaded by a separate query)
            // must also be updated so orphan-grid selections reflect the edit in real time.
            foreach (var zg in controller.ZoneGrids)
                foreach (var wp in zg.Waypoints)
                    if (wp.GridId == gridId && wp.Number == number)
                        mutate(wp);
        }

        public static void MutateEveryGrid(Controller controller, int gridId, int zoneId, Action<Grid> mutate)
        {
            foreach (var sp in controller.SpawnManager.SpawnPoints)
            {
                var g = sp.Record.Grid;
                if (g != null && g.Id == gridId && g.ZoneId == zoneId) mutate(g);
            }
            foreach (var zg in controller.ZoneGrids)
            {
                if (zg.Grid != null && zg.Grid.Id == gridId && zg.Grid.ZoneId == zoneId)
                    mutate(zg.Grid);
            }
        }

        // Ensures a GridEntryEdit exists (seeded from the snapshot for first-touch),
        // mirrors post-mutation Current* values into it, and drops the entry when the
        // row has walked back to Original in every field so commit doesn't emit no-ops.
        public static void SyncBufferForWaypoint(
            Controller controller, int gridId, int number,
            WaypointSnapshot preMutation, GridEntry post)
        {
            var buffer = controller.PendingBuffer;
            if (buffer == null) return;

            var key = EditBuffer.GridEntryKey(gridId, number);

            // Pending inserts don't get a GridEntryEdit — mirror straight into the insert.
            if (buffer.GridEntryInserts.TryGetValue(key, out var ins))
            {
                ins.X           = post.X;
                ins.Y           = post.Y;
                ins.Z           = post.Z;
                ins.Heading     = post.Heading;
                ins.Pause       = post.Pause;
                ins.Centerpoint = post.Centerpoint;
                controller.MarkBufferDirty();
                return;
            }

            if (!buffer.GridEntries.TryGetValue(key, out var edit))
            {
                edit = new GridEntryEdit
                {
                    GridId              = gridId,
                    Number              = number,
                    OriginalX           = preMutation.X,
                    OriginalY           = preMutation.Y,
                    OriginalZ           = preMutation.Z,
                    OriginalHeading     = preMutation.Heading,
                    OriginalPause       = preMutation.Pause,
                    OriginalCenterpoint = preMutation.Centerpoint,
                };
                buffer.GridEntries[key] = edit;
            }

            edit.CurrentX           = post.X;
            edit.CurrentY           = post.Y;
            edit.CurrentZ           = post.Z;
            edit.CurrentHeading     = post.Heading;
            edit.CurrentPause       = post.Pause;
            edit.CurrentCenterpoint = post.Centerpoint;
            edit.LastModifiedAt     = DateTime.UtcNow;

            const float eps = 0.001f;
            if (Math.Abs(edit.CurrentX - edit.OriginalX) < eps &&
                Math.Abs(edit.CurrentY - edit.OriginalY) < eps &&
                Math.Abs(edit.CurrentZ - edit.OriginalZ) < eps &&
                Math.Abs(edit.CurrentHeading - edit.OriginalHeading) < eps &&
                edit.CurrentPause == edit.OriginalPause &&
                edit.CurrentCenterpoint == edit.OriginalCenterpoint)
            {
                buffer.GridEntries.Remove(key);
            }
            controller.MarkBufferDirty();
        }

        public static void SyncBufferForGrid(
            Controller controller, int gridId, int zoneId, int origType, int origType2)
        {
            var buffer = controller.PendingBuffer;
            if (buffer == null) return;

            var g = FindGrid(controller, gridId, zoneId);
            if (g == null) return;

            var key = EditBuffer.GridKey(gridId, zoneId);
            if (!buffer.Grids.TryGetValue(key, out var edit))
            {
                edit = new GridEdit
                {
                    Id            = gridId,
                    ZoneId        = zoneId,
                    OriginalType  = origType,
                    OriginalType2 = origType2,
                };
                buffer.Grids[key] = edit;
            }

            edit.CurrentType  = g.Type;
            edit.CurrentType2 = g.Type2;
            edit.LastModifiedAt = DateTime.UtcNow;

            if (edit.CurrentType == edit.OriginalType && edit.CurrentType2 == edit.OriginalType2)
                buffer.Grids.Remove(key);

            controller.MarkBufferDirty();
        }
    }
}
