using System;
using System.Linq;
using VisualEQ.SpawnSystem;

namespace VisualEQ.EditSystem
{
    // Assigns / clears a grid on an existing spawn2 row. Mutates sp.Record.Spawn.PathGrid,
    // refreshes sp.Record.Waypoints to match the new grid (so the amber polyline updates
    // immediately when the spawn is the selection source), and syncs the pending buffer.
    //
    // Pending-insert spawns (temp negative id) mirror into buffer.SpawnInserts.PathGrid
    // instead of buffer.Spawns — the commit path is an INSERT there, not an UPDATE, so a
    // SpawnEdit row on a temp id would never fire.
    public sealed class SpawnPathGridAction : IEditAction
    {
        public int SpawnId { get; }
        public int FromGridId { get; }
        public int ToGridId { get; }
        public string DisplayName { get; }
        public DateTime Timestamp { get; }

        public string Description =>
            ToGridId == 0
                ? $"Cleared path grid on '{DisplayName}' (#{SpawnId})"
                : FromGridId == 0
                    ? $"Assigned grid {ToGridId} to '{DisplayName}' (#{SpawnId})"
                    : $"Changed '{DisplayName}' (#{SpawnId}) grid {FromGridId} → {ToGridId}";
        public string TargetKey => $"spawn:{SpawnId}";

        public SpawnPathGridAction(SpawnPoint sp, int fromGridId, int toGridId)
        {
            SpawnId     = sp.Record.Spawn.Id;
            FromGridId  = fromGridId;
            ToGridId    = toGridId;
            DisplayName = PrimaryName(sp);
            Timestamp   = DateTime.UtcNow;
        }

        public void Apply(Controller controller)  => AssignGrid(controller, ToGridId);
        public void Revert(Controller controller) => AssignGrid(controller, FromGridId);

        void AssignGrid(Controller controller, int gridId)
        {
            var sp = controller.SpawnManager.SpawnPoints.FirstOrDefault(p => p.Record.Spawn.Id == SpawnId);
            if (sp == null) return;

            sp.Record.Spawn.PathGrid = gridId;
            controller.RefreshSpawnWaypointsFromZoneGrids(sp, gridId);
            UpdateBufferEntry(controller, sp, gridId);
            RefreshGridSpawnCounts(controller);

            // Clear any sticky Grid-List selection so UpdatePathGrids falls back to the
            // spawn-driven path. Otherwise assigning a grid right after creating it (which
            // leaves SelectedGridId set to the new grid — see GridInsertAction) freezes the
            // polyline on that grid; selecting another NPC won't repaint because SelectedGridId
            // is checked first in UpdatePathGrids.
            controller.SelectGrid(null);

            controller.MarkBufferDirty();
        }

        // Recount ZoneGridRecord.SpawnCount so the Grid List sidebar chip flips between
        // [A]/[O] as spawns are attached/detached. Cheap — the grid count in a zone is
        // typically double-digits.
        static void RefreshGridSpawnCounts(Controller controller)
        {
            foreach (var zg in controller.ZoneGrids)
            {
                if (zg.Grid == null) continue;
                zg.SpawnCount = controller.SpawnManager.SpawnPoints.Count(
                    sp => sp.Record.Spawn.PathGrid == zg.Grid.Id);
            }
        }

        void UpdateBufferEntry(Controller controller, SpawnPoint sp, int gridId)
        {
            var buffer = controller.PendingBuffer;
            if (buffer == null) return;

            if (buffer.SpawnInserts.TryGetValue(sp.Record.Spawn.Id, out var insert))
            {
                insert.PathGrid = gridId;
                return;
            }

            if (!buffer.Spawns.TryGetValue(sp.Record.Spawn.Id, out var edit))
            {
                edit = new SpawnEdit
                {
                    SpawnId          = sp.Record.Spawn.Id,
                    OriginalX        = sp.Record.Spawn.X,
                    OriginalY        = sp.Record.Spawn.Y,
                    OriginalZ        = sp.Record.Spawn.Z,
                    OriginalHeading  = sp.OriginalHeading,
                    OriginalPathGrid = FromGridId,
                    CurrentX         = sp.Record.Spawn.X,
                    CurrentY         = sp.Record.Spawn.Y,
                    CurrentZ         = sp.Record.Spawn.Z,
                    CurrentHeading   = sp.OriginalHeading,
                    DisplayName      = PrimaryName(sp),
                };
                buffer.Spawns[sp.Record.Spawn.Id] = edit;
            }

            edit.CurrentPathGrid = gridId;
            edit.LastModifiedAt  = DateTime.UtcNow;

            // Walk-back: entry drops when every tracked field matches its baseline.
            const float eps = 0.001f;
            if (Math.Abs(edit.CurrentX - edit.OriginalX) < eps &&
                Math.Abs(edit.CurrentY - edit.OriginalY) < eps &&
                Math.Abs(edit.CurrentZ - edit.OriginalZ) < eps &&
                Math.Abs(edit.CurrentHeading - edit.OriginalHeading) < eps &&
                edit.CurrentPathGrid == edit.OriginalPathGrid)
            {
                buffer.Spawns.Remove(sp.Record.Spawn.Id);
            }
        }

        static string PrimaryName(SpawnPoint sp) =>
            sp.FocusedNpc?.Name
            ?? sp.Record.Entries.FirstOrDefault()?.Npc?.Name
            ?? "?";
    }
}
