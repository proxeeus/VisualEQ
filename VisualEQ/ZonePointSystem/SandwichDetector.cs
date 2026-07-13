using System.Collections.Generic;
using VisualEQ.Database.Models;

namespace VisualEQ.ZonePointSystem
{
    // Detects "sandwich" cases per the trilogy_zone_editor_spec §"Sandwich detector":
    // an owned row A→B lands at (Bx, By, Bz), and B has another row whose fire region
    // contains that landing coord — the player who just teleported would immediately
    // fire the destination-zone's trigger and be teleported again. Result: silent
    // teleport loops or unexpected sandwich zones.
    //
    // Runs off the ZonePointManager's cached peer-zone rows. No DB access.
    public static class SandwichDetector
    {
        // Per-row detection result. Sandwiched = true when the landing coord falls inside
        // at least one destination-zone row's fire region; OffendingRow identifies the
        // specific conflicting trigger (used by the sidebar suggestion + link-line
        // rendering).
        public struct Result
        {
            public bool Sandwiched;
            public TrilogyZonePoint OffendingRow;
        }

        // Computes sandwich status for a single owned row. Returns Sandwiched=false when
        // there's no peer data for the destination zone, when the landing coord uses a
        // wildcard (no fixed landing to conflict with), or when no destination-zone row
        // fires at the landing coord.
        public static Result Check(
            ZonePoint owned,
            IReadOnlyDictionary<string, List<TrilogyZonePoint>> peerRows)
        {
            var row = owned.Row;
            if (string.IsNullOrEmpty(row.TargetZone)) return default;
            if (ZonePointWildcards.IsWildcard(row.TargetX) &&
                ZonePointWildcards.IsWildcard(row.TargetY)) return default;
            if (!peerRows.TryGetValue(row.TargetZone, out var candidates)) return default;

            // Landing coord in destination-zone DB space.
            float lx = row.TargetX, ly = row.TargetY, lz = row.TargetZ;

            foreach (var peer in candidates)
            {
                if (peer.Id == row.Id) continue; // shouldn't happen (different zone), but defensive
                if (FiresAt(peer, lx, ly, lz))
                {
                    return new Result { Sandwiched = true, OffendingRow = peer };
                }
            }
            return default;
        }

        // Returns true when the DB coord (x, y, z) falls inside the peer row's fire
        // region. Handles all three modes:
        //   0 (box)         — axis-aligned XY box (2*Zrange square) + Z tolerance (2*MaxZDiff, or infinite when 0)
        //   1 (X-plane)     — crossing detected by sign of peer.X; MinVert/MaxVert bound Y (0/0 = unbounded)
        //   2 (Y-plane)     — same swapped
        //
        // Source-wildcard rows never fire on a specific coord (they gate by "any Z" or
        // similar fall-through behavior); treated as non-conflicting for detector purposes.
        static bool FiresAt(TrilogyZonePoint peer, float x, float y, float z)
        {
            // Source wildcard means "no XY gating" — skip (see spec §"Wildcard semantics").
            if (ZonePointWildcards.IsWildcard(peer.X) || ZonePointWildcards.IsWildcard(peer.Y))
                return false;

            switch (peer.UseNewZoning)
            {
                case 0:
                    return FiresAtBox(peer, x, y, z);
                case 1:
                    return FiresAtXPlane(peer, x, y, z);
                case 2:
                    return FiresAtYPlane(peer, x, y, z);
                default:
                    return FiresAtBox(peer, x, y, z);
            }
        }

        static bool FiresAtBox(TrilogyZonePoint peer, float x, float y, float z)
        {
            float half = System.MathF.Max(1f, peer.Zrange);
            if (System.MathF.Abs(x - peer.X) > half) return false;
            if (System.MathF.Abs(y - peer.Y) > half) return false;
            // MaxZDiff == 0 → unbounded per spec.
            if (peer.MaxZDiff > 0 && System.MathF.Abs(z - peer.Z) > peer.MaxZDiff) return false;
            return true;
        }

        // X-plane: peer.X is the crossing threshold on the DB X axis. Direction inferred
        // from sign of peer.X (spec §"X-plane crossing"): peer.X >= 0 fires on x >= peer.X,
        // peer.X <= 0 fires on x <= peer.X. MinVert/MaxVert (Y axis) bound extent; 0/0 =
        // unbounded.
        static bool FiresAtXPlane(TrilogyZonePoint peer, float x, float y, float z)
        {
            var fireSideXGeq = peer.X >= 0;
            var crossed = fireSideXGeq ? (x >= peer.X) : (x <= peer.X);
            if (!crossed) return false;

            // Perpendicular-axis (Y) bounds. 0/0 = unbounded.
            if (peer.MinVert != 0 || peer.MaxVert != 0)
            {
                var lo = System.MathF.Min(peer.MinVert, peer.MaxVert);
                var hi = System.MathF.Max(peer.MinVert, peer.MaxVert);
                if (y < lo || y > hi) return false;
            }
            return true;
        }

        static bool FiresAtYPlane(TrilogyZonePoint peer, float x, float y, float z)
        {
            var fireSideYGeq = peer.Y >= 0;
            var crossed = fireSideYGeq ? (y >= peer.Y) : (y <= peer.Y);
            if (!crossed) return false;

            if (peer.MinVert != 0 || peer.MaxVert != 0)
            {
                var lo = System.MathF.Min(peer.MinVert, peer.MaxVert);
                var hi = System.MathF.Max(peer.MinVert, peer.MaxVert);
                if (x < lo || x > hi) return false;
            }
            return true;
        }
    }
}
