using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using static VisualEQ.Engine.Globals;

namespace VisualEQ.Engine
{
    // Selection + drag for grid-path waypoints. Runs alongside ModelSelector — when a spawn
    // is selected and the click is close to one of that spawn's waypoints, WaypointEditor
    // takes precedence over ModelSelector for the drag. Otherwise it stays out of the way.
    //
    // Only the currently-visible waypoints (Phase 6 renders one grid at a time — the
    // selected spawn's) are candidates. Controller repopulates the candidate list from
    // its per-frame UpdatePathGrids pass.
    public class WaypointEditor
    {
        public struct Handle : IEquatable<Handle>
        {
            public int GridId;
            public int Number;
            public Vector3 ScenePos;

            public bool Equals(Handle other) => GridId == other.GridId && Number == other.Number;
            public override bool Equals(object obj) => obj is Handle h && Equals(h);
            public override int GetHashCode() => (GridId, Number).GetHashCode();
        }

        readonly EngineCore _engine;

        // In-flight candidate set (Controller populates each frame).
        readonly List<Handle> _candidates = new List<Handle>();

        Handle? _selected;
        public Handle? Selected => _selected;

        // True once the drag has actually engaged (past the threshold).
        public bool IsDragging => _isDragging;

        // Edit-mode gate, mirrors ModelSelector.
        public bool EditModeEnabled = false;

        // Fires when the selected waypoint changes (or clears to null).
        public event Action<Handle?> OnSelectionChanged;

        // Fires once per successful drag with the from/to scene positions and the
        // (gridId, number) identifier. Controller subscribes to record an edit action.
        public event Action<int, int, Vector3, Vector3> OnDragCompleted;

        // Drag state — copy of the pattern in ModelSelector so tiny mouse jitters don't
        // register as edits.
        bool _isDragging;
        bool _dragPending;
        int _dragStartMouseX, _dragStartMouseY;
        Vector3 _dragStartPosition;
        Vector3 _dragPlaneNormal;
        float _dragPlaneDistance;
        Vector3 _dragOffset;
        Vector3 _currentPosition; // The scene position we've dragged the selected waypoint to.
        bool _useCameraPlane;
        // Recorded at BeginActualDrag — the waypoint's height above ground at drag start.
        // Preserved during ground-plane drag for the same reason as ModelSelector: EQ stores
        // the NPC's hip/center Z at each grid entry, not the terrain Z, so a naïve snap
        // would drop the waypoint into the floor.
        float _dragGroundOffset;
        Vector2 _dragHorizOffset;
        float _wheelZOffset;
        // Sampled once at BeginActualDrag. When true, ground-plane drag skips the downward
        // collision probe — place waypoints below water surface, inside dock recesses, etc.
        // Mirrors _useCameraPlane's sample-once policy so a released Ctrl mid-drag doesn't
        // re-engage the snap.
        bool _freezeZ;
        const int DragThresholdPixels = 8;
        const float DefaultGroundOffset = 0.1f;
        // Downward-probe start Z — see ModelSelector for rationale.
        const float HighProbeAltitude = 5000f;

        public WaypointEditor(EngineCore engine)
        {
            _engine = engine;
        }

        // Called each frame from Controller.UpdatePathGrids so we know what to hit-test.
        public void SetCandidates(IEnumerable<Handle> candidates)
        {
            _candidates.Clear();
            _candidates.AddRange(candidates);
            if (_selected.HasValue && !_candidates.Any(c => c.Equals(_selected.Value)))
                ClearSelection();
        }

        public void ClearSelection()
        {
            if (_selected == null) return;
            _selected = null;
            OnSelectionChanged?.Invoke(null);
        }

        // Ray-cast against candidate waypoints. Selects the closest hit; returns true if
        // anything was hit (so EngineCore.OnMouseDown can decide whether ModelSelector
        // should also run).
        // Screen-space waypoint picker. Mirrors ModelSelector.TrySelect — see the
        // rationale there. Waypoints are small crosshair markers so the pixel radius
        // is tighter than the model picker's; the camera-distance tiebreak still
        // resolves overlapping waypoints (rare but possible when two grids share a
        // node).
        public bool TrySelect(int mouseX, int mouseY)
        {
            if (_candidates.Count == 0) return false;

            const float PixelHitRadius   = 18f;
            const float PixelHitRadiusSq = PixelHitRadius * PixelHitRadius;
            const float PixelTieEpsilonSq = 4f;

            var vp  = FpsCamera.Matrix * ProjectionMat;
            var eye = Camera.Position + new Vector3(0, 0, FpsCamera.CameraHeight);

            Handle? best = null;
            float bestPixelDistSq = PixelHitRadiusSq;
            float bestCameraDistSq = float.MaxValue;

            foreach (var wp in _candidates)
            {
                var clip = Vector4.Transform(new Vector4(wp.ScenePos, 1f), vp);
                if (clip.W <= 0.001f) continue;

                var ndcX = clip.X / clip.W;
                var ndcY = clip.Y / clip.W;
                var screenX = (ndcX * 0.5f + 0.5f) * _engine.Width;
                var screenY = (1f - (ndcY * 0.5f + 0.5f)) * _engine.Height;

                var dx = screenX - mouseX;
                var dy = screenY - mouseY;
                var pixelDistSq = dx * dx + dy * dy;

                if (pixelDistSq > PixelHitRadiusSq) continue;

                var camDistSq = Vector3.DistanceSquared(wp.ScenePos, eye);

                var pixelDelta = pixelDistSq - bestPixelDistSq;
                if (pixelDelta < -PixelTieEpsilonSq ||
                    (System.Math.Abs(pixelDelta) < PixelTieEpsilonSq && camDistSq < bestCameraDistSq))
                {
                    best = wp;
                    bestPixelDistSq = pixelDistSq;
                    bestCameraDistSq = camDistSq;
                }
            }

            if (best.HasValue)
            {
                _selected = best;
                _currentPosition = best.Value.ScenePos;
                OnSelectionChanged?.Invoke(best);
                return true;
            }
            return false;
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
            if (!_isDragging) return false;

            var eye = Camera.Position + new Vector3(0, 0, FpsCamera.CameraHeight);
            var rayDir = ScreenToWorldRay(mouseX, mouseY);
            Vector3 newPos;

            if (_useCameraPlane)
            {
                var t = IntersectRayPlane(eye, rayDir, _dragPlaneNormal, _dragPlaneDistance);
                if (t <= 0) return false;
                newPos = eye + rayDir * t + _dragOffset;
            }
            else
            {
                // Ground-plane: XY from mouse-on-XY-plane projection; Z from downward probe
                // from HIGH altitude (catches wall tops / rooftops correctly).
                var groundNormal = new Vector3(0, 0, 1);
                var tPlane = IntersectRayPlane(eye, rayDir, groundNormal, _dragStartPosition.Z);
                if (tPlane <= 0) return false;

                var xyProj = eye + rayDir * tPlane;
                var targetX = xyProj.X + _dragHorizOffset.X;
                var targetY = xyProj.Y + _dragHorizOffset.Y;

                float targetZ = _dragStartPosition.Z + _wheelZOffset;
                // Ctrl held at drag start = freeze Z; skip the auto-snap probe entirely.
                if (!_freezeZ && Collider != null)
                {
                    var probe = new Vector3(targetX, targetY, HighProbeAltitude);
                    var hit = Collider.FindIntersection(probe, new Vector3(0, 0, -1), 0.5f);
                    if (hit.HasValue)
                        targetZ = hit.Value.Item2.Z + _dragGroundOffset + _wheelZOffset;
                }
                newPos = new Vector3(targetX, targetY, targetZ);
            }

            _currentPosition = newPos;

            var h = _selected.Value;
            h.ScenePos = _currentPosition;
            _selected = h;

            return true;
        }

        bool BeginActualDrag(int mouseX, int mouseY)
        {
            _dragStartPosition = _selected.Value.ScenePos;

            _useCameraPlane = _engine.GetPressedKeys().Contains(OpenTK.Input.Key.AltLeft)
                            || _engine.GetPressedKeys().Contains(OpenTK.Input.Key.AltRight);
            _freezeZ = _engine.GetPressedKeys().Contains(OpenTK.Input.Key.ControlLeft)
                     || _engine.GetPressedKeys().Contains(OpenTK.Input.Key.ControlRight);
            _wheelZOffset = 0f;
            _dragHorizOffset = Vector2.Zero;
            _dragGroundOffset = DefaultGroundOffset;

            var eye = Camera.Position + new Vector3(0, 0, FpsCamera.CameraHeight);
            var rayDir = ScreenToWorldRay(mouseX, mouseY);

            if (_useCameraPlane)
            {
                var forward = Vector3.Normalize(Vector3.Transform(FpsCamera.Forward, Camera.LookRotation));
                _dragPlaneNormal = forward;
                _dragPlaneDistance = Vector3.Dot(_dragPlaneNormal, _dragStartPosition);

                var t = IntersectRayPlane(eye, rayDir, _dragPlaneNormal, _dragPlaneDistance);
                if (t <= 0) return false;
                var hitPoint = eye + rayDir * t;
                _dragOffset = _dragStartPosition - hitPoint;
            }
            else
            {
                // Ground-plane setup: capture horizontal grab-offset via XY-plane projection
                // and vertical ground-offset via a downward probe from high altitude (catches
                // whichever surface — floor or wall top — the waypoint is currently on).
                var groundNormal = new Vector3(0, 0, 1);
                var tPlane = IntersectRayPlane(eye, rayDir, groundNormal, _dragStartPosition.Z);
                if (tPlane > 0)
                {
                    var xyProj = eye + rayDir * tPlane;
                    _dragHorizOffset = new Vector2(_dragStartPosition.X - xyProj.X, _dragStartPosition.Y - xyProj.Y);
                }
                if (Collider != null)
                {
                    var probe = new Vector3(_dragStartPosition.X, _dragStartPosition.Y, HighProbeAltitude);
                    var below = Collider.FindIntersection(probe, new Vector3(0, 0, -1), 0.5f);
                    if (below.HasValue) _dragGroundOffset = _dragStartPosition.Z - below.Value.Item2.Z;
                }
            }

            _isDragging = true;
            return true;
        }

        // Mouse wheel during ground-plane drag adds a free Z offset — user can nudge a
        // waypoint above or below its terrain snap without switching to Alt-drag.
        public void AdjustDragDepth(float amount)
        {
            if (!_isDragging || _selected == null) return;
            if (_useCameraPlane) return; // camera-perp mode has no wheel adjustment

            var distance = Vector3.Distance(Camera.Position, _currentPosition);
            var scaledAmount = amount * Math.Max(0.05f, distance / 500f);
            _wheelZOffset += scaledAmount;

            var h = _selected.Value;
            h.ScenePos = new Vector3(h.ScenePos.X, h.ScenePos.Y, h.ScenePos.Z + scaledAmount);
            _selected = h;
            _currentPosition = h.ScenePos;
        }

        public void StopDrag()
        {
            if (_isDragging && _selected.HasValue)
            {
                var sel = _selected.Value;
                OnDragCompleted?.Invoke(sel.GridId, sel.Number, _dragStartPosition, _currentPosition);
            }
            _isDragging = false;
            _dragPending = false;
            _freezeZ = false;
        }

        // Abort an in-flight drag without recording an edit. Restores the selected handle's
        // ScenePos back to the drag anchor so PathGridRenderer redraws the crosshair at the
        // original location. Called by Escape hotkey.
        public void CancelDrag()
        {
            if (_isDragging && _selected.HasValue)
            {
                var h = _selected.Value;
                h.ScenePos = _dragStartPosition;
                _selected = h;
                _currentPosition = _dragStartPosition;
            }
            _isDragging = false;
            _dragPending = false;
            _freezeZ = false;
        }

        // ─── Shared math with ModelSelector; kept private-duplicated to avoid an intrusive
        // extraction. If a 3rd selector shows up, promote to a helper. ─────────────────
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
