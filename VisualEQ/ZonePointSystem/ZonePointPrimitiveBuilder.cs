using System.Collections.Generic;
using System.Numerics;

namespace VisualEQ.ZonePointSystem
{
    // Turns ZonePoint rows into (lines, triangles) primitive lists for the renderer.
    // All output is scene-space (X/Y swapped from DB space). Health drives color; the
    // selected row gets a brighter, thicker wireframe.
    //
    // Stateless — Controller calls Build() each frame and pushes the result.
    public static class ZonePointPrimitiveBuilder
    {
        // Line list (edges + arrows + handle crosses).
        public readonly struct Line
        {
            public readonly Vector3 A, B;
            public readonly Vector4 Color;
            public Line(Vector3 a, Vector3 b, Vector4 color) { A = a; B = b; Color = color; }
        }

        // Triangle list (volumetric fills).
        public readonly struct Tri
        {
            public readonly Vector3 A, B, C;
            public readonly Vector4 Color;
            public Tri(Vector3 a, Vector3 b, Vector3 c, Vector4 color) { A = a; B = b; C = c; Color = color; }
        }

        public struct Output
        {
            public List<Line> Lines;
            public List<Tri>  Tris;
        }

        // Fill alpha keeps volumes readable while not obscuring the scene underneath.
        const float FillAlpha = 0.15f;
        // Fallback Z half-extent when maxZDiff == 0 (spec: "effectively unbounded 50000").
        // Kept small on purpose — multi-level zones (dungeons especially) get unreadable
        // when boxes span the full zone height and stack vertically. Users read the
        // "MaxZDiff=∞" state from the inspector badge; the 3D volume just marks the
        // trigger's location, not its true logical extent.
        const float InfiniteZHalfExtent = 20f;
        // Plane-crossing wall Z half-extent when zone bounds aren't available.
        const float PlaneWallHalfHeight = 2000f;
        // Wildcard slab Z half-extent when maxZDiff == 0. Fall-through triggers care about
        // a modest Z band; render a thin slab so it doesn't visually swamp the zone.
        const float WildcardSlabDefaultZ = 50f;
        // Arrow length for plane-crossing direction indicator.
        const float ArrowLength = 40f;

        public static Output Build(
            IReadOnlyList<ZonePoint> zonePoints,
            IReadOnlyList<ZonePoint> incomingPoints,
            ZonePoint selected,
            (Vector3 Min, Vector3 Max)? zoneBounds,
            IReadOnlyDictionary<int, SandwichDetector.Result> sandwichResults = null)
        {
            var lines = new List<Line>(zonePoints.Count * 24 + (incomingPoints?.Count ?? 0) * 12);
            var tris  = new List<Tri>(zonePoints.Count * 12 + (incomingPoints?.Count ?? 0) * 6);

            if (incomingPoints != null)
            {
                foreach (var zp in incomingPoints)
                {
                    var isSelected = ReferenceEquals(zp, selected);
                    EmitIncomingPad(zp, isSelected, lines, tris);
                }
            }

            foreach (var zp in zonePoints)
            {
                bool isSelected = ReferenceEquals(zp, selected);
                bool isSandwiched = sandwichResults != null && sandwichResults.ContainsKey(zp.Row.Id);
                var (wireColor, fillColor) = isSandwiched
                    ? SandwichColors(isSelected)
                    : ColorsFor(zp.Health, isSelected);

                if (zp.HasSourceWildcard)
                {
                    // Purple wildcard slab — spans the loaded zone bounds at real Z with
                    // a thin band matching MaxZDiff (or a small default).
                    EmitWildcardSlab(zp, wireColor, fillColor, zoneBounds, lines, tris);
                    continue;
                }

                switch (zp.Row.UseNewZoning)
                {
                    case 0:
                        EmitBox(zp, wireColor, fillColor, zoneBounds, lines, tris);
                        break;
                    case 1:
                        EmitPlaneCrossing(zp, wireColor, fillColor, axisIsX: true,  zoneBounds, lines, tris);
                        break;
                    case 2:
                        EmitPlaneCrossing(zp, wireColor, fillColor, axisIsX: false, zoneBounds, lines, tris);
                        break;
                    default:
                        EmitBox(zp, wireColor, fillColor, zoneBounds, lines, tris);
                        break;
                }

                // Sandwich link line: from this row's landing coord (target) to the
                // offending peer row's source coord in the destination zone. Only
                // drawn when the target lives in the current zone's coord space — for
                // cross-zone targets, the offending source is in another world.
                if (isSandwiched && SameZoneTarget(zp))
                {
                    var target  = zp.SceneTarget;
                    var offender = sandwichResults[zp.Row.Id].OffendingRow;
                    var offenderScene = new Vector3(offender.Y, offender.X, offender.Z);
                    var sandwichLink = new Vector4(1.0f, 0.15f, 0.15f, 1.0f);
                    lines.Add(new Line(target, offenderScene, sandwichLink));
                }

                // Destination pip if the target lives in the same zone. The spec calls for
                // a cross-zone arrow otherwise — deferred (needs a target-zone-in-loaded-set
                // check + on-screen off-view arrow, both out of scope for v1).
                if (SameZoneTarget(zp))
                {
                    var target = zp.SceneTarget;
                    // Tiny 3D crosshair marking the landing coord.
                    const float arm = 6f;
                    var pip = new Vector4(wireColor.X, wireColor.Y, wireColor.Z, 1f);
                    lines.Add(new Line(target - new Vector3(arm, 0, 0), target + new Vector3(arm, 0, 0), pip));
                    lines.Add(new Line(target - new Vector3(0, arm, 0), target + new Vector3(0, arm, 0), pip));
                    lines.Add(new Line(target - new Vector3(0, 0, arm), target + new Vector3(0, 0, arm), pip));
                    // Faint tether from source center to target so the pairing is legible.
                    var tether = new Vector4(wireColor.X, wireColor.Y, wireColor.Z, 0.35f);
                    lines.Add(new Line(zp.SceneCenter, target, tether));
                }
            }

            return new Output { Lines = lines, Tris = tris };
        }

        static bool SameZoneTarget(ZonePoint zp) =>
            !string.IsNullOrEmpty(zp.Row.TargetZone) &&
            string.Equals(zp.Row.Zone, zp.Row.TargetZone, System.StringComparison.OrdinalIgnoreCase);

        // ─── Box mode ────────────────────────────────────────────────────────────────
        // Public so the interactive hit-test in ZonePointEditor can compute the exact
        // same AABB the renderer draws — click-and-visual stay in sync when MaxZDiff==0
        // (infinite Z) is clamped to zone bounds.
        public static (Vector3 Min, Vector3 Max)? TryGetBoxSceneAabb(
            ZonePoint zp, (Vector3 Min, Vector3 Max)? zoneBounds)
        {
            if (zp.HasSourceWildcard) return null;
            if (zp.Row.UseNewZoning != 0) return null;

            var c = zp.SceneCenter;
            var xy = System.MathF.Max(1f, zp.Row.Zrange);
            float zMinAbs, zMaxAbs;
            if (zp.Row.MaxZDiff == 0)
            {
                // Infinite Z → compact stub around the real trigger Z so multi-level
                // zones stay readable. See InfiniteZHalfExtent comment for rationale.
                zMinAbs = c.Z - InfiniteZHalfExtent;
                zMaxAbs = c.Z + InfiniteZHalfExtent;
            }
            else
            {
                var zHalf = System.MathF.Max(1f, zp.Row.MaxZDiff);
                zMinAbs = c.Z - zHalf;
                zMaxAbs = c.Z + zHalf;
            }
            return (new Vector3(c.X - xy, c.Y - xy, zMinAbs), new Vector3(c.X + xy, c.Y + xy, zMaxAbs));
        }

        static void EmitBox(
            ZonePoint zp, Vector4 wire, Vector4 fill,
            (Vector3 Min, Vector3 Max)? zoneBounds,
            List<Line> lines, List<Tri> tris)
        {
            var aabb = TryGetBoxSceneAabb(zp, zoneBounds);
            if (!aabb.HasValue) return;
            var c   = zp.SceneCenter;
            var min = aabb.Value.Min;
            var max = aabb.Value.Max;

            EmitAabbLines(min, max, wire, lines);
            EmitAabbTris (min, max, fill, tris);

            // Cap indicator for "infinite Z" columns so users can tell them apart from
            // finite boxes at a glance — a bright ring at the trigger's real Z.
            if (zp.Row.MaxZDiff == 0)
            {
                var capColor = new Vector4(wire.X, wire.Y, wire.Z, 1f);
                lines.Add(new Line(new Vector3(min.X, min.Y, c.Z), new Vector3(max.X, min.Y, c.Z), capColor));
                lines.Add(new Line(new Vector3(max.X, min.Y, c.Z), new Vector3(max.X, max.Y, c.Z), capColor));
                lines.Add(new Line(new Vector3(max.X, max.Y, c.Z), new Vector3(min.X, max.Y, c.Z), capColor));
                lines.Add(new Line(new Vector3(min.X, max.Y, c.Z), new Vector3(min.X, min.Y, c.Z), capColor));
            }
        }

        // ─── Plane crossing (modes 1 + 2) ────────────────────────────────────────────
        // The plane is axis-aligned. axisIsX=true → mode 1 (plane at X=constant, extends on Y).
        // axisIsX=false → mode 2 (plane at Y=constant, extends on X).
        //
        // In scene space the axes are swapped from DB — so DB-mode-1 (X-plane) becomes a
        // scene-space plane at Y = row.X, extending along the scene X axis with limits
        // MinVert/MaxVert (which stay in the perpendicular DB axis = scene X axis).
        static void EmitPlaneCrossing(
            ZonePoint zp, Vector4 wire, Vector4 fill,
            bool axisIsX,
            (Vector3 Min, Vector3 Max)? zoneBounds,
            List<Line> lines, List<Tri> tris)
        {
            var c = zp.SceneCenter;

            float zMin, zMax;
            if (zoneBounds.HasValue)
            {
                zMin = zoneBounds.Value.Min.Z - 50f;
                zMax = zoneBounds.Value.Max.Z + 50f;
            }
            else
            {
                zMin = c.Z - PlaneWallHalfHeight;
                zMax = c.Z + PlaneWallHalfHeight;
            }

            // Perpendicular extent bounds: MinVert/MaxVert are in DB space on the axis
            // perpendicular to the plane. For DB mode 1 (X-plane) that's the Y axis;
            // scene-space that axis is the scene X axis. For DB mode 2 (Y-plane) that's
            // the DB X axis → scene Y axis. Either way we call the perpendicular scene
            // axis "u" here.
            float uMin, uMax;
            if (zp.Row.MinVert == 0 && zp.Row.MaxVert == 0 && zoneBounds.HasValue)
            {
                uMin = axisIsX ? zoneBounds.Value.Min.X - 100f : zoneBounds.Value.Min.Y - 100f;
                uMax = axisIsX ? zoneBounds.Value.Max.X + 100f : zoneBounds.Value.Max.Y + 100f;
            }
            else
            {
                uMin = zp.Row.MinVert;
                uMax = zp.Row.MaxVert;
            }

            Vector3 v(float u, float z) => axisIsX
                ? new Vector3(u, c.Y, z)   // DB X-plane → scene wall at Y=c.Y, extending on X
                : new Vector3(c.X, u, z);  // DB Y-plane → scene wall at X=c.X, extending on Y

            var p00 = v(uMin, zMin);
            var p10 = v(uMax, zMin);
            var p11 = v(uMax, zMax);
            var p01 = v(uMin, zMax);

            // Wall wireframe
            lines.Add(new Line(p00, p10, wire));
            lines.Add(new Line(p10, p11, wire));
            lines.Add(new Line(p11, p01, wire));
            lines.Add(new Line(p01, p00, wire));

            // Wall fill (2 tris)
            tris.Add(new Tri(p00, p10, p11, fill));
            tris.Add(new Tri(p00, p11, p01, fill));

            // Direction arrow at plane center. Sign of the trigger's own coord tells us
            // which side fires (per spec: x>=0 → fires on player X>=x). In scene space:
            //   axisIsX (DB-mode-1) — direction is the DB X axis = scene Y axis, sign from DB X.
            //   axisIsY (DB-mode-2) — direction is the DB Y axis = scene X axis, sign from DB Y.
            float sign = axisIsX
                ? System.MathF.Sign(zp.Row.X == 0 ? 1f : zp.Row.X)
                : System.MathF.Sign(zp.Row.Y == 0 ? 1f : zp.Row.Y);
            if (sign == 0) sign = 1f;

            var arrowStart = new Vector3(c.X, c.Y, c.Z);
            var arrowDir = axisIsX ? new Vector3(0, sign, 0) : new Vector3(sign, 0, 0);
            var arrowEnd = arrowStart + arrowDir * ArrowLength;
            var arrowColor = new Vector4(wire.X, wire.Y, wire.Z, 1f);
            lines.Add(new Line(arrowStart, arrowEnd, arrowColor));

            // Simple arrowhead: two short lines splaying back from the tip.
            var perp = axisIsX ? new Vector3(1, 0, 0) : new Vector3(0, 1, 0);
            var head = ArrowLength * 0.25f;
            lines.Add(new Line(arrowEnd, arrowEnd - arrowDir * head + perp * head, arrowColor));
            lines.Add(new Line(arrowEnd, arrowEnd - arrowDir * head - perp * head, arrowColor));
        }

        // ─── Wildcard slab (purple, source has |x| or |y| >= 999998) ─────────────────
        static void EmitWildcardSlab(
            ZonePoint zp, Vector4 wire, Vector4 fill,
            (Vector3 Min, Vector3 Max)? zoneBounds,
            List<Line> lines, List<Tri> tris)
        {
            // Center on the real (non-wildcard) axis if there is one; otherwise fall back
            // to the zone-bounds center. In scene space the row's SceneCenter uses the
            // sentinel value for the wildcarded axes — replace those with bounds.
            var c = zp.SceneCenter;
            float cx = c.X, cy = c.Y;
            if (zoneBounds.HasValue)
            {
                var b = zoneBounds.Value;
                if (ZonePointWildcards.IsWildcard(zp.Row.Y)) cx = (b.Min.X + b.Max.X) * 0.5f; // DB Y = scene X
                if (ZonePointWildcards.IsWildcard(zp.Row.X)) cy = (b.Min.Y + b.Max.Y) * 0.5f; // DB X = scene Y
            }
            else
            {
                if (ZonePointWildcards.IsWildcard(zp.Row.Y)) cx = 0f;
                if (ZonePointWildcards.IsWildcard(zp.Row.X)) cy = 0f;
            }

            float xMin, xMax, yMin, yMax;
            if (zoneBounds.HasValue)
            {
                var b = zoneBounds.Value;
                xMin = b.Min.X - 50f; xMax = b.Max.X + 50f;
                yMin = b.Min.Y - 50f; yMax = b.Max.Y + 50f;
            }
            else
            {
                xMin = cx - 1000f; xMax = cx + 1000f;
                yMin = cy - 1000f; yMax = cy + 1000f;
            }

            float zHalf = zp.Row.MaxZDiff == 0 ? WildcardSlabDefaultZ : System.MathF.Max(1f, zp.Row.MaxZDiff);
            float zMin = zp.Row.Z - zHalf;
            float zMax = zp.Row.Z + zHalf;

            var min = new Vector3(xMin, yMin, zMin);
            var max = new Vector3(xMax, yMax, zMax);
            EmitAabbLines(min, max, wire, lines);
            EmitAabbTris (min, max, fill, tris);
        }

        // ─── Incoming landing pad (foreign row landing INTO this zone) ───────────────
        // Renders a hexagonal ground pad at the landing coord + an arrow oriented by the
        // row's heading (the direction arriving players face). Cyan tint distinguishes
        // it from the current zone's owned rows.
        //
        // trilogy_zone_points.heading is on the 0-255 Trilogy scale; scale to the 0-512
        // EQEmu convention SpawnManager uses so both spawns and incoming pads read the
        // same "facing this direction" cue at a given heading value.
        static void EmitIncomingPad(ZonePoint zp, bool selected, List<Line> lines, List<Tri> tris)
        {
            var center = zp.SceneTarget;
            const float padRadius   = 6f;
            const float padLiftZ    = 0.5f;  // float slightly above ground so the fill isn't z-fighting
            const float arrowLength = 20f;

            Vector4 wire, fill;
            if (selected)
            {
                wire = new Vector4(0.55f, 1.00f, 1.00f, 1.00f);
                fill = new Vector4(0.30f, 0.95f, 1.00f, 0.45f);
            }
            else
            {
                wire = new Vector4(0.30f, 0.90f, 1.00f, 0.85f);
                fill = new Vector4(0.20f, 0.85f, 1.00f, 0.30f);
            }

            // Hexagon vertices in scene space.
            var padCenter = new Vector3(center.X, center.Y, center.Z + padLiftZ);
            var padVerts = new Vector3[6];
            for (int i = 0; i < 6; i++)
            {
                var a = i * (System.MathF.PI * 2f / 6f);
                padVerts[i] = padCenter + new Vector3(padRadius * System.MathF.Cos(a), padRadius * System.MathF.Sin(a), 0);
            }
            for (int i = 0; i < 6; i++)
            {
                var next = padVerts[(i + 1) % 6];
                lines.Add(new Line(padVerts[i], next, wire));
                tris.Add(new Tri(padCenter, padVerts[i], next, fill));
            }

            // Heading arrow. trilogy_zone_points.heading is on the 0-255 wire scale;
            // the server does heading * 2 at fire time (trilogy_client.cpp:1919) to get
            // its 0-512 internal value. HeadingToRotation expects 0-512, so match the
            // server: ×2 to convert wire → server.
            var rot           = VisualEQ.SpawnSystem.SpawnManager.HeadingToRotation(zp.Row.Heading * 2f);
            var forward       = Vector3.Normalize(Vector3.Transform(new Vector3(0, 1, 0), rot));
            var arrowStart    = padCenter;
            var arrowEnd      = arrowStart + forward * arrowLength;

            lines.Add(new Line(arrowStart, arrowEnd, wire));
            // Simple arrowhead: two short lines splaying back from the tip.
            var perp = new Vector3(-forward.Y, forward.X, 0);   // 90° CCW around Z
            var head = arrowLength * 0.28f;
            lines.Add(new Line(arrowEnd, arrowEnd - forward * head + perp * head, wire));
            lines.Add(new Line(arrowEnd, arrowEnd - forward * head - perp * head, wire));

            // Vertical stalk above the pad so it's visible from far away and gives the
            // sidebar list a hoverable/click-anchor point.
            var stalkColor = new Vector4(wire.X, wire.Y, wire.Z, 0.60f);
            lines.Add(new Line(padCenter, padCenter + new Vector3(0, 0, 18f), stalkColor));
        }

        // ─── AABB helpers ────────────────────────────────────────────────────────────
        static void EmitAabbLines(Vector3 min, Vector3 max, Vector4 color, List<Line> lines)
        {
            var v000 = new Vector3(min.X, min.Y, min.Z);
            var v100 = new Vector3(max.X, min.Y, min.Z);
            var v110 = new Vector3(max.X, max.Y, min.Z);
            var v010 = new Vector3(min.X, max.Y, min.Z);
            var v001 = new Vector3(min.X, min.Y, max.Z);
            var v101 = new Vector3(max.X, min.Y, max.Z);
            var v111 = new Vector3(max.X, max.Y, max.Z);
            var v011 = new Vector3(min.X, max.Y, max.Z);

            lines.Add(new Line(v000, v100, color));
            lines.Add(new Line(v100, v110, color));
            lines.Add(new Line(v110, v010, color));
            lines.Add(new Line(v010, v000, color));

            lines.Add(new Line(v001, v101, color));
            lines.Add(new Line(v101, v111, color));
            lines.Add(new Line(v111, v011, color));
            lines.Add(new Line(v011, v001, color));

            lines.Add(new Line(v000, v001, color));
            lines.Add(new Line(v100, v101, color));
            lines.Add(new Line(v110, v111, color));
            lines.Add(new Line(v010, v011, color));
        }

        static void EmitAabbTris(Vector3 min, Vector3 max, Vector4 color, List<Tri> tris)
        {
            var v000 = new Vector3(min.X, min.Y, min.Z);
            var v100 = new Vector3(max.X, min.Y, min.Z);
            var v110 = new Vector3(max.X, max.Y, min.Z);
            var v010 = new Vector3(min.X, max.Y, min.Z);
            var v001 = new Vector3(min.X, min.Y, max.Z);
            var v101 = new Vector3(max.X, min.Y, max.Z);
            var v111 = new Vector3(max.X, max.Y, max.Z);
            var v011 = new Vector3(min.X, max.Y, max.Z);

            // Bottom (z = min.Z)
            tris.Add(new Tri(v000, v100, v110, color));
            tris.Add(new Tri(v000, v110, v010, color));
            // Top (z = max.Z)
            tris.Add(new Tri(v001, v111, v101, color));
            tris.Add(new Tri(v001, v011, v111, color));
            // -Y (y = min.Y)
            tris.Add(new Tri(v000, v101, v100, color));
            tris.Add(new Tri(v000, v001, v101, color));
            // +Y (y = max.Y)
            tris.Add(new Tri(v010, v110, v111, color));
            tris.Add(new Tri(v010, v111, v011, color));
            // -X (x = min.X)
            tris.Add(new Tri(v000, v010, v011, color));
            tris.Add(new Tri(v000, v011, v001, color));
            // +X (x = max.X)
            tris.Add(new Tri(v100, v111, v110, color));
            tris.Add(new Tri(v100, v101, v111, color));
        }

        // Bright red palette for sandwiched rows — overrides the normal health coloring
        // so a Green health row that's ALSO sandwiched still reads as "danger" at a glance.
        static (Vector4 wire, Vector4 fill) SandwichColors(bool selected)
        {
            var rgb = new Vector3(1.0f, 0.20f, 0.20f);
            return (
                new Vector4(rgb, selected ? 1.0f : 0.95f),
                new Vector4(rgb, selected ? 0.32f : 0.25f)
            );
        }

        static (Vector4 wire, Vector4 fill) ColorsFor(ZonePointHealth health, bool selected)
        {
            Vector3 rgb;
            float fillA = FillAlpha;
            switch (health)
            {
                case ZonePointHealth.Green:  rgb = new Vector3(0.30f, 1.00f, 0.30f); break;
                case ZonePointHealth.Yellow: rgb = new Vector3(1.00f, 0.90f, 0.20f); break;
                case ZonePointHealth.Purple: rgb = new Vector3(0.75f, 0.40f, 1.00f); break;
                case ZonePointHealth.Red:    rgb = new Vector3(1.00f, 0.25f, 0.25f); fillA = 0.22f; break;
                default:                     rgb = new Vector3(0.70f, 0.70f, 0.70f); break;
            }

            var wireAlpha = selected ? 1.0f : 0.85f;
            var fillAlpha = selected ? fillA + 0.10f : fillA;
            return (new Vector4(rgb, wireAlpha), new Vector4(rgb, fillAlpha));
        }
    }
}
