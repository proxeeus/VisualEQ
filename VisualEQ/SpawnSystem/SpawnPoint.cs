using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using VisualEQ.Database.Models;
using VisualEQ.Engine;

namespace VisualEQ.SpawnSystem
{
    public class SpawnPoint
    {
        public SpawnRecord Record { get; }

        // AniModelInstance is replaced (not mutated in-place) when a visual-affecting
        // NPC field edit fires — race/gender changes need a whole new AniModel from a
        // different chr code, so Controller.RefreshNpcVisualForNpc removes the old
        // instance from the engine, builds a new one, and swaps this reference. The
        // internal setter keeps the "external readers only read" contract while letting
        // the refresh path inside the same assembly rewire.
        public AniModelInstance Model { get; internal set; }
        public bool IsPlaceholder { get; internal set; }

        public bool IsDirty { get; private set; }

        public Vector3 OriginalPosition { get; }
        public float OriginalHeading { get; }
        public float CurrentHeading { get; private set; }

        // Which of the spawngroup's Entries the sidebar (info panel + NPC editor) and
        // scene model are currently focused on. Defaults at load to the highest-Chance
        // entry (the "primary"), matching the previous fixed-primary behaviour. Changes
        // via the sidebar's variant cycler or a search-result click. Not persisted —
        // it's transient UI state, reset with the SpawnPoint on zone unload.
        //
        // Every downstream site that used to compute the primary from
        // Entries.OrderByDescending(Chance).FirstOrDefault() now reads FocusedEntry
        // instead, so an entry with a low spawn chance becomes visible + editable when
        // the user cycles onto it.
        public int FocusedEntryIndex { get; internal set; }

        public SpawnEntryWithNpc FocusedEntry =>
            (Record?.Entries != null && FocusedEntryIndex >= 0 && FocusedEntryIndex < Record.Entries.Count)
                ? Record.Entries[FocusedEntryIndex]
                : null;

        public NpcType FocusedNpc => FocusedEntry?.Npc;

        // Populated by SpawnManager.ComputeStacks() at load time when this spawn2 row
        // shares an exact DB (x, y, z) with one or more sibling rows. When set, contains
        // every stacked SpawnPoint (INCLUDING this one) in stable spawn2.id order.
        // Null when this spawn isn't part of any stack.
        //
        // Stacked spawn2 rows are how EQEmu encodes respawn rotation — only one member
        // is alive at a time in-game, but VisualEQ visualises all of them, hence the
        // "skeleton on top of a wood elf" visual in oot. See project memory:
        // spawn-stacking-not-a-bug.
        public IReadOnlyList<SpawnPoint> StackSiblings { get; internal set; }

        public SpawnPoint(SpawnRecord record, AniModelInstance model, bool isPlaceholder)
        {
            Record = record;
            Model = model;
            IsPlaceholder = isPlaceholder;
            OriginalPosition = model.Position;
            OriginalHeading = record.Spawn.Heading;
            CurrentHeading = record.Spawn.Heading;
        }

        public void MarkMoved(Vector3 newPos, float heading)
        {
            System.Console.WriteLine(
                $"[SpawnPoint] MarkMoved #{Record.Spawn.Id} pos ({Model.Position.X:F1},{Model.Position.Y:F1},{Model.Position.Z:F1}) " +
                $"→ ({newPos.X:F1},{newPos.Y:F1},{newPos.Z:F1}) heading {CurrentHeading:F0}→{heading:F0}");
            Model.Rotation = SpawnManager.HeadingToRotation(heading);
            Model.Position = newPos;
            CurrentHeading = heading;
            IsDirty = true;
        }

        public void Revert()
        {
            System.Console.WriteLine(
                $"[SpawnPoint] Revert #{Record.Spawn.Id} pos ({Model.Position.X:F1},{Model.Position.Y:F1},{Model.Position.Z:F1}) " +
                $"→ ({OriginalPosition.X:F1},{OriginalPosition.Y:F1},{OriginalPosition.Z:F1}) heading {CurrentHeading:F0}→{OriginalHeading:F0}");
            Model.Rotation = SpawnManager.HeadingToRotation(OriginalHeading);
            Model.Position = OriginalPosition;
            CurrentHeading = OriginalHeading;
            IsDirty = false;
        }
    }
}
