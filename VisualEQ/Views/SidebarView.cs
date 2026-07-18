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

        // Trigger for the NPC picker (Slice 3 — Ctrl+double-click terrain placement).
        // Set when Controller.PendingPlacementRequested fires; widget consumes on next
        // render + resets. Widget reads Controller.PendingPlacementScenePos to find
        // where the click landed.
        internal bool ShowNpcPicker;

        public SidebarView(Controller controller) : base(controller)
        {
            controller.ModelSelector.OnSelectionChanged += OnModelSelectionChanged;
            controller.ModelSelector.OnPositionChanged += OnModelPositionChanged;
            controller.SpawnManager.SpawnSelected += sp => SelectedSpawn = sp;
            controller.ZoneChanged += OnZoneChanged;
            controller.UnsavedChangesOnClearRequested += () => ShowF10Warning = true;
            controller.PendingPlacementRequested += () => ShowNpcPicker = true;
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
            // Constants are named in scene-space (matching how they're passed to the
            // Camera). Camera.Position is scene-space — see CLAUDE.md §8.
            const float ORC_SCENE_X = -153f, ORC_SCENE_Y = 149f, ORC_SCENE_Z = 80f;
            Camera.Position = new Vector3(ORC_SCENE_X, ORC_SCENE_Y, ORC_SCENE_Z);
            // Report in DB coords so it matches the client's /loc.
            StatusMessage = $"Teleported to ORC at (X={ORC_SCENE_Y}, Y={ORC_SCENE_X}, Z={ORC_SCENE_Z})";
            MessageTimer = 3f;
        }
    }

    internal class SidebarWidget : BaseWidget
    {
        // Section IDs — stable strings used in the saved order list. Adding a new section?
        // Append its ID to DefaultOrder and add a switch case in RenderSectionById.
        public const string SectionStatus       = "status";
        public const string SectionPending      = "pending_changes";
        public const string SectionSpawnInfo    = "spawn_info";
        public const string SectionWaypointInfo = "waypoint_info";
        public const string SectionSpawnList    = "spawn_list";
        public const string SectionGridList     = "grid_list";
        public const string SectionZonePoints   = "zone_points";
        public const string SectionTeleport     = "teleport";
        public const string SectionModelEditor  = "model_editor";

        static readonly string[] DefaultOrder = { SectionStatus, SectionPending, SectionSpawnInfo, SectionWaypointInfo, SectionSpawnList, SectionGridList, SectionZonePoints, SectionModelEditor, SectionTeleport };

        private readonly SidebarView _view;

        // Resize IS allowed — ResizeFromAnySide lets the user drag the right edge directly.
        // Height is forced to full window height each frame via SetWindowSize.
        private const WindowFlags PinnedPanel =
            WindowFlags.NoTitleBar | WindowFlags.NoMove |
            WindowFlags.NoCollapse | WindowFlags.NoBringToFrontOnFocus |
            WindowFlags.NoSavedSettings | WindowFlags.ResizeFromAnySide |
            // Always-visible right-side scrollbar so users know they CAN scroll when
            // stacked sections push content off the bottom (inspector fields especially).
            WindowFlags.AlwaysVerticalScrollbar;

        private const float DefaultWidth = 380f;
        private const float MinWidth = 180f;
        private const float MinRightGutter = 200f; // leave at least this many px for the 3D view
        private const float WidthSaveDebounceSec = 0.5f;
        // Sidebar height reserves this many pixels at the bottom of the client area so
        // the OS taskbar (which can overlap the app on some Windows setups, especially
        // maximized-over-work-area quirks in Parallels) doesn't cover the last row of
        // scrollable content. Users on setups without this issue lose a small amount of
        // vertical space — trade for reliable "scroll reaches the end" behavior.
        private const float BottomSafeAreaPx = 60f;

        private float _width;
        private readonly List<string> _order;

        // Spawn list filter — persists across frames (widget lives for the whole session).
        private readonly byte[] _spawnListFilter = new byte[128];

        // Grid list filter — mirrors the spawn-list pattern. Substring match against the
        // grid id (short enough that 64 bytes is generous).
        private readonly byte[] _gridListFilter = new byte[64];

        // Commit-to-DB dialog state. Modal draws over the whole screen when != None.
        private enum CommitPhase { None, Confirm, Running, Result }
        private CommitPhase _commitPhase = CommitPhase.None;
        private System.Threading.Tasks.Task<EditCommitter.Result> _commitTask;
        private EditCommitter.Result _commitResult;
        private int _commitEditCountSnapshot;
        private int _commitSpawnCountSnapshot;
        private int _commitSpawnDeleteCountSnapshot;
        private int _commitSpawnInsertCountSnapshot;
        private int _commitGridCountSnapshot;
        private int _commitGridInsertCountSnapshot;
        private int _commitGridDeleteCountSnapshot;
        private int _commitGridMetaCountSnapshot;
        private int _commitGridWholeInsertsSnapshot;

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

        // Zone-point inspector state — single "currently active field" tracker so per-field
        // edits get one action on release (not one per keystroke). ImGui only lets one item
        // be active at a time so a single-slot tracker is sufficient.
        //
        // The reader is stored alongside the field so a defensive flush (fires when the user
        // moves focus from field A to field B) uses A's reader, not B's — otherwise we'd
        // record an action for field A with field B's value type and blow up on the cast.
        private int? _zpActiveEditZonePointId;
        private VisualEQ.EditSystem.ZonePointFieldEditAction.Field _zpActiveEditField;
        private object _zpActiveEditBeforeValue;
        private Func<object> _zpActiveEditReader;

        // Text-field state for InputText widgets in the inspector. Byte buffer must persist
        // across frames or focus is lost each frame; reset when selection or the underlying
        // row value changes while not being edited. Used only as a fallback when the target-
        // zone dropdown can't populate (no DB configured, no cached shortnames).
        private readonly byte[] _zpTargetZoneBuffer = new byte[64];
        private int? _zpTargetZoneBufferForId;

        // Delete-confirmation state. Two-click gate — first click arms, second click within
        // DeleteConfirmSeconds actually deletes. Simpler than a modal, matches the
        // "click twice to confirm" convention.
        private int _zpDeleteArmedForId;
        private float _zpDeleteArmedAt;
        private const float DeleteConfirmSeconds = 3f;

        // Spawn Info delete button — same two-click arm/confirm pattern as zone-points.
        private int _spDeleteArmedForId;
        private float _spDeleteArmedAt;

        // NPC picker modal (Slice 3). Opened by ShowNpcPicker flag (set by Controller
        // event via SidebarView). Fetch runs on background Task; results ranked by
        // whatever SqlQueries.SearchNpcTypes returns (ORDER BY name). Filter refetches
        // fire when the byte-buffer text changes.
        private bool _npcPickerActive;
        private System.Numerics.Vector3 _npcPickerScenePos;
        private readonly byte[] _npcPickerFilterBuf = new byte[64];
        private string _npcPickerLastQueried;
        private System.Threading.Tasks.Task<System.Collections.Generic.List<VisualEQ.Database.Models.NpcType>> _npcPickerFetchTask;
        private System.Collections.Generic.List<VisualEQ.Database.Models.NpcType> _npcPickerResults =
            new System.Collections.Generic.List<VisualEQ.Database.Models.NpcType>();
        private int _npcPickerSelectedIdx = -1;
        private string _npcPickerError;

        // Waypoint inspector state — parallel to _zpActiveEdit* but keyed on (gridId, number)
        // instead of a single row id.
        private int? _wpActiveEditGridId;
        private int _wpActiveEditNumber;
        private VisualEQ.EditSystem.GridEntryFieldEditAction.Field _wpActiveEditField;
        private object _wpActiveEditBeforeValue;
        private Func<object> _wpActiveEditReader;

        // Spawn-position edit state. Only ONE ImGui item can be active at a time, so a
        // single-slot tracker is sufficient. We store:
        //   _spawnPosBeforeScalar — the tracked-axis DB value at activation, used for the
        //     diff check on flush. Per-field (matches the waypoint/ZP inspector pattern) so
        //     a neighboring widget's flush doesn't fire on this axis's live delta.
        //   _spawnPosBeforeScene  — the whole scene position at activation, used to build
        //     the SpawnMoveAction's from-vector.
        // The per-axis scalar check is what prevents the "Y drag records X's + Y's + Z's
        // flush all firing on the same delta" duplicate-action bug — each axis flushes
        // only when ITS own DB value has moved.
        private enum SpawnPosAxis { X, Y, Z }
        private int? _spawnPosActiveSpawnId;
        private SpawnPosAxis _spawnPosActiveAxis;
        private float _spawnPosBeforeScalar;
        private Vector3 _spawnPosBeforeScene;

        // Waypoint delete-confirmation state. Key is the "gridId:number" composite so the
        // arm survives frame-to-frame even if selection briefly clears.
        private string _wpDeleteArmedForKey;
        private float _wpDeleteArmedAt;

        // Grid-metadata edits track their pre-mutation value at combo-select time (they
        // fire immediately, no field-active transition needed).

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
            // Reserve safe area at the bottom so scroll-to-end reveals the last row above
            // any OS taskbar overlap.
            var winH = Math.Max(100f, gui.Dimensions.Y - BottomSafeAreaPx);

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

            // Always-visible coordinate overlay in the top-right — camera + selected spawn +
            // drag delta. Renders as its own small ImGui window with no chrome.
            RenderCoordinateHud(gui);

            // Modal precedence: commit dialog first, then discard confirm, then F10 warning,
            // then NPC picker. Only one shows at a time; NPC picker sits last so the more
            // important buffer-lifecycle modals always win when the user Ctrl+double-clicks
            // during e.g. a commit prompt.
            if (_commitPhase != CommitPhase.None)
                RenderCommitDialog(gui);
            else if (_discardConfirmActive)
                RenderDiscardConfirmDialog(gui);
            else if (_view.ShowF10Warning)
                RenderF10WarningDialog(gui);
            else
            {
                // Latch the sidebar-view request → widget-owned state on first render
                // so subsequent frames stay in the modal until user confirms/cancels.
                if (_view.ShowNpcPicker && !_npcPickerActive)
                {
                    BeginNpcPicker(_view.Controller.PendingPlacementScenePos ?? System.Numerics.Vector3.Zero);
                    _view.ShowNpcPicker = false;
                }
                if (_npcPickerActive)
                    RenderNpcPickerDialog(gui);
            }
        }

        // Small always-visible overlay showing camera + selection state. Positioned in the
        // top-right corner of the OS window; grows/shrinks based on how much info there is.
        void RenderCoordinateHud(Gui gui)
        {
            const float hudW = 300f;
            var pos = new Vector2(gui.Dimensions.X - hudW - 8f, 8f);

            ImGui.SetNextWindowPos(pos, Condition.Always, Vector2.Zero);
            ImGui.SetNextWindowSize(new Vector2(hudW, 0), Condition.Always);

            const WindowFlags flags = WindowFlags.NoTitleBar | WindowFlags.NoMove
                                    | WindowFlags.NoResize   | WindowFlags.NoCollapse
                                    | WindowFlags.NoSavedSettings | WindowFlags.NoBringToFrontOnFocus
                                    | WindowFlags.NoInputs   | WindowFlags.AlwaysAutoResize;

            ImGui.BeginWindow($"###{Id}Hud", flags);

            var cam = Camera.Position;
            // Camera.Position is scene-space (X/Y swapped from DB per CLAUDE.md §8).
            // Un-swap so the labels match DB coord axes and cross-reference 1:1 with
            // the EQ client's /loc output and the trilogy_zone_points x/y/z columns.
            ImGui.Text($"Cam: X={cam.Y:F0}  Y={cam.X:F0}  Z={cam.Z:F0}");

            var sp = _view.SelectedSpawn;
            if (sp != null)
            {
                // Scene → DB coord un-swap for the readout.
                var p = sp.Model.Position;
                ImGui.Text($"Sel: X={p.Y:F0}  Y={p.X:F0}  Z={p.Z:F0}  H={sp.CurrentHeading:F0}");
                if (sp.IsDirty)
                {
                    var op = sp.OriginalPosition;
                    var dx = p.Y - op.Y;
                    var dy = p.X - op.X;
                    var dz = p.Z - op.Z;
                    ImGui.Text($"Δ:   dX={dx:+0;-0}  dY={dy:+0;-0}  dZ={dz:+0;-0}");
                }
            }

            var wp = _view.Controller.Engine.WaypointEditor.Selected;
            if (wp.HasValue)
            {
                var s = wp.Value.ScenePos;
                ImGui.Text($"WP:  grid={wp.Value.GridId}  #{wp.Value.Number}  X={s.Y:F0}  Y={s.X:F0}  Z={s.Z:F0}");
            }

            ImGui.EndWindow();
        }

        void BeginCommit()
        {
            var buffer = _view.Controller.PendingBuffer;
            if (buffer == null || buffer.IsEmpty) return;
            _commitEditCountSnapshot        = buffer.TotalPending;
            _commitSpawnCountSnapshot       = buffer.Spawns.Count;
            _commitSpawnDeleteCountSnapshot = buffer.SpawnDeletes.Count;
            _commitSpawnInsertCountSnapshot = buffer.SpawnInserts.Count;
            _commitGridCountSnapshot        = buffer.GridEntries.Count;
            _commitGridInsertCountSnapshot  = buffer.GridEntryInserts.Count;
            _commitGridDeleteCountSnapshot  = buffer.GridEntryDeletes.Count;
            _commitGridMetaCountSnapshot    = buffer.Grids.Count;
            _commitGridWholeInsertsSnapshot = buffer.GridInserts.Count;
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
                    _view.Controller.OnCommitSucceeded(_commitResult);
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
            if (_commitSpawnDeleteCountSnapshot > 0)
                ImGui.Text($"  {_commitSpawnDeleteCountSnapshot} spawn delete(s)");
            if (_commitSpawnInsertCountSnapshot > 0)
                ImGui.Text($"  {_commitSpawnInsertCountSnapshot} new spawn(s)");
            if (_commitGridCountSnapshot > 0)
                ImGui.Text($"  {_commitGridCountSnapshot} waypoint edit(s)");
            if (_commitGridInsertCountSnapshot > 0)
                ImGui.Text($"  {_commitGridInsertCountSnapshot} waypoint add(s)");
            if (_commitGridDeleteCountSnapshot > 0)
                ImGui.Text($"  {_commitGridDeleteCountSnapshot} waypoint delete(s)");
            if (_commitGridMetaCountSnapshot > 0)
                ImGui.Text($"  {_commitGridMetaCountSnapshot} grid metadata edit(s)");
            if (_commitGridWholeInsertsSnapshot > 0)
                ImGui.Text($"  {_commitGridWholeInsertsSnapshot} new grid(s)");
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
                if (r.SpawnDeletesWritten > 0)
                    ImGui.Text($"  {r.SpawnDeletesWritten} spawn2 row(s) deleted");
                if (r.SpawnInsertsWritten > 0)
                    ImGui.Text($"  {r.SpawnInsertsWritten} spawn2 row(s) inserted (+ spawngroup + spawnentries)");
                ImGui.Text($"  {r.GridRowsWritten} grid_entries row(s) updated");
                if (r.GridEntryInsertsWritten > 0)
                    ImGui.Text($"  {r.GridEntryInsertsWritten} grid_entries row(s) inserted");
                if (r.GridEntryDeletesWritten > 0)
                    ImGui.Text($"  {r.GridEntryDeletesWritten} grid_entries row(s) deleted");
                if (r.GridMetaRowsWritten > 0)
                    ImGui.Text($"  {r.GridMetaRowsWritten} grid row(s) updated");
                if (r.GridInsertsWritten > 0)
                    ImGui.Text($"  {r.GridInsertsWritten} grid row(s) inserted");
                ImGui.Text($"  {r.ZonePointRowsWritten} trilogy_zone_points row(s) updated");
                if (r.ZonePointInsertsWritten > 0)
                    ImGui.Text($"  {r.ZonePointInsertsWritten} trilogy_zone_points row(s) inserted");
                if (r.ZonePointDeletesWritten > 0)
                    ImGui.Text($"  {r.ZonePointDeletesWritten} trilogy_zone_points row(s) deleted");
                ImGui.Separator();
                ImGui.Text("Buffer + undo history cleared.");
                var touchedZonePoints = r.ZonePointRowsWritten + r.ZonePointInsertsWritten + r.ZonePointDeletesWritten;
                if (touchedZonePoints > 0)
                {
                    ImGui.Separator();
                    ImGui.Text("Run '#reload static' on the zone process to apply live.");
                }
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

        // ─── NPC picker modal (Slice 3) ──────────────────────────────────────────────

        void BeginNpcPicker(System.Numerics.Vector3 sceneHitPos)
        {
            _npcPickerActive       = true;
            _npcPickerScenePos     = sceneHitPos;
            System.Array.Clear(_npcPickerFilterBuf, 0, _npcPickerFilterBuf.Length);
            _npcPickerLastQueried  = null;   // force initial fetch (empty filter → up to 500 rows)
            _npcPickerResults.Clear();
            _npcPickerSelectedIdx  = -1;
            _npcPickerError        = null;
            _npcPickerFetchTask    = null;
        }

        void EndNpcPicker()
        {
            _npcPickerActive = false;
            _view.Controller.ClearPendingPlacement();
        }

        void RenderNpcPickerDialog(Gui gui)
        {
            const float dlgW = 520f;
            const float dlgH = 460f;
            var pos = new Vector2((gui.Dimensions.X - dlgW) / 2, (gui.Dimensions.Y - dlgH) / 2);

            ImGui.SetNextWindowPos(pos, Condition.Always, Vector2.Zero);
            ImGui.SetNextWindowSize(new Vector2(dlgW, dlgH), Condition.Always);

            const WindowFlags flags = WindowFlags.NoTitleBar | WindowFlags.NoMove
                                    | WindowFlags.NoResize   | WindowFlags.NoCollapse
                                    | WindowFlags.NoSavedSettings;

            ImGui.BeginWindow($"###{Id}NpcPickerDlg", flags);

            // Scene → DB coord swap for the readout so the numbers match sidebar / DB.
            var dbX = _npcPickerScenePos.Y;
            var dbY = _npcPickerScenePos.X;
            var dbZ = _npcPickerScenePos.Z;
            ImGui.Text($"Place new spawn at X={dbX:F1}  Y={dbY:F1}  Z={dbZ:F1}");
            ImGui.Separator();

            ImGui.Text("Filter (name substring):");
            ImGui.InputText($"###{Id}npcF", _npcPickerFilterBuf, (uint)_npcPickerFilterBuf.Length, InputTextFlags.Default, null);
            var filter = ReadBuffer(_npcPickerFilterBuf).Trim();

            // Reap any in-flight fetch first, so we can display fresh results this frame.
            if (_npcPickerFetchTask != null && _npcPickerFetchTask.IsCompleted)
            {
                if (_npcPickerFetchTask.IsFaulted)
                {
                    _npcPickerError = _npcPickerFetchTask.Exception?.GetBaseException().Message ?? "unknown error";
                    _npcPickerResults.Clear();
                }
                else
                {
                    _npcPickerError = null;
                    _npcPickerResults = _npcPickerFetchTask.Result ?? new System.Collections.Generic.List<VisualEQ.Database.Models.NpcType>();
                    // Reset selection when list changes (previous idx would point at wrong NPC).
                    _npcPickerSelectedIdx = _npcPickerResults.Count > 0 ? 0 : -1;
                }
                _npcPickerFetchTask = null;
            }

            // Fire a fresh query if the filter changed since the last one we sent (and
            // there's nothing in flight — one query at a time keeps this simple).
            if (_npcPickerFetchTask == null && filter != _npcPickerLastQueried)
            {
                _npcPickerLastQueried = filter;
                var factory = _view.Controller.DbFactory;
                if (factory == null)
                {
                    _npcPickerError = "No database connection is configured.";
                    _npcPickerResults.Clear();
                }
                else
                {
                    var repo = new VisualEQ.Database.Repositories.SpawnRepository(factory);
                    // Cap at 200 so a paginated NPC picker isn't needed yet (users will
                    // narrow via filter). Enough to browse a zone's likely candidates.
                    _npcPickerFetchTask = System.Threading.Tasks.Task.Run(async () =>
                        (await repo.SearchNpcTypesAsync(filter, 200)).ToList());
                }
            }

            ImGui.Separator();

            if (_npcPickerError != null)
            {
                ImGui.Text($"Error: {_npcPickerError}", new Vector4(0.95f, 0.35f, 0.25f, 1f));
            }
            else if (_npcPickerFetchTask != null)
            {
                ImGui.Text("Searching…");
            }
            else
            {
                ImGui.Text($"{_npcPickerResults.Count} match(es)");
            }

            // Result list. Each row is a Selectable so click-to-select works; double-
            // click confirms (fired via the button — Selectable doesn't expose
            // double-click here in ImGui.NET 0.4.6).
            ImGui.BeginChild($"###{Id}npcList", new Vector2(0, 300), true, WindowFlags.Default);
            for (int i = 0; i < _npcPickerResults.Count; i++)
            {
                var n = _npcPickerResults[i];
                var raceName = SpawnInfoLookups.RaceName(n.Race);
                var label = $"{n.Name ?? "?"}  [L{n.Level}]  {raceName}  (id {n.Id})###{Id}npcRow{i}";
                if (ImGui.Selectable(label, i == _npcPickerSelectedIdx))
                    _npcPickerSelectedIdx = i;
            }
            ImGui.EndChild();

            ImGui.Separator();

            var confirmSize = new Vector2(140, 28);
            var confirmEnabled = _npcPickerSelectedIdx >= 0 && _npcPickerSelectedIdx < _npcPickerResults.Count;
            if (confirmEnabled)
            {
                if (ImGui.Button($"Place spawn###{Id}npcOk", confirmSize))
                {
                    var picked = _npcPickerResults[_npcPickerSelectedIdx];
                    _view.Controller.PlaceNewSpawn(picked, _npcPickerScenePos);
                    EndNpcPicker();
                }
            }
            else
            {
                // Grayed-out no-op button. Cheaper than InvisibleButton + rect for the
                // handful of frames the dialog spends without a selection.
                ImGui.Text("(pick an NPC to enable Place)");
            }
            ImGui.SameLine();
            if (ImGui.Button($"Cancel###{Id}npcX", confirmSize))
                EndNpcPicker();

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
                case SectionStatus:       RenderStatusSection(index); break;
                case SectionPending:      RenderPendingChangesSection(index); break;
                case SectionSpawnInfo:    RenderSpawnInfoSection(index); break;
                case SectionWaypointInfo: RenderWaypointInfoSection(index); break;
                case SectionSpawnList:    RenderSpawnListSection(index); break;
                case SectionGridList:     RenderGridListSection(index); break;
                case SectionZonePoints:   RenderZonePointsSection(index); break;
                case SectionTeleport:     RenderTeleportSection(index); break;
                case SectionModelEditor:  RenderModelEditorSection(index); break;
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
            // Camera.Position is scene-space (X/Y swapped). Un-swap for DB / /loc parity.
            var pos = Camera.Position;
            ImGui.Text($"Position: X={pos.Y:F1}  Y={pos.X:F1}  Z={pos.Z:F1}");
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

            // Camera.Position is scene-space (X/Y swapped). Un-swap for DB / /loc parity.
            var tpPos = Camera.Position;
            ImGui.Text($"Current position:\nX={tpPos.Y:F1}  Y={tpPos.X:F1}  Z={tpPos.Z:F1}");
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

            if (_view.Controller.EditModeEnabled)
                RenderSpawnPositionFields(sp);
            else
                ImGui.Text($"Pos: X={displayX:F1} Y={displayY:F1} Z={displayZ:F1}");

            ImGui.Text($"Heading: {sp.CurrentHeading:F0}");
            if (record.Spawn.PathGrid > 0)
                ImGui.Text($"Path grid: {record.Spawn.PathGrid} ({record.Waypoints.Count} waypoints)");

            if (sp.IsPlaceholder)
                ImGui.Text("(placeholder model)");
            if (sp.IsDirty)
                ImGui.Text("(unsaved changes)");

            // Delete affordance — two-click confirm to prevent misclicks. Mirrors the
            // zone-point Delete flow. Only visible in edit mode; the Delete key hotkey
            // fires the same action.
            if (_view.Controller.EditModeEnabled)
            {
                var spawnId = record.Spawn.Id;
                if (_spDeleteArmedForId == spawnId &&
                    (FrameTime - _spDeleteArmedAt) < DeleteConfirmSeconds)
                {
                    if (ImGui.Button($"Confirm delete###{Id}spDelC", new Vector2(160, 24)))
                    {
                        _view.Controller.DeleteSelectedSpawn();
                        _spDeleteArmedForId = 0;
                    }
                    ImGui.SameLine();
                    if (ImGui.Button($"Cancel###{Id}spDelX", new Vector2(90, 24)))
                        _spDeleteArmedForId = 0;
                }
                else
                {
                    if (ImGui.Button($"Delete this spawn###{Id}spDel", new Vector2(180, 24)))
                    {
                        _spDeleteArmedForId = spawnId;
                        _spDeleteArmedAt    = FrameTime;
                    }
                }

                // Duplicate — no confirm gate (it's non-destructive; you can Ctrl+Z or
                // Discard). Ctrl+D hotkey fires the same action. New spawn is created at
                // the camera-anchor ground point with a cloned spawngroup + spawnentries.
                ImGui.SameLine();
                if (ImGui.Button($"Duplicate (Ctrl+D)###{Id}spDup", new Vector2(180, 24)))
                    _view.Controller.DuplicateSelectedSpawn();
            }

            if (_view.Controller.EditModeEnabled)
                RenderHeadingSlider(sp);

            if (_view.Controller.EditModeEnabled)
                RenderSpawnSnapToWater(sp);

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

            // Stacked spawn2 rows at the same DB coord — EQEmu respawn-rotation encoding.
            // In-game only one of these is alive at a time; VisualEQ visualises all of
            // them so they z-fight into a smear. The cycler lets the user step through
            // stacked siblings without a 3D click.
            RenderStackedSpawnsBlock(sp);
        }

        void RenderStackedSpawnsBlock(SpawnPoint sp)
        {
            if (sp.StackSiblings == null || sp.StackSiblings.Count <= 1) return;

            ImGui.Separator();
            var idx = 0;
            for (int i = 0; i < sp.StackSiblings.Count; i++)
                if (sp.StackSiblings[i] == sp) { idx = i; break; }

            ImGui.Text($"Stacked at same coord: {idx + 1} of {sp.StackSiblings.Count} spawn2 rows");
            ImGui.Text("(server rotates on respawn — only one is alive at a time)");

            var prev = sp.StackSiblings[(idx - 1 + sp.StackSiblings.Count) % sp.StackSiblings.Count];
            var next = sp.StackSiblings[(idx + 1) % sp.StackSiblings.Count];

            if (ImGui.Button($"< prev in stack###{Id}spStackPrev", new Vector2(140, 22)))
                _view.Controller.SpawnManager.Select(prev.Model);
            ImGui.SameLine();
            if (ImGui.Button($"next in stack >###{Id}spStackNext", new Vector2(140, 22)))
                _view.Controller.SpawnManager.Select(next.Model);

            ImGui.Text("Stack members:");
            foreach (var member in sp.StackSiblings)
            {
                var memberNpc = member.Record.Entries
                    .OrderByDescending(e => e.Entry.Chance)
                    .FirstOrDefault()?.Npc;
                var mName = memberNpc?.Name ?? "?";
                var mRace = memberNpc?.Race.ToString() ?? "?";
                var marker = member == sp ? ">" : " ";
                ImGui.Text($"{marker} #{member.Record.Spawn.Id} {mName} (race {mRace})");
            }
        }

        // Selected-waypoint inspector. Renders the grid_entries row for the waypoint
        // currently owned by Engine.WaypointEditor, plus the parent grid metadata
        // (grid.type / grid.type2) and add/delete controls. When edit mode is off,
        // fields render as read-only text.
        void RenderWaypointInfoSection(int index)
        {
            RenderReorderHandles(index, "wi");
            if (!ImGui.CollapsingHeader($"Waypoint Info###{Id}wi", TreeNodeFlags.DefaultOpen))
                return;

            var ctrl = _view.Controller;
            var handle = ctrl.Engine.WaypointEditor.Selected;
            if (!handle.HasValue)
            {
                ImGui.Text("Click a waypoint crosshair to view its DB details.");
                return;
            }

            var gridId = handle.Value.GridId;
            var number = handle.Value.Number;
            var wp = VisualEQ.EditSystem.GridActionHelpers.FindWaypoint(ctrl, gridId, number);
            if (wp == null)
            {
                ImGui.Text($"Waypoint (grid {gridId}, #{number}) no longer in scene.");
                return;
            }

            // Which grid does this waypoint belong to? Look up via any spawn referencing
            // it — zoneId comes from the grid row itself. Orphan grids don't have any
            // referencing spawn, so fall back to the zone-wide list before giving up.
            var parentGrid = ctrl.SpawnManager.SpawnPoints
                .Select(sp => sp.Record.Grid)
                .FirstOrDefault(g => g != null && g.Id == gridId);
            if (parentGrid == null)
            {
                parentGrid = ctrl.ZoneGrids
                    .Select(zg => zg.Grid)
                    .FirstOrDefault(g => g != null && g.Id == gridId);
            }
            int zoneId = parentGrid?.ZoneId ?? 0;

            var editable = ctrl.EditModeEnabled;
            var buffer = ctrl.PendingBuffer;
            var key = VisualEQ.EditSystem.EditBuffer.GridEntryKey(gridId, number);
            var isPendingInsert = buffer != null && buffer.GridEntryInserts.ContainsKey(key);
            var isDirty         = buffer != null && (buffer.GridEntries.ContainsKey(key) || isPendingInsert);

            var dirtySuffix = isPendingInsert ? " [NEW]" : (isDirty ? " *" : "");
            ImGui.Text($"Grid {gridId}  waypoint #{number}{dirtySuffix}");
            ImGui.Text($"  (drag in world for X/Y/Z, or type values below)");

            ImGui.Separator();
            ImGui.Text("Position (DB axes)");
            RenderWpFloatField(gridId, number, VisualEQ.EditSystem.GridEntryFieldEditAction.Field.X,
                "x", () => wp.X, v => wp.X = v, editable);
            RenderWpFloatField(gridId, number, VisualEQ.EditSystem.GridEntryFieldEditAction.Field.Y,
                "y", () => wp.Y, v => wp.Y = v, editable);
            RenderWpFloatField(gridId, number, VisualEQ.EditSystem.GridEntryFieldEditAction.Field.Z,
                "z", () => wp.Z, v => wp.Z = v, editable);

            ImGui.Separator();
            ImGui.Text("Facing");
            RenderWpHeading(gridId, number, wp, editable);

            ImGui.Separator();
            ImGui.Text("Timing");
            RenderWpIntField(gridId, number, VisualEQ.EditSystem.GridEntryFieldEditAction.Field.Pause,
                "pause (s)", () => wp.Pause, v => wp.Pause = v, editable);

            ImGui.Separator();
            RenderWpCenterpointCheckbox(gridId, number, wp, editable);

            if (parentGrid != null)
            {
                ImGui.Separator();
                ImGui.Text($"Grid {gridId} metadata");
                RenderGridTypeCombo(parentGrid, editable);
                RenderGridType2Combo(parentGrid, editable);
            }

            if (editable)
            {
                RenderWaypointSnapToWater(gridId, number, wp);
                ImGui.Separator();
                RenderWaypointAddButton(gridId, zoneId, wp);
                RenderWaypointDeleteButton(gridId, zoneId, number, wp, isPendingInsert);
            }
        }

        // Snap-Z-to-water for waypoints. Waypoint fields are DB coords, but the region
        // query expects SCENE coords — so pass (wp.Y, wp.X) as the query XY (same swap
        // spawn instances use at load: Vector3(spawn.Y, spawn.X, spawn.Z)). Same
        // diagnostic-always-visible pattern as the spawn version. Fires a
        // GridEntryFieldEditAction on the Z field so undo/redo and buffer coalescing
        // behave exactly like a manual Z edit.
        void RenderWaypointSnapToWater(int gridId, int number, VisualEQ.Database.Models.GridEntry wp)
        {
            var engine = _view.Controller.Engine;
            var waterCount = 0;
            foreach (var r in engine.Regions)
                if (r.Kind == VisualEQ.Engine.LiquidRegion.KindWater) waterCount++;

            // DB → scene swap for the query point.
            var qx = wp.Y;
            var qy = wp.X;

            ImGui.Separator();
            ImGui.Text($"Snap to water  (this zone has {waterCount} water region(s))");
            ImGui.Text($"Waypoint DB XY = ({wp.X:F1}, {wp.Y:F1})");

            if (waterCount == 0)
            {
                ImGui.Text("This zone has no water — re-convert to detect any.");
                return;
            }

            string label;
            float surfaceZ;
            if (engine.TryGetLiquidSurfaceZAt(qx, qy, VisualEQ.Engine.LiquidRegion.KindWater, out surfaceZ))
            {
                label = $"Snap Z to water (surface Z = {surfaceZ:F1})";
                ImGui.Text($"Over water. Current Z = {wp.Z:F1}");
            }
            else if (engine.TryGetNearestLiquidSurfaceZ(qx, qy, VisualEQ.Engine.LiquidRegion.KindWater,
                out surfaceZ, out var nearestName, out var dist))
            {
                label = $"Snap Z to nearest water plane (surface Z = {surfaceZ:F1})";
                ImGui.Text($"Outside water AABB — nearest region '{nearestName}' (~{dist:F0} units away)");
                ImGui.Text($"Current Z = {wp.Z:F1}");
            }
            else
            {
                return;
            }

            if (ImGui.Button($"{label}###{Id}wpSnapW{gridId}_{number}", new Vector2(280, 24)))
            {
                if (Math.Abs(wp.Z - surfaceZ) > 0.001f)
                    _view.Controller.RecordAction(new VisualEQ.EditSystem.GridEntryFieldEditAction(
                        gridId, number,
                        VisualEQ.EditSystem.GridEntryFieldEditAction.Field.Z,
                        wp.Z, surfaceZ));
            }
            ImGui.Text("Tip: hold Ctrl while dragging to keep custom Z (below surface, etc.)");
        }

        void RenderWpFloatField(int gridId, int number,
            VisualEQ.EditSystem.GridEntryFieldEditAction.Field which,
            string label, Func<float> read, Action<float> write, bool editable)
        {
            var current = read();
            if (!editable)
            {
                ImGui.Text($"  {label} = {current:F2}");
                return;
            }
            var val = current;
            var changed = ImGui.DragFloat($"{label}###{Id}wpF{gridId}_{number}_{(int)which}",
                ref val, 0f, 0f, 1f, "%.2f", 1f);
            if (changed) write(val);
            HandleWpActivationTransition(gridId, number, which, current, () => (object)read());
        }

        // Heading has three states, so it gets its own render helper rather than reusing
        // the generic float-field:
        //   heading == -1  → EQEmu sentinel "don't rotate on arrival". Preserved literally.
        //   heading ∈ [0, 511]  → normal arrival facing.
        //   heading outside those  → legacy/foreign value. Slider clamps on save; warning shown.
        void RenderWpHeading(int gridId, int number, VisualEQ.Database.Models.GridEntry wp, bool editable)
        {
            const float NoRotationSentinel = -1f;
            var current = wp.Heading;
            var noRotation = Math.Abs(current - NoRotationSentinel) < 0.001f;

            if (!editable)
            {
                ImGui.Text(noRotation
                    ? "  heading = -1 (no rotation on arrival)"
                    : $"  heading = {current:F2}");
                return;
            }

            var boxVal = noRotation;
            if (ImGui.Checkbox($"No rotation on arrival###{Id}wpNoRot{gridId}_{number}", ref boxVal)
                && boxVal != noRotation)
            {
                // Checked: set sentinel. Unchecked: seed a valid heading (0 = due north).
                // Both flow through the standard field-edit action → one undo step.
                float from = current;
                float to   = boxVal ? NoRotationSentinel : 0f;
                _view.Controller.RecordAction(
                    new VisualEQ.EditSystem.GridEntryFieldEditAction(
                        gridId, number,
                        VisualEQ.EditSystem.GridEntryFieldEditAction.Field.Heading,
                        from, to));
                return; // Skip the slider this frame — value changed via checkbox.
            }

            if (noRotation)
            {
                ImGui.Text("  (mob keeps its incoming facing at this waypoint)");
                return;
            }

            // Regular slider path. Legacy out-of-range values (e.g. -126 from an older tool
            // with a different convention) render with a warning and clamp on interaction.
            RenderWpBoundedFloatField(gridId, number,
                VisualEQ.EditSystem.GridEntryFieldEditAction.Field.Heading,
                "heading (0–511)", 0f, 511f, () => wp.Heading, v => wp.Heading = v, editable);
        }

        // SliderFloat variant for fields with a fixed range (currently just heading, 0-511).
        // Unbounded DragFloat is dangerous for heading: a stray drag on the widget can shove
        // the value hundreds of units off in a single gesture and users don't realize they
        // dragged — they think they clicked. Bounded slider matches the spawn heading widget.
        void RenderWpBoundedFloatField(int gridId, int number,
            VisualEQ.EditSystem.GridEntryFieldEditAction.Field which,
            string label, float min, float max,
            Func<float> read, Action<float> write, bool editable)
        {
            var current = read();
            if (!editable)
            {
                ImGui.Text($"  {label} = {current:F2}");
                return;
            }
            var val = current;
            // Legacy rows may store out-of-range values (older EQEmu tools with different
            // conventions, hand-authored SQL). Show them as-is via the label but clamp the
            // slider input so the widget is safe to interact with.
            if (val < min || val > max)
                ImGui.Text($"  (current DB value {current:F2} is outside slider range — save will clamp)");
            var clamped = Math.Max(min, Math.Min(max, val));
            var changed = ImGui.SliderFloat($"{label}###{Id}wpS{gridId}_{number}_{(int)which}",
                ref clamped, min, max, "%.0f", 1f);
            if (changed) write(clamped);
            HandleWpActivationTransition(gridId, number, which, current, () => (object)read());
        }

        void RenderWpIntField(int gridId, int number,
            VisualEQ.EditSystem.GridEntryFieldEditAction.Field which,
            string label, Func<int> read, Action<int> write, bool editable)
        {
            var current = read();
            if (!editable)
            {
                ImGui.Text($"  {label} = {current}");
                return;
            }
            // ImGui.NET 0.4.6 doesn't expose InputInt — use DragFloat and round to int.
            var val = (float)current;
            var changed = ImGui.DragFloat($"{label}###{Id}wpI{gridId}_{number}_{(int)which}",
                ref val, 0f, 0f, 1f, "%.0f", 1f);
            if (changed)
            {
                var asInt = (int)Math.Round(val);
                if (asInt < 0) asInt = 0;
                write(asInt);
            }
            HandleWpActivationTransition(gridId, number, which, current, () => (object)read());
        }

        void RenderWpCenterpointCheckbox(int gridId, int number, VisualEQ.Database.Models.GridEntry wp, bool editable)
        {
            var current = wp.Centerpoint;
            if (!editable)
            {
                ImGui.Text($"  centerpoint = {(current != 0 ? "true" : "false")}");
                return;
            }
            var val = current != 0;
            if (ImGui.Checkbox($"centerpoint###{Id}wpCP{gridId}_{number}", ref val))
            {
                byte before = current;
                byte after  = (byte)(val ? 1 : 0);
                if (before != after)
                {
                    _view.Controller.RecordAction(
                        new VisualEQ.EditSystem.GridEntryFieldEditAction(
                            gridId, number,
                            VisualEQ.EditSystem.GridEntryFieldEditAction.Field.Centerpoint,
                            before, after));
                }
            }
        }

        static readonly string[] GridTypeLabels =
        {
            "Circular",
            "Random10",
            "Patrol",
            "One-way",
            "Random5",
        };

        static readonly string[] GridType2Labels =
        {
            "Half-random pause",
            "Full pause",
            "Full-random pause",
        };

        void RenderGridTypeCombo(VisualEQ.Database.Models.Grid grid, bool editable)
        {
            var current = grid.Type;
            var name = current >= 0 && current < GridTypeLabels.Length ? GridTypeLabels[current] : "?";
            if (!editable)
            {
                ImGui.Text($"  type = {name} ({current})");
                return;
            }

            ImGui.Text("type (wander behavior)");
            // Clamp to valid Combo range so legacy out-of-list values don't crash the widget.
            var refIdx = Math.Max(0, Math.Min(GridTypeLabels.Length - 1, current));
            if (ImGui.Combo($"###{Id}gT{grid.Id}", ref refIdx, GridTypeLabels) && refIdx != current)
            {
                _view.Controller.RecordAction(
                    new VisualEQ.EditSystem.GridFieldEditAction(
                        grid.Id, grid.ZoneId,
                        VisualEQ.EditSystem.GridFieldEditAction.Field.Type,
                        current, refIdx));
            }
        }

        void RenderGridType2Combo(VisualEQ.Database.Models.Grid grid, bool editable)
        {
            var current = grid.Type2;
            var name = current >= 0 && current < GridType2Labels.Length ? GridType2Labels[current] : "?";
            if (!editable)
            {
                ImGui.Text($"  type2 = {name} ({current})");
                return;
            }

            ImGui.Text("type2 (pause behavior)");
            var refIdx = Math.Max(0, Math.Min(GridType2Labels.Length - 1, current));
            if (ImGui.Combo($"###{Id}gT2{grid.Id}", ref refIdx, GridType2Labels) && refIdx != current)
            {
                _view.Controller.RecordAction(
                    new VisualEQ.EditSystem.GridFieldEditAction(
                        grid.Id, grid.ZoneId,
                        VisualEQ.EditSystem.GridFieldEditAction.Field.Type2,
                        current, refIdx));
            }
        }

        // "Add next waypoint" — appends a new grid_entries row at max(Number)+1 within the
        // grid, seeded from the currently-selected waypoint's coordinates + heading + pause.
        void RenderWaypointAddButton(int gridId, int zoneId, VisualEQ.Database.Models.GridEntry seed)
        {
            if (ImGui.Button($"Add next waypoint###{Id}wpAdd{gridId}", new Vector2(200, 24)))
            {
                var ctrl = _view.Controller;
                int maxNumber = 0;
                foreach (var sp in ctrl.SpawnManager.SpawnPoints)
                    foreach (var wp in sp.Record.Waypoints)
                        if (wp.GridId == gridId && wp.Number > maxNumber)
                            maxNumber = wp.Number;
                // Orphan grids only live in ZoneGrids — scan there too so the new number
                // doesn't collide with an existing waypoint the SpawnPoints scan missed.
                foreach (var zg in ctrl.ZoneGrids)
                    foreach (var wp in zg.Waypoints)
                        if (wp.GridId == gridId && wp.Number > maxNumber)
                            maxNumber = wp.Number;
                int newNumber = maxNumber + 1;

                var action = new VisualEQ.EditSystem.GridEntryInsertAction(
                    gridId, zoneId, newNumber,
                    seed.X, seed.Y, seed.Z, seed.Heading, seed.Pause, seed.Centerpoint);
                ctrl.RecordAction(action);
            }
        }

        // Delete affordance — two-click confirm. Uses the composite key so the arm persists
        // even if the user briefly clicks elsewhere between click 1 and click 2.
        void RenderWaypointDeleteButton(int gridId, int zoneId, int number,
            VisualEQ.Database.Models.GridEntry snapshot, bool isPendingInsert)
        {
            var key = VisualEQ.EditSystem.EditBuffer.GridEntryKey(gridId, number);
            var armed = _wpDeleteArmedForKey == key && (FrameTime - _wpDeleteArmedAt) < DeleteConfirmSeconds;

            if (armed)
            {
                if (ImGui.Button($"Confirm delete###{Id}wpDelC{gridId}_{number}", new Vector2(160, 24)))
                {
                    var ctrl = _view.Controller;
                    ctrl.RecordAction(new VisualEQ.EditSystem.GridEntryDeleteAction(
                        gridId, zoneId, snapshot, isPendingInsert));
                    _wpDeleteArmedForKey = null;
                }
                ImGui.SameLine();
                if (ImGui.Button($"Cancel###{Id}wpDelX{gridId}_{number}", new Vector2(90, 24)))
                    _wpDeleteArmedForKey = null;
            }
            else
            {
                if (ImGui.Button($"Delete this waypoint###{Id}wpDel{gridId}_{number}", new Vector2(200, 24)))
                {
                    _wpDeleteArmedForKey = key;
                    _wpDeleteArmedAt     = FrameTime;
                }
            }
        }

        void HandleWpActivationTransition(int gridId, int number,
            VisualEQ.EditSystem.GridEntryFieldEditAction.Field which,
            object beforeValueIfStarting,
            Func<object> readCurrent)
        {
            var isActive = ImGui.IsAnyItemActive();
            var wasThisFieldActive =
                _wpActiveEditGridId == gridId &&
                _wpActiveEditNumber == number &&
                _wpActiveEditField == which;

            if (isActive && !wasThisFieldActive)
            {
                if (_wpActiveEditGridId.HasValue)
                    FlushWpActiveEditIfChanged();
                _wpActiveEditGridId      = gridId;
                _wpActiveEditNumber      = number;
                _wpActiveEditField       = which;
                _wpActiveEditBeforeValue = beforeValueIfStarting;
                _wpActiveEditReader      = readCurrent;
            }
            else if (!isActive && wasThisFieldActive)
            {
                FlushWpActiveEditIfChanged();
            }
        }

        void FlushWpActiveEditIfChanged()
        {
            if (!_wpActiveEditGridId.HasValue || _wpActiveEditReader == null) return;

            var ctrl = _view.Controller;
            var gridId = _wpActiveEditGridId.Value;
            var number = _wpActiveEditNumber;
            var after  = _wpActiveEditReader();
            var before = _wpActiveEditBeforeValue;

            bool changed = !object.Equals(before ?? "", after ?? "");
            if (before is float bf && after is float af) changed = Math.Abs(bf - af) > 0.001f;
            if (before is int bi && after is int ai)     changed = bi != ai;
            if (before is byte bb && after is byte ab)   changed = bb != ab;

            if (changed)
            {
                ctrl.RecordAction(new VisualEQ.EditSystem.GridEntryFieldEditAction(
                    gridId, number, _wpActiveEditField, before, after));
            }
            _wpActiveEditGridId      = null;
            _wpActiveEditBeforeValue = null;
            _wpActiveEditReader      = null;
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

        // Editable X/Y/Z fields for spawn position. Mirrors the waypoint DragFloat pattern:
        // live-mutate sp.Model.Position for immediate visual feedback while typing/dragging,
        // then emit a single SpawnMoveAction on release so undo/redo + pending buffer stay
        // coherent. DB axes (X = scene.Y, Y = scene.X, Z = scene.Z) — same swap the display
        // uses. Per-axis snapshots are captured BEFORE each DragFloat's write-back so the
        // activation-transition baseline is the true pre-edit state.
        void RenderSpawnPositionFields(SpawnPoint sp)
        {
            var spawnId = sp.Record.Spawn.Id;

            ImGui.Text("Position (DB axes)");

            // X (DB) ↔ scene.Y
            var beforeX = sp.Model.Position;
            var xVal = beforeX.Y;
            var xChanged = ImGui.DragFloat($"x###{Id}siPosX{spawnId}",
                ref xVal, 0f, 0f, 1f, "%.2f", 1f);
            if (xChanged)
                sp.Model.Position = new Vector3(beforeX.X, xVal, beforeX.Z);
            HandleSpawnPosTransition(sp, SpawnPosAxis.X, beforeX.Y, beforeX);

            // Y (DB) ↔ scene.X — re-read since X's write may have changed sp.Model.Position.
            var beforeY = sp.Model.Position;
            var yVal = beforeY.X;
            var yChanged = ImGui.DragFloat($"y###{Id}siPosY{spawnId}",
                ref yVal, 0f, 0f, 1f, "%.2f", 1f);
            if (yChanged)
                sp.Model.Position = new Vector3(yVal, beforeY.Y, beforeY.Z);
            HandleSpawnPosTransition(sp, SpawnPosAxis.Y, beforeY.X, beforeY);

            // Z (DB) ↔ scene.Z
            var beforeZ = sp.Model.Position;
            var zVal = beforeZ.Z;
            var zChanged = ImGui.DragFloat($"z###{Id}siPosZ{spawnId}",
                ref zVal, 0f, 0f, 1f, "%.2f", 1f);
            if (zChanged)
                sp.Model.Position = new Vector3(beforeZ.X, beforeZ.Y, zVal);
            HandleSpawnPosTransition(sp, SpawnPosAxis.Z, beforeZ.Z, beforeZ);
        }

        void HandleSpawnPosTransition(SpawnPoint sp, SpawnPosAxis axis,
            float beforeScalarIfStarting, Vector3 beforeSceneIfStarting)
        {
            var isActive = ImGui.IsAnyItemActive();
            var wasThisFieldActive =
                _spawnPosActiveSpawnId == sp.Record.Spawn.Id &&
                _spawnPosActiveAxis == axis;

            if (isActive && !wasThisFieldActive)
            {
                if (_spawnPosActiveSpawnId.HasValue) FlushSpawnPosEdit();
                _spawnPosActiveSpawnId = sp.Record.Spawn.Id;
                _spawnPosActiveAxis    = axis;
                _spawnPosBeforeScalar  = beforeScalarIfStarting;
                _spawnPosBeforeScene   = beforeSceneIfStarting;
            }
            else if (!isActive && wasThisFieldActive)
            {
                FlushSpawnPosEdit();
            }
        }

        void FlushSpawnPosEdit()
        {
            if (!_spawnPosActiveSpawnId.HasValue) return;

            var ctrl = _view.Controller;
            var spawnId = _spawnPosActiveSpawnId.Value;
            var axis    = _spawnPosActiveAxis;
            var before  = _spawnPosBeforeScene;
            var beforeScalar = _spawnPosBeforeScalar;
            _spawnPosActiveSpawnId = null;

            var sp = ctrl.SpawnManager.SpawnPoints.FirstOrDefault(p => p.Record.Spawn.Id == spawnId);
            if (sp == null) return;

            // Per-axis diff. This is the key correctness bit: IsAnyItemActive is global, so
            // when the user drags Y, X's and Z's HandleSpawnPosTransition will ALSO see
            // isActive=true and try to flush any prior-tracked axis. Comparing the whole
            // vector would treat Y's delta as though it belonged to X (or Z), producing
            // duplicate SpawnMoveActions and breaking undo. Comparing only the tracked axis
            // means a flush from a neighboring widget is a clean no-op unless THAT axis
            // was actually moved.
            var after = sp.Model.Position;
            float currentScalar;
            switch (axis)
            {
                case SpawnPosAxis.X: currentScalar = after.Y; break;
                case SpawnPosAxis.Y: currentScalar = after.X; break;
                default:             currentScalar = after.Z; break;
            }
            if (Math.Abs(currentScalar - beforeScalar) < 0.001f)
                return;

            // Live mutation already happened during the drag; Apply's MarkMoved(after) is a
            // visual no-op but is what wires IsDirty + buffer + undo state. Same flow the
            // 3D-world drag uses (see Controller.HandleDragCompleted).
            ctrl.RecordAction(new SpawnMoveAction(sp, before, after));
        }

        // Snap-Z-to-water affordance for spawns. Always renders a diagnostic block in edit
        // mode so a user who expects the button but isn't over water can see WHY it's not
        // firing (out-of-region, no regions loaded, wrong zone). Button only enables when
        // a water region actually contains the spawn's DB XY footprint. Records a standard
        // SpawnMoveAction so undo/redo, pending-buffer, and commit flow through unchanged.
        void RenderSpawnSnapToWater(SpawnPoint sp)
        {
            var engine = _view.Controller.Engine;
            // Region AABBs live in SCENE coords (WLD vertices loaded with Identity).
            // sp.Model.Position is scene coords too, so pass it straight — no swap.
            var scenePos = sp.Model.Position;

            var waterCount = 0;
            foreach (var r in engine.Regions)
                if (r.Kind == VisualEQ.Engine.LiquidRegion.KindWater) waterCount++;

            ImGui.Separator();
            ImGui.Text($"Snap to water  (this zone has {waterCount} water region(s))");
            // Display the DB coord un-swap so users can cross-reference with spawn2 rows.
            ImGui.Text($"Spawn DB XY = ({scenePos.Y:F1}, {scenePos.X:F1})");

            if (waterCount == 0)
            {
                ImGui.Text("This zone has no water — re-convert to detect any.");
                return;
            }

            string label;
            float surfaceZ;
            if (engine.TryGetLiquidSurfaceZAt(scenePos.X, scenePos.Y, VisualEQ.Engine.LiquidRegion.KindWater, out surfaceZ))
            {
                label = $"Snap Z to water (surface Z = {surfaceZ:F1})";
                ImGui.Text($"Over water. Current Z = {scenePos.Z:F1}");
            }
            else if (engine.TryGetNearestLiquidSurfaceZ(scenePos.X, scenePos.Y, VisualEQ.Engine.LiquidRegion.KindWater,
                out surfaceZ, out var nearestName, out var dist))
            {
                label = $"Snap Z to nearest water plane (surface Z = {surfaceZ:F1})";
                ImGui.Text($"Outside water AABB — nearest region '{nearestName}' (~{dist:F0} units away)");
                ImGui.Text($"Current Z = {scenePos.Z:F1}");
            }
            else
            {
                return; // No water at all — shouldn't hit this since waterCount > 0.
            }

            if (ImGui.Button($"{label}###{Id}siSnapW{sp.Record.Spawn.Id}", new Vector2(280, 24)))
            {
                var newScenePos = new Vector3(scenePos.X, scenePos.Y, surfaceZ);
                if (Vector3.DistanceSquared(scenePos, newScenePos) > 0.0001f)
                    _view.Controller.RecordAction(new SpawnMoveAction(sp, scenePos, newScenePos));
            }
            ImGui.Text("Tip: hold Ctrl while dragging to keep custom Z (below surface, etc.)");
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
            var recentSpawnDeletes = buffer.SpawnDeletes.Values
                .OrderByDescending(s => s.DeletedAt)
                .Take(maxItems)
                .ToList();
            var recentSpawnInserts = buffer.SpawnInserts.Values
                .OrderByDescending(s => s.CreatedAt)
                .Take(maxItems)
                .ToList();
            var recentGrids = buffer.GridEntries.Values
                .OrderByDescending(g => g.LastModifiedAt)
                .Take(maxItems)
                .ToList();
            var recentGridMeta = buffer.Grids.Values
                .OrderByDescending(g => g.LastModifiedAt)
                .Take(maxItems)
                .ToList();
            var recentInserts = buffer.GridEntryInserts.Values
                .OrderByDescending(g => g.CreatedAt)
                .Take(maxItems)
                .ToList();
            var recentDeletes = buffer.GridEntryDeletes.Values
                .OrderByDescending(g => g.DeletedAt)
                .Take(maxItems)
                .ToList();
            var hidden = total
                - recentSpawns.Count - recentSpawnDeletes.Count - recentSpawnInserts.Count - recentGrids.Count
                - recentGridMeta.Count - recentInserts.Count - recentDeletes.Count;

            // Fixed-height list child. Wheel scroll IS handled by the child when the
            // mouse hovers it (standard ImGui behavior) — so if you hover the list,
            // wheel scrolls the list; if you hover the inspector below, wheel scrolls
            // the parent sidebar. Kept modest so the sidebar doesn't drown under lists.
            ImGui.BeginChild($"###{Id}pcList", new Vector2(0, 180), true, WindowFlags.Default);

            if (recentSpawns.Count > 0)
            {
                ImGui.Text($"Spawn moves ({buffer.Spawns.Count}):");
                foreach (var edit in recentSpawns)
                    RenderPendingSpawnRow(ctrl, edit);
            }

            if (recentSpawnDeletes.Count > 0)
            {
                ImGui.Text($"Spawn deletes ({buffer.SpawnDeletes.Count}):");
                foreach (var del in recentSpawnDeletes)
                    RenderPendingSpawnDeleteRow(ctrl, del);
            }

            if (recentSpawnInserts.Count > 0)
            {
                ImGui.Text($"Spawn adds ({buffer.SpawnInserts.Count}):");
                foreach (var ins in recentSpawnInserts)
                    RenderPendingSpawnInsertRow(ctrl, ins);
            }

            if (recentGrids.Count > 0)
            {
                ImGui.Text($"Waypoint edits ({buffer.GridEntries.Count}):");
                foreach (var edit in recentGrids)
                    RenderPendingGridRow(ctrl, edit);
            }

            if (recentInserts.Count > 0)
            {
                ImGui.Text($"Waypoint adds ({buffer.GridEntryInserts.Count}):");
                foreach (var ins in recentInserts)
                    RenderPendingGridInsertRow(ctrl, ins);
            }

            if (recentDeletes.Count > 0)
            {
                ImGui.Text($"Waypoint deletes ({buffer.GridEntryDeletes.Count}):");
                foreach (var del in recentDeletes)
                    RenderPendingGridDeleteRow(ctrl, del);
            }

            if (recentGridMeta.Count > 0)
            {
                ImGui.Text($"Grid metadata ({buffer.Grids.Count}):");
                foreach (var edit in recentGridMeta)
                    RenderPendingGridMetaRow(ctrl, edit);
            }

            ImGui.EndChild();

            if (hidden > 0)
                ImGui.Text($"... and {hidden} more item(s) not shown.");
        }

        void RenderPendingGridInsertRow(Controller ctrl, VisualEQ.EditSystem.GridEntryInsert ins)
        {
            ImGui.Text($"Grid {ins.GridId} #{ins.Number} [NEW]");
            ImGui.SameLine();
            if (ImGui.Button($"Revert###{Id}pcRevGi{ins.GridId}_{ins.Number}", new Vector2(70, 22)))
            {
                // Deleting a pending-insert row cleanly undoes the add.
                ctrl.RecordAction(new VisualEQ.EditSystem.GridEntryDeleteAction(
                    ins.GridId, ins.ZoneId,
                    new VisualEQ.Database.Models.GridEntry
                    {
                        GridId      = ins.GridId,
                        Number      = ins.Number,
                        X           = ins.X,
                        Y           = ins.Y,
                        Z           = ins.Z,
                        Heading     = ins.Heading,
                        Pause       = ins.Pause,
                        Centerpoint = ins.Centerpoint,
                    },
                    wasPendingInsert: true));
            }
        }

        void RenderPendingGridDeleteRow(Controller ctrl, VisualEQ.EditSystem.GridEntryDelete del)
        {
            ImGui.Text($"Grid {del.GridId} #{del.Number} [DELETE]");
            ImGui.SameLine();
            if (ImGui.Button($"Revert###{Id}pcRevGd{del.GridId}_{del.Number}", new Vector2(70, 22)))
            {
                // Re-inserts the snapshot row.
                ctrl.RecordAction(new VisualEQ.EditSystem.GridEntryInsertAction(
                    del.GridId, del.ZoneId, del.Number,
                    del.X, del.Y, del.Z, del.Heading, del.Pause, del.Centerpoint));
            }
        }

        void RenderPendingGridMetaRow(Controller ctrl, VisualEQ.EditSystem.GridEdit edit)
        {
            ImGui.Text($"Grid {edit.Id} type={edit.CurrentType} type2={edit.CurrentType2}");
            ImGui.SameLine();
            if (ImGui.Button($"Revert###{Id}pcRevGm{edit.Id}", new Vector2(70, 22)))
            {
                if (edit.CurrentType != edit.OriginalType)
                    ctrl.RecordAction(new VisualEQ.EditSystem.GridFieldEditAction(
                        edit.Id, edit.ZoneId,
                        VisualEQ.EditSystem.GridFieldEditAction.Field.Type,
                        edit.CurrentType, edit.OriginalType));
                if (edit.CurrentType2 != edit.OriginalType2)
                    ctrl.RecordAction(new VisualEQ.EditSystem.GridFieldEditAction(
                        edit.Id, edit.ZoneId,
                        VisualEQ.EditSystem.GridFieldEditAction.Field.Type2,
                        edit.CurrentType2, edit.OriginalType2));
            }
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
            // Find the live waypoint so we can compute per-field deltas.
            VisualEQ.Database.Models.GridEntry live = null;
            foreach (var sp in ctrl.SpawnManager.SpawnPoints)
            {
                var wp = sp.Record.Waypoints.FirstOrDefault(w => w.GridId == edit.GridId && w.Number == edit.Number);
                if (wp != null) { live = wp; break; }
            }
            if (live == null) return;

            // Position revert (X/Y/Z as one move action so the polyline snaps in one step).
            var currentScene = new Vector3(live.Y, live.X, live.Z);
            var targetScene  = new Vector3(edit.OriginalY, edit.OriginalX, edit.OriginalZ);
            if (Vector3.DistanceSquared(currentScene, targetScene) > 0.0001f)
                ctrl.RecordAction(new GridWaypointMoveAction(edit.GridId, edit.Number, currentScene, targetScene));

            // Scalar reverts — heading, pause, centerpoint — one action each so undo can
            // walk them back individually.
            if (Math.Abs(live.Heading - edit.OriginalHeading) > 0.001f)
                ctrl.RecordAction(new VisualEQ.EditSystem.GridEntryFieldEditAction(
                    edit.GridId, edit.Number,
                    VisualEQ.EditSystem.GridEntryFieldEditAction.Field.Heading,
                    live.Heading, edit.OriginalHeading));
            if (live.Pause != edit.OriginalPause)
                ctrl.RecordAction(new VisualEQ.EditSystem.GridEntryFieldEditAction(
                    edit.GridId, edit.Number,
                    VisualEQ.EditSystem.GridEntryFieldEditAction.Field.Pause,
                    live.Pause, edit.OriginalPause));
            if (live.Centerpoint != edit.OriginalCenterpoint)
                ctrl.RecordAction(new VisualEQ.EditSystem.GridEntryFieldEditAction(
                    edit.GridId, edit.Number,
                    VisualEQ.EditSystem.GridEntryFieldEditAction.Field.Centerpoint,
                    live.Centerpoint, edit.OriginalCenterpoint));
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

        void RenderPendingSpawnDeleteRow(Controller ctrl, VisualEQ.EditSystem.SpawnDelete del)
        {
            var name = string.IsNullOrEmpty(del.DisplayName) ? "?" : del.DisplayName;
            ImGui.Text($"'{name}' (#{del.SpawnId}) [DELETE]");
            ImGui.SameLine();
            if (ImGui.Button($"Revert###{Id}pcRevSd{del.SpawnId}", new Vector2(70, 22)))
            {
                // Symmetric with SpawnDeleteAction — Apply un-hides, Revert re-hides. Undo
                // history stays coherent: Ctrl+Z after this re-deletes, another Ctrl+Z restores.
                ctrl.RecordAction(new VisualEQ.EditSystem.SpawnRestoreAction(del.SpawnId, del.DisplayName));
            }
        }

        void RenderPendingSpawnInsertRow(Controller ctrl, VisualEQ.EditSystem.SpawnInsert ins)
        {
            var name = string.IsNullOrEmpty(ins.DisplayName) ? "?" : ins.DisplayName;
            ImGui.Text($"'{name}' (temp #{ins.TempSpawnId}) [NEW]");
            ImGui.SameLine();
            if (ImGui.Button($"Revert###{Id}pcRevSi{ins.TempSpawnId}", new Vector2(70, 22)))
            {
                // Find the temp SpawnPoint in the scene and issue a delete against it —
                // SpawnDeleteAction's pending-insert path drops the SpawnInsert entry and
                // detaches the node in one shot (mirrors the grid-entry pattern).
                var sp = ctrl.SpawnManager.SpawnPoints
                    .FirstOrDefault(p => p.Record.Spawn.Id == ins.TempSpawnId);
                if (sp != null)
                    ctrl.RecordAction(new VisualEQ.EditSystem.SpawnDeleteAction(sp, ins));
            }
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

            ImGui.BeginChild($"###{Id}slList", new Vector2(0, 200), true, WindowFlags.Default);
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
            _view.Controller.FrameSelection();
        }

        // Grid List — every grid in the current zone, attached AND orphan. Clicking a row
        // sets Controller.SelectedGridId which drives UpdatePathGrids to render that grid's
        // polyline (magenta for orphan, amber for attached). Clicking the currently-selected
        // row again deselects. Independent of spawn selection — the polyline for the picked
        // grid overrides the selected-spawn's polyline until deselected.
        void RenderGridListSection(int index)
        {
            RenderReorderHandles(index, "gl");
            var ctrl = _view.Controller;
            var total = ctrl.ZoneGrids.Count;
            if (!ImGui.CollapsingHeader($"Grid List ({total})###{Id}gl", 0))
                return;

            // Grid Mode toggle — the primary create/extend flow. When active, double-
            // clicking any collision surface either appends a waypoint to the selected
            // grid or creates a new grid + first waypoint at the click. Escape exits.
            if (ctrl.EditModeEnabled)
            {
                var label = ctrl.GridModeActive
                    ? $"Grid Mode: ON — double-click to place###{Id}glMode"
                    : $"Grid Mode: OFF — click to enable###{Id}glMode";
                if (ImGui.Button(label, new Vector2(280, 24)))
                    ctrl.ToggleGridMode();

                if (ctrl.GridModeActive)
                {
                    var targetLabel = ctrl.SelectedGridId.HasValue
                        ? $"Target: grid {ctrl.SelectedGridId.Value} (append waypoint)"
                        : "Target: NEW GRID (no grid selected)";
                    ImGui.Text(targetLabel);
                    ImGui.Text("Escape to exit. Spawn clicks disabled in Grid Mode.");
                }

                // Kept as a fallback for cases where the cursor can't reach a good surface
                // (e.g. camera stuck inside geometry). Grid Mode is the recommended path.
                if (ImGui.Button($"+ New Grid at camera (fallback)###{Id}glNew", new Vector2(280, 20)))
                    ctrl.CreateNewGridAtCamera();
            }

            if (total == 0)
            {
                ImGui.Text("No grids loaded for this zone.");
                return;
            }

            ImGui.Text("Filter (id substring):");
            ImGui.InputText($"###{Id}glF", _gridListFilter, (uint)_gridListFilter.Length, InputTextFlags.Default, null);
            var filter = ReadBuffer(_gridListFilter).Trim();

            // Sort order: pending [N] first (negative ids → top), then attached [A],
            // then orphan [O]; each bucket sorted by id ascending.
            int Rank(VisualEQ.Database.Models.ZoneGridRecord zg)
            {
                if (zg.Grid.Id < 0) return 0;             // pending insert
                if (zg.SpawnCount > 0) return 1;          // attached
                return 2;                                 // orphan
            }

            var matches = ctrl.ZoneGrids
                .Where(zg => filter.Length == 0 || zg.Grid.Id.ToString().IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
                .OrderBy(Rank)
                .ThenBy(zg => zg.Grid.Id)
                .ToList();

            var pendingCount  = ctrl.ZoneGrids.Count(zg => zg.Grid.Id < 0);
            var attachedCount = ctrl.ZoneGrids.Count(zg => zg.Grid.Id >= 0 && zg.SpawnCount > 0);
            var orphanCount   = total - attachedCount - pendingCount;
            var summary = pendingCount > 0
                ? $"{matches.Count} of {total} — {pendingCount} pending, {attachedCount} attached, {orphanCount} orphan"
                : $"{matches.Count} of {total} — {attachedCount} attached, {orphanCount} orphan";
            ImGui.Text(summary);

            ImGui.BeginChild($"###{Id}glList", new Vector2(0, 220), true, WindowFlags.Default);
            foreach (var zg in matches)
            {
                var isPending  = zg.Grid.Id < 0;
                var isAttached = zg.SpawnCount > 0;
                var prefix = isPending ? "[N]" : (isAttached ? "[A]" : "[O]");
                var attachedSuffix = (!isPending && isAttached) ? $" — {zg.SpawnCount} spawn{(zg.SpawnCount == 1 ? "" : "s")}" : "";
                var pendingSuffix  = isPending ? " (uncommitted)" : "";
                var label = $"{prefix} Grid {zg.Grid.Id} — {zg.Waypoints.Count} wp{(zg.Waypoints.Count == 1 ? "" : "s")}{attachedSuffix}{pendingSuffix}###{Id}gl{zg.Grid.Id}";
                var isSelected = ctrl.SelectedGridId == zg.Grid.Id;
                if (ImGui.Selectable(label, isSelected))
                {
                    // Second click on the currently-selected row deselects.
                    ctrl.SelectGrid(isSelected ? (int?)null : zg.Grid.Id);
                }
            }
            ImGui.EndChild();

            if (ctrl.SelectedGridId.HasValue)
            {
                if (ImGui.Button($"Clear grid selection###{Id}glClear", new Vector2(200, 22)))
                    ctrl.SelectGrid(null);
            }
        }

        static string PrimaryName(SpawnPoint sp) =>
            sp.Record.Entries
                .OrderByDescending(e => e.Entry.Chance)
                .FirstOrDefault()?.Npc?.Name ?? "?";

        static string ReadBuffer(byte[] buf) =>
            System.Text.Encoding.UTF8.GetString(buf).TrimEnd('\0');

        void RenderZonePointsSection(int index)
        {
            RenderReorderHandles(index, "zp");
            var ctrl = _view.Controller;
            var owned = ctrl.ZonePointManager.ZonePoints.Count;
            var incoming = ctrl.ZonePointManager.IncomingPoints.Count;
            var header = incoming > 0
                ? $"Zone Points ({owned} owned + {incoming} incoming)###{Id}zp"
                : $"Zone Points ({owned})###{Id}zp";
            if (!ImGui.CollapsingHeader(header, 0))
                return;

            // Creation controls — quick camera-XY spawn + drag-to-draw. Only visible in
            // edit mode.
            if (ctrl.EditModeEnabled)
            {
                if (ImGui.Button($"+ New Box###{Id}zpNewBox", new Vector2(110, 22)))
                    ctrl.CreateZonePoint(0);
                ImGui.SameLine();
                if (ImGui.Button($"+ New Plane###{Id}zpNewPlane", new Vector2(110, 22)))
                    ctrl.CreateZonePoint(1); // default to X-plane; user flips to Y in inspector

                // Drag-to-create buttons. Toggle the Controller's active creation mode;
                // when active, EngineCore intercepts left-click-drag on the ground plane
                // to draw a preview + commit an INSERT on release. Escape cancels.
                var boxActive   = ctrl.ActiveCreation == Controller.CreationMode.DrawBox;
                var planeActive = ctrl.ActiveCreation == Controller.CreationMode.DrawPlane;

                if (boxActive)
                {
                    if (ImGui.Button($"Cancel draw###{Id}zpDrawCancel", new Vector2(110, 22)))
                        ctrl.CancelCreation();
                }
                else
                {
                    if (ImGui.Button($"Draw Box###{Id}zpDrawBox", new Vector2(110, 22)))
                        ctrl.EnterCreationMode(Controller.CreationMode.DrawBox);
                }
                ImGui.SameLine();
                if (planeActive)
                {
                    if (ImGui.Button($"Cancel draw###{Id}zpDrawCancelP", new Vector2(110, 22)))
                        ctrl.CancelCreation();
                }
                else
                {
                    if (ImGui.Button($"Draw Plane###{Id}zpDrawPlane", new Vector2(110, 22)))
                        ctrl.EnterCreationMode(Controller.CreationMode.DrawPlane);
                }

                if (boxActive)   ImGui.Text("Left-click-drag on the ground → new box. Esc cancels.");
                if (planeActive) ImGui.Text("Left-click-drag a line → new plane. Esc cancels.");
                if (!boxActive && !planeActive)
                    ImGui.Text("+ New: at camera position.  Draw: click-and-drag in world.");
                ImGui.Separator();
            }

            if (owned == 0 && incoming == 0)
            {
                ImGui.Text("No trilogy_zone_points rows for this zone.");
                if (ctrl.EditModeEnabled)
                    ImGui.Text("Click + New Box or + New Plane above to add one.");
                return;
            }

            var selected = ctrl.ZonePointManager.Selected;
            ImGui.BeginChild($"###{Id}zpList", new Vector2(0, 160), true, WindowFlags.Default);

            // Incoming rows first — small `[IN]` badge + "← <source>" so users can spot
            // arrival pads before scrolling through owned rows.
            foreach (var zp in ctrl.ZonePointManager.IncomingPoints)
            {
                var dirty  = zp.IsDirty ? " *" : "";
                var label  = $"[IN] #{zp.Row.Id} ← {zp.Row.Zone}{dirty}###{Id}zpItemIN{zp.Row.Id}";
                if (ImGui.Selectable(label, ReferenceEquals(zp, selected)))
                    FlyToZonePoint(zp);
            }
            if (incoming > 0 && owned > 0) ImGui.Separator();

            foreach (var zp in ctrl.ZonePointManager.ZonePoints)
            {
                string badge;
                switch (zp.Health)
                {
                    case VisualEQ.ZonePointSystem.ZonePointHealth.Green:  badge = "[G]"; break;
                    case VisualEQ.ZonePointSystem.ZonePointHealth.Yellow: badge = "[Y]"; break;
                    case VisualEQ.ZonePointSystem.ZonePointHealth.Purple: badge = "[P]"; break;
                    case VisualEQ.ZonePointSystem.ZonePointHealth.Red:    badge = "[R]"; break;
                    default: badge = "[?]"; break;
                }
                string mode;
                switch (zp.Row.UseNewZoning)
                {
                    case 0: mode = "box"; break;
                    case 1: mode = "X-plane"; break;
                    case 2: mode = "Y-plane"; break;
                    default: mode = "mode?"; break;
                }
                var dirty = zp.IsDirty ? " *" : "";
                var pending = zp.IsPendingInsert ? " [NEW]" : "";
                var sandwich = ctrl.SandwichResults.ContainsKey(zp.Row.Id) ? " [SANDWICH]" : "";
                var idLabel = zp.IsPendingInsert ? "new" : zp.Row.Id.ToString();
                var label = $"{badge}{sandwich} #{idLabel} {mode} → {zp.Row.TargetZone}{dirty}{pending}###{Id}zpItem{zp.Row.Id}";

                if (ImGui.Selectable(label, ReferenceEquals(zp, selected)))
                    FlyToZonePoint(zp);
            }
            ImGui.EndChild();

            if (selected != null)
                RenderZonePointInspector(selected, ctrl.EditModeEnabled);
        }

        void FlyToZonePoint(VisualEQ.ZonePointSystem.ZonePoint zp)
        {
            _view.Controller.ZonePointManager.Select(zp);
            _view.Controller.FrameZonePointSelection();
        }

        // Selected-zone-point inspector. When editable is false (edit mode off) fields
        // render as read-only text; when true, fields render as ImGui inputs and each
        // field-edit commits a ZonePointFieldEditAction on release-with-change (so
        // one keystroke doesn't spawn a hundred undo entries).
        void RenderZonePointInspector(VisualEQ.ZonePointSystem.ZonePoint zp, bool editable)
        {
            // Incoming rows get a simpler read-mostly view — the row physically belongs to
            // another zone (trilogy_zone_points.zone = source), so most fields don't make
            // sense to edit from this zone's perspective. Only the heading (which direction
            // arriving players face on landing) is exposed for editing.
            if (zp.IsIncoming)
            {
                RenderIncomingInspector(zp, editable);
                return;
            }

            ImGui.Separator();
            var idLabel = zp.IsPendingInsert ? "new" : zp.Row.Id.ToString();
            var suffix  = zp.IsPendingInsert ? " [NEW — will INSERT on commit]" : "";
            ImGui.Text($"Selected: #{idLabel}  (health: {zp.Health}){suffix}");

            // Delete affordance — two-click confirm to prevent misclicks. First click arms;
            // second within a couple seconds actually deletes. Only visible in edit mode.
            if (editable)
            {
                if (_zpDeleteArmedForId == zp.Row.Id &&
                    (FrameTime - _zpDeleteArmedAt) < DeleteConfirmSeconds)
                {
                    if (ImGui.Button($"Confirm delete###{Id}zpDelC", new Vector2(160, 24)))
                    {
                        _view.Controller.DeleteSelectedZonePoint();
                        _zpDeleteArmedForId = 0;
                    }
                    ImGui.SameLine();
                    if (ImGui.Button($"Cancel###{Id}zpDelX", new Vector2(90, 24)))
                        _zpDeleteArmedForId = 0;
                }
                else
                {
                    if (ImGui.Button($"Delete this trigger###{Id}zpDel", new Vector2(180, 24)))
                    {
                        _zpDeleteArmedForId = zp.Row.Id;
                        _zpDeleteArmedAt    = FrameTime;
                    }
                }
            }

            // ─── Sandwich warning (bright red) ─────────────────────────────────────
            if (_view.Controller.SandwichResults.TryGetValue(zp.Row.Id, out var sandwich))
            {
                ImGui.Separator();
                var warn = new Vector4(1.0f, 0.30f, 0.30f, 1f);
                ImGui.Text("SANDWICH DETECTED", warn);
                ImGui.Text($"Landing in {zp.Row.TargetZone} at ({zp.Row.TargetX:F0},{zp.Row.TargetY:F0},{zp.Row.TargetZ:F0})");
                ImGui.Text($"falls inside row #{sandwich.OffendingRow.Id} in {sandwich.OffendingRow.TargetZone ?? "?"}.");
                ImGui.Text("Arriving players will be re-teleported immediately.");
                if (editable)
                {
                    if (ImGui.Button($"Shift landing 50 units away###{Id}zpSw", new Vector2(230, 24)))
                        _view.Controller.ShiftLandingAwayFromSandwich(zp.Row.Id);
                }
            }

            // ─── Source position + size (read-only readout; edit these via world drag) ──
            ImGui.Separator();
            ImGui.Text("Source (drag in world to edit)");
            var db = zp.Row;
            ImGui.Text($"  x, y, z = ({db.X:F1}, {db.Y:F1}, {db.Z:F1})");
            if (db.UseNewZoning == 0)
            {
                var zStr = db.MaxZDiff == 0 ? "∞" : db.MaxZDiff.ToString();
                ImGui.Text($"  Zrange={db.Zrange}  MaxZDiff={zStr}");
            }

            // ─── Mode ────────────────────────────────────────────────────────────────
            ImGui.Separator();
            ImGui.Text("Mode");
            RenderModeRadios(zp, editable);
            ImGui.Text("Box = tight portal / door. Plane = wide outdoor zone edge.");

            // ─── Plane crossing bounds — only relevant for modes 1/2 ─────────────────
            if (db.UseNewZoning == 1 || db.UseNewZoning == 2)
            {
                ImGui.Separator();
                var perpAxis = db.UseNewZoning == 1 ? "Y" : "X";
                ImGui.Text($"Plane bounds ({perpAxis} axis extent)");
                RenderFloatField(zp, VisualEQ.EditSystem.ZonePointFieldEditAction.Field.MinVert,
                    "MinVert", () => zp.Row.MinVert, v => zp.Row.MinVert = v, editable);
                RenderFloatField(zp, VisualEQ.EditSystem.ZonePointFieldEditAction.Field.MaxVert,
                    "MaxVert", () => zp.Row.MaxVert, v => zp.Row.MaxVert = v, editable);
                RenderFloatField(zp, VisualEQ.EditSystem.ZonePointFieldEditAction.Field.CenterPoint,
                    "CenterPoint", () => zp.Row.CenterPoint, v => zp.Row.CenterPoint = v, editable);
                ImGui.Text($"MinVert/MaxVert bracket the trigger line along the {perpAxis} axis.");
                ImGui.Text("Both 0 = unbounded (spans the whole zone).");
            }

            // ─── Target ──────────────────────────────────────────────────────────────
            ImGui.Separator();
            ImGui.Text("Target");
            RenderTargetZoneField(zp, editable);
            RenderCoordFieldWithWildcard(zp, "target_x",
                VisualEQ.EditSystem.ZonePointFieldEditAction.Field.TargetX,
                () => zp.Row.TargetX, v => zp.Row.TargetX = v, editable);
            RenderCoordFieldWithWildcard(zp, "target_y",
                VisualEQ.EditSystem.ZonePointFieldEditAction.Field.TargetY,
                () => zp.Row.TargetY, v => zp.Row.TargetY = v, editable);
            RenderCoordFieldWithWildcard(zp, "target_z",
                VisualEQ.EditSystem.ZonePointFieldEditAction.Field.TargetZ,
                () => zp.Row.TargetZ, v => zp.Row.TargetZ = v, editable);
            RenderFloatField(zp, VisualEQ.EditSystem.ZonePointFieldEditAction.Field.Heading,
                "heading (0–255)", () => zp.Row.Heading, v => zp.Row.Heading = v, editable);
            ImGui.Text("Check 'wild' on an axis to preserve the player's coord across zones.");

            // ─── Keep flags ──────────────────────────────────────────────────────────
            ImGui.Separator();
            ImGui.Text("Keep player axis on teleport");
            RenderKeepCheckbox(zp, VisualEQ.EditSystem.ZonePointFieldEditAction.Field.KeepX,
                "keepX", () => zp.Row.KeepX, v => zp.Row.KeepX = v, editable);
            RenderKeepCheckbox(zp, VisualEQ.EditSystem.ZonePointFieldEditAction.Field.KeepY,
                "keepY", () => zp.Row.KeepY, v => zp.Row.KeepY = v, editable);
            RenderKeepCheckbox(zp, VisualEQ.EditSystem.ZonePointFieldEditAction.Field.KeepZ,
                "keepZ", () => zp.Row.KeepZ, v => zp.Row.KeepZ = v, editable);
            ImGui.Text("Preserve the player's axis-value on teleport. Check for outdoor");
            ImGui.Text("edges where source and target zones share a coord system.");

            if (zp.IsDirty)
            {
                ImGui.Separator();
                ImGui.Text("Unsaved edits (see Pending Changes).");
            }
        }

        // Compact inspector for incoming rows. The row lives in another zone (Row.Zone =
        // the source shortname); we can still UPDATE it by id at commit time, but the only
        // field that's meaningful to edit from the arrival-zone's perspective is the
        // landing heading. Everything else renders as read-only context.
        void RenderIncomingInspector(VisualEQ.ZonePointSystem.ZonePoint zp, bool editable)
        {
            ImGui.Separator();
            var dirty = zp.IsDirty ? "  *" : "";
            ImGui.Text($"Selected: #{zp.Row.Id}  (incoming from {zp.Row.Zone}){dirty}");
            ImGui.Text("Arriving players land here from another zone. Read-only except heading.");

            ImGui.Separator();
            ImGui.Text("Landing coord (target_x / y / z)");
            ImGui.Text($"  ({zp.Row.TargetX:F1}, {zp.Row.TargetY:F1}, {zp.Row.TargetZ:F1})");

            ImGui.Text("Source coord in " + zp.Row.Zone + " (context)");
            ImGui.Text($"  ({zp.Row.X:F1}, {zp.Row.Y:F1}, {zp.Row.Z:F1})");

            ImGui.Separator();
            ImGui.Text("Landing heading (0–255)");
            RenderIncomingHeadingSlider(zp, editable);
            ImGui.Text("Angle the arriving character faces on entry.");

            if (zp.IsDirty)
            {
                ImGui.Separator();
                ImGui.Text("Unsaved edits (see Pending Changes).");
            }
        }

        // Per-incoming heading slider — its own buffered state so a drag records a single
        // action on release, matching the SpawnRotateAction / regular heading edit pattern.
        private float _zpIncHeadingBuffer;
        private float _zpIncHeadingBeforeEdit;
        private int? _zpIncHeadingRowId;
        private bool _zpIncHeadingSliderWasActive;

        void RenderIncomingHeadingSlider(VisualEQ.ZonePointSystem.ZonePoint zp, bool editable)
        {
            if (!editable)
            {
                ImGui.Text($"  heading = {zp.Row.Heading:F0}");
                return;
            }

            // Resync the buffer with the row when:
            //   • selection changed (different row id), OR
            //   • slider isn't being held (user is not mid-drag), OR
            //   • the row's live value has drifted from the buffer (undo/redo/discard/
            //     external revert while the slider was believed active by a stale flag).
            // Third condition is a belt-and-suspenders catch that guarantees the slider
            // always reflects Row.Heading when the user isn't touching it.
            var isSameRow = _zpIncHeadingRowId == zp.Row.Id;
            var drifted   = System.MathF.Abs(_zpIncHeadingBuffer - zp.Row.Heading) > 0.5f;
            if (!isSameRow || !_zpIncHeadingSliderWasActive || drifted)
            {
                _zpIncHeadingBuffer = zp.Row.Heading;
                _zpIncHeadingRowId  = zp.Row.Id;
            }

            var changed = ImGui.SliderFloat($"###{Id}zpIncHead", ref _zpIncHeadingBuffer, 0f, 255f, "%.0f", 1f);
            var sliderActive = ImGui.IsAnyItemActive();

            if (changed)
            {
                // Live-update the row so the arrow re-renders in-scene as the user drags.
                zp.Row.Heading = _zpIncHeadingBuffer;
            }

            if (!_zpIncHeadingSliderWasActive && sliderActive)
                _zpIncHeadingBeforeEdit = zp.Row.Heading;

            if (_zpIncHeadingSliderWasActive && !sliderActive)
            {
                if (System.MathF.Abs(_zpIncHeadingBeforeEdit - _zpIncHeadingBuffer) > 0.5f)
                {
                    _view.Controller.RecordAction(
                        new VisualEQ.EditSystem.ZonePointFieldEditAction(
                            zp,
                            VisualEQ.EditSystem.ZonePointFieldEditAction.Field.Heading,
                            _zpIncHeadingBeforeEdit, _zpIncHeadingBuffer));
                }
                else
                {
                    // Snap live-updated row back to the pre-edit value if the slider only
                    // nudged below threshold — otherwise the row is dirty but no action was
                    // recorded, and the buffer flush would drop the entry as clean but the
                    // scene would still show the sub-threshold change until next reload.
                    zp.Row.Heading = _zpIncHeadingBeforeEdit;
                }
            }
            _zpIncHeadingSliderWasActive = sliderActive;
        }

        // ─── Field render helpers ────────────────────────────────────────────────────
        //
        // Common pattern:
        //   1. Snapshot the current value from the row.
        //   2. Render the ImGui widget. If value changed, write back to the row live
        //      (so the volume renders the change immediately).
        //   3. When the widget transitions from active → inactive, if the value changed
        //      since edit start, record a ZonePointFieldEditAction with (before, after).
        //   4. When the widget is not active and this field isn't the "active edit",
        //      nothing else happens — the row value is authoritative.

        void RenderFloatField(
            VisualEQ.ZonePointSystem.ZonePoint zp,
            VisualEQ.EditSystem.ZonePointFieldEditAction.Field which,
            string label,
            Func<float> read,
            Action<float> write,
            bool editable)
        {
            var current = read();
            if (!editable)
            {
                ImGui.Text($"  {label} = {current:F2}");
                return;
            }
            var val = current;
            var changed = ImGui.DragFloat($"{label}###{Id}zpF{(int)which}", ref val, 0f, 0f, 1f, "%.2f", 1f);
            if (changed) write(val);
            HandleActivationTransition(zp, which, current, () => read());
        }

        // target_x/y/z gets a wildcard checkbox next to the numeric input. Toggling on
        // sets the sentinel 999999; toggling off drops to 0 (user can then type a real
        // value). Wildcard toggles route through the same field-edit action path.
        void RenderCoordFieldWithWildcard(
            VisualEQ.ZonePointSystem.ZonePoint zp,
            string label,
            VisualEQ.EditSystem.ZonePointFieldEditAction.Field which,
            Func<float> read,
            Action<float> write,
            bool editable)
        {
            var current = read();
            var isWild = VisualEQ.ZonePointSystem.ZonePointWildcards.IsWildcard(current);

            if (!editable)
            {
                ImGui.Text(isWild ? $"  {label} = <wildcard>" : $"  {label} = {current:F2}");
                return;
            }

            var wildLocal = isWild;
            if (ImGui.Checkbox($"wild###{Id}zpW{(int)which}", ref wildLocal))
            {
                var before = current;
                var after  = wildLocal
                    ? VisualEQ.ZonePointSystem.ZonePointWildcards.Sentinel
                    : 0f;
                if (Math.Abs(before - after) > 0.001f)
                {
                    _view.Controller.RecordAction(
                        new VisualEQ.EditSystem.ZonePointFieldEditAction(zp, which, before, after));
                }
                return; // don't render the InputFloat this frame — value just changed
            }
            ImGui.SameLine();

            if (isWild)
            {
                ImGui.Text($"{label} = <wildcard>");
                return;
            }

            var val = current;
            var changed = ImGui.DragFloat($"{label}###{Id}zpF{(int)which}", ref val, 0f, 0f, 1f, "%.2f", 1f);
            if (changed) write(val);
            HandleActivationTransition(zp, which, current, () => read());
        }

        void RenderTargetZoneField(VisualEQ.ZonePointSystem.ZonePoint zp, bool editable)
        {
            var current = zp.Row.TargetZone ?? "";

            if (!editable)
            {
                ImGui.Text($"  target_zone = '{current}'");
                return;
            }

            var shortNames = _view.Controller.ZoneShortNames;
            if (shortNames.Count > 0)
            {
                RenderTargetZoneCombo(zp, current, shortNames);
                return;
            }

            RenderTargetZoneInputTextFallback(zp, current);
        }

        // Combo dropdown fed by the cached zone shortnames list. On any selection change,
        // records a single ZonePointFieldEditAction — same undo path as any other field.
        void RenderTargetZoneCombo(
            VisualEQ.ZonePointSystem.ZonePoint zp,
            string current,
            System.Collections.Generic.List<string> shortNames)
        {
            var items = shortNames.ToArray();
            int idx = Array.IndexOf(items, current);
            if (idx < 0)
            {
                // Current value isn't in the cached list (unusual — stale zone shortname or
                // a value not in `zone` table). Show a message so the user can spot it.
                ImGui.Text($"target_zone = '{current}' (unknown to zone table)");
                ImGui.Text("Pick a known zone below to fix:");
                idx = 0;
            }
            else
            {
                ImGui.Text("target_zone");
            }

            var refIdx = idx;
            if (ImGui.Combo($"###{Id}zpTZCombo", ref refIdx, items) && refIdx != idx)
            {
                var newValue = items[refIdx];
                _view.Controller.RecordAction(
                    new VisualEQ.EditSystem.ZonePointFieldEditAction(
                        zp,
                        VisualEQ.EditSystem.ZonePointFieldEditAction.Field.TargetZone,
                        current, newValue));
            }
        }

        // Fallback path when no zone shortnames are cached (DB not configured yet). Uses
        // the same byte-buffer + active-edit pattern as the other InputText widgets.
        void RenderTargetZoneInputTextFallback(VisualEQ.ZonePointSystem.ZonePoint zp, string current)
        {
            var isThisFieldActive =
                _zpActiveEditZonePointId == zp.Row.Id &&
                _zpActiveEditField == VisualEQ.EditSystem.ZonePointFieldEditAction.Field.TargetZone;

            if (_zpTargetZoneBufferForId != zp.Row.Id || (!isThisFieldActive && ReadBuffer(_zpTargetZoneBuffer) != current))
            {
                Array.Clear(_zpTargetZoneBuffer, 0, _zpTargetZoneBuffer.Length);
                var bytes = System.Text.Encoding.UTF8.GetBytes(current);
                Array.Copy(bytes, _zpTargetZoneBuffer, Math.Min(bytes.Length, _zpTargetZoneBuffer.Length - 1));
                _zpTargetZoneBufferForId = zp.Row.Id;
            }

            ImGui.Text("target_zone (no zone table cached — type shortname)");
            ImGui.InputText($"###{Id}zpTZ", _zpTargetZoneBuffer, (uint)_zpTargetZoneBuffer.Length, InputTextFlags.Default, null);
            HandleActivationTransition(
                zp,
                VisualEQ.EditSystem.ZonePointFieldEditAction.Field.TargetZone,
                current,
                () => ReadBuffer(_zpTargetZoneBuffer));
        }

        void RenderKeepCheckbox(
            VisualEQ.ZonePointSystem.ZonePoint zp,
            VisualEQ.EditSystem.ZonePointFieldEditAction.Field which,
            string label,
            Func<int> read,
            Action<int> write,
            bool editable)
        {
            var current = read();
            if (!editable)
            {
                ImGui.Text($"  {label} = {(current != 0 ? "true" : "false")}");
                return;
            }
            var val = current != 0;
            if (ImGui.Checkbox($"{label}###{Id}zpK{(int)which}", ref val))
            {
                var before = current;
                var after  = val ? 1 : 0;
                if (before != after)
                {
                    _view.Controller.RecordAction(
                        new VisualEQ.EditSystem.ZonePointFieldEditAction(zp, which, before, after));
                }
            }
        }

        void RenderModeRadios(VisualEQ.ZonePointSystem.ZonePoint zp, bool editable)
        {
            var current = (int)zp.Row.UseNewZoning;
            if (!editable)
            {
                string name;
                switch (current)
                {
                    case 0: name = "box"; break;
                    case 1: name = "X-plane"; break;
                    case 2: name = "Y-plane"; break;
                    default: name = "?"; break;
                }
                ImGui.Text($"  {name} (UseNewZoning={current})");
                return;
            }

            void Radio(int value, string label, string idSuffix)
            {
                var isOn = current == value;
                if (ImGui.RadioButtonBool($"{label}###{Id}zpM{idSuffix}", isOn) && !isOn)
                {
                    _view.Controller.RecordAction(
                        new VisualEQ.EditSystem.ZonePointFieldEditAction(
                            zp,
                            VisualEQ.EditSystem.ZonePointFieldEditAction.Field.UseNewZoning,
                            (byte)current, (byte)value));
                }
            }
            Radio(0, "box",     "0");
            ImGui.SameLine();
            Radio(1, "X-plane", "1");
            ImGui.SameLine();
            Radio(2, "Y-plane", "2");
        }

        // Detects the was-active → not-active transition for the most-recently-rendered
        // ImGui item. On active-start, snapshot the before-value. On active-end, if the
        // value drifted, record a ZonePointFieldEditAction. The `readCurrent` closure lets
        // us pull the "after" value from the row (or a byte-buffer, for strings).
        void HandleActivationTransition(
            VisualEQ.ZonePointSystem.ZonePoint zp,
            VisualEQ.EditSystem.ZonePointFieldEditAction.Field which,
            object beforeValueIfStarting,
            Func<object> readCurrent)
        {
            // 0.4.6 doesn't expose per-item IsItemActive — IsAnyItemActive queried right
            // after rendering effectively equals "is THIS item active" since only one item
            // can be active at a time. Same pattern the heading slider uses.
            var isActive = ImGui.IsAnyItemActive();
            var wasThisFieldActive =
                _zpActiveEditZonePointId == zp.Row.Id &&
                _zpActiveEditField == which;

            if (isActive && !wasThisFieldActive)
            {
                // Starting an edit — capture the before-value. If a different field's edit
                // was pending, flush that first using ITS reader (stored on start) — using
                // the new field's reader here would apply the new field's value to the old
                // field's action and crash on the type cast at Apply time.
                if (_zpActiveEditZonePointId.HasValue)
                    FlushActiveEditIfChanged();
                _zpActiveEditZonePointId = zp.Row.Id;
                _zpActiveEditField       = which;
                _zpActiveEditBeforeValue = beforeValueIfStarting;
                _zpActiveEditReader      = readCurrent;
            }
            else if (!isActive && wasThisFieldActive)
            {
                FlushActiveEditIfChanged();
            }
        }

        void FlushActiveEditIfChanged()
        {
            if (!_zpActiveEditZonePointId.HasValue || _zpActiveEditReader == null) return;

            var ctrl = _view.Controller;
            var zp = ctrl.ZonePointManager.ZonePoints
                .FirstOrDefault(p => p.Row.Id == _zpActiveEditZonePointId.Value);
            if (zp == null)
            {
                _zpActiveEditZonePointId = null;
                _zpActiveEditReader      = null;
                return;
            }

            var after  = _zpActiveEditReader();
            var before = _zpActiveEditBeforeValue;

            bool changed = !object.Equals(before ?? "", after ?? "");
            if (before is float bf && after is float af) changed = Math.Abs(bf - af) > 0.001f;
            if (before is int bi && after is int ai)     changed = bi != ai;
            if (before is byte bb && after is byte ab)   changed = bb != ab;

            if (changed)
            {
                ctrl.RecordAction(new VisualEQ.EditSystem.ZonePointFieldEditAction(
                    zp, _zpActiveEditField, before, after));
            }
            _zpActiveEditZonePointId = null;
            _zpActiveEditBeforeValue = null;
            _zpActiveEditReader      = null;
        }

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
