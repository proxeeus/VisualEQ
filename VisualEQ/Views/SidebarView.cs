using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using ImGuiNET;
using NsimGui;
using NsimGui.Widgets;
using VisualEQ.EditSystem;
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

        // Trigger for the F10 unsaved-changes warning modal. Set from a Controller event
        // and consumed by SidebarWidget's render.
        internal bool ShowF10Warning;

        public SidebarView(Controller controller) : base(controller)
        {
            controller.ModelSelector.OnSelectionChanged += OnModelSelectionChanged;
            controller.ModelSelector.OnPositionChanged += OnModelPositionChanged;
            controller.SpawnManager.SpawnSelected += sp => SelectedSpawn = sp;
            controller.ZoneChanged += OnZoneChanged;
            controller.UnsavedChangesOnClearRequested += () => ShowF10Warning = true;
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
        public const string SectionPending     = "pending_changes";
        public const string SectionSpawnInfo   = "spawn_info";
        public const string SectionSpawnList   = "spawn_list";
        public const string SectionTeleport    = "teleport";
        public const string SectionModelEditor = "model_editor";

        static readonly string[] DefaultOrder = { SectionStatus, SectionPending, SectionSpawnInfo, SectionSpawnList, SectionModelEditor, SectionTeleport };

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

        // Commit-to-DB dialog state. Modal draws over the whole screen when != None.
        private enum CommitPhase { None, Confirm, Running, Result }
        private CommitPhase _commitPhase = CommitPhase.None;
        private System.Threading.Tasks.Task<EditCommitter.Result> _commitTask;
        private EditCommitter.Result _commitResult;
        private int _commitEditCountSnapshot;
        private int _commitSpawnCountSnapshot;
        private int _commitGridCountSnapshot;

        // Simple confirm modals — no extra state beyond "is it open?" + a snapshot count
        // so the dialog can display consistent numbers even if the buffer mutates while
        // the dialog is up (edge case: undo runs).
        private bool _discardConfirmActive;
        private int _discardConfirmSnapshot;

        // Heading slider state. `_headingBuffer` holds the value we're currently editing
        // when the user is dragging; `_wasHeadingSliderActive` + `_headingBeforeEdit` let
        // us record a single SpawnRotateAction on release rather than one per frame.
        private float _headingBuffer;
        private bool _wasHeadingSliderActive;
        private float _headingBeforeEdit;
        private int? _headingBufferSpawnId;

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

            RenderModeBanner();

            for (int i = 0; i < _order.Count; i++)
                RenderSectionById(_order[i], i);

            ImGui.EndWindow();

            // Draw the edit-mode viewport border AFTER EndWindow so it sits above everything.
            if (_view.Controller.EditModeEnabled)
                DrawEditModeBorder(gui);

            // Modal precedence: commit dialog first, then discard confirm, then F10 warning.
            if (_commitPhase != CommitPhase.None)
                RenderCommitDialog(gui);
            else if (_discardConfirmActive)
                RenderDiscardConfirmDialog(gui);
            else if (_view.ShowF10Warning)
                RenderF10WarningDialog(gui);
        }

        void BeginCommit()
        {
            var buffer = _view.Controller.PendingBuffer;
            if (buffer == null || buffer.IsEmpty) return;
            _commitEditCountSnapshot  = buffer.TotalPending;
            _commitSpawnCountSnapshot = buffer.Spawns.Count;
            _commitGridCountSnapshot  = buffer.GridEntries.Count;
            _commitPhase = CommitPhase.Confirm;
            _commitResult = null;
        }

        void RenderCommitDialog(Gui gui)
        {
            // Reap the Task on the main thread if it just finished.
            if (_commitPhase == CommitPhase.Running && _commitTask != null && _commitTask.IsCompleted)
            {
                _commitResult = _commitTask.Result;
                _commitTask   = null;
                if (_commitResult.Success)
                    _view.Controller.OnCommitSucceeded();
                _commitPhase = CommitPhase.Result;
            }

            const float dlgW = 460f;
            var dlgH = _commitPhase == CommitPhase.Result ? 210f : 180f;
            var pos = new Vector2((gui.Dimensions.X - dlgW) / 2, (gui.Dimensions.Y - dlgH) / 2);

            ImGui.SetNextWindowPos(pos, Condition.Always, Vector2.Zero);
            ImGui.SetNextWindowSize(new Vector2(dlgW, dlgH), Condition.Always);

            const WindowFlags flags = WindowFlags.NoTitleBar | WindowFlags.NoMove
                                    | WindowFlags.NoResize   | WindowFlags.NoCollapse
                                    | WindowFlags.NoSavedSettings;

            ImGui.BeginWindow($"###{Id}CommitDlg", flags);

            switch (_commitPhase)
            {
                case CommitPhase.Confirm: RenderCommitConfirm(); break;
                case CommitPhase.Running: RenderCommitRunning(); break;
                case CommitPhase.Result:  RenderCommitResult();  break;
            }

            ImGui.EndWindow();
        }

        void RenderCommitConfirm()
        {
            var db = _view.Controller.Settings.Database;
            ImGui.Text($"Commit {_commitEditCountSnapshot} pending edits?");
            if (_commitSpawnCountSnapshot > 0)
                ImGui.Text($"  {_commitSpawnCountSnapshot} spawn move(s)");
            if (_commitGridCountSnapshot > 0)
                ImGui.Text($"  {_commitGridCountSnapshot} waypoint move(s)");
            ImGui.Separator();
            ImGui.Text($"Target: {db.Server}/{db.Database}");
            ImGui.Text("Runs as a single transaction — all-or-nothing.");
            ImGui.Separator();

            var sz = new Vector2(140, 28);
            if (ImGui.Button($"Commit###{Id}cdlgY", sz))
            {
                _commitPhase = CommitPhase.Running;
                _commitTask  = _view.Controller.CommitPendingChangesAsync();
                if (_commitTask == null)
                {
                    // Nothing to commit — reset.
                    _commitPhase = CommitPhase.None;
                }
            }
            ImGui.SameLine();
            if (ImGui.Button($"Cancel###{Id}cdlgN", sz))
                _commitPhase = CommitPhase.None;
        }

        void RenderCommitRunning()
        {
            ImGui.Text("Committing to database…");
            ImGui.Text($"  {_commitSpawnCountSnapshot} spawn move(s)");
            if (_commitGridCountSnapshot > 0)
                ImGui.Text($"  {_commitGridCountSnapshot} waypoint move(s)");
            ImGui.Separator();
            ImGui.Text("Please wait.");
        }

        void RenderCommitResult()
        {
            var r = _commitResult;
            if (r != null && r.Success)
            {
                ImGui.Text("Commit successful.");
                ImGui.Separator();
                ImGui.Text($"  {r.SpawnRowsWritten} spawn2 row(s) updated");
                ImGui.Text($"  {r.GridRowsWritten} grid_entries row(s) updated");
                ImGui.Separator();
                ImGui.Text("Buffer + undo history cleared.");
            }
            else
            {
                ImGui.Text("Commit failed.", new Vector4(0.95f, 0.35f, 0.25f, 1f));
                ImGui.Separator();
                ImGui.Text(r?.Error ?? "Unknown error.");
                ImGui.Separator();
                ImGui.Text("Pending changes are preserved — try again after resolving.");
            }

            if (ImGui.Button($"OK###{Id}cdlgOk", new Vector2(120, 28)))
                _commitPhase = CommitPhase.None;
        }

        void RenderDiscardConfirmDialog(Gui gui)
        {
            const float dlgW = 460f;
            const float dlgH = 170f;
            var pos = new Vector2((gui.Dimensions.X - dlgW) / 2, (gui.Dimensions.Y - dlgH) / 2);

            ImGui.SetNextWindowPos(pos, Condition.Always, Vector2.Zero);
            ImGui.SetNextWindowSize(new Vector2(dlgW, dlgH), Condition.Always);

            const WindowFlags flags = WindowFlags.NoTitleBar | WindowFlags.NoMove
                                    | WindowFlags.NoResize   | WindowFlags.NoCollapse
                                    | WindowFlags.NoSavedSettings;

            ImGui.BeginWindow($"###{Id}DiscardDlg", flags);

            ImGui.Text($"Discard {_discardConfirmSnapshot} pending change(s)?");
            ImGui.Separator();
            ImGui.Text("All un-committed edits will be reverted.");
            ImGui.Text("This cannot be undone.", new Vector4(0.95f, 0.35f, 0.25f, 1f));
            ImGui.Separator();

            var sz = new Vector2(140, 28);
            if (ImGui.Button($"Discard###{Id}discY", sz))
            {
                _view.Controller.DiscardPendingBuffer();
                _discardConfirmActive = false;
            }
            ImGui.SameLine();
            if (ImGui.Button($"Cancel###{Id}discN", sz))
                _discardConfirmActive = false;

            ImGui.EndWindow();
        }

        void RenderF10WarningDialog(Gui gui)
        {
            const float dlgW = 480f;
            const float dlgH = 200f;
            var pos = new Vector2((gui.Dimensions.X - dlgW) / 2, (gui.Dimensions.Y - dlgH) / 2);

            ImGui.SetNextWindowPos(pos, Condition.Always, Vector2.Zero);
            ImGui.SetNextWindowSize(new Vector2(dlgW, dlgH), Condition.Always);

            const WindowFlags flags = WindowFlags.NoTitleBar | WindowFlags.NoMove
                                    | WindowFlags.NoResize   | WindowFlags.NoCollapse
                                    | WindowFlags.NoSavedSettings;

            ImGui.BeginWindow($"###{Id}F10Dlg", flags);

            var pending = _view.Controller.PendingBuffer?.TotalPending ?? 0;
            ImGui.Text($"You have {pending} un-committed change(s).");
            ImGui.Separator();
            ImGui.Text("Leaving the zone keeps changes on disk (auto-restore on next load).");
            ImGui.Text("Commit first to write them to the database.");
            ImGui.Separator();

            var sz = new Vector2(130, 28);
            if (ImGui.Button($"Leave###{Id}f10L", sz))
            {
                _view.ShowF10Warning = false;
                _view.Controller.ClearCurrentZone();
            }
            ImGui.SameLine();
            if (ImGui.Button($"Commit first###{Id}f10C", sz))
            {
                _view.ShowF10Warning = false;
                BeginCommit();
            }
            ImGui.SameLine();
            if (ImGui.Button($"Cancel###{Id}f10X", sz))
                _view.ShowF10Warning = false;

            ImGui.EndWindow();
        }

        // Full-screen orange rectangle drawn via ImGui's overlay draw list. Sits on top of
        // both the 3D scene and every widget (including this sidebar), so the "you are in
        // edit mode" signal is always visible.
        static void DrawEditModeBorder(Gui gui)
        {
            var dl = ImGui.GetOverlayDrawList();
            const float thickness = 4f;
            var min = new Vector2(thickness / 2f, thickness / 2f);
            var max = new Vector2(gui.Dimensions.X - thickness / 2f, gui.Dimensions.Y - thickness / 2f);
            // ABGR packing: 0xAABBGGRR. Orange = (255,152,38,255) → 0xFF2698FF.
            const uint orange = 0xFF2698FFu;
            dl.AddRect(min, max, orange, 0f, 0, thickness);
        }

        // Always-visible edit-mode indicator at the top of the sidebar. Colored text +
        // toggle button. Also the anchor for future pending-change counts.
        void RenderModeBanner()
        {
            var ctrl = _view.Controller;
            var editing = ctrl.EditModeEnabled;

            if (editing)
            {
                // Orange banner + label. Colored via Vector4 overload of ImGui.Text.
                ImGui.Text("EDIT MODE — changes are staged", new Vector4(1f, 0.6f, 0.15f, 1f));
            }
            else
            {
                ImGui.Text("READ-ONLY (press E or click below to edit)", new Vector4(0.6f, 0.85f, 0.6f, 1f));
            }

            var btnLabel = editing
                ? $"Exit edit mode###{Id}editOff"
                : $"Enter edit mode###{Id}editOn";
            if (ImGui.Button(btnLabel, new Vector2(200, 24)))
                ctrl.EditModeEnabled = !editing;

            ImGui.Separator();
        }

        void RenderSectionById(string id, int index)
        {
            switch (id)
            {
                case SectionStatus:      RenderStatusSection(index); break;
                case SectionPending:     RenderPendingChangesSection(index); break;
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

            // Spawn2 row info. Position / heading show the CURRENT in-scene state, not the
            // DB baseline, so drag + rotate feedback is visible here too.
            var modelPos = sp.Model.Position;
            // Scene → DB coord swap for display purposes.
            var displayX = modelPos.Y;
            var displayY = modelPos.X;
            var displayZ = modelPos.Z;

            ImGui.Text($"Spawn id: {record.Spawn.Id}");
            ImGui.Text($"Group: {record.Spawn.SpawnGroupName} (id {record.Spawn.SpawnGroupId})");
            ImGui.Text($"Respawn: {record.Spawn.RespawnTime}s ± {record.Spawn.Variance}s");
            ImGui.Text($"Pos: X={displayX:F1} Y={displayY:F1} Z={displayZ:F1}");
            ImGui.Text($"Heading: {sp.CurrentHeading:F0}");
            if (record.Spawn.PathGrid > 0)
                ImGui.Text($"Path grid: {record.Spawn.PathGrid} ({record.Waypoints.Count} waypoints)");

            if (sp.IsPlaceholder)
                ImGui.Text("(placeholder model)");
            if (sp.IsDirty)
                ImGui.Text("(unsaved changes)");

            if (_view.Controller.EditModeEnabled)
                RenderHeadingSlider(sp);

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

        // Heading slider — live visual feedback while dragging; records a single
        // SpawnRotateAction on release. When not being actively dragged, the buffer
        // resyncs with the authoritative sp.CurrentHeading so undo/redo stay coherent.
        void RenderHeadingSlider(SpawnPoint sp)
        {
            ImGui.Separator();

            // Resync buffer with authoritative heading unless the user is currently dragging.
            var isSameSpawn = _headingBufferSpawnId == sp.Record.Spawn.Id;
            if (!_wasHeadingSliderActive || !isSameSpawn)
            {
                _headingBuffer = sp.CurrentHeading;
                _headingBufferSpawnId = sp.Record.Spawn.Id;
            }

            ImGui.Text("Edit heading (0–511):");
            var changed = ImGui.SliderFloat($"###{Id}siHead", ref _headingBuffer, 0f, 511f, "%.0f", 1f);
            var sliderActive = ImGui.IsAnyItemActive();

            if (changed)
            {
                // Live rotation of the model for feedback. sp.CurrentHeading is left alone
                // until we finalize the edit via an action on release.
                sp.Model.Rotation = SpawnManager.HeadingToRotation(_headingBuffer);
            }

            if (!_wasHeadingSliderActive && sliderActive)
            {
                _headingBeforeEdit = sp.CurrentHeading;
            }
            if (_wasHeadingSliderActive && !sliderActive)
            {
                if (Math.Abs(_headingBeforeEdit - _headingBuffer) > 0.5f)
                {
                    var action = new SpawnRotateAction(sp, _headingBeforeEdit, _headingBuffer);
                    _view.Controller.RecordAction(action);
                }
                else
                {
                    // No meaningful change — snap visual back to authoritative in case
                    // slider produced a nudge below threshold.
                    sp.Model.Rotation = SpawnManager.HeadingToRotation(sp.CurrentHeading);
                }
            }
            _wasHeadingSliderActive = sliderActive;
        }

        void RenderPendingChangesSection(int index)
        {
            RenderReorderHandles(index, "pc");

            var ctrl = _view.Controller;
            var buffer = ctrl.PendingBuffer;
            var total = buffer?.TotalPending ?? 0;

            var header = total == 0 ? "Pending Changes" : $"Pending Changes ({total})";
            if (!ImGui.CollapsingHeader($"{header}###{Id}pc", TreeNodeFlags.DefaultOpen))
                return;

            if (buffer == null || total == 0)
            {
                ImGui.Text("No pending changes.");
                return;
            }

            // Commit + Discard row.
            if (ImGui.Button($"Commit to DB###{Id}pcC", new Vector2(140, 26)))
                BeginCommit();
            ImGui.SameLine();
            if (ImGui.Button($"Discard All###{Id}pcD", new Vector2(140, 26)))
            {
                _discardConfirmSnapshot = buffer.TotalPending;
                _discardConfirmActive = true;
            }

            // Undo / Redo row.
            var us = ctrl.UndoStack;
            if (ImGui.Button($"Undo ({us.UndoCount})###{Id}pcU", new Vector2(90, 24)))
                ctrl.TryUndo();
            ImGui.SameLine();
            if (ImGui.Button($"Redo ({us.RedoCount})###{Id}pcR", new Vector2(90, 24)))
                ctrl.TryRedo();

            ImGui.Separator();

            const int maxItems = 20;
            var recentSpawns = buffer.Spawns.Values
                .OrderByDescending(s => s.LastModifiedAt)
                .Take(maxItems)
                .ToList();
            var recentGrids = buffer.GridEntries.Values
                .OrderByDescending(g => g.LastModifiedAt)
                .Take(maxItems)
                .ToList();
            var hidden = total - recentSpawns.Count - recentGrids.Count;

            ImGui.BeginChild($"###{Id}pcList", new Vector2(0, 260), true, WindowFlags.Default);

            if (recentSpawns.Count > 0)
            {
                ImGui.Text($"Spawn moves ({buffer.Spawns.Count}):");
                foreach (var edit in recentSpawns)
                    RenderPendingSpawnRow(ctrl, edit);
            }

            if (recentGrids.Count > 0)
            {
                ImGui.Text($"Waypoint moves ({buffer.GridEntries.Count}):");
                foreach (var edit in recentGrids)
                    RenderPendingGridRow(ctrl, edit);
            }

            ImGui.EndChild();

            if (hidden > 0)
                ImGui.Text($"... and {hidden} more item(s) not shown.");
        }

        void RenderPendingGridRow(Controller ctrl, GridEntryEdit edit)
        {
            ImGui.Text($"Grid {edit.GridId} waypoint #{edit.Number}");
            ImGui.SameLine();
            if (ImGui.Button($"Revert###{Id}pcRevG{edit.GridId}_{edit.Number}", new Vector2(70, 22)))
                RevertGridEdit(ctrl, edit);
        }

        void RevertGridEdit(Controller ctrl, GridEntryEdit edit)
        {
            // Current scene position of the waypoint (any spawn's copy — they're all
            // identical in-scene).
            Vector3? currentScene = null;
            foreach (var sp in ctrl.SpawnManager.SpawnPoints)
            {
                var wp = sp.Record.Waypoints.FirstOrDefault(w => w.GridId == edit.GridId && w.Number == edit.Number);
                if (wp != null) { currentScene = new Vector3(wp.Y, wp.X, wp.Z); break; }
            }
            if (currentScene == null) return;

            var targetScene = new Vector3(edit.OriginalY, edit.OriginalX, edit.OriginalZ);
            if (Vector3.DistanceSquared(currentScene.Value, targetScene) < 0.0001f) return;

            var action = new GridWaypointMoveAction(edit.GridId, edit.Number, currentScene.Value, targetScene);
            ctrl.RecordAction(action);
        }

        void RenderPendingSpawnRow(Controller ctrl, SpawnEdit edit)
        {
            var name = string.IsNullOrEmpty(edit.DisplayName) ? "?" : edit.DisplayName;
            var label = $"'{name}' (#{edit.SpawnId})";
            ImGui.Text(label);
            ImGui.SameLine();
            if (ImGui.Button($"Revert###{Id}pcRev{edit.SpawnId}", new Vector2(70, 22)))
                RevertSpawnEdit(ctrl, edit);
        }

        // Records a SpawnMoveAction whose target is the original DB position. The action's
        // buffer-cleanup logic removes the entry once Current == Original.
        void RevertSpawnEdit(Controller ctrl, SpawnEdit edit)
        {
            var sp = ctrl.SpawnManager.SpawnPoints
                .FirstOrDefault(p => p.Record.Spawn.Id == edit.SpawnId);
            if (sp == null) return;

            var currentScene = sp.Model.Position;
            // DB → scene swap: scene X = DB Y, scene Y = DB X.
            var targetScene = new Vector3(edit.OriginalY, edit.OriginalX, edit.OriginalZ);

            if (Vector3.DistanceSquared(currentScene, targetScene) < 0.0001f) return;

            var action = new SpawnMoveAction(sp, currentScene, targetScene);
            ctrl.RecordAction(action);
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
