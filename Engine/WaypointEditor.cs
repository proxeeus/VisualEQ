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
        const int DragThresholdPixels = 8;
        const float DefaultGroundOffset = 0.1f;

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
        public bool TrySelect(int mouseX, int mouseY)
        {
            if (_candidates.Count == 0) return false;

            var rayOrigin = Camera.Position + new Vector3(0, 0, FpsCamera.CameraHeight);
            var rayDir = ScreenToWorldRay(mouseX, mouseY);

            Handle? closest = null;
            float bestProj = float.MaxValue;

            foreach (var wp in _candidates)
            {
                var toWp = wp.ScenePos - rayOrigin;
                var proj = Vector3.Dot(toWp, rayDir);
                if (proj < 0) continue;

                // Same distance-scaled radius formula as ModelSelector so waypoints and
                // spawns feel consistent to click, plus a smaller floor since waypoints are
                // small anyway.
                var radius = 12f + proj * 0.008f;

                var closestPoint = rayOrigin + rayDir * proj;
                var d2 = Vector3.DistanceSquared(closestPoint, wp.ScenePos);
                if (d2 < radius * radius && proj < bestProj)
                {
                    closest = wp;
                    bestProj = proj;
                }
            }

            if (closest.HasValue)
            {
                _selected = closest;
                _currentPosition = closest.Value.ScenePos;
                OnSelectionChanged?.Invoke(closest);
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
            var t = IntersectRayPlane(eye, rayDir, _dragPlaneNormal, _dragPlaneDistance);
            if (t <= 0) return false;

            var hitPoint = eye + rayDir * t;
            var newPos = hitPoint + _dragOffset;

            // Ground-plane drag: snap Z to (ground below + drag-start ground-offset). Alt
            // stays on the camera-perpendicular plane for precise vertical adjustments.
            if (!_useCameraPlane && Collider != null)
            {
                var probeOrigin = new Vector3(newPos.X, newPos.Y, newPos.Z + 20f);
                var hit = Collider.FindIntersection(probeOrigin, new Vector3(0, 0, -1), 0.5f);
                if (hit.HasValue)
                    newPos.Z = hit.Value.Item2.Z + _dragGroundOffset;
            }

            _currentPosition = newPos;

            // Push the updated position back into the selected handle so PathGridRenderer
            // draws the waypoint at its dragged spot in real time.
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

            // Preserve waypoint height above terrain for the whole drag.
            _dragGroundOffset = DefaultGroundOffset;
            if (Collider != null)
            {
                var probe = _dragStartPosition + new Vector3(0, 0, 20f);
                var hit = Collider.FindIntersection(probe, new Vector3(0, 0, -1), 0.5f);
                if (hit.HasValue)
                    _dragGroundOffset = _dragStartPosition.Z - hit.Value.Item2.Z;
            }

            var eye = Camera.Position + new Vector3(0, 0, FpsCamera.CameraHeight);
            if (_useCameraPlane)
            {
                var forward = Vector3.Normalize(Vector3.Transform(FpsCamera.Forward, Camera.LookRotation));
                _dragPlaneNormal   = forward;
                _dragPlaneDistance = Vector3.Dot(_dragPlaneNormal, _dragStartPosition);
            }
            else
            {
                _dragPlaneNormal   = new Vector3(0, 0, 1);
                _dragPlaneDistance = _dragStartPosition.Z;
            }

            var rayDir = ScreenToWorldRay(mouseX, mouseY);
            var t = IntersectRayPlane(eye, rayDir, _dragPlaneNormal, _dragPlaneDistance);
            if (t <= 0) return false;

            var hitPoint = eye + rayDir * t;
            _dragOffset = _dragStartPosition - hitPoint;
            _isDragging = true;
            return true;
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
