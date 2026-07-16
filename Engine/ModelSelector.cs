using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using OpenTK.Input;
using static VisualEQ.Engine.Globals;

namespace VisualEQ.Engine
{
    public class ModelSelector
    {
        // Currently selected model
        private AniModelInstance selectedModel = null;

        // Drag state
        private bool isDragging = false;
        private Vector3 dragOffset = Vector3.Zero;
        private Vector3 dragPlaneNormal = Vector3.Zero;
        private float dragPlaneDistance = 0f;
        private float dragDepthOffset = 0f;
        // Recorded at BeginActualDrag so mid-drag surface-stick knows whether the drag plane
        // is world XY (default) or camera-perpendicular (Alt-modified).
        private bool _useCameraPlane;

        // Sampled once at BeginActualDrag. When true, ground-plane drag skips the downward
        // collision probe and leaves Z at _dragStartPosition.Z + _wheelZOffset — for placing
        // NPCs below water, inside dock recesses, or anywhere the auto-snap gets in the way.
        // Mirrors _useCameraPlane's "sample once, hold for the drag" pattern so a slipped
        // finger mid-drag doesn't re-engage the snap.
        private bool _freezeZ;

        // Vertical distance from the ground under the model to the model's origin at the
        // moment drag started. Preserved during ground-plane drag so the model keeps sitting
        // on terrain the same way it did at load time — the DB stores hip/center Z, not feet,
        // so a naïve "snap origin to ground + tiny offset" sinks the model half-height under.
        private float _dragGroundOffset;

        // Horizontal offset (world XY) from the initial click-point-on-world to the model
        // center at drag start. Preserves the "you grabbed here" feel across drags.
        private Vector2 _dragHorizOffset;

        // Free-Z adjustment accumulated via mouse wheel during ground-plane drag. Lets the
        // user push the model above/below the terrain snap without switching to Alt-drag.
        private float _wheelZOffset;

        // Click-vs-drag threshold. Between MouseDown and this many pixels of movement,
        // the click is treated as "select only". Past the threshold, the drag actually
        // begins (with the anchor recomputed at the current mouse pos so there's no jump).
        private bool _dragPending = false;
        private int _dragStartMouseX, _dragStartMouseY;
        private const int DragThresholdPixels = 8;

        // Surface constraint data
        private Vector3 lastValidPosition = Vector3.Zero;
        private float currentSurfaceHeight = float.MinValue;

        // Constants for surface sticking
        private const float SURFACE_CHECK_DISTANCE = 50.0f; // How far down to check for a surface
        private const float MODEL_GROUND_OFFSET = 0.1f;     // Small offset to avoid Z-fighting
        private const float SURFACE_PROBE_RADIUS = 10.0f;   // Radius to check for surfaces around the model

        // The EngineCore reference for input access
        private readonly EngineCore engine;

        // Model list reference for selection
        private readonly List<AniModelInstance> models;

        // Selection changed event
        public event Action<AniModelInstance> OnSelectionChanged;

        // Position changed event (per-frame during a drag)
        public event Action<AniModelInstance, Vector3> OnPositionChanged;

        // Drag completed event — fires ONCE per successful drag with from/to positions.
        // Consumed by Controller to record a SpawnMoveAction. `from` is the position at the
        // moment drag activated (post-threshold); `to` is the final resting position.
        public event Action<AniModelInstance, Vector3, Vector3> OnDragCompleted;

        // Where the model was when the drag actually activated (past the threshold).
        // Recorded in BeginActualDrag so StopDrag can fire OnDragCompleted with an
        // accurate "from" position.
        private Vector3 _dragStartPosition;

        public ModelSelector(EngineCore engine, List<AniModelInstance> models)
        {
            this.engine = engine;
            this.models = models;
        }

        // Get currently selected model
        public AniModelInstance SelectedModel => selectedModel;

        // True once the drag has actually engaged (past the threshold).
        public bool IsDragging => isDragging;

        // Ray-vs-vertical-cylinder picker. The ray from camera through the click pixel
        // is well-tested (ScreenToWorldRay powers FlyToCursor, WaypointEditor, and
        // ZonePointEditor); we take the closest approach between that ray and each
        // model's vertical spine (feet → head), and pick the model whose spine sits
        // inside a modest radius. Camera distance ranks candidates so overlapping
        // spawns (EQEmu respawn-rotation stacks) resolve to the front one.
        //
        // The old world-sphere approach aimed at Position (feet) with a 15+dist*0.008
        // radius that ballooned with distance — clicks nowhere near a distant model
        // could still land inside its huge selection volume, letting it steal focus
        // from nearby models. This version:
        //   * aims at the spine, not the feet, so clicks on the head still hit
        //   * uses a small model-scaled radius (~2 world units for a humanoid) so
        //     distant models require actually clicking on the model, not near it
        //   * keeps a small +dist*0.005 tolerance so distant-but-visible models don't
        //     need pixel-perfect aim
        public bool TrySelect(int mouseX, int mouseY)
        {
            if (models.Count == 0)
            {
                if (selectedModel != null) SelectModel(null);
                return false;
            }

            var rayOrigin = Camera.Position + new Vector3(0, 0, FpsCamera.CameraHeight);
            var rayDir    = ScreenToWorldRay(mouseX, mouseY);

            AniModelInstance best = null;
            float bestCamDist = float.MaxValue;

            foreach (var model in models)
            {
                // Vertical spine from feet to head. Height ≈ 6 world units per unit
                // of Scale (matches SpawnManager's authored-height convention). We
                // pick a torso point (midway) as the projection target — clicks on
                // the head or the feet are both ~3 units from it, which fits under
                // any reasonable radius.
                var height   = 6f * model.Scale;
                var feet     = model.Position;
                var head     = feet + new Vector3(0, 0, height);
                var torso    = feet + new Vector3(0, 0, height * 0.5f);

                // Distance-along-ray to the torso (used for camera-distance ranking
                // + the "behind camera" cull).
                var toTorso = torso - rayOrigin;
                var proj = Vector3.Dot(toTorso, rayDir);
                if (proj < 0) continue;

                // Approximate ray-vs-cylinder: compare the ray's closest approach to
                // the spine against a per-model radius. Uses the segment-to-line
                // distance in world space, clamped so a ray that passes above/below
                // the head/feet doesn't count.
                var rayPoint = rayOrigin + rayDir * proj;
                var spineT   = System.Math.Max(0f, System.Math.Min(1f,
                    Vector3.Dot(rayPoint - feet, head - feet) / (height * height)));
                var spinePt  = feet + (head - feet) * spineT;
                var dist     = Vector3.Distance(rayPoint, spinePt);

                // Radius: ~2 world units for a scale-1 humanoid (about mesh width),
                // plus a small distance term so distant humanoids don't require pixel-
                // perfect aim. Much smaller than the old 15+dist*0.008 base.
                var radius = 2.5f * model.Scale + proj * 0.005f;
                if (dist > radius) continue;

                if (proj < bestCamDist)
                {
                    best = model;
                    bestCamDist = proj;
                }
            }

            if (best != null)
            {
                SelectModel(best);
                return true;
            }

            if (selectedModel != null) SelectModel(null);
            return false;
        }

        // Read-only vs edit mode gate. When false, StartDrag is a no-op — clicks still
        // select but drags never mutate anything. Controller sets this via the mode toggle.
        public bool EditModeEnabled = false;

        // Record intent to drag. The actual drag setup is deferred to the first UpdateDrag
        // call that reports mouse movement past DragThresholdPixels, so a click without
        // meaningful movement is a pure "select" gesture. No-op in read-only mode.
        public bool StartDrag(int mouseX, int mouseY)
        {
            if (!EditModeEnabled) return false;
            if (selectedModel == null) return false;

            _dragPending = true;
            _dragStartMouseX = mouseX;
            _dragStartMouseY = mouseY;
            return true;
        }

        // Sets up drag anchor at the current mouse position. Called by UpdateDrag once the
        // threshold is crossed. Setting the anchor at the *current* pos (not the click pos)
        // avoids the model snapping to the mouse when drag activates.
        private bool BeginActualDrag(int mouseX, int mouseY)
        {
            _dragStartPosition = selectedModel.Position;
            lastValidPosition = selectedModel.Position;
            FindSurfaceHeight(selectedModel.Position);

            _useCameraPlane = engine.GetPressedKeys().Contains(OpenTK.Input.Key.AltLeft)
                            || engine.GetPressedKeys().Contains(OpenTK.Input.Key.AltRight);
            _freezeZ = engine.GetPressedKeys().Contains(OpenTK.Input.Key.ControlLeft)
                     || engine.GetPressedKeys().Contains(OpenTK.Input.Key.ControlRight);
            _wheelZOffset = 0f;
            _dragHorizOffset = Vector2.Zero;
            _dragGroundOffset = MODEL_GROUND_OFFSET;

            Vector3 eye = Camera.Position + new Vector3(0, 0, FpsCamera.CameraHeight);
            Vector3 modelPos = selectedModel.Position;
            Vector3 rayDirection = ScreenToWorldRay(mouseX, mouseY);

            if (_useCameraPlane)
            {
                // Alt-drag: camera-perpendicular plane. Depth adjust via wheel.
                Vector3 cameraForward = Vector3.Normalize(Vector3.Transform(FpsCamera.Forward, Camera.LookRotation));
                dragPlaneNormal = cameraForward;
                dragPlaneDistance = Vector3.Dot(dragPlaneNormal, modelPos);
                dragDepthOffset = 0f;

                float t = IntersectRayPlane(eye, rayDirection, dragPlaneNormal, dragPlaneDistance);
                if (t <= 0) return false;
                Vector3 hitPoint = eye + rayDirection * t;
                dragOffset = modelPos - hitPoint;
            }
            else
            {
                // Ground-plane setup: XY comes from projecting the mouse onto the XY plane
                // at model's start Z (predictable). Z snap comes from a downward probe from
                // WAY above the target XY (so walls, rooftops, ledges all get hit correctly
                // — the previous "start-Z + 20" probe missed anything taller than 20 units).

                // Capture horizontal grab-offset relative to the XY-plane projection.
                var groundNormal = new Vector3(0, 0, 1);
                float tPlane = IntersectRayPlane(eye, rayDirection, groundNormal, modelPos.Z);
                if (tPlane > 0)
                {
                    var xyProj = eye + rayDirection * tPlane;
                    _dragHorizOffset = new Vector2(modelPos.X - xyProj.X, modelPos.Y - xyProj.Y);
                }

                // Capture vertical ground-offset by probing from HIGH altitude — catches
                // whichever surface (ground OR wall top the model is currently on).
                if (Collider != null)
                {
                    var probe = new Vector3(modelPos.X, modelPos.Y, HighProbeAltitude);
                    var below = Collider.FindIntersection(probe, new Vector3(0, 0, -1), 0.5f);
                    if (below.HasValue)
                        _dragGroundOffset = modelPos.Z - below.Value.Item2.Z;
                }
            }

            isDragging = true;
            return true;
        }

        // Downward-probe start Z used for ground snap. Needs to be higher than any wall or
        // rooftop in the zone so probes can hit those surfaces from above. 5000 covers every
        // outdoor zone we deal with; dungeons with lower ceilings should use Alt-drag.
        const float HighProbeAltitude = 5000f;

        // Update dragging based on current mouse position
        public bool UpdateDrag(int mouseX, int mouseY)
        {
            if (selectedModel == null) return false;

            // Below threshold? Still a click, not a drag — do nothing.
            if (_dragPending)
            {
                int dx = mouseX - _dragStartMouseX;
                int dy = mouseY - _dragStartMouseY;
                if (dx * dx + dy * dy < DragThresholdPixels * DragThresholdPixels)
                    return false;

                _dragPending = false;
                if (!BeginActualDrag(mouseX, mouseY)) return false;
            }

            if (!isDragging) return false;

            Vector3 eye = Camera.Position + new Vector3(0, 0, FpsCamera.CameraHeight);
            Vector3 rayDirection = ScreenToWorldRay(mouseX, mouseY);
            Vector3 newPosition;

            if (_useCameraPlane)
            {
                float adjustedDragDistance = dragPlaneDistance + dragDepthOffset;
                float t = IntersectRayPlane(eye, rayDirection, dragPlaneNormal, adjustedDragDistance);
                if (t <= 0) return false;
                Vector3 hitPoint = eye + rayDirection * t;
                newPosition = hitPoint + dragOffset;

                // Keep old "don't fall through the floor" clamp for camera-perp mode.
                if (currentSurfaceHeight > float.MinValue && newPosition.Z < currentSurfaceHeight)
                    newPosition.Z = currentSurfaceHeight + MODEL_GROUND_OFFSET;
                FindSurfaceHeight(newPosition);
            }
            else
            {
                // Ground-plane drag: XY from mouse-on-XY-plane projection (predictable);
                // Z from downward probe from HIGH altitude (catches walls / rooftops that
                // are taller than the model's current altitude).
                var groundNormal = new Vector3(0, 0, 1);
                float tPlane = IntersectRayPlane(eye, rayDirection, groundNormal, _dragStartPosition.Z);
                if (tPlane <= 0) return false;

                var xyProj = eye + rayDirection * tPlane;
                var targetX = xyProj.X + _dragHorizOffset.X;
                var targetY = xyProj.Y + _dragHorizOffset.Y;

                float targetZ = _dragStartPosition.Z + _wheelZOffset; // fallback if no ground
                // Ctrl held at drag start = freeze Z; skip the auto-snap probe entirely.
                if (!_freezeZ && Collider != null)
                {
                    var probe = new Vector3(targetX, targetY, HighProbeAltitude);
                    var hit = Collider.FindIntersection(probe, new Vector3(0, 0, -1), 0.5f);
                    if (hit.HasValue)
                    {
                        targetZ = hit.Value.Item2.Z + _dragGroundOffset + _wheelZOffset;
                        currentSurfaceHeight = hit.Value.Item2.Z;
                    }
                }
                newPosition = new Vector3(targetX, targetY, targetZ);
            }

            selectedModel.Position = newPosition;
            lastValidPosition = selectedModel.Position;

            OnPositionChanged?.Invoke(selectedModel, selectedModel.Position);
            return true;
        }

        // Abort an in-flight drag without firing OnDragCompleted, restoring the model to
        // where it was when the drag anchor was set. Called by Escape hotkey.
        public void CancelDrag()
        {
            if (isDragging && selectedModel != null)
                selectedModel.Position = _dragStartPosition;
            isDragging = false;
            _dragPending = false;
            _freezeZ = false;
            dragDepthOffset = 0f;
            currentSurfaceHeight = float.MinValue;
        }

        // Public deselect used by Escape and the sidebar's list-click "cleared" state.
        public void ClearSelection()
        {
            SelectModel(null);
        }

        // Stop dragging
        public void StopDrag()
        {
            // Only stick to surface if we actually dragged past the threshold — a bare click
            // shouldn't move the model at all.
            if (isDragging && selectedModel != null)
            {
                // Frozen-Z drag intentionally sits off-surface — don't undo it on drop.
                if (!_freezeZ) StickModelToSurface();
                // Notify listeners of the completed drag so they can record an edit action.
                OnDragCompleted?.Invoke(selectedModel, _dragStartPosition, selectedModel.Position);
            }

            isDragging = false;
            _dragPending = false;
            _freezeZ = false;
            dragDepthOffset = 0f;
            currentSurfaceHeight = float.MinValue;
        }

        // Mouse wheel during drag: adjust Z (free vertical) in ground-plane mode; adjust
        // camera-plane depth in Alt-drag mode.
        public void AdjustDragDepth(float amount)
        {
            if (!isDragging || selectedModel == null) return;

            if (_useCameraPlane)
            {
                float distance = Vector3.Distance(Camera.Position, selectedModel.Position);
                float scaledAmount = amount * (distance / 100f);
                dragDepthOffset += scaledAmount;

                Vector3 moveDirection = Vector3.Normalize(dragPlaneNormal);
                Vector3 newPosition = selectedModel.Position + moveDirection * scaledAmount;
                if (currentSurfaceHeight > float.MinValue && newPosition.Z < currentSurfaceHeight) return;
                selectedModel.Position = newPosition;
                FindSurfaceHeight(newPosition);
            }
            else
            {
                // Ground-plane mode: wheel adds/subtracts a free Z offset. Scaled by view
                // distance so wheel feels roughly the same regardless of how far the model is.
                float distance = Vector3.Distance(Camera.Position, selectedModel.Position);
                float scaledAmount = amount * Math.Max(0.05f, distance / 500f);
                _wheelZOffset += scaledAmount;

                var p = selectedModel.Position;
                p.Z += scaledAmount;
                selectedModel.Position = p;
            }

            lastValidPosition = selectedModel.Position;
            OnPositionChanged?.Invoke(selectedModel, selectedModel.Position);
        }

        // Find the height of the surface under a position. Independent of PhysicsEnabled —
        // the collision octree exists whenever a zone is loaded, and we want models to
        // snap to terrain during drag whether the user has toggled gravity or not.
        private void FindSurfaceHeight(Vector3 position)
        {
            if (Collider == null) return;

            // Cast a ray downward from the model position
            Vector3 rayOrigin = position;
            Vector3 rayDirection = new Vector3(0, 0, -1); // Straight down

            // Find intersection with the world
            var hit = Collider.FindIntersection(rayOrigin, rayDirection, 0.5f);

            if (hit != null)
            {
                // Store the surface height
                currentSurfaceHeight = hit.Value.Item2.Z;
            }
            else
            {
                // Also check a few points around the model to find nearby surfaces
                // This helps with edge cases where the model is right at the edge of a surface
                bool foundSurface = false;
                float highestSurface = float.MinValue;

                // Check in a small radius around the model
                for (int i = 0; i < 4; i++)
                {
                    float angle = i * MathF.PI / 2.0f; // 4 points around the model
                    Vector3 offset = new Vector3(
                        SURFACE_PROBE_RADIUS * MathF.Cos(angle),
                        SURFACE_PROBE_RADIUS * MathF.Sin(angle),
                        0
                    );

                    Vector3 probeOrigin = position + offset;
                    var probeHit = Collider.FindIntersection(probeOrigin, rayDirection, 0.5f);

                    if (probeHit != null)
                    {
                        foundSurface = true;
                        highestSurface = MathF.Max(highestSurface, probeHit.Value.Item2.Z);
                    }
                }

                if (foundSurface)
                {
                    currentSurfaceHeight = highestSurface;
                }
            }
        }

        // Make the model stick to surfaces by casting a ray downward. Same rationale as
        // FindSurfaceHeight — collision is always available; physics enable is orthogonal.
        private void StickModelToSurface()
        {
            if (selectedModel == null || Collider == null) return;

            // Cast a ray downward from the model position
            Vector3 rayOrigin = selectedModel.Position;
            Vector3 rayDirection = new Vector3(0, 0, -1); // Straight down

            // Find intersection with the world
            var hit = Collider.FindIntersection(rayOrigin, rayDirection, 0.5f);

            if (hit != null)
            {
                // Distance to the surface
                float distance = Math.Abs(rayOrigin.Z - hit.Value.Item2.Z);

                // If the surface is within reasonable distance, snap to it — preserving the
                // ground-offset we recorded at drag start rather than assuming feet==origin.
                // Also preserve _wheelZOffset so an in-drag mouse-wheel adjustment survives
                // the drop (without this, releasing a wheel-adjusted drag reverts to plain
                // ground snap and the user's Z tweak is silently dropped).
                if (distance <= SURFACE_CHECK_DISTANCE)
                {
                    Vector3 newPosition = selectedModel.Position;
                    newPosition.Z = hit.Value.Item2.Z + _dragGroundOffset + _wheelZOffset;
                    selectedModel.Position = newPosition;
                    currentSurfaceHeight = hit.Value.Item2.Z;
                }
            }
        }

        // Helper method to convert screen coordinates to world ray
        private Vector3 ScreenToWorldRay(int mouseX, int mouseY)
        {
            // Convert mouse position to normalized device coordinates (-1 to 1)
            float ndcX = 2.0f * mouseX / engine.Width - 1.0f;
            float ndcY = 1.0f - 2.0f * mouseY / engine.Height; // Y is inverted

            // Create NDC position with depth
            Vector4 rayClip = new Vector4(ndcX, ndcY, -1.0f, 1.0f);

            // Transform to view space
            Matrix4x4.Invert(ProjectionMat, out Matrix4x4 invProj);
            Vector4 rayView = Vector4.Transform(rayClip, invProj);
            rayView = new Vector4(rayView.X, rayView.Y, -1.0f, 0.0f); // Setting w to 0 for direction

            // Transform to world space
            Matrix4x4.Invert(FpsCamera.Matrix, out Matrix4x4 invView);
            Vector4 rayWorld = Vector4.Transform(rayView, invView);

            // Normalize the direction
            Vector3 rayDirection = new Vector3(rayWorld.X, rayWorld.Y, rayWorld.Z);
            return Vector3.Normalize(rayDirection);
        }

        // Helper method to intersect ray with plane
        private float IntersectRayPlane(Vector3 rayOrigin, Vector3 rayDirection, Vector3 planeNormal, float planeDistance)
        {
            float denominator = Vector3.Dot(rayDirection, planeNormal);

            // Ray is parallel to plane
            if (Math.Abs(denominator) < 0.0001f) return -1.0f;

            float t = (planeDistance - Vector3.Dot(rayOrigin, planeNormal)) / denominator;
            return t;
        }

        // Select a model and notify listeners
        private void SelectModel(AniModelInstance model)
        {
            if (selectedModel == model) return;

            selectedModel = model;
            isDragging = false;
            currentSurfaceHeight = float.MinValue;

            if (model != null)
            {
                // Find the surface height under the selected model
                FindSurfaceHeight(model.Position);
                lastValidPosition = model.Position;
            }

            OnSelectionChanged?.Invoke(model);
        }
    }
}
