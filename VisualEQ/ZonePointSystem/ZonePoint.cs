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

        // v3 scalar-field baselines — captured at construction so the inspector's per-field
        // actions have a stable revert target and the buffer's "clean now, drop it" check
        // has a comparison value.
        public float OriginalHeading { get; }
        public string OriginalTargetZone { get; }
        public float OriginalTargetX { get; }
        public float OriginalTargetY { get; }
        public float OriginalTargetZ { get; }
        public byte OriginalUseNewZoning { get; }
        public float OriginalMinVert { get; }
        public float OriginalMaxVert { get; }
        public float OriginalCenterPoint { get; }
        public int OriginalKeepX { get; }
        public int OriginalKeepY { get; }
        public int OriginalKeepZ { get; }

        public bool IsDirty { get; private set; }

        // A newly-created row that hasn't been persisted yet (its Row.Id is a negative
        // temp id). Renderers can style it distinctly; commit path INSERTs instead of
        // UPDATEs; undo-insert removes it entirely rather than reverting Row.*.
        public bool IsPendingInsert => Row.Id < 0;

        public ZonePoint(TrilogyZonePoint row)
        {
            Row                  = row;
            OriginalX            = row.X;
            OriginalY            = row.Y;
            OriginalZ            = row.Z;
            OriginalZrange       = row.Zrange;
            OriginalMaxZDiff     = row.MaxZDiff;
            OriginalHeading      = row.Heading;
            OriginalTargetZone   = row.TargetZone;
            OriginalTargetX      = row.TargetX;
            OriginalTargetY      = row.TargetY;
            OriginalTargetZ      = row.TargetZ;
            OriginalUseNewZoning = row.UseNewZoning;
            OriginalMinVert      = row.MinVert;
            OriginalMaxVert      = row.MaxVert;
            OriginalCenterPoint  = row.CenterPoint;
            OriginalKeepX        = row.KeepX;
            OriginalKeepY        = row.KeepY;
            OriginalKeepZ        = row.KeepZ;
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

        // ─── Mutations ────────────────────────────────────────────────────────────────
        // Every mutator flips IsDirty; the buffer/commit layer decides whether the row
        // has actually diverged from its Original* baseline (and drops the entry if not).

        public void MarkMoved(Vector3 sceneCenter)
        {
            Row.X = sceneCenter.Y;
            Row.Y = sceneCenter.X;
            Row.Z = sceneCenter.Z;
            IsDirty = true;
        }

        public void MarkResized(int zrange, int maxZDiff)
        {
            Row.Zrange   = zrange;
            Row.MaxZDiff = maxZDiff;
            IsDirty = true;
        }

        public void SetHeading(float heading)      { Row.Heading = heading; IsDirty = true; }
        public void SetTargetZone(string zone)     { Row.TargetZone = zone; IsDirty = true; }
        public void SetTargetX(float v)            { Row.TargetX = v; IsDirty = true; }
        public void SetTargetY(float v)            { Row.TargetY = v; IsDirty = true; }
        public void SetTargetZ(float v)            { Row.TargetZ = v; IsDirty = true; }
        public void SetUseNewZoning(byte v)        { Row.UseNewZoning = v; IsDirty = true; }
        public void SetMinVert(float v)            { Row.MinVert = v; IsDirty = true; }
        public void SetMaxVert(float v)            { Row.MaxVert = v; IsDirty = true; }
        public void SetCenterPoint(float v)        { Row.CenterPoint = v; IsDirty = true; }
        public void SetKeepX(int v)                { Row.KeepX = v; IsDirty = true; }
        public void SetKeepY(int v)                { Row.KeepY = v; IsDirty = true; }
        public void SetKeepZ(int v)                { Row.KeepZ = v; IsDirty = true; }

        public void Revert()
        {
            Row.X            = OriginalX;
            Row.Y            = OriginalY;
            Row.Z            = OriginalZ;
            Row.Zrange       = OriginalZrange;
            Row.MaxZDiff     = OriginalMaxZDiff;
            Row.Heading      = OriginalHeading;
            Row.TargetZone   = OriginalTargetZone;
            Row.TargetX      = OriginalTargetX;
            Row.TargetY      = OriginalTargetY;
            Row.TargetZ      = OriginalTargetZ;
            Row.UseNewZoning = OriginalUseNewZoning;
            Row.MinVert      = OriginalMinVert;
            Row.MaxVert      = OriginalMaxVert;
            Row.CenterPoint  = OriginalCenterPoint;
            Row.KeepX        = OriginalKeepX;
            Row.KeepY        = OriginalKeepY;
            Row.KeepZ        = OriginalKeepZ;
            IsDirty = false;
        }
    }
}
