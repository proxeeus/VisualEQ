using System.Numerics;
using VisualEQ.Database.Models;

namespace VisualEQ.ZonePointSystem
{
    // Editor-side wrapper around one TrilogyZonePoint row. Same shape as SpawnPoint:
    // Original* fields captured at load, Current* fields mutated by edits, IsDirty flag
    // for the marker/sidebar UI. Revert() restores originals.
    //
    // Coordinates: the Row holds DB-space (X = east/west, Y = north/south, Z = up).
    // SceneCenter / SceneTarget expose scene-space (X/Y swapped) for renderer + hit-test.
    public class ZonePoint
    {
        public TrilogyZonePoint Row { get; }

        // Baseline captured at load — used by Revert and by undo/commit to know when a
        // Current* value has drifted back to the original (drop the buffer entry).
        public float OriginalX { get; }
        public float OriginalY { get; }
        public float OriginalZ { get; }
        public int OriginalZrange { get; }
        public int OriginalMaxZDiff { get; }

        public bool IsDirty { get; private set; }

        public ZonePoint(TrilogyZonePoint row)
        {
            Row              = row;
            OriginalX        = row.X;
            OriginalY        = row.Y;
            OriginalZ        = row.Z;
            OriginalZrange   = row.Zrange;
            OriginalMaxZDiff = row.MaxZDiff;
        }

        // Scene-space center. Wildcarded axes stay at their sentinel value — the renderer
        // decides how to handle them (slab, hide, etc.).
        public Vector3 SceneCenter => new Vector3(Row.Y, Row.X, Row.Z);

        public Vector3 SceneTarget => new Vector3(Row.TargetY, Row.TargetX, Row.TargetZ);

        public bool HasSourceWildcard =>
            ZonePointWildcards.IsWildcard(Row.X) || ZonePointWildcards.IsWildcard(Row.Y);

        public bool HasTargetWildcard =>
            ZonePointWildcards.IsWildcard(Row.TargetX) ||
            ZonePointWildcards.IsWildcard(Row.TargetY) ||
            ZonePointWildcards.IsWildcard(Row.TargetZ);

        // Red trumps Purple trumps Yellow trumps Green. (0,0,0) is dead even if it has a
        // valid target — the row will silently never fire.
        public ZonePointHealth Health
        {
            get
            {
                if (Row.X == 0 && Row.Y == 0 && Row.Z == 0) return ZonePointHealth.Red;
                if (HasSourceWildcard)                       return ZonePointHealth.Purple;
                if (HasTargetWildcard)                       return ZonePointHealth.Yellow;
                return ZonePointHealth.Green;
            }
        }

        // Position edit — drag on center handle. sceneCenter is scene-space; the row's DB
        // coords are updated with the axes swapped back.
        public void MarkMoved(Vector3 sceneCenter)
        {
            Row.X = sceneCenter.Y;
            Row.Y = sceneCenter.X;
            Row.Z = sceneCenter.Z;
            IsDirty = true;
        }

        // Box-mode resize — corner drag adjusts Zrange (XY square) and/or MaxZDiff (Z-face).
        // Either arg may be unchanged; caller sets both.
        public void MarkResized(int zrange, int maxZDiff)
        {
            Row.Zrange   = zrange;
            Row.MaxZDiff = maxZDiff;
            IsDirty = true;
        }

        public void Revert()
        {
            Row.X        = OriginalX;
            Row.Y        = OriginalY;
            Row.Z        = OriginalZ;
            Row.Zrange   = OriginalZrange;
            Row.MaxZDiff = OriginalMaxZDiff;
            IsDirty = false;
        }
    }
}
