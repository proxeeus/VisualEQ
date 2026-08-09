using System;
using System.Linq;
using System.Numerics;
using Microsoft.Scripting.Utils;
using VisualEQ.Common;
using static System.MathF;
using static VisualEQ.Engine.Globals;

namespace VisualEQ.Engine
{
    public class FpsCamera
    {
        public Vector3 Position;
        public float Pitch { get; private set; }
        public float Yaw { get; private set; }

        public static Matrix4x4 Matrix;
        public Matrix4x4 LookRotation = Matrix4x4.Identity;

        public static readonly Vector3 Up = new Vector3(0, 0, 1);
        public static readonly Vector3 Right = vec3(1, 0, 0);
        public static readonly Vector3 Forward = new Vector3(0, 1, 0);

        public float FallingVelocity;
        public bool OnGround;

        public const float CameraHeight = 5.5f;

        // Fly-to-cursor animation state. When _flying is true, Update() interpolates
        // Position from _flyFrom → _flyTo over _flyDuration seconds with an ease-out
        // cubic and suppresses gravity so physics doesn't fight the lerp mid-flight.
        bool _flying;
        Vector3 _flyFrom;
        Vector3 _flyTo;
        float _flyStartTime;
        float _flyDuration;

        // Look-lock target. When non-null, Update() re-orients the camera to face
        // _lookLockTarget every frame (rebuilding Pitch/Yaw/LookRotation). Cleared
        // automatically the moment the user drags mouse-look — the "any keyboard/mouse
        // input releases" behavior lives in EngineCore's mouse handler, which calls
        // ClearLookLock() before applying user rotation input.
        Vector3? _lookLockTarget;

        public FpsCamera(Vector3 pos)
        {
            Position = pos;
            Pitch = Yaw = 0;
        }

        // Restores a saved camera pose (used by the zone-snapshot cache so F10 → re-visit
        // lands the user back exactly where they were, not at the (0,0,1000) default).
        // Rebuilds LookRotation so the next render frame reflects the restored orientation
        // without waiting for a mouse-look event.
        public void SetPose(Vector3 position, float pitch, float yaw)
        {
            Position = position;
            Pitch = pitch;
            Yaw = yaw;
            LookRotation = Matrix4x4.CreateFromAxisAngle(Right, Pitch);
            if (Yaw != 0)
                LookRotation *= Matrix4x4.CreateFromAxisAngle(Up, Yaw);
        }

        // Smoothly moves the camera to `target` over `duration` seconds. Any in-progress
        // flight is overridden. Callers pass the world-space landing spot; adding altitude
        // offset is the caller's responsibility.
        public void FlyTo(Vector3 target, float duration = 0.25f)
        {
            _flyFrom      = Position;
            _flyTo        = target;
            _flyStartTime = FrameTime;
            _flyDuration  = Math.Max(0.01f, duration);
            _flying       = true;
        }

        // Combined "fly to camera pose + orient at a lookAt target" — used by the NPC
        // editor's visual-field auto-framing so the camera lands facing the subject at
        // the end of the tween, not just at a coincidental pose. LockLookAt runs per-
        // frame during the flight AND after, so the orientation tracks the subject if
        // the model swaps mid-flight (race change while flying-in, etc.).
        public void FlyToLookAt(Vector3 cameraPos, Vector3 lookAtTarget, float duration = 0.25f)
        {
            FlyTo(cameraPos, duration);
            LockLookAt(lookAtTarget);
        }

        // Enables a per-frame look-at override. While active, camera Pitch/Yaw are recomputed
        // each Update() to face `worldPoint`, so a subject that moves (or a scale/model swap
        // that shifts the head position) stays framed. Any user-driven Look() call clears
        // this — the sidebar can also call ClearLookLock() explicitly on defocus.
        public void LockLookAt(Vector3 worldPoint)
        {
            _lookLockTarget = worldPoint;
            LookAt(worldPoint); // apply immediately so this frame reflects the lock
        }

        public void ClearLookLock() => _lookLockTarget = null;

        public bool IsLookLocked => _lookLockTarget.HasValue;

        public void Move(Vector3 _movement)
        {
            // TODO: https://github.com/dotnet/coreclr/issues/19674
            var movement = _movement;
            if (movement.LengthSquared() < 0.0001) return;
            movement = Vector3.Transform(movement, LookRotation);
            if (PhysicsEnabled)
            {
                movement = ClipMovement(movement);
                // TODO: Figure out what a reasonable max is
                if (movement.Z > 1)
                    movement.Z = 1;
                else if (movement.Z < -1)
                    movement.Z = -1;
            }

            Position += movement;
        }

        Vector3 ClipMovement(Vector3 movement, int iterations = 3)
        {
            const float padding = 3f;
            var moveLen = movement.Length();
            var moveDir = movement.Normalized();
            var adjPosition = Position + new Vector3(0, 0, 5);
            var hit = Collider.FindIntersection(adjPosition, moveDir, 0.5f);
            if (hit == null) return movement;
            var dist = (hit.Value.Item2 - adjPosition).Length();
            if (dist > moveLen + padding) return movement;
            if (iterations == 0) return moveDir * (dist - padding);
            var triNormal = hit.Value.Item1.Normal;
            var backoff = Vector3.Dot(movement, triNormal);
            movement -= triNormal * backoff;
            return ClipMovement(movement, iterations - 1);
        }

        public void Look(float pitchmod, float yawmod)
        {
            // User-driven rotation clears any active look-lock — matches the
            // "any keyboard/mouse-drag releases" spec from the NPC editor plan doc.
            _lookLockTarget = null;
            var eps = 0.01f;
            Pitch = clamp(Pitch + pitchmod, -PI / 2 + eps, PI / 2 - eps);
            Yaw += yawmod;
            LookRotation = Matrix4x4.CreateFromAxisAngle(Right, Pitch);
            if (Yaw != 0)
                LookRotation *= Matrix4x4.CreateFromAxisAngle(Up, Yaw);
        }

        // Orient the camera to point at a world-space target. Used by the sidebar's
        // "click a spawn in the list → fly to it" flow. Pitch is clamped to the same
        // near-±π/2 range as Look() so gimbal-lock cases behave.
        public void LookAt(Vector3 target)
        {
            // Same eye-height offset as Update() applies before the LookAt matrix.
            var eye = Position + new Vector3(0, 0, CameraHeight);
            var dir = target - eye;
            if (dir.LengthSquared() < 0.0001f) return;
            dir = Vector3.Normalize(dir);

            var eps = 0.01f;
            Pitch = clamp(Asin(clamp(dir.Z, -1f, 1f)), -PI / 2 + eps, PI / 2 - eps);
            Yaw   = Atan2(-dir.X, dir.Y);

            LookRotation = Matrix4x4.CreateFromAxisAngle(Right, Pitch);
            if (Yaw != 0)
                LookRotation *= Matrix4x4.CreateFromAxisAngle(Up, Yaw);
        }

        public void Update(float timestep)
        {
            // Re-apply the look-lock every frame so the camera tracks a moving/re-scaling
            // subject. Runs BEFORE the flying branch so the tween's landing pose is also
            // oriented at the subject (not just at wherever the yaw happened to land).
            if (_lookLockTarget.HasValue)
            {
                var target = _lookLockTarget.Value;
                var eye = Position + new Vector3(0, 0, CameraHeight);
                var dir = target - eye;
                if (dir.LengthSquared() >= 0.0001f)
                {
                    dir = Vector3.Normalize(dir);
                    var eps = 0.01f;
                    Pitch = clamp(Asin(clamp(dir.Z, -1f, 1f)), -PI / 2 + eps, PI / 2 - eps);
                    Yaw   = Atan2(-dir.X, dir.Y);
                    LookRotation = Matrix4x4.CreateFromAxisAngle(Right, Pitch);
                    if (Yaw != 0)
                        LookRotation *= Matrix4x4.CreateFromAxisAngle(Up, Yaw);
                }
            }

            if (_flying)
            {
                var t = (FrameTime - _flyStartTime) / _flyDuration;
                if (t >= 1f)
                {
                    Position = _flyTo;
                    _flying = false;
                    FallingVelocity = 0;
                }
                else
                {
                    // Ease-out cubic — decelerates into the landing so the stop doesn't jar.
                    var oneMinusT = 1f - t;
                    var eased = 1f - oneMinusT * oneMinusT * oneMinusT;
                    Position = Vector3.Lerp(_flyFrom, _flyTo, eased);
                }

                var flyEye = Position + new Vector3(0, 0, CameraHeight);
                var flyAt  = Vector3.Normalize(Vector3.Transform(Forward, LookRotation));
                Matrix = Matrix4x4.CreateLookAt(flyEye, flyEye + flyAt, Up);
                return;
            }

            if (PhysicsEnabled)
                NoProfile("Gravity", () =>
                {
                    if (FallingVelocity < 0)
                        Position.Z -= FallingVelocity * timestep;

                    Position.Z += 5;
                    var downray = vec3(0.00001f, 0.00001f, -1).Normalized();
                    var hit = Collider.FindIntersection(Position, downray, 0.5f);

                    Position.Z -= 5;
                    var dist = hit != null ? Position.Z - hit.Value.Item2.Z : float.PositiveInfinity;
                    var angle = hit != null ? Abs(Vector3.Dot(hit.Value.Item1.Normal, vec3(0, 0, 1))) : 1;
                    OnGround = false;
                    if (FallingVelocity >= 0 && dist < 1 && angle > 0.25f)
                    {
                        // Todo: Figure out what a reasonable default is
                        Position.Z -= dist;
                        FallingVelocity = 0;
                        OnGround = true;
                    }
                    else
                    {
                        if (angle < 0.25f && hit != null)
                        {
                            var normal = hit.Value.Item1.Normal;
                            Position.X += normal.X * timestep;
                            Position.Y += normal.Y * timestep;
                        }

                        FallingVelocity += 250 * timestep;
                        var delta = FallingVelocity * timestep;
                        Position.Z -= Math.Min(delta, dist);
                        if (delta > dist)
                        {
                            FallingVelocity = 0;
                            OnGround = true;
                        }
                    }
                });

            var tallPosition = Position + new Vector3(0, 0, CameraHeight);
            var at = Vector3.Normalize(Vector3.Transform(Forward, LookRotation));
            Matrix = Matrix4x4.CreateLookAt(tallPosition, tallPosition + at, Up);
        }
    }
}
