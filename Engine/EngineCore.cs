using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using CollisionManager;
using NsimGui;
using NsimGui.Widgets;
using VisualEQ.Common;
using OpenTK;
using OpenTK.Graphics;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Input;
using static VisualEQ.Engine.Globals;
using Vector2 = System.Numerics.Vector2;
using Vector3 = System.Numerics.Vector3;

namespace VisualEQ.Engine
{
    public partial class EngineCore : GameWindow
    {
        bool DeferredEnabled;

        public readonly Gui Gui;

        readonly List<Model> Models = new List<Model>();
        readonly List<AniModelInstance> AniModels = new List<AniModelInstance>();
        readonly List<double> FrameTimes = new List<double>();
        readonly List<PointLight> Lights = new List<PointLight>();
        // Named water/lava/slime regions loaded from the zone's OES `regn` chunks. Empty
        // for zones that predate liquid-region detection. Public list — callers iterate
        // to render debug volumes, run point-in-region queries, or snap NPC Z to a surface.
        public readonly List<LiquidRegion> Regions = new List<LiquidRegion>();

        public double FPS => FrameTimes.Count == 0 ? 0 : 1 / (FrameTimes.Sum() / FrameTimes.Count);

        Matrix4x4 ProjectionView;

        bool MouseLooking;
        (double, double) MouseBeforeLook;

        (double X, double Y) MousePosition
        {
            get
            {
                var state = OpenTK.Input.Mouse.GetCursorState();
                return (state.X, state.Y);
            }
            set => OpenTK.Input.Mouse.SetPosition(value.X, value.Y);
        }

        Vector2 MouseDelta
        {
            get
            {
                var state = OpenTK.Input.Mouse.GetState();
                return vec2(state.X, state.Y);
            }
        }

        Vector2 LastMouseDelta;

        // Reference to the controller that owns this engine
        public IController Controller { get; set; }

        // World-space bounding box of the collidable zone geometry. Null until the first
        // successful RebuildCollision. Consumed by ZonePointRenderer to size the wildcard
        // slab (a horizontal fill covering the whole zone at the wildcard row's Z).
        public (Vector3 Min, Vector3 Max)? ZoneBounds { get; private set; }

        // Waypoint editing runs alongside spawn selection — clicks near a candidate waypoint
        // engage this instead of ModelSelector.
        public WaypointEditor WaypointEditor { get; }

        // Zone-point handle editing. Same priority slot as WaypointEditor — clicks near a
        // zone-point handle take precedence over both waypoint and spawn selection so the
        // small handle icons don't get swallowed by an overlapping spawn model.
        public ZonePointEditor ZonePointEditor { get; }

        // Spawn state indicators drawn after the forward pass. Lazily instantiated on the
        // first render frame (GL context guaranteed). Controller pushes lines via
        // SetSpawnMarkerLines; last-set data is reused if a frame doesn't provide new lines.
        SpawnMarkers _spawnMarkers;
        System.Collections.Generic.IReadOnlyList<(System.Numerics.Vector3 A, System.Numerics.Vector3 B, System.Numerics.Vector4 Color)> _pendingMarkerLines;

        public void SetSpawnMarkerLines(System.Collections.Generic.IReadOnlyList<(System.Numerics.Vector3, System.Numerics.Vector3, System.Numerics.Vector4)> lines) =>
            _pendingMarkerLines = lines;

        // Path grid overlay for the selected spawn. Same lifecycle as _spawnMarkers.
        PathGridRenderer _pathGrids;
        System.Collections.Generic.IReadOnlyList<(System.Numerics.Vector3 A, System.Numerics.Vector3 B, System.Numerics.Vector4 Color)> _pendingPathGridLines;

        public void SetPathGridLines(System.Collections.Generic.IReadOnlyList<(System.Numerics.Vector3, System.Numerics.Vector3, System.Numerics.Vector4)> lines) =>
            _pendingPathGridLines = lines;

        // trilogy_zone_points volume overlay. Same lazy-instantiation + pending-buffer
        // pattern as SpawnMarkers/PathGridRenderer. Two buffers (lines + triangles) share
        // a single push call so the frame stays atomic.
        ZonePointRenderer _zonePoints;
        System.Collections.Generic.IReadOnlyList<(System.Numerics.Vector3 A, System.Numerics.Vector3 B, System.Numerics.Vector4 Color)> _pendingZonePointLines;
        System.Collections.Generic.IReadOnlyList<(System.Numerics.Vector3 A, System.Numerics.Vector3 B, System.Numerics.Vector3 C, System.Numerics.Vector4 Color)> _pendingZonePointTris;

        public void SetZonePointPrimitives(
            System.Collections.Generic.IReadOnlyList<(System.Numerics.Vector3, System.Numerics.Vector3, System.Numerics.Vector4)> lines,
            System.Collections.Generic.IReadOnlyList<(System.Numerics.Vector3, System.Numerics.Vector3, System.Numerics.Vector3, System.Numerics.Vector4)> tris)
        {
            _pendingZonePointLines = lines;
            _pendingZonePointTris = tris;
        }

        // Whether we're currently dragging a model
        private bool ModelDragging = false;

        // Whether we're currently dragging a waypoint. Mutually exclusive with ModelDragging —
        // WaypointEditor gets first crack at MouseDown, and if it grabs the click ModelSelector
        // stays out of the way.
        private bool WaypointDragging = false;

        // Ditto for zone-point handle dragging. Priority chain (highest first):
        //   ZonePointEditor > WaypointEditor > ModelSelector.
        private bool ZonePointDragging = false;

        // True while the user is drawing a new zone_point (drag-to-create). Intercepts
        // MouseMove so the preview updates in real time; released on MouseUp which
        // commits the insert. Higher priority than every selector — creation mode
        // completely overrides the normal click behaviour.
        private bool CreationDragging = false;

        // Make Width and Height properties publicly accessible
        public new int Width => base.Width;
        public new int Height => base.Height;

        public EngineCore() : base(
            1280, 720, new GraphicsMode(new ColorFormat(8, 8, 8, 8), 16, 0), "VisualEQ",
            GameWindowFlags.Default, DisplayDevice.Default, 4, 1, GraphicsContextFlags.ForwardCompatible
        )
        {
            VSync = VSyncMode.Off;
            // Fill the primary display on launch so the menu and rendered scene get maximum space.
            WindowState = WindowState.Maximized;
            Stopwatch.Start();
            Gui = new Gui(new GuiRenderer());
            WaypointEditor = new WaypointEditor(this);
            ZonePointEditor = new ZonePointEditor(this);

            MouseMove += (_, e) =>
            {
                if (!MouseLooking)
                {
                    Gui.MousePosition = (e.X, e.Y);

                    if (CreationDragging)
                    {
                        var groundHit = ProjectMouseToGround(e.X, e.Y);
                        if (groundHit.HasValue) Controller?.OnCreationMouseMove(groundHit.Value);
                    }
                    else if (ZonePointDragging)
                    {
                        ZonePointEditor?.UpdateDrag(e.X, e.Y);
                    }
                    else if (WaypointDragging)
                    {
                        WaypointEditor?.UpdateDrag(e.X, e.Y);
                    }
                    else if (ModelDragging)
                    {
                        // Use dynamic to avoid type issues
                        dynamic modelSelector = Controller?.ModelSelector;
                        modelSelector?.UpdateDrag(e.X, e.Y);
                    }
                }
            };

            MouseDown += (_, e) =>
            {
                UpdateMouseButton(e.Button, true);

                // Handle selection and dragging with left mouse button when not in GUI.
                // Waypoint edits take precedence over spawn selection so clicking a waypoint
                // crosshair (which sits at the same screen area as a spawn model) doesn't
                // accidentally grab the spawn.
                if (e.Button == MouseButton.Left && !Gui.MouseWanted)
                {
                    // Creation mode overrides selectors entirely — a click while in draw
                    // mode always starts the ground-plane drag, never selects existing
                    // handles/spawns/waypoints.
                    if (Controller != null && Controller.IsCreationActive)
                    {
                        var groundHit = ProjectMouseToGround(e.X, e.Y);
                        if (groundHit.HasValue)
                        {
                            Controller.OnCreationMouseDown(groundHit.Value);
                            CreationDragging = true;
                        }
                        return;
                    }

                    if (ZonePointEditor != null && ZonePointEditor.TrySelect(e.X, e.Y))
                    {
                        ZonePointDragging = true;
                        ZonePointEditor.StartDrag(e.X, e.Y);
                        return;
                    }

                    if (WaypointEditor != null && WaypointEditor.TrySelect(e.X, e.Y))
                    {
                        WaypointDragging = true;
                        WaypointEditor.StartDrag(e.X, e.Y);
                        return;
                    }

                    dynamic modelSelector = Controller?.ModelSelector;
                    if (modelSelector?.TrySelect(e.X, e.Y) == true)
                    {
                        ModelDragging = true;
                        modelSelector?.StartDrag(e.X, e.Y);
                        // Selecting a new spawn should clear any prior waypoint sub-selection.
                        WaypointEditor?.ClearSelection();
                    }
                }
            };

            MouseUp += (_, e) =>
            {
                UpdateMouseButton(e.Button, false);

                if (e.Button == MouseButton.Left && CreationDragging)
                {
                    CreationDragging = false;
                    Controller?.OnCreationMouseUp();
                    return;
                }

                if (e.Button == MouseButton.Left && ZonePointDragging)
                {
                    ZonePointDragging = false;
                    ZonePointEditor?.StopDrag();
                    return;
                }

                if (e.Button == MouseButton.Left && WaypointDragging)
                {
                    WaypointDragging = false;
                    WaypointEditor?.StopDrag();
                    return;
                }

                // Stop dragging on mouse up
                if (e.Button == MouseButton.Left && ModelDragging)
                {
                    ModelDragging = false;
                    dynamic modelSelector = Controller?.ModelSelector;
                    modelSelector?.StopDrag();
                }
            };
            MouseWheel += (_, e) =>
            {
                if (ZonePointDragging)
                {
                    ZonePointEditor?.AdjustDragDepth(e.Delta * 2.0f);
                }
                else if (WaypointDragging)
                {
                    WaypointEditor?.AdjustDragDepth(e.Delta * 2.0f);
                }
                else if (ModelDragging)
                {
                    // Use mouse wheel to adjust depth while dragging
                    dynamic modelSelector = Controller?.ModelSelector;
                    modelSelector?.AdjustDragDepth(e.Delta * 2.0f); // Scale the effect for better control
                }
                else
                {
                    // Normal mouse wheel handling
                    Gui.WheelDelta += e.Delta;
                }
            };

            Resize += (_, e) =>
            {
                Gui.Dimensions = new Vector2(Width, Height);
                Gui.Scale = new Vector2(1.5f);
                ProjectionMat = Matrix4x4.CreatePerspectiveFieldOfView(45 * (MathF.PI / 180), (float)Width / Height, 1, 5000);
            };

            // ImGui key map: GuiKey index → OpenTK Key int value (obtained via reflection).
            // GuiKey enum: Tab=0, LeftArrow=1, RightArrow=2, UpArrow=3, DownArrow=4,
            //              PageUp=5, PageDown=6, Home=7, End=8, Delete=9, Backspace=10,
            //              Enter=11, Escape=12, A=13, C=14, V=15, X=16, Y=17, Z=18
            Gui.SetKeyMap(0,  (int)Key.Tab);
            Gui.SetKeyMap(1,  (int)Key.Left);
            Gui.SetKeyMap(2,  (int)Key.Right);
            Gui.SetKeyMap(3,  (int)Key.Up);
            Gui.SetKeyMap(4,  (int)Key.Down);
            Gui.SetKeyMap(5,  (int)Key.PageUp);
            Gui.SetKeyMap(6,  (int)Key.PageDown);
            Gui.SetKeyMap(7,  (int)Key.Home);
            Gui.SetKeyMap(8,  (int)Key.End);
            Gui.SetKeyMap(9,  (int)Key.Delete);
            Gui.SetKeyMap(10, (int)Key.BackSpace);
            Gui.SetKeyMap(11, (int)Key.Enter);
            Gui.SetKeyMap(12, (int)Key.Escape);
            Gui.SetKeyMap(13, (int)Key.A);
            Gui.SetKeyMap(14, (int)Key.C);
            Gui.SetKeyMap(15, (int)Key.V);
            Gui.SetKeyMap(16, (int)Key.X);
            Gui.SetKeyMap(17, (int)Key.Y);
            Gui.SetKeyMap(18, (int)Key.Z);

            // Forward typed characters to ImGui so InputText fields receive input.
            KeyPress += (_, e) => Gui.HandleChar(e.KeyChar);
        }

        public void AddLight(Vector3 pos, float radius, float attenuation, Vector3 color) =>
            Lights.Add(new PointLight(pos, radius, attenuation, color));

        public void Add(Model model) => Models.Add(model);
        public void Add(AniModelInstance modelInstance) => AniModels.Add(modelInstance);

        public void AddRegion(string name, byte kind, Vector3 min, Vector3 max) =>
            Regions.Add(new LiquidRegion(name, kind, min, max));

        // Given a DB-space (X, Y) query point, returns the top-Z of the highest region of
        // the requested kind whose XY footprint contains the point. Multiple overlapping
        // pools use the highest surface — matches the intuition of a boat floating on the
        // topmost water. Returns false when no region contains the point, so the sidebar
        // can disable the snap button.
        //
        // Both inputs are DB coords (matching how spawn2.x/y and grid_entries.x/y are
        // stored). The region AABBs live in DB coords too — set at convert time from raw
        // WLD vertices, no scene-space swap.
        public bool TryGetLiquidSurfaceZAt(float dbX, float dbY, byte kind, out float surfaceZ)
        {
            surfaceZ = float.MinValue;
            var found = false;
            foreach (var r in Regions)
            {
                if (r.Kind != kind) continue;
                if (dbX < r.Min.X || dbX > r.Max.X) continue;
                if (dbY < r.Min.Y || dbY > r.Max.Y) continue;
                if (!found || r.Max.Z > surfaceZ)
                {
                    surfaceZ = r.Max.Z;
                    found = true;
                }
            }
            return found;
        }

        public void Start()
        {
            // Boot with an empty collider so the engine can run before a zone is loaded.
            // Each LoadZone call rebuilds the octree from the newly-loaded meshes.
            Collider = new CollisionHelper(new Octree(new CollisionManager.Mesh(new List<Triangle>()), 250));
            Run();
        }

        // Rebuilds the collision octree from the currently-loaded static, collidable meshes.
        // Call after loading (or swapping) a zone.
        public void RebuildCollision()
        {
            var ot = new List<Triangle>();
            Console.WriteLine("Building mesh for physics");
            foreach (var model in Models)
            {
                if (!model.IsFixed) continue;
                foreach (var mesh in model.Meshes)
                {
                    if (!mesh.IsCollidable) continue;
                    ot.AddRange(mesh.PhysicsMesh);
                }
            }

            Console.WriteLine($"Building octree for {ot.Count} triangles");
            var collMesh = new CollisionManager.Mesh(ot);
            Collider = new CollisionHelper(new Octree(collMesh, 250));
            if (ot.Count > 0)
                ZoneBounds = (collMesh.BoundingBox.Min, collMesh.BoundingBox.Max);
            else
                ZoneBounds = null;
            Console.WriteLine("Built octree");
        }

        // Drops all scene content so a new zone can be loaded on top.
        public void ClearScene()
        {
            Models.Clear();
            AniModels.Clear();
            Lights.Clear();
            Regions.Clear();
        }

        // Projects a screen-space mouse position onto the "ground" — a downward probe
        // from a high altitude through the mouse's XY ray-plane intersection at camera Z.
        // Returns the collider hit's Z if any, else the camera Z. Used by the drag-to-
        // create pipeline to compute where in-world the user is drawing.
        public Vector3? ProjectMouseToGround(int mouseX, int mouseY)
        {
            var eye = Camera.Position + new Vector3(0, 0, FpsCamera.CameraHeight);
            var rayDir = ScreenToWorldRay(mouseX, mouseY);

            // Cast into the ground plane at the camera's height — gives us the XY point
            // the user is aiming at. Then probe straight down from high altitude for the
            // actual ground Z so the box/plane sits on the surface.
            var groundNormal = new Vector3(0, 0, 1);
            var denom = Vector3.Dot(rayDir, groundNormal);
            if (Math.Abs(denom) < 0.0001f) return null;
            var t = (Camera.Position.Z - Vector3.Dot(eye, groundNormal)) / denom;
            if (t <= 0) return null;
            var xy = eye + rayDir * t;

            float z = xy.Z;
            if (Collider != null)
            {
                var probe = new Vector3(xy.X, xy.Y, 5000f);
                var hit = Collider.FindIntersection(probe, new Vector3(0, 0, -1), 0.5f);
                if (hit.HasValue) z = hit.Value.Item2.Z;
            }
            return new Vector3(xy.X, xy.Y, z);
        }

        // Screen → world ray. Same math as ZonePointEditor/WaypointEditor use internally.
        Vector3 ScreenToWorldRay(int mouseX, int mouseY)
        {
            float ndcX = 2f * mouseX / Width - 1f;
            float ndcY = 1f - 2f * mouseY / Height;
            var rayClip = new System.Numerics.Vector4(ndcX, ndcY, -1f, 1f);
            Matrix4x4.Invert(ProjectionMat, out var invProj);
            var rayView = System.Numerics.Vector4.Transform(rayClip, invProj);
            rayView = new System.Numerics.Vector4(rayView.X, rayView.Y, -1f, 0f);
            Matrix4x4.Invert(FpsCamera.Matrix, out var invView);
            var rayWorld = System.Numerics.Vector4.Transform(rayView, invView);
            return Vector3.Normalize(new Vector3(rayWorld.X, rayWorld.Y, rayWorld.Z));
        }

        void UpdateMouseButton(MouseButton button, bool state)
        {
            switch (button)
            {
                case MouseButton.Left:
                    Gui.MouseLeft = state;
                    break;
                case MouseButton.Right:
                    if (Gui.MouseWanted)
                        Gui.MouseRight = state;
                    else
                    {
                        if (state)
                        {
                            MouseLooking = true;
                            MouseBeforeLook = MousePosition;
                            CursorVisible = false;
                            LastMouseDelta = MouseDelta;
                        }
                        else
                        {
                            MouseLooking = false;
                            CursorVisible = true;
                            MousePosition = MouseBeforeLook;
                        }
                    }
                    break;
            }
        }

        readonly Dictionary<Key, bool> KeyState = new Dictionary<Key, bool>();

        protected override void OnKeyDown(KeyboardKeyEventArgs e)
        {
            // Always update ImGui key state so text editing works.
            Gui.SetModifiers(e.Control, e.Shift, e.Alt);
            Gui.SetKeyDown((int)e.Key, true);

            // Don't drive camera or trigger game actions while a text field has focus.
            if (Gui.KeyboardWanted) return;

            // Ctrl+ combos are always modifier gestures — never propagate to camera movement.
            // Undo maps to Ctrl+Z on QWERTY, which arrives here as Key.W on AZERTY (Windows
            // layout-independent VK codes: physical top-left of the top letter row is Key.W
            // regardless of the layout label). We accept both so both layouts get real Ctrl+Z.
            // Redo (Ctrl+Y) works from both layouts because Y sits at the same physical spot.
            if (e.Control && !e.Shift && !e.Alt)
            {
                if (e.Key == Key.Z || e.Key == Key.W) { Controller?.TryUndo(); return; }
                if (e.Key == Key.Y)                  { Controller?.TryRedo(); return; }
                return; // consume unrecognized Ctrl+* so camera never sees the key
            }

            switch (e.Key)
            {
                case Key.L:
                    DeferredEnabled = !DeferredEnabled;
                    break;
                case Key.Space:
                    if (Camera.OnGround)
                        Camera.FallingVelocity = -50;
                    break;
                case Key.P:
                    PhysicsEnabled = !PhysicsEnabled;
                    break;
                case Key.F10:
                    // Return to main menu — Controller may prompt if there are unsaved edits.
                    Controller?.RequestClearCurrentZone();
                    break;
                case Key.E:
                    // Toggle edit mode. Only meaningful while a zone is loaded.
                    Controller?.ToggleEditMode();
                    break;
                case Key.Escape:
                    // Priority: cancel creation → cancel drag → clear selection.
                    if (Controller != null && Controller.IsCreationActive)
                    {
                        Controller.CancelCreation();
                        CreationDragging = false;
                        break;
                    }
                    if (ZonePointDragging)
                    {
                        ZonePointEditor?.CancelDrag();
                        ZonePointDragging = false;
                        break;
                    }
                    if (WaypointDragging)
                    {
                        WaypointEditor?.CancelDrag();
                        WaypointDragging = false;
                        break;
                    }
                    if (ModelDragging)
                    {
                        dynamic modelSelector = Controller?.ModelSelector;
                        modelSelector?.CancelDrag();
                        ModelDragging = false;
                        break;
                    }
                    Controller?.ClearSelection();
                    break;
                case Key.F:
                    // Frame selection: fly camera to the currently-selected spawn using the
                    // same wall-aware vantage as sidebar list-click. No-op when nothing's
                    // selected. Fires the FrameSelectionRequested event so Controller can
                    // resolve target + perform the fly-to.
                    Controller?.FrameSelection();
                    break;
                default:
                    KeyState[e.Key] = true;
                    break;
            }
        }

        protected override void OnKeyUp(KeyboardKeyEventArgs e)
        {
            Gui.SetKeyDown((int)e.Key, false);
            Gui.SetModifiers(e.Control, e.Shift, e.Alt);
            KeyState.Remove(e.Key);
        }

        // Method to expose pressed keys (simpler implementation)
        public IEnumerable<Key> GetPressedKeys()
        {
            // Create a copy to avoid any potential collection modification issues
            return new List<Key>(KeyState.Keys);
        }

        protected override void OnUpdateFrame(FrameEventArgs e)
        {
            var movement = vec3();
            var movescale = KeyState.Keys.Contains(Key.ShiftLeft) ? 250 : 75;
            var pitchscale = 1.25f;
            var yawscale = 1.25f;
            // Skip camera movement while ImGui has keyboard focus (user is typing in a field).
            if (!Gui.KeyboardWanted)
            foreach (var key in KeyState.Keys)
                switch (key)
                {
                    case Key.W:
                        movement += vec3(0, (float)e.Time * movescale, 0);
                        break;
                    case Key.S:
                        movement += vec3(0, (float)-e.Time * movescale, 0);
                        break;
                    case Key.A:
                        movement += vec3((float)-e.Time * movescale, 0, 0);
                        break;
                    case Key.D:
                        movement += vec3((float)e.Time * movescale, 0, 0);
                        break;
                    case Key.Up:
                        Camera.Look((float)e.Time * yawscale, 0);
                        break;
                    case Key.Down:
                        Camera.Look((float)-e.Time * yawscale, 0);
                        break;
                    case Key.Left:
                        Camera.Look(0, (float)e.Time * pitchscale);
                        break;
                    case Key.Right:
                        Camera.Look(0, (float)-e.Time * pitchscale);
                        break;
                    case Key.Home:
                        Camera.Position.Z = 1000;
                        break;
                    case Key.Tilde:
                        Exit();
                        break;
                }
            if (movement.Length() > 0)
                Camera.Move(movement);
            var mdelta = MouseDelta - LastMouseDelta;
            LastMouseDelta = MouseDelta;
            if (MouseLooking && (mdelta.X != 0 || mdelta.Y != 0))
            {
                var oscale = -0.005f;
                Camera.Look(mdelta.Y * pitchscale * oscale, mdelta.X * yawscale * oscale);
            }

            Camera.Update((float)e.Time);

            base.OnUpdateFrame(e);
        }

        int _diagFrameCount = 0;

        protected override void OnRenderFrame(FrameEventArgs e)
        {
            FrameTime = Time;
            if (FrameTimes.Count == 200)
                FrameTimes.RemoveAt(0);
            FrameTimes.Add(e.Time);

            bool diag = _diagFrameCount++ == 0;

            if (diag)
            {
                Console.WriteLine($"[DIAG] Models={Models.Count} AniModels={AniModels.Count} DeferredEnabled={DeferredEnabled}");
                Console.WriteLine($"[DIAG] Width={Width} Height={Height}");
                Console.WriteLine($"[DIAG] GL_VERSION={GL.GetString(StringName.Version)}");
                Console.WriteLine($"[DIAG] GL_RENDERER={GL.GetString(StringName.Renderer)}");
            }

            ProjectionView = FpsCamera.Matrix * ProjectionMat;

            if (diag)
            {
                // Log view matrix to verify camera is set up (all zeros = Update not called yet)
                Console.WriteLine($"[DIAG] ProjView[0]={ProjectionView.M11:F4},{ProjectionView.M12:F4},{ProjectionView.M13:F4},{ProjectionView.M14:F4}");
                Console.WriteLine($"[DIAG] ProjView[3]={ProjectionView.M41:F4},{ProjectionView.M42:F4},{ProjectionView.M43:F4},{ProjectionView.M44:F4}");
                Console.WriteLine($"[DIAG] CamPos={Camera.Position.X:F1},{Camera.Position.Y:F1},{Camera.Position.Z:F1}");
                GL.GetError(); // flush any pre-existing errors
            }

            if (DeferredEnabled)
            {
                SetupDeferredPathway();
                NoProfile("Deferred render", RenderDeferredPathway);
            }

            NoProfile("Forward render", () =>
            {
                if (!DeferredEnabled)
                {
                    GL.Viewport(0, 0, Width, Height);
                    GL.ClearColor(0, 0, 0, 1);
                    GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

                    GL.Disable(EnableCap.CullFace); // disabled to rule out winding-order issues
                    GL.Disable(EnableCap.Blend);
                    GL.Enable(EnableCap.DepthTest);
                    Models.ForEach(model => model.Draw(ProjectionView, forward: false));
                    AniModels.ForEach(model => model.Draw(ProjectionView, forward: false));
                    if (diag)
                        Console.WriteLine($"[DIAG] GL error after deferred-pass draw={GL.GetError()}");
                }
                GL.Enable(EnableCap.Blend);
                GL.BlendFunc(BlendingFactorSrc.SrcAlpha, BlendingFactorDest.OneMinusSrcAlpha);
                GL.Enable(EnableCap.DepthTest);
                GL.ActiveTexture(TextureUnit.Texture0);
                GL.DepthMask(false);
                Models.ForEach(model => model.Draw(ProjectionView, forward: true));
                AniModels.ForEach(model => model.Draw(ProjectionView, forward: true));
                GL.DepthMask(true);
                if (diag)
                    Console.WriteLine($"[DIAG] GL error after forward-pass draw={GL.GetError()}");
                GL.Finish();
            });

            // Spawn state indicators (colored vertical lines above placeholder / dirty /
            // selected spawns). Lazily created on first render frame — GL context safe here.
            if (_spawnMarkers == null) _spawnMarkers = new SpawnMarkers();
            if (_pendingMarkerLines != null)
            {
                _spawnMarkers.SetLines(_pendingMarkerLines);
                _pendingMarkerLines = null;
            }
            _spawnMarkers.Draw(ProjectionView);

            // Path grid overlay (waypoints + connecting lines for the selected spawn).
            if (_pathGrids == null) _pathGrids = new PathGridRenderer();
            if (_pendingPathGridLines != null)
            {
                _pathGrids.SetLines(_pendingPathGridLines);
                _pendingPathGridLines = null;
            }
            _pathGrids.Draw(ProjectionView);

            // Zone-point trigger volumes.
            if (_zonePoints == null) _zonePoints = new ZonePointRenderer();
            if (_pendingZonePointLines != null)
            {
                _zonePoints.SetLines(_pendingZonePointLines);
                _pendingZonePointLines = null;
            }
            if (_pendingZonePointTris != null)
            {
                _zonePoints.SetTriangles(_pendingZonePointTris);
                _pendingZonePointTris = null;
            }
            _zonePoints.Draw(ProjectionView);

            Debugging.Draw(ProjectionView);

            Gui.Render((float)e.Time);

            SwapBuffers();
        }
    }
}
