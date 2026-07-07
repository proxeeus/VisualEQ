using System;
using System.Collections.Generic;
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

        // Try to select a model at the current mouse position
        public bool TrySelect(int mouseX, int mouseY)
        {
            // Convert screen coordinates to ray in world space
            Vector3 rayOrigin = Camera.Position + new Vector3(0, 0, FpsCamera.CameraHeight);
            Vector3 rayDirection = ScreenToWorldRay(mouseX, mouseY);

            // Find closest model along ray
            AniModelInstance closestModel = null;
            float closestDistance = float.MaxValue;

            foreach (var model in models)
            {
                // Simple sphere-based collision detection
                const float selectionRadius = 10.0f; // Adjust based on model size
                Vector3 modelPos = model.Position;

                // Calculate closest point on ray to model position
                Vector3 toModel = modelPos - rayOrigin;
                float projectionLength = Vector3.Dot(toModel, rayDirection);

                // Model is behind camera
                if (projectionLength < 0) continue;

                // Calculate closest point on ray to model center
                Vector3 closestPoint = rayOrigin + rayDirection * projectionLength;
                float distanceSquared = Vector3.DistanceSquared(closestPoint, modelPos);

                // Check if point is within selection radius and closer than previous hits
                if (distanceSquared < selectionRadius * selectionRadius && projectionLength < closestDistance)
                {
                    closestModel = model;
                    closestDistance = projectionLength;
                }
            }

            // Select the closest model if found
            if (closestModel != null)
            {
                SelectModel(closestModel);
                return true;
            }

            // No model found
            if (selectedModel != null)
            {
                SelectModel(null);
            }
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

            Vector3 eye = Camera.Position + new Vector3(0, 0, FpsCamera.CameraHeight);
            Vector3 modelPos = selectedModel.Position;
            Vector3 cameraForward = Vector3.Normalize(Vector3.Transform(FpsCamera.Forward, Camera.LookRotation));
            dragPlaneNormal = cameraForward;
            dragPlaneDistance = Vector3.Dot(dragPlaneNormal, modelPos);
            dragDepthOffset = 0f;

            Vector3 rayDirection = ScreenToWorldRay(mouseX, mouseY);
            float t = IntersectRayPlane(eye, rayDirection, dragPlaneNormal, dragPlaneDistance);
            if (t <= 0) return false;

            Vector3 hitPoint = eye + rayDirection * t;
            dragOffset = modelPos - hitPoint;

            isDragging = true;
            return true;
        }

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

            // Calculate current position on drag plane
            Vector3 eye = Camera.Position + new Vector3(0, 0, FpsCamera.CameraHeight);
            Vector3 rayDirection = ScreenToWorldRay(mouseX, mouseY);

            // Apply depth offset to drag plane
            float adjustedDragDistance = dragPlaneDistance + dragDepthOffset;

            // Find intersection with drag plane
            float t = IntersectRayPlane(eye, rayDirection, dragPlaneNormal, adjustedDragDistance);
            if (t <= 0) return false;

            // Calculate new model position
            Vector3 hitPoint = eye + rayDirection * t;
            Vector3 newPosition = hitPoint + dragOffset;

            // Check if new position would go below current surface
            if (currentSurfaceHeight > float.MinValue && newPosition.Z < currentSurfaceHeight)
            {
                // Constrain Z coordinate to stay above the surface
                newPosition.Z = currentSurfaceHeight + MODEL_GROUND_OFFSET;
            }

            // Update model position
            Vector3 oldPosition = selectedModel.Position;
            selectedModel.Position = newPosition;

            // Check if the new position has a different surface underneath
            FindSurfaceHeight(newPosition);

            // Keep track of last valid position
            lastValidPosition = selectedModel.Position;

            // Notify about position change
            OnPositionChanged?.Invoke(selectedModel, selectedModel.Position);

            return true;
        }

        // Stop dragging
        public void StopDrag()
        {
            // Only stick to surface if we actually dragged past the threshold — a bare click
            // shouldn't move the model at all.
            if (isDragging && selectedModel != null)
            {
                StickModelToSurface();
                // Notify listeners of the completed drag so they can record an edit action.
                OnDragCompleted?.Invoke(selectedModel, _dragStartPosition, selectedModel.Position);
            }

            isDragging = false;
            _dragPending = false;
            dragDepthOffset = 0f;
            currentSurfaceHeight = float.MinValue;
        }

        // Adjust the depth (distance from camera) while dragging
        public void AdjustDragDepth(float amount)
        {
            if (!isDragging || selectedModel == null) return;

            // Scale the amount based on distance from camera to model
            // This makes depth adjustment more intuitive
            float distance = Vector3.Distance(Camera.Position, selectedModel.Position);
            float scaledAmount = amount * (distance / 100f);

            // Update the depth offset
            dragDepthOffset += scaledAmount;

            // Apply the new depth
            Vector3 moveDirection = Vector3.Normalize(dragPlaneNormal);
            Vector3 newPosition = selectedModel.Position + moveDirection * scaledAmount;

            // Check if new position would go below current surface
            if (currentSurfaceHeight > float.MinValue && newPosition.Z < currentSurfaceHeight)
            {
                // Don't allow moving below the surface
                return;
            }

            // Update model position
            selectedModel.Position = newPosition;

            // Update surface height for the new position
            FindSurfaceHeight(newPosition);

            // Keep track of last valid position
            lastValidPosition = selectedModel.Position;

            // Notify about position change
            OnPositionChanged?.Invoke(selectedModel, selectedModel.Position);
        }

        // Find the height of the surface under a position
        private void FindSurfaceHeight(Vector3 position)
        {
            if (!PhysicsEnabled || Collider == null) return;

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

        // Make the model stick to surfaces by casting a ray downward
        private void StickModelToSurface()
        {
            if (selectedModel == null || !PhysicsEnabled || Collider == null) return;

            // Cast a ray downward from the model position
            Vector3 rayOrigin = selectedModel.Position;
            Vector3 rayDirection = new Vector3(0, 0, -1); // Straight down

            // Find intersection with the world
            var hit = Collider.FindIntersection(rayOrigin, rayDirection, 0.5f);

            if (hit != null)
            {
                // Distance to the surface
                float distance = Math.Abs(rayOrigin.Z - hit.Value.Item2.Z);

                // If the surface is within reasonable distance, snap to it
                if (distance <= SURFACE_CHECK_DISTANCE)
                {
                    Vector3 newPosition = selectedModel.Position;
                    newPosition.Z = hit.Value.Item2.Z + MODEL_GROUND_OFFSET;
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
