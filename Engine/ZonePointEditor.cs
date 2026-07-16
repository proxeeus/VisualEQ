using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using static VisualEQ.Engine.Globals;

namespace VisualEQ.Engine
{
    // Hit-test + drag for trilogy_zone_point handles. Runs alongside ModelSelector and
    // WaypointEditor — clicks near a zone-point handle take precedence over spawn clicks
    // so the small handle icons never get swallowed by an overlapping spawn model.
    //
    // Interaction model:
    //   • Every zone point renders a small center-handle cross → click to select + start
    //     a center-drag (moves x/y/z).
    //   • The currently-selected zone point additionally renders four XY-corner handles
    //     (top + bottom = 8 total) → drag any corner to resize Zrange. Because Zrange is
    //     a single scalar, the box stays square — the drag uses the max of |dx|/|dy|
    //     from the center as the new half-extent.
    //   • The selected zone point renders two Z-face handles (top + bottom face center) →
    //     drag to resize MaxZDiff (Z tolerance).
    //   • Plane crossings (UseNewZoning 1 or 2) currently only expose the center handle;
    //     end-cap / MinVert / MaxVert handles come with the inspector slice.
    //
    // Handle math + drag threshold + camera-plane fallback mirror WaypointEditor exactly.
    public class ZonePointEditor
    {
        public enum HandleKind
        {
            Center,
            CornerXmYm,   // -X, -Y (bottom-face corner is the same XY column with different Z)
            CornerXpYm,   // +X, -Y
            CornerXpYp,   // +X, +Y
            CornerXmYp,   // -X, +Y
            FaceZm,       // bottom face center
            FaceZp,       // top face center
            PlaneEndMinVert, // plane-crossing end-cap at MinVert (drag → adjusts MinVert)
            PlaneEndMaxVert, // plane-crossing end-cap at MaxVert (drag → adjusts MaxVert)
        }

        public struct Handle : IEquatable<Handle>
        {
            public int ZonePointId;
            public HandleKind Kind;
            public Vector3 ScenePos;
            // For corner handles we also carry the corresponding Z (top/bottom) so drag
            // math and rendering stay consistent when MaxZDiff changes mid-drag.
            public bool IsTop;
            // Incoming landing-pad handles get priority in TrySelect so a pad sitting
            // inside/beneath an owned box is still clickable — otherwise the box's
            // center handle (closer to camera) would always win.
            public bool IsIncoming;

            public bool Equals(Handle other) =>
                ZonePointId == other.ZonePointId && Kind == other.Kind && IsTop == other.IsTop;
            public override bool Equals(object obj) => obj is Handle h && Equals(h);
            public override int GetHashCode() => (ZonePointId, Kind, IsTop).GetHashCode();
        }

        readonly EngineCore _engine;

        readonly List<Handle> _candidates = new List<Handle>();

        // Body candidates — box AABBs that participate in the click hit-test as a
        // FALLBACK after the screen-space handle test. Clicking anywhere on/inside a
        // box now selects it as if you'd clicked its center handle, so clicks don't
        // fall through to the ModelSelector (which grabs distant NPCs with its own
        // depth-scaled hit sphere).
        public struct BoxBody
        {
            public int ZonePointId;
            public Vector3 Min;
            public Vector3 Max;
            public Vector3 Center;   // scene-space; drives the resulting Handle.ScenePos
        }
        readonly List<BoxBody> _bodyCandidates = new List<BoxBody>();
        public void SetBodyCandidates(IEnumerable<BoxBody> bodies)
        {
            _bodyCandidates.Clear();
            _bodyCandidates.AddRange(bodies);
        }

        Handle? _selected;
        public Handle? SelectedHandle => _selected;

        public bool IsDragging => _isDragging;
        public bool EditModeEnabled = false;

        // Fires when the user clicks a center handle on a zone point that isn't the
        // currently-selected one — Controller consumes to update ZonePointManager.Selected.
        public event Action<int> OnZonePointClicked;

        // Fires once per successful drag with the from/to values for whichever field
        // the drag was actually manipulating. Signature is wide because a single
        // callback covers every handle kind (center → position, corner/face → box
        // size, plane end-cap → plane bound). The Controller dispatches on `kind` and
        // records the appropriate action.
        public struct DragResult
        {
            public HandleKind Kind;
            public int ZonePointId;
            public Vector3 FromCenter;
            public Vector3 ToCenter;
            public int FromZrange;
            public int ToZrange;
            public int FromMaxZDiff;
            public int ToMaxZDiff;
            public float FromMinVert;
            public float ToMinVert;
            public float FromMaxVert;
            public float ToMaxVert;
        }
        public event Action<DragResult> OnDragCompleted;

        bool _isDragging;
        bool _dragPending;
        int _dragStartMouseX, _dragStartMouseY;

        // Center-drag state (mirrors WaypointEditor).
        Vector3 _dragStartCenter;
        Vector3 _dragPlaneNormal;
        float _dragPlaneDistance;
        Vector3 _dragOffset;
        Vector2 _dragHorizOffset;
        float _dragGroundOffset;
        float _wheelZOffset;
        bool _useCameraPlane;

        // Resize-drag state: capture the row's center at drag start so we can compute
        // half-extents in world space from the mouse-on-plane point.
        int _dragStartZrange;
        int _dragStartMaxZDiff;
        float _dragStartMinVert;
        float _dragStartMaxVert;
        Vector3 _dragStartCornerPos;   // world-space position of the grabbed corner at drag start
        byte _dragStartMode;           // captured so plane-end drag knows which axis to move along

        // Current (live) values while dragging — kept so cancel restores originals.
        Vector3 _currentCenter;
        int _currentZrange;
        int _currentMaxZDiff;
        float _currentMinVert;
        float _currentMaxVert;

        const int DragThresholdPixels = 8;
        const float DefaultGroundOffset = 0.1f;
        const float HighProbeAltitude = 5000f;

        public ZonePointEditor(EngineCore engine)
        {
            _engine = engine;
        }

        // Called each frame from Controller.UpdateZonePoints so we know what to hit-test.
        // Candidates always include every zone point's Center handle; the currently
        // "editor-selected" zone point (matches ZonePointManager.Selected) also contributes
        // corner + face handles.
        public void SetCandidates(IEnumerable<Handle> candidates)
        {
            _candidates.Clear();
            _candidates.AddRange(candidates);
            // If our stored selection no longer appears in the candidate set (zone unload,
            // selection cleared elsewhere), drop it so state doesn't linger.
            if (_selected.HasValue && !_candidates.Any(c => c.Equals(_selected.Value)))
                _selected = null;
        }

        public void ClearSelection()
        {
            _selected = null;
        }

        // Hybrid hit-test:
        //   1. World-space ray/sphere filters to "which handles is the click ray roughly
        //      pointing at?" (uses a generous depth-scaled world radius so distant handles
        //      don't require pixel-perfect aim).
        //   2. Screen-space distance-to-cursor picks the winner among those. Whichever
        //      glyph is visually closest to the mouse cursor wins — matches user vision
        //      regardless of world-space depth ordering.
        //
        // Fallback: ray/AABB test on box bodies so clicking anywhere on a box's volume
        // selects it (prevents falling through to ModelSelector's depth-scaled hit
        // spheres that grab NPCs across the map).
        public bool TrySelect(int mouseX, int mouseY)
        {
            if (_candidates.Count == 0 && _bodyCandidates.Count == 0) return false;

            var mouse     = new Vector2(mouseX, mouseY);
            var rayOrigin = Camera.Position + new Vector3(0, 0, FpsCamera.CameraHeight);
            var rayDir    = ScreenToWorldRay(mouseX, mouseY);

            // Ray/sphere world-space picker with per-kind radii (see BaseRadiusFor).
            // Reduced from the old formula (14/20 base, 0.005 scale) — was letting
            // distant handles occupy huge world volumes and steal clicks meant for
            // nearby handles. Screen-space pixel distance to the mouse still ranks
            // survivors so nearby-in-screen wins over merely-inside-sphere.
            //
            // Two passes preserve the "incoming pads take priority over owned handles
            // sitting on top of them" invariant — a landing pad inside an owned box
            // remains clickable even when the box's center handle is closer to camera.
            Handle? best = PickBestHandle(_candidates.Where(h => h.IsIncoming),
                mouse, rayOrigin, rayDir);
            if (!best.HasValue)
                best = PickBestHandle(_candidates.Where(h => !h.IsIncoming),
                    mouse, rayOrigin, rayDir);

            if (best.HasValue)
            {
                _selected = best;
                if (best.Value.Kind == HandleKind.Center)
                    OnZonePointClicked?.Invoke(best.Value.ZonePointId);
                return true;
            }

            // No handle hit — try box bodies. Clicking anywhere inside/on a box's
            // wireframe/fill selects it as if you'd clicked its center handle.
            BoxBody? bestBody = null;
            float bestBodyT = float.MaxValue;
            foreach (var b in _bodyCandidates)
            {
                if (RayIntersectsAabb(rayOrigin, rayDir, b.Min, b.Max, out var tHit) && tHit < bestBodyT)
                {
                    bestBody = b;
                    bestBodyT = tHit;
                }
            }
            if (bestBody.HasValue)
            {
                var b = bestBody.Value;
                _selected = new Handle { ZonePointId = b.ZonePointId, Kind = HandleKind.Center, ScenePos = b.Center };
                OnZonePointClicked?.Invoke(b.ZonePointId);
                return true;
            }
            return false;
        }

        // Standard slab-method ray/AABB test. tHit is the entry distance along the ray
        // (may be negative if the origin is inside the box — treat as t=0).
        static bool RayIntersectsAabb(Vector3 origin, Vector3 dir, Vector3 min, Vector3 max, out float tHit)
        {
            float tmin = float.NegativeInfinity;
            float tmax = float.PositiveInfinity;
            for (int i = 0; i < 3; i++)
            {
                float o = i == 0 ? origin.X : (i == 1 ? origin.Y : origin.Z);
                float d = i == 0 ? dir.X    : (i == 1 ? dir.Y    : dir.Z);
                float lo = i == 0 ? min.X   : (i == 1 ? min.Y   : min.Z);
                float hi = i == 0 ? max.X   : (i == 1 ? max.Y   : max.Z);
                if (Math.Abs(d) < 1e-8f)
                {
                    if (o < lo || o > hi) { tHit = 0; return false; }
                    continue;
                }
                float t1 = (lo - o) / d;
                float t2 = (hi - o) / d;
                if (t1 > t2) { var tmp = t1; t1 = t2; t2 = tmp; }
                if (t1 > tmin) tmin = t1;
                if (t2 < tmax) tmax = t2;
                if (tmin > tmax) { tHit = 0; return false; }
            }
            tHit = tmin > 0 ? tmin : 0f;
            return tmax >= 0;
        }

        // Ray/sphere test with screen-space tiebreak. Ray/sphere filter first (fast,
        // and rejects handles well off the click direction), then pixel distance
        // ranks candidates so the visually-closest one wins. Reused across the
        // incoming-pads pass and the owned-handles pass so the two share tolerances.
        Handle? PickBestHandle(
            System.Collections.Generic.IEnumerable<Handle> candidates,
            Vector2 mouse, Vector3 rayOrigin, Vector3 rayDir)
        {
            Handle? best = null;
            float bestScreenDist2 = float.MaxValue;

            foreach (var h in candidates)
            {
                var to = h.ScenePos - rayOrigin;
                var proj = Vector3.Dot(to, rayDir);
                if (proj < 0) continue;

                var worldRadius = BaseRadiusFor(h.Kind, h.IsIncoming) + proj * 0.005f;
                var closestPoint = rayOrigin + rayDir * proj;
                var d2 = Vector3.DistanceSquared(closestPoint, h.ScenePos);
                if (d2 >= worldRadius * worldRadius) continue;

                if (!TryProjectToScreen(h.ScenePos, out var screenPos)) continue;
                var screenDist2 = (screenPos - mouse).LengthSquared();
                if (screenDist2 < bestScreenDist2)
                {
                    best = h;
                    bestScreenDist2 = screenDist2;
                }
            }
            return best;
        }

        // Per-kind base world-space hit radius. Reduced from the old 14/20 defaults —
        // still generous enough to make handles easy to click at typical camera
        // distance, but small enough that distant handles don't reach across the
        // scene. Distance scale (proj * 0.005) applied at the callsite adds a small
        // "distant handles don't need pixel-perfect aim" allowance.
        static float BaseRadiusFor(HandleKind kind, bool isIncoming)
        {
            if (isIncoming) return 10f;   // was 20 — still the largest to preserve pad priority
            switch (kind)
            {
                case HandleKind.Center: return 6f;   // was 14
                default:                return 4f;   // was 10 — corners / faces / plane end-caps
            }
        }

        // Projects a world position to screen pixel coords. Returns false if the point
        // is behind the camera (perspective divide would flip signs).
        bool TryProjectToScreen(Vector3 worldPos, out Vector2 screen)
        {
            var clip = System.Numerics.Vector4.Transform(new System.Numerics.Vector4(worldPos, 1f), FpsCamera.Matrix * ProjectionMat);
            if (clip.W <= 0.0001f) { screen = default; return false; }
            var ndcX = clip.X / clip.W;
            var ndcY = clip.Y / clip.W;
            // NDC (-1..+1) → pixel (0..W, 0..H). Y flips because pixel space is top-down.
            var px = (ndcX * 0.5f + 0.5f) * _engine.Width;
            var py = (1f - (ndcY * 0.5f + 0.5f)) * _engine.Height;
            screen = new Vector2(px, py);
            return true;
        }

        public bool StartDrag(int mouseX, int mouseY)
        {
            if (!EditModeEnabled) return false;
            if (_selected == null) return false;

            _dragPending = true;
            _dragStartMouseX = mouseX;
            _dragStartMouseY = mouseY;
            return true;
        }

        // Kicks off the actual drag once the mouse has moved past the threshold. The
        // caller (Controller) needs to hand us the live ZonePoint reference so we can
        // read/write its row + fire the completion event with accurate before/after values.
        public delegate ZonePointDragTarget ZonePointLookup(int zonePointId);

        // Bag of the mutable + immutable state the editor needs about a zone point during
        // a drag. Controller supplies this via a lookup delegate at drag start.
        public class ZonePointDragTarget
        {
            public int Id;
            public Vector3 SceneCenter;
            public int Zrange;
            public int MaxZDiff;
            public float MinVert;
            public float MaxVert;
            public byte UseNewZoning;
            // Callbacks so the editor never touches the domain object directly.
            public Action<Vector3> ApplyMove;      // live-move during center drag
            public Action<int, int> ApplyResize;   // (zrange, maxZDiff) during resize drag
            public Action<float, float> ApplyPlaneBounds; // (minVert, maxVert) during end-cap drag
        }

        ZonePointLookup _lookup;
        ZonePointDragTarget _target;

        // Controller wires this once at startup. Editor never holds a strong ref to the
        // manager or the domain layer.
        public void SetLookup(ZonePointLookup lookup) => _lookup = lookup;

        public bool UpdateDrag(int mouseX, int mouseY)
        {
            if (_selected == null) return false;

            if (_dragPending)
            {
                int dx = mouseX - _dragStartMouseX;
                int dy = mouseY - _dragStartMouseY;
                if (dx * dx + dy * dy < DragThresholdPixels * DragThresholdPixels)
                    return false;
                _dragPending = false;
                if (!BeginActualDrag(mouseX, mouseY)) return false;
            }
            if (!_isDragging || _target == null) return false;

            var eye = Camera.Position + new Vector3(0, 0, FpsCamera.CameraHeight);
            var rayDir = ScreenToWorldRay(mouseX, mouseY);

            switch (_selected.Value.Kind)
            {
                case HandleKind.Center:
                    UpdateCenterDrag(eye, rayDir);
                    break;
                case HandleKind.CornerXmYm:
                case HandleKind.CornerXpYm:
                case HandleKind.CornerXpYp:
                case HandleKind.CornerXmYp:
                    UpdateCornerDrag(eye, rayDir);
                    break;
                case HandleKind.FaceZm:
                case HandleKind.FaceZp:
                    UpdateFaceDrag(eye, rayDir);
                    break;
                case HandleKind.PlaneEndMinVert:
                case HandleKind.PlaneEndMaxVert:
                    UpdatePlaneEndDrag(eye, rayDir);
                    break;
            }

            return true;
        }

        // Drag a plane-crossing end-cap. Mode 1 (X-plane) extends along DB Y = scene X;
        // mode 2 (Y-plane) extends along DB X = scene Y. The new MinVert/MaxVert is the
        // scene-projected coordinate along that perpendicular axis.
        void UpdatePlaneEndDrag(Vector3 eye, Vector3 rayDir)
        {
            var groundNormal = new Vector3(0, 0, 1);
            var tPlane = IntersectRayPlane(eye, rayDir, groundNormal, _dragStartCenter.Z);
            if (tPlane <= 0) return;
            var hit = eye + rayDir * tPlane;

            // Which scene axis carries the perpendicular value? Mode 1 → scene X, mode 2 → scene Y.
            float value;
            if (_dragStartMode == 1) value = hit.X;
            else                     value = hit.Y;

            if (_selected.Value.Kind == HandleKind.PlaneEndMinVert) _currentMinVert = value;
            else                                                    _currentMaxVert = value;

            _target.ApplyPlaneBounds?.Invoke(_currentMinVert, _currentMaxVert);
        }

        void UpdateCenterDrag(Vector3 eye, Vector3 rayDir)
        {
            Vector3 newPos;
            if (_useCameraPlane)
            {
                var t = IntersectRayPlane(eye, rayDir, _dragPlaneNormal, _dragPlaneDistance);
                if (t <= 0) return;
                newPos = eye + rayDir * t + _dragOffset;
            }
            else
            {
                var groundNormal = new Vector3(0, 0, 1);
                var tPlane = IntersectRayPlane(eye, rayDir, groundNormal, _dragStartCenter.Z);
                if (tPlane <= 0) return;

                var xyProj = eye + rayDir * tPlane;
                var targetX = xyProj.X + _dragHorizOffset.X;
                var targetY = xyProj.Y + _dragHorizOffset.Y;

                float targetZ = _dragStartCenter.Z + _wheelZOffset;
                if (Collider != null)
                {
                    var probe = new Vector3(targetX, targetY, HighProbeAltitude);
                    var hit = Collider.FindIntersection(probe, new Vector3(0, 0, -1), 0.5f);
                    if (hit.HasValue) targetZ = hit.Value.Item2.Z + _dragGroundOffset + _wheelZOffset;
                }
                newPos = new Vector3(targetX, targetY, targetZ);
            }
            _currentCenter = newPos;
            _target.ApplyMove?.Invoke(newPos);
        }

        void UpdateCornerDrag(Vector3 eye, Vector3 rayDir)
        {
            // Project drag onto the ground plane at the row's center Z. The new Zrange
            // is the max of |dx|, |dy| from center to the mouse — Zrange is square, so
            // both XY halves match.
            var groundNormal = new Vector3(0, 0, 1);
            var tPlane = IntersectRayPlane(eye, rayDir, groundNormal, _dragStartCenter.Z);
            if (tPlane <= 0) return;
            var xy = eye + rayDir * tPlane;
            var dx = System.MathF.Abs(xy.X - _dragStartCenter.X);
            var dy = System.MathF.Abs(xy.Y - _dragStartCenter.Y);
            var newZrange = (int)System.MathF.Max(1f, System.MathF.Max(dx, dy));
            _currentZrange = newZrange;
            _target.ApplyResize?.Invoke(_currentZrange, _currentMaxZDiff);
        }

        void UpdateFaceDrag(Vector3 eye, Vector3 rayDir)
        {
            // Project onto a camera-facing vertical plane that passes through the row's
            // center. New MaxZDiff is |mouse.Z - center.Z|. Wheel adjustments not exposed
            // here — face-drag is already purely 1D.
            var forward = Vector3.Normalize(Vector3.Transform(FpsCamera.Forward, Camera.LookRotation));
            // Zero the vertical component so the drag plane is truly vertical (Z-only motion).
            var normal = new Vector3(forward.X, forward.Y, 0);
            if (normal.LengthSquared() < 0.0001f) normal = new Vector3(1, 0, 0);
            normal = Vector3.Normalize(normal);

            var planeDist = Vector3.Dot(normal, _dragStartCenter);
            var t = IntersectRayPlane(eye, rayDir, normal, planeDist);
            if (t <= 0) return;
            var hit = eye + rayDir * t;

            var newMaxZDiff = (int)System.MathF.Max(1f, System.MathF.Abs(hit.Z - _dragStartCenter.Z));
            _currentMaxZDiff = newMaxZDiff;
            _target.ApplyResize?.Invoke(_currentZrange, _currentMaxZDiff);
        }

        bool BeginActualDrag(int mouseX, int mouseY)
        {
            if (_lookup == null) return false;
            _target = _lookup(_selected.Value.ZonePointId);
            if (_target == null) return false;

            _dragStartCenter   = _target.SceneCenter;
            _dragStartZrange   = _target.Zrange;
            _dragStartMaxZDiff = _target.MaxZDiff;
            _dragStartMinVert  = _target.MinVert;
            _dragStartMaxVert  = _target.MaxVert;
            _dragStartMode     = _target.UseNewZoning;
            _currentCenter     = _dragStartCenter;
            _currentZrange     = _dragStartZrange;
            _currentMaxZDiff   = _dragStartMaxZDiff;
            _currentMinVert    = _dragStartMinVert;
            _currentMaxVert    = _dragStartMaxVert;
            _dragStartCornerPos = _selected.Value.ScenePos;

            _useCameraPlane = _engine.GetPressedKeys().Contains(OpenTK.Input.Key.AltLeft)
                            || _engine.GetPressedKeys().Contains(OpenTK.Input.Key.AltRight);
            _wheelZOffset = 0f;
            _dragHorizOffset = Vector2.Zero;
            _dragGroundOffset = DefaultGroundOffset;

            // Only center-drag needs plane-intersection setup; resize drags compute from
            // the raw mouse ray each frame.
            if (_selected.Value.Kind == HandleKind.Center)
            {
                var eye = Camera.Position + new Vector3(0, 0, FpsCamera.CameraHeight);
                var rayDir = ScreenToWorldRay(mouseX, mouseY);

                if (_useCameraPlane)
                {
                    var forward = Vector3.Normalize(Vector3.Transform(FpsCamera.Forward, Camera.LookRotation));
                    _dragPlaneNormal = forward;
                    _dragPlaneDistance = Vector3.Dot(_dragPlaneNormal, _dragStartCenter);

                    var t = IntersectRayPlane(eye, rayDir, _dragPlaneNormal, _dragPlaneDistance);
                    if (t <= 0) return false;
                    var hitPoint = eye + rayDir * t;
                    _dragOffset = _dragStartCenter - hitPoint;
                }
                else
                {
                    var groundNormal = new Vector3(0, 0, 1);
                    var tPlane = IntersectRayPlane(eye, rayDir, groundNormal, _dragStartCenter.Z);
                    if (tPlane > 0)
                    {
                        var xyProj = eye + rayDir * tPlane;
                        _dragHorizOffset = new Vector2(_dragStartCenter.X - xyProj.X, _dragStartCenter.Y - xyProj.Y);
                    }
                    if (Collider != null)
                    {
                        var probe = new Vector3(_dragStartCenter.X, _dragStartCenter.Y, HighProbeAltitude);
                        var below = Collider.FindIntersection(probe, new Vector3(0, 0, -1), 0.5f);
                        if (below.HasValue) _dragGroundOffset = _dragStartCenter.Z - below.Value.Item2.Z;
                    }
                }
            }

            _isDragging = true;
            return true;
        }

        public void AdjustDragDepth(float amount)
        {
            if (!_isDragging || _selected == null) return;
            if (_selected.Value.Kind != HandleKind.Center) return;
            if (_useCameraPlane) return;

            var distance = Vector3.Distance(Camera.Position, _currentCenter);
            var scaledAmount = amount * Math.Max(0.05f, distance / 500f);
            _wheelZOffset += scaledAmount;
            _currentCenter = new Vector3(_currentCenter.X, _currentCenter.Y, _currentCenter.Z + scaledAmount);
            _target?.ApplyMove?.Invoke(_currentCenter);
        }

        public void StopDrag()
        {
            if (_isDragging && _selected.HasValue && _target != null)
            {
                OnDragCompleted?.Invoke(new DragResult
                {
                    Kind         = _selected.Value.Kind,
                    ZonePointId  = _selected.Value.ZonePointId,
                    FromCenter   = _dragStartCenter,
                    ToCenter     = _currentCenter,
                    FromZrange   = _dragStartZrange,
                    ToZrange     = _currentZrange,
                    FromMaxZDiff = _dragStartMaxZDiff,
                    ToMaxZDiff   = _currentMaxZDiff,
                    FromMinVert  = _dragStartMinVert,
                    ToMinVert    = _currentMinVert,
                    FromMaxVert  = _dragStartMaxVert,
                    ToMaxVert    = _currentMaxVert,
                });
            }
            _isDragging = false;
            _dragPending = false;
            _target = null;
        }

        public void CancelDrag()
        {
            if (_isDragging && _selected.HasValue && _target != null)
            {
                // Restore the live values.
                _target.ApplyMove?.Invoke(_dragStartCenter);
                _target.ApplyResize?.Invoke(_dragStartZrange, _dragStartMaxZDiff);
                _target.ApplyPlaneBounds?.Invoke(_dragStartMinVert, _dragStartMaxVert);
            }
            _isDragging = false;
            _dragPending = false;
            _target = null;
        }

        // ─── Shared math (kept private-duplicated, matching WaypointEditor). ─────────
        Vector3 ScreenToWorldRay(int mouseX, int mouseY)
        {
            float ndcX = 2f * mouseX / _engine.Width - 1f;
            float ndcY = 1f - 2f * mouseY / _engine.Height;
            var rayClip = new Vector4(ndcX, ndcY, -1f, 1f);
            Matrix4x4.Invert(ProjectionMat, out var invProj);
            var rayView = Vector4.Transform(rayClip, invProj);
            rayView = new Vector4(rayView.X, rayView.Y, -1f, 0f);
            Matrix4x4.Invert(FpsCamera.Matrix, out var invView);
            var rayWorld = Vector4.Transform(rayView, invView);
            return Vector3.Normalize(new Vector3(rayWorld.X, rayWorld.Y, rayWorld.Z));
        }

        static float IntersectRayPlane(Vector3 rayOrigin, Vector3 rayDir, Vector3 planeNormal, float planeDistance)
        {
            var denom = Vector3.Dot(rayDir, planeNormal);
            if (Math.Abs(denom) < 0.0001f) return -1f;
            return (planeDistance - Vector3.Dot(rayOrigin, planeNormal)) / denom;
        }
    }
}
