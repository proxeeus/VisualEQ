using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using ImGuiNET;
using NsimGui;
using NsimGui.Widgets;
using VisualEQ.Engine;
using VisualEQ.Settings;
using VisualEQ.SpawnSystem;
using static VisualEQ.Engine.Globals;

namespace VisualEQ.Views
{
    // Fixed left-side sidebar with collapsible sections. Replaces the previously-floating
    // StatusView, TeleportView, and ModelEditorView so widget clutter doesn't overlap the
    // 3D viewport. Only visible while a zone is loaded. Section order and panel width
    // persist to settings.json.
    public class SidebarView : BaseView
    {
        private Gui _gui;
        private SidebarWidget _widget;
        private bool _widgetShown;

        // State migrated from ModelEditorView — updated by ModelSelector event handlers.
        internal AniModelInstance SelectedModel;
        internal SpawnPoint SelectedSpawn;
        internal string PosX = "0", PosY = "0", PosZ = "0";

        // State migrated from TeleportView — status message with a 3-second decay.
        internal string StatusMessage = "";
        internal float MessageTimer;

        private float _lastFrameTime;

        public SidebarView(Controller controller) : base(controller)
        {
            controller.ModelSelector.OnSelectionChanged += OnModelSelectionChanged;
            controller.ModelSelector.OnPositionChanged += OnModelPositionChanged;
            controller.SpawnManager.SpawnSelected += sp => SelectedSpawn = sp;
            controller.ZoneChanged += OnZoneChanged;
        }

        public override void Setup(Gui gui)
        {
            _gui = gui;
            _widget = new SidebarWidget(this);
            if (Controller.CurrentZoneName != null) ShowWidget();
        }

        public override void Update(Gui gui)
        {
            // Decay status message.
            float dt = FrameTime - _lastFrameTime;
            _lastFrameTime = FrameTime;
            if (StatusMessage != "" && MessageTimer > 0)
            {
                MessageTimer -= dt;
                if (MessageTimer <= 0) StatusMessage = "";
            }

            _widget?.MaybeFlushSettings();
        }

        void OnZoneChanged(string zone)
        {
            if (zone == null) HideWidget();
            else ShowWidget();
        }

        void ShowWidget()
        {
            if (_widgetShown || _gui == null || _widget == null) return;
            _gui.Add(_widget);
            _widgetShown = true;
        }

        void HideWidget()
        {
            if (!_widgetShown || _gui == null || _widget == null) return;
            _gui.Remove(_widget);
            _widgetShown = false;
        }

        void OnModelSelectionChanged(AniModelInstance model)
        {
            SelectedModel = model;
            if (model != null)
            {
                UpdatePosDisplay(model.Position);
                StatusMessage = "Model selected!";
                MessageTimer = 3f;
            }
            else
            {
                PosX = PosY = PosZ = "0";
                StatusMessage = "Model deselected";
                MessageTimer = 3f;
            }
        }

        void OnModelPositionChanged(AniModelInstance model, Vector3 pos) => UpdatePosDisplay(pos);

        void UpdatePosDisplay(Vector3 pos)
        {
            PosX = pos.X.ToString("0.00");
            PosY = pos.Y.ToString("0.00");
            PosZ = pos.Z.ToString("0.00");
        }

        internal void TeleportToOrc()
        {
            const float ORC_X = -153f, ORC_Y = 149f, ORC_Z = 80f;
            Camera.Position = new Vector3(ORC_X, ORC_Y, ORC_Z);
            StatusMessage = $"Teleported to ORC at ({ORC_X}, {ORC_Y}, {ORC_Z})";
            MessageTimer = 3f;
        }
    }

    internal class SidebarWidget : BaseWidget
    {
        // Section IDs — stable strings used in the saved order list. Adding a new section?
        // Append its ID to DefaultOrder and add a switch case in RenderSectionById.
        public const string SectionStatus      = "status";
        public const string SectionSpawnInfo   = "spawn_info";
        public const string SectionSpawnList   = "spawn_list";
        public const string SectionTeleport    = "teleport";
        public const string SectionModelEditor = "model_editor";

        static readonly string[] DefaultOrder = { SectionStatus, SectionSpawnInfo, SectionSpawnList, SectionModelEditor, SectionTeleport };

        private readonly SidebarView _view;

        // Resize IS allowed — ResizeFromAnySide lets the user drag the right edge directly.
        // Height is forced to full window height each frame via SetWindowSize.
        private const WindowFlags PinnedPanel =
            WindowFlags.NoTitleBar | WindowFlags.NoMove |
            WindowFlags.NoCollapse | WindowFlags.NoBringToFrontOnFocus |
            WindowFlags.NoSavedSettings | WindowFlags.ResizeFromAnySide;

        private const float DefaultWidth = 380f;
        private const float MinWidth = 180f;
        private const float MinRightGutter = 200f; // leave at least this many px for the 3D view
        private const float WidthSaveDebounceSec = 0.5f;

        private float _width;
        private readonly List<string> _order;

        // Spawn list filter — persists across frames (widget lives for the whole session).
        private readonly byte[] _spawnListFilter = new byte[128];

        // Debounced settings save — flush when width has been stable for a moment.
        private bool _widthDirty;
        private float _widthDirtyAt;

        public SidebarWidget(SidebarView view)
        {
            _view = view;

            var settings = view.Controller.Settings;
            _width = settings.SidebarWidth > 0 ? settings.SidebarWidth : DefaultWidth;

            _order = (settings.SidebarSectionOrder != null && settings.SidebarSectionOrder.Count > 0)
                ? new List<string>(settings.SidebarSectionOrder)
                : new List<string>(DefaultOrder);

            // Ensure any newly-added section (e.g. after upgrade) shows up at the end.
            foreach (var def in DefaultOrder)
                if (!_order.Contains(def)) _order.Add(def);
            // Drop any unknown section IDs so a corrupt/legacy settings file doesn't leave gaps.
            _order.RemoveAll(id => Array.IndexOf(DefaultOrder, id) < 0);
        }

        public override void Render(Gui gui)
        {
            var winW = gui.Dimensions.X;
            var winH = gui.Dimensions.Y;

            ImGui.SetNextWindowPos(new Vector2(0, 0), Condition.Always, Vector2.Zero);
            ImGui.SetNextWindowSize(new Vector2(_width, winH), Condition.FirstUseEver);

            ImGui.BeginWindow($"Sidebar###{Id}", PinnedPanel);

            // Force height + clamp width. Preserves whatever width the user dragged to.
            var current = ImGui.GetWindowSize();
            var w = current.X;
            if (w < MinWidth) w = MinWidth;
            if (w > winW - MinRightGutter) w = winW - MinRightGutter;
            if (Math.Abs(current.X - w) > 0.5f || Math.Abs(current.Y - winH) > 0.5f)
                ImGui.SetWindowSize(new Vector2(w, winH));
            if (Math.Abs(w - _width) > 0.5f)
            {
                _width = w;
                _widthDirty = true;
                _widthDirtyAt = FrameTime;
            }

            for (int i = 0; i < _order.Count; i++)
                RenderSectionById(_order[i], i);

            ImGui.EndWindow();
        }

        void RenderSectionById(string id, int index)
        {
            switch (id)
            {
                case SectionStatus:      RenderStatusSection(index); break;
                case SectionSpawnInfo:   RenderSpawnInfoSection(index); break;
                case SectionSpawnList:   RenderSpawnListSection(index); break;
                case SectionTeleport:    RenderTeleportSection(index); break;
                case SectionModelEditor: RenderModelEditorSection(index); break;
            }
        }

        // Small ^/v buttons rendered on the same line as the section's CollapsingHeader.
        // Returns after emitting SameLine so the header follows on the same row.
        void RenderReorderHandles(int index, string idSuffix)
        {
            var btn = new Vector2(22, 20);
            bool canUp = index > 0;
            bool canDown = index < _order.Count - 1;

            // ImGui.SmallButton doesn't take a size — use Button with a small vector for
            // consistent height regardless of font metrics.
            if (!canUp) ImGui.PushStyleColor(ColorTarget.Text, new Vector4(0.4f, 0.4f, 0.4f, 1f));
            if (ImGui.Button($"^###{Id}{idSuffix}up", btn) && canUp)
                MoveSection(index, index - 1);
            if (!canUp) ImGui.PopStyleColor();

            ImGui.SameLine();
            if (!canDown) ImGui.PushStyleColor(ColorTarget.Text, new Vector4(0.4f, 0.4f, 0.4f, 1f));
            if (ImGui.Button($"v###{Id}{idSuffix}dn", btn) && canDown)
                MoveSection(index, index + 1);
            if (!canDown) ImGui.PopStyleColor();

            ImGui.SameLine();
        }

        void MoveSection(int from, int to)
        {
            var id = _order[from];
            _order.RemoveAt(from);
            _order.Insert(to, id);

            _view.Controller.Settings.SidebarSectionOrder = new List<string>(_order);
            SettingsManager.Save(_view.Controller.Settings);
        }

        // Called from SidebarView.Update — flushes width to settings once the user stops
        // resizing (debounced). Avoids hammering settings.json every frame during a drag.
        internal void MaybeFlushSettings()
        {
            if (!_widthDirty) return;
            if (FrameTime - _widthDirtyAt < WidthSaveDebounceSec) return;

            _view.Controller.Settings.SidebarWidth = _width;
            SettingsManager.Save(_view.Controller.Settings);
            _widthDirty = false;
        }

        void RenderStatusSection(int index)
        {
            RenderReorderHandles(index, "s");
            if (!ImGui.CollapsingHeader($"Status###{Id}s", TreeNodeFlags.DefaultOpen))
                return;

            var ctrl = _view.Controller;
            ImGui.Text($"Zone: {ctrl.CurrentZoneName ?? "(none)"}");
            ImGui.Text($"Position: {Camera.Position}");
            ImGui.Text($"FPS: {ctrl.Engine.FPS:F0}");
            ImGui.Text(ctrl.DbFactory != null
                ? $"DB: Connected ({ctrl.Settings.Database.Server}/{ctrl.Settings.Database.Database})"
                : "DB: Not connected");
            ImGui.Text($"Spawns: {ctrl.SpawnManager.SpawnPoints.Count}" +
                (ctrl.SpawnManager.DirtyCount > 0 ? $"  [{ctrl.SpawnManager.DirtyCount} unsaved]" : ""));
        }

        void RenderTeleportSection(int index)
        {
            RenderReorderHandles(index, "t");
            if (!ImGui.CollapsingHeader($"Teleport###{Id}t", 0))
                return;

            if (ImGui.Button($"Teleport to ORC###{Id}tOrc", new Vector2(180, 30)))
                _view.TeleportToOrc();

            ImGui.Text($"Current position:\n{Camera.Position}");
            if (_view.StatusMessage != "")
                ImGui.Text(_view.StatusMessage);
        }

        void RenderSpawnInfoSection(int index)
        {
            RenderReorderHandles(index, "si");
            if (!ImGui.CollapsingHeader($"Spawn Info###{Id}si", TreeNodeFlags.DefaultOpen))
                return;

            var sp = _view.SelectedSpawn;
            if (sp == null)
            {
                ImGui.Text("Click a spawn to view its DB details.");
                return;
            }

            var record = sp.Record;
            var primary = record.Entries
                .OrderByDescending(e => e.Entry.Chance)
                .FirstOrDefault();
            var npc = primary?.Npc;

            if (npc != null)
            {
                ImGui.Text($"{npc.Name ?? "?"}");
                if (!string.IsNullOrEmpty(npc.LastName))
                    ImGui.Text($"  \"{npc.LastName}\"");
                ImGui.Separator();

                ImGui.Text($"Level: {npc.Level}");
                ImGui.Text($"Race: {SpawnInfoLookups.RaceName(npc.Race)} ({npc.Race})");
                ImGui.Text($"Class: {SpawnInfoLookups.ClassName(npc.Class)} ({npc.Class})");
                ImGui.Text($"Body: {SpawnInfoLookups.BodyTypeName(npc.BodyType)} ({npc.BodyType})");
                ImGui.Text($"Gender: {SpawnInfoLookups.GenderName(npc.Gender)}");
                ImGui.Text($"Size: {npc.Size:F2}");
                ImGui.Text($"Textures: body={npc.Texture}, helm={npc.HelmTexture}, face={npc.Face}");
                ImGui.Text($"NPC id: {npc.Id}");
                ImGui.Separator();
            }
            else
            {
                ImGui.Text("(no primary NPC in spawngroup)");
                ImGui.Separator();
            }

            // Spawn2 row info
            ImGui.Text($"Spawn id: {record.Spawn.Id}");
            ImGui.Text($"Group: {record.Spawn.SpawnGroupName} (id {record.Spawn.SpawnGroupId})");
            ImGui.Text($"Respawn: {record.Spawn.RespawnTime}s ± {record.Spawn.Variance}s");
            ImGui.Text($"Pos: X={record.Spawn.X:F1} Y={record.Spawn.Y:F1} Z={record.Spawn.Z:F1}");
            ImGui.Text($"Heading: {record.Spawn.Heading:F0}");
            if (record.Spawn.PathGrid > 0)
                ImGui.Text($"Path grid: {record.Spawn.PathGrid} ({record.Waypoints.Count} waypoints)");

            if (sp.IsPlaceholder)
                ImGui.Text("(placeholder model)");
            if (sp.IsDirty)
                ImGui.Text("(unsaved position changes)");

            // Other entries in the spawngroup, if any.
            if (record.Entries.Count > 1)
            {
                ImGui.Separator();
                ImGui.Text($"Spawngroup entries ({record.Entries.Count}):");
                foreach (var e in record.Entries.OrderByDescending(x => x.Entry.Chance))
                {
                    var eNpc = e.Npc;
                    ImGui.Text($"  {e.Entry.Chance,3}%: {eNpc?.Name ?? "?"} (race {eNpc?.Race}, lvl {eNpc?.Level})");
                }
            }
        }

        void RenderSpawnListSection(int index)
        {
            RenderReorderHandles(index, "sl");
            if (!ImGui.CollapsingHeader($"Spawn List###{Id}sl", 0))
                return;

            ImGui.Text("Filter (name substring):");
            ImGui.InputText($"###{Id}slF", _spawnListFilter, (uint)_spawnListFilter.Length, InputTextFlags.Default, null);
            var filter = ReadBuffer(_spawnListFilter).Trim();

            var ctrl = _view.Controller;
            var spawns = ctrl.SpawnManager.SpawnPoints;

            var matches = spawns
                .Select(sp => new { Point = sp, Name = PrimaryName(sp) })
                .Where(x => filter.Length == 0 || x.Name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
                .OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            ImGui.Text($"{matches.Count} of {spawns.Count} spawns");

            ImGui.BeginChild($"###{Id}slList", new Vector2(0, 300), true, WindowFlags.Default);
            var selected = ctrl.SpawnManager.Selected;
            foreach (var m in matches)
            {
                var sp = m.Point;
                var primary = sp.Record.Entries.OrderByDescending(e => e.Entry.Chance).FirstOrDefault();
                var lvl = primary?.Npc?.Level ?? 0;
                var label = $"{m.Name} [L{lvl}]###{Id}sl{sp.Record.Spawn.Id}";
                if (ImGui.Selectable(label, sp == selected))
                    FlyToAndSelect(sp);
            }
            ImGui.EndChild();
        }

        void FlyToAndSelect(SpawnPoint sp)
        {
            _view.Controller.SpawnManager.Select(sp.Model);

            // Face-to-face vantage. Pull the camera in if the octree finds a wall between
            // the spawn and the preferred distance so we don't end up looking at a building.
            const float PreferredDistance = 20f;
            const float MinDistance = 5f;
            const float HeadHeight = 6f;
            const float CastHeight = 3f;
            const float WallPadding = 2f;

            var basePos = sp.Model.Position;
            var forward = Vector3.Normalize(Vector3.Transform(new Vector3(0, 1, 0), sp.Model.Rotation));

            var distance = PreferredDistance;
            if (Collider != null)
            {
                var castOrigin = basePos + new Vector3(0, 0, CastHeight);
                var hit = Collider.FindIntersection(castOrigin, forward);
                if (hit.HasValue)
                {
                    var hitDist = (hit.Value.Item2 - castOrigin).Length();
                    if (hitDist < PreferredDistance)
                        distance = Math.Max(hitDist - WallPadding, MinDistance);
                }
            }

            Camera.Position = basePos + forward * distance;
            Camera.LookAt(basePos + new Vector3(0, 0, HeadHeight));
        }

        static string PrimaryName(SpawnPoint sp) =>
            sp.Record.Entries
                .OrderByDescending(e => e.Entry.Chance)
                .FirstOrDefault()?.Npc?.Name ?? "?";

        static string ReadBuffer(byte[] buf) =>
            System.Text.Encoding.UTF8.GetString(buf).TrimEnd('\0');

        void RenderModelEditorSection(int index)
        {
            RenderReorderHandles(index, "me");
            if (!ImGui.CollapsingHeader($"Model Editor###{Id}me", 0))
                return;

            ImGui.Text(_view.SelectedModel != null
                ? "Selected: Character Model"
                : "No model selected — click on a model to select");

            ImGui.Text("Position:");
            ImGui.Text($"  X: {_view.PosX}");
            ImGui.Text($"  Y: {_view.PosY}");
            ImGui.Text($"  Z: {_view.PosZ}");

            ImGui.Text("");
            ImGui.Text("Controls:");
            ImGui.Text("Left click: select model");
            ImGui.Text("Left drag: move model");
            ImGui.Text("Wheel while dragging: depth");
            ImGui.Text("Models stick to surfaces below");
        }
    }
}
