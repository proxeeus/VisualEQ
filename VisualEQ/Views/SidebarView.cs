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
        public const string SectionNpcDetails   = "npc_details";

        static readonly string[] DefaultOrder = { SectionStatus, SectionPending, SectionSpawnInfo, SectionNpcDetails, SectionWaypointInfo, SectionSpawnList, SectionGridList, SectionZonePoints, SectionModelEditor, SectionTeleport };

        private readonly SidebarView _view;

        // Resize IS allowed — ResizeFromAnySide lets the user drag the right edge directly.
        // Height is forced to full window height each frame via SetWindowSize.
        // Scroll is handled by a BeginChild inside the window (the outer window's
        // scroll in ImGui.NET 0.4.6 caps at ~one page of content). NoScrollbar
        // suppresses the outer's own bar — we don't want two visible bars stacking —
        // but we do NOT pass NoScrollWithMouse: on this ImGui version that flag on
        // the outer eats wheel events before they can reach the inner child, so the
        // scrollbar renders but the wheel does nothing.
        private const WindowFlags PinnedPanel =
            WindowFlags.NoTitleBar | WindowFlags.NoMove |
            WindowFlags.NoCollapse | WindowFlags.NoBringToFrontOnFocus |
            WindowFlags.NoSavedSettings | WindowFlags.ResizeFromAnySide |
            WindowFlags.NoScrollbar;

        private const float DefaultWidth = 380f;
        private const float MinWidth = 180f;
        private const float MinRightGutter = 200f; // leave at least this many px for the 3D view
        private const float WidthSaveDebounceSec = 0.5f;
        // Small aesthetic bottom gap. Was 60px to reserve space against a taskbar
        // that could overlap the app on Parallels ARM64 setups, but EngineCore's
        // TryFitToWorkArea now sizes the OS window to Win32's work-area rect
        // (screen minus taskbar/etc.) explicitly, so we no longer need to leave a
        // huge buffer inside the sidebar. 4px keeps the last row from touching the
        // bottom pixel; anything more is wasted usable height.
        private const float BottomSafeAreaPx = 4f;

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
        private int _commitNpcCountSnapshot;
        private int _commitNpcFactionEntryCountSnapshot;
        private int _commitLootTableEntryCountSnapshot;
        private int _commitLootDropEntryCountSnapshot;
        private int _commitLootTableCountSnapshot;
        private int _commitNpcFactionRowCountSnapshot;

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

        // NPC-details fetch state (Slice 1 read-side). Task fires whenever the primary NPC
        // of the SelectedSpawn changes; result cached against that id so re-selecting the
        // same spawn (or another spawn sharing the same npc_types row) is free. One in-flight
        // fetch at a time — matches the NPC-picker pattern above.
        private int? _npcDetailsFetchedForId;
        private int? _npcDetailsInFlightForId;
        private System.Threading.Tasks.Task<VisualEQ.Database.Models.NpcTypeFull> _npcDetailsFetchTask;
        private VisualEQ.Database.Models.NpcTypeFull _npcDetailsData;
        private string _npcDetailsError;

        // Slice 7a — shared-record indicator + duplicate flow.
        // Usage count fetches alongside the NPC details; keyed on the same npc
        // id, so re-selecting a spawn with the same NPC is still a cache hit.
        // Task nulled at the same points as the details fetch.
        private System.Threading.Tasks.Task<int> _npcUsageTask;
        private int? _npcUsageFetchedForId;
        private int _npcUsageCount;

        // Duplicate-NPC confirm modal. Captures the source id + owning
        // spawngroup at Begin time so the follow-up repoint targets the same
        // spawn even if the user switches selection mid-modal.
        // Slice 7b — "Manage Faction Sets" pop-out browser. Reuses the
        // ReferenceDataCache preload for the row list (20k rows, fine in
        // memory). "Assign to current NPC" fires a buffered NPC edit for
        // npc_faction_id — same commit path as an FK picker selection.
        // "Create new set" opens an inline sub-form (name + primaryfaction
        // via nested FK picker + ignore flag), fires CreateEmptyNpcFaction
        // + buffered assign on the returned id.
        private bool _factionMgrActive;
        private readonly byte[] _factionMgrFilterBuf = new byte[64];
        private int _factionMgrSelectedIdx = -1;
        private int _factionMgrTargetNpcId;
        private int _factionMgrTargetOldNpcFactionId;
        private string _factionMgrTargetNpcName;
        // Create sub-form
        private bool _factionMgrCreateFormOpen;
        private readonly byte[] _factionMgrCreateNameBuf = new byte[128];
        private int _factionMgrCreatePrimaryFaction;
        private bool _factionMgrCreateIgnoreAssist;
        private System.Threading.Tasks.Task<int> _factionMgrCreateTask;
        private string _factionMgrCreateError;

        private bool _duplicateConfirmActive;
        private int _duplicateConfirmSourceNpcId;
        private string _duplicateConfirmSourceName;
        private int _duplicateConfirmSpawnGroupId;
        private int _duplicateConfirmUsage;
        private System.Threading.Tasks.Task<int> _duplicateTask;
        private string _duplicateError;
        // Set true when a duplicate finishes so the sidebar (running on the
        // main GL thread) can pump the follow-up on its next Render tick.
        // Follow-up = repoint spawnentry + swap in-memory Record.Entries + drop
        // NPC details cache so the sidebar fetches the fresh clone.
        private int _duplicateFollowUpNewNpcId;
        private int _duplicateFollowUpOldNpcId;
        private int _duplicateFollowUpSpawnGroupId;

        // Displayed NPC = clone of the DB row with any pending buffer edits overlaid. Widgets
        // mutate this directly for live drag feedback; the buffer entry is only touched at
        // widget-release time by NpcFieldEditAction. Rebuilt from baseline + overlay whenever
        // the fetched id changes or the pending-edit version bumps (undo/redo/commit paths).
        private VisualEQ.Database.Models.NpcTypeFull _displayedNpc;
        private long _npcDisplayedVersion;
        private bool _npcDisplayedHadEdit;

        // Activation-transition tracker for NPC field widgets. Single-slot like the WP one
        // — ImGui only permits one active item per frame. FromValue is captured on rising
        // edge; reader lambda produces the post-mutation value at release for the diff.
        private int? _npcActiveEditForId;
        private string _npcActiveEditField;
        private object _npcActiveEditBeforeValue;
        private Func<object> _npcActiveEditReader;

        // FK-picker modal state. One instance shared across every FK field (loottable,
        // faction, merchant, spells, spells_effects) — only one can be open at a time.
        // Filter buffer + selection index reset per open.
        private bool _fkPickerActive;
        private VisualEQ.SpawnSystem.ReferenceDataCache.Table _fkPickerTable;
        private string _fkPickerFieldName;
        private string _fkPickerLabel;
        private int _fkPickerNpcId;
        private int _fkPickerCurrentValue;
        private string _fkPickerNpcDisplayName;
        private readonly byte[] _fkPickerFilterBuf = new byte[64];
        private int _fkPickerSelectedIdx = -1;
        // Optional callback fired when the user hits Select. When null (default),
        // the picker writes an NpcFieldEditAction to _fkPickerFieldName on the NPC.
        // When set (e.g. faction-entry-add), the callback owns what to do with the
        // picked id — the picker just closes.
        private System.Action<int> _fkPickerOnPicked;

        // ── Slice 6b: SEARCH picker state ────────────────────────────────
        // Same shape as the cache-backed FK picker above, but fetch is server-
        // side (LIKE query per filter change) instead of client-side filter
        // over a preloaded list. Used for lootdrops (24k rows) and items (80k
        // rows) — anything too big to preload into ReferenceDataCache.
        private bool _searchPickerActive;
        private string _searchPickerLabel;
        private readonly byte[] _searchPickerFilterBuf = new byte[64];
        private string _searchPickerLastFilter = "";
        private int _searchPickerSelectedIdx = -1;
        private System.Collections.Generic.List<VisualEQ.Database.Models.ReferenceItem> _searchPickerResults;
        private System.Threading.Tasks.Task<System.Collections.Generic.List<VisualEQ.Database.Models.ReferenceItem>> _searchPickerTask;
        private string _searchPickerTaskFilter; // filter that the in-flight task is fetching for
        private string _searchPickerError;
        // Delegate captured at Begin time — the picker calls this each time the
        // filter changes and awaits the result. Returning empty is fine; nulls
        // treated as error.
        private System.Func<string, System.Threading.Tasks.Task<System.Collections.Generic.List<VisualEQ.Database.Models.ReferenceItem>>> _searchPickerFetch;
        private System.Action<VisualEQ.Database.Models.ReferenceItem> _searchPickerOnPicked;

        // Faction-entries fetch state (Slice 5). Keyed by npc_faction_id (from the
        // currently-selected NPC's NpcTypeFull.NpcFactionId). Cache is npc_faction-
        // scoped so switching NPCs that share a faction set is free.
        //
        // Two fetches run in parallel per set: the entries list (rows in
        // npc_faction_entries) and the parent set metadata (name / primaryfaction /
        // ignore_primary_assist from npc_faction). Both must complete before the
        // editor renders; we wait for the entries task and then just read whatever
        // the set task has produced (name/primary lookup is cosmetic — if the parent
        // row is missing we still render entries).
        private int? _factionEntriesFetchedFor;
        private int? _factionEntriesInFlightFor;
        private System.Threading.Tasks.Task<System.Collections.Generic.List<VisualEQ.Database.Models.NpcFactionEntry>> _factionEntriesTask;
        private System.Collections.Generic.List<VisualEQ.Database.Models.NpcFactionEntry> _factionEntriesData;
        private System.Threading.Tasks.Task<VisualEQ.Database.Models.NpcFactionSet> _factionSetTask;
        private VisualEQ.Database.Models.NpcFactionSet _factionSetData;
        private string _factionEntriesError;

        // Enum labels for npc_value (reaction) and temp — mirrors peqphpeditor's
        // faction_values / tmpfaction dicts so DB values render as human labels.
        //   npc_value: -1 = Aggressive, 0 = Passive, 1 = Assist (signed tinyint on wire)
        //   temp:       0 = Perm, 1 = Temp/NoMsg, 2 = Perm/NoMsg, 3 = Temp
        static readonly int[]    _reactionVals   = { -1, 0, 1 };
        static readonly string[] _reactionLabels = { "Aggressive", "Passive", "Assist" };
        // Kept short — the sidebar column is tight and peqphpeditor's tmpfacshort
        // uses the same abbreviations in its per-row rendering.
        static readonly int[]    _tempVals   = { 0, 1, 2, 3 };
        static readonly string[] _tempLabels = { "Perm", "Temp/NM", "Perm/NM", "Temp" };

        // Loot-editor fetch state (Slice 6a — read-only view). Keyed by loottable
        // id so multiple NPCs sharing a loottable share the fetch. One composite
        // Task fires three queries in sequence (loottable header, entries with
        // JOIN on lootdrop.name, batch items-per-lootdrop with JOIN on items.Name)
        // and publishes the whole payload atomically. Editor renders "Loading…"
        // until it lands.
        private int? _lootFetchedForLoottableId;
        private int? _lootInFlightForLoottableId;
        private System.Threading.Tasks.Task<LootFetchResult> _lootTask;
        private LootFetchResult _lootData;
        private string _lootError;

        // Composite of everything the loot widget needs to render one loottable.
        // Held in one place so the widget's "have I got data yet?" check is a
        // single null-guard rather than three-way task juggling.
        // ── Slice 6c: clone + create-empty modal state ─────────────────
        // Two modals share their fetch/error state; only one can be up at a
        // time. Clone flows: async DB write on Confirm, then buffered NPC
        // edit / LTE insert so the link between owning NPC/loottable and the
        // new row lands with the normal commit path. Create-empty flows: same
        // shape, plus a name-entry buffer.
        //
        // On success: invalidate the loot fetch so the widget re-reads.
        private bool _cloneConfirmActive;
        private string _cloneConfirmKind;           // "loottable" or "lootdrop"
        private int _cloneConfirmSourceId;
        private string _cloneConfirmSourceName;
        private int _cloneConfirmSourceUsage;       // pre-fetched usage count for the message
        private int _cloneConfirmContextNpcId;              // loottable: NPC to repoint via buffered edit
        private int _cloneConfirmContextOldLoottableId;     // loottable: baseline id to include in the buffered edit's "from"
        private int _cloneConfirmContextLoottableId;        // lootdrop: which loottable's LTE row gets repointed
        private System.Threading.Tasks.Task<int> _cloneTask;
        private string _cloneError;

        private bool _createEmptyActive;
        private string _createEmptyKind;                    // "loottable" or "lootdrop"
        private readonly byte[] _createEmptyNameBuf = new byte[128];
        // Cash inputs (loottable kind only). Held as floats for DragFloat
        // compatibility; parsed as ints on Confirm.
        private float _createEmptyMinCash;
        private float _createEmptyMaxCash;
        private float _createEmptyAvgCoin;
        private int _createEmptyContextNpcId;               // loottable: NPC to repoint via buffered edit
        private int _createEmptyContextOldLoottableId;      // loottable: current value for the buffered edit's "from"
        private int _createEmptyContextLoottableId;         // lootdrop: which loottable's LTE row gets inserted
        private System.Threading.Tasks.Task<int> _createEmptyTask;
        private string _createEmptyError;

        sealed class LootFetchResult
        {
            public VisualEQ.Database.Models.LootTable LootTable;
            public System.Collections.Generic.List<VisualEQ.Database.Models.LootTableEntry> Entries;
            public System.Collections.Generic.Dictionary<int, System.Collections.Generic.List<VisualEQ.Database.Models.LootDropEntry>> ItemsByLootdrop;
            public int UsageCount;                                                 // # of NPCs pointing at this loottable
            public System.Collections.Generic.Dictionary<int, int> LootdropUsage;  // lootdrop id → # of loottables referencing it (Slice 6c)
        }

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

            var current = ImGui.GetWindowSize();
            var w = current.X;
            if (w < MinWidth) w = MinWidth;
            if (w > winW - MinRightGutter) w = winW - MinRightGutter;

            // Only force-set the window size when there's a MEANINGFUL drift — either
            // width clamp fired (user dragged past bounds) or height needs to grow/
            // shrink by more than a handful of pixels (OS window resize). Calling
            // SetWindowSize every frame on tiny sub-pixel drifts appears to reset
            // the sidebar's scroll offset in ImGui.NET 0.4.6, capping user scroll at
            // ~one page of content — the "can't scroll indefinitely" bug.
            var widthNeedsClamp = System.Math.Abs(current.X - w) > 0.5f;
            var heightDrifted   = System.Math.Abs(current.Y - winH) > 10f;
            if (widthNeedsClamp || heightDrifted)
                ImGui.SetWindowSize(new Vector2(w, winH));

            // Persist the (possibly-user-dragged) width for settings.json.
            if (Math.Abs(w - _width) > 0.5f)
            {
                _width = w;
                _widthDirty = true;
                _widthDirtyAt = FrameTime;
            }

            RenderModeBanner();

            // Query the remaining space AFTER the banner draws, then size the child
            // to fill it explicitly. Passing 0 (fill-parent) in ImGui.NET 0.4.6
            // miscalculates when the parent's own content-region tracking is off,
            // silently clipping bottom content without exposing a scrollbar.
            var avail  = ImGui.GetContentRegionAvailable();
            var childH = System.Math.Max(50f, avail.Y - 4f); // small footer margin

            // Hint a very large virtual content size to the NEXT (child) window
            // via raw cimgui P/Invoke — ImGui.NET 0.4.6's C# wrapper never bound
            // SetNextWindowContentSize, and without it the child's scroll extent
            // caps at ~one page, making tall sidebars unreachable regardless of
            // BeginChild flags. 20000 is safely larger than any realistic sidebar
            // content height (all sections expanded totals well under that);
            // scrollbar thumb will look small but the bar reaches the actual end.
            NsimGui.CimguiRaw.igSetNextWindowContentSize(new NsimGui.CimguiRaw.ImVec2(0f, 20000f));

            // AlwaysVerticalScrollbar so the scrollbar is visible even when content
            // fits (users know they CAN scroll — no confusion about missing widgets).
            ImGui.BeginChild($"###{Id}scroll", new Vector2(0, childH), false,
                WindowFlags.AlwaysVerticalScrollbar);

            for (int i = 0; i < _order.Count; i++)
                RenderSectionById(_order[i], i);

            ImGui.EndChild();
            ImGui.EndWindow();

            // Draw the edit-mode viewport border AFTER EndWindow so it sits above everything.
            if (_view.Controller.EditModeEnabled)
                DrawEditModeBorder(gui);

            // Always-visible coordinate overlay in the top-right — camera + selected spawn +
            // drag delta. Renders as its own small ImGui window with no chrome.
            RenderCoordinateHud(gui);

            // Modal precedence: commit dialog first, then discard confirm, then F10 warning,
            // then NPC picker, then FK picker. Only one shows at a time; NPC / FK pickers
            // sit last so the more important buffer-lifecycle modals always win when the
            // user Ctrl+double-clicks during e.g. a commit prompt.
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
                else if (_fkPickerActive)
                    RenderFkPickerDialog(gui);
                else if (_searchPickerActive)
                    RenderSearchPickerDialog(gui);
                else if (_cloneConfirmActive)
                    RenderCloneConfirmDialog(gui);
                else if (_createEmptyActive)
                    RenderCreateEmptyDialog(gui);
                else if (_duplicateConfirmActive)
                    RenderDuplicateConfirmDialog(gui);
                else if (_factionMgrActive)
                    RenderManageFactionSetsDialog(gui);
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
            _commitNpcCountSnapshot         = buffer.Npcs.Count;
            _commitNpcFactionEntryCountSnapshot = buffer.NpcFactionEntries.Count;
            _commitLootTableEntryCountSnapshot  = buffer.LootTableEntries.Count;
            _commitLootDropEntryCountSnapshot   = buffer.LootDropEntries.Count;
            _commitLootTableCountSnapshot       = buffer.LootTables.Count;
            _commitNpcFactionRowCountSnapshot   = buffer.NpcFactions.Count;
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
                {
                    _view.Controller.OnCommitSucceeded(_commitResult);
                    // NPC edits landed in the DB — the cached NpcTypeFull we fetched pre-
                    // commit is now stale (any subsequent selection would fall back to it
                    // and hide the just-saved values). Drop the cache so the next render
                    // triggers a fresh fetch of the committed row.
                    if (_commitResult.NpcRowsWritten > 0)
                    {
                        _npcDetailsFetchedForId = null;
                        _displayedNpc           = null;
                    }
                    // Same idea for faction entries — DB rows are now the committed
                    // set, so drop the fetched baseline to force a re-query on the
                    // next render (which now reflects Insert/Update/Delete).
                    if (_commitResult.NpcFactionEntryInserts > 0 ||
                        _commitResult.NpcFactionEntryUpdates > 0 ||
                        _commitResult.NpcFactionEntryDeletes > 0 ||
                        _commitResult.NpcFactionRowsWritten  > 0)
                    {
                        _factionEntriesFetchedFor = null;
                        _factionEntriesData       = null;
                        _factionSetData           = null;
                    }
                    // Loot editor (Slice 6b) — same idea: any successful write
                    // to loottable_entries or lootdrop_entries makes the
                    // cached baseline stale, so drop it and let the next
                    // render re-fetch.
                    if (_commitResult.LootTableEntryInserts > 0 ||
                        _commitResult.LootTableEntryUpdates > 0 ||
                        _commitResult.LootTableEntryDeletes > 0 ||
                        _commitResult.LootDropEntryInserts  > 0 ||
                        _commitResult.LootDropEntryUpdates  > 0 ||
                        _commitResult.LootDropEntryDeletes  > 0 ||
                        _commitResult.LootTableRowsWritten  > 0)
                    {
                        _lootFetchedForLoottableId = null;
                        _lootData                  = null;
                    }
                }
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
            if (_commitNpcCountSnapshot > 0)
                ImGui.Text($"  {_commitNpcCountSnapshot} NPC edit(s)");
            if (_commitNpcFactionEntryCountSnapshot > 0)
                ImGui.Text($"  {_commitNpcFactionEntryCountSnapshot} faction-entry op(s)");
            if (_commitLootTableEntryCountSnapshot > 0)
                ImGui.Text($"  {_commitLootTableEntryCountSnapshot} loottable-entry op(s)");
            if (_commitLootDropEntryCountSnapshot > 0)
                ImGui.Text($"  {_commitLootDropEntryCountSnapshot} lootdrop-entry op(s)");
            if (_commitLootTableCountSnapshot > 0)
                ImGui.Text($"  {_commitLootTableCountSnapshot} loottable header op(s)");
            if (_commitNpcFactionRowCountSnapshot > 0)
                ImGui.Text($"  {_commitNpcFactionRowCountSnapshot} faction-set header op(s)");
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
                if (r.NpcRowsWritten > 0)
                    ImGui.Text($"  {r.NpcRowsWritten} npc_types row(s) updated");
                if (r.NpcFactionEntryInserts > 0)
                    ImGui.Text($"  {r.NpcFactionEntryInserts} npc_faction_entries row(s) inserted");
                if (r.NpcFactionEntryUpdates > 0)
                    ImGui.Text($"  {r.NpcFactionEntryUpdates} npc_faction_entries row(s) updated");
                if (r.NpcFactionEntryDeletes > 0)
                    ImGui.Text($"  {r.NpcFactionEntryDeletes} npc_faction_entries row(s) deleted");
                if (r.LootTableEntryInserts > 0)
                    ImGui.Text($"  {r.LootTableEntryInserts} loottable_entries row(s) inserted");
                if (r.LootTableEntryUpdates > 0)
                    ImGui.Text($"  {r.LootTableEntryUpdates} loottable_entries row(s) updated");
                if (r.LootTableEntryDeletes > 0)
                    ImGui.Text($"  {r.LootTableEntryDeletes} loottable_entries row(s) deleted");
                if (r.LootDropEntryInserts > 0)
                    ImGui.Text($"  {r.LootDropEntryInserts} lootdrop_entries row(s) inserted");
                if (r.LootDropEntryUpdates > 0)
                    ImGui.Text($"  {r.LootDropEntryUpdates} lootdrop_entries row(s) updated");
                if (r.LootDropEntryDeletes > 0)
                    ImGui.Text($"  {r.LootDropEntryDeletes} lootdrop_entries row(s) deleted");
                if (r.LootTableRowsWritten > 0)
                    ImGui.Text($"  {r.LootTableRowsWritten} loottable row(s) updated");
                if (r.NpcFactionRowsWritten > 0)
                    ImGui.Text($"  {r.NpcFactionRowsWritten} npc_faction row(s) updated");
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
                case SectionNpcDetails:   RenderNpcDetailsSection(index); break;
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
            if (!ImGui.CollapsingHeader($"Status###{Id}s", 0))
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
            if (!ImGui.CollapsingHeader($"Spawn Info###{Id}si", 0))
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
            // Closed by default — only relevant when a waypoint is picked; keeps
            // the sidebar quiet during normal spawn editing. User expands as needed;
            // ImGui remembers the toggle for the session (NoSavedSettings scopes to
            // the outer window, not per-header state).
            if (!ImGui.CollapsingHeader($"Waypoint Info###{Id}wi", 0))
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
            // Closed by default — the count in the header keeps ambient awareness
            // when unopened; expanding is only needed for detailed review.
            if (!ImGui.CollapsingHeader($"{header}###{Id}pc", 0))
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
            // Owned heading. Uses a dedicated slider (not RenderFloatField) so the
            // recorded action carries WIRE-scale values that Apply writes back to
            // zp.Row.Heading verbatim. RenderFloatField's transition capture stores the
            // reader's display value, and Apply would then push /loc-scale numbers
            // straight into a wire-scale field — that broke undo/redo before this
            // dedicated widget was added.
            //
            // trilogy_zone_points.heading is on the 0-255 wire scale — the server does
            // heading * 2 at fire time (trilogy_client.cpp:1919) to produce its 0-512
            // internal value which the client then displays via /loc. Edit in /loc
            // scale so what the user types matches what they see in game.
            RenderOwnedHeadingSlider(zp, editable);
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
            ImGui.Text("Landing heading (0–511, matches client /loc)");
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
                // Display in /loc scale (row.Heading * 2). DB storage stays 0-255 wire.
                ImGui.Text($"  heading = {zp.Row.Heading * 2f:F0}  (client /loc scale)");
                return;
            }

            // Buffer stays in the DB / wire scale (0-255) so the recorded action carries
            // the exact value that Apply writes to zp.Row.Heading. Slider display is
            // 0-511 (server / /loc scale) via ×2 on read, ÷2 on write. See the comment
            // above the owned-heading RenderFloatField site for why this table isn't 0-511.
            var isSameRow = _zpIncHeadingRowId == zp.Row.Id;
            var drifted   = System.MathF.Abs(_zpIncHeadingBuffer - zp.Row.Heading) > 0.5f;
            if (!isSameRow || !_zpIncHeadingSliderWasActive || drifted)
            {
                _zpIncHeadingBuffer = zp.Row.Heading;
                _zpIncHeadingRowId  = zp.Row.Id;
            }

            var displayVal = _zpIncHeadingBuffer * 2f;
            var changed = ImGui.SliderFloat($"###{Id}zpIncHead", ref displayVal, 0f, 511f, "%.0f", 1f);
            var sliderActive = ImGui.IsAnyItemActive();

            if (changed)
            {
                _zpIncHeadingBuffer = ClampWireHeading(displayVal * 0.5f);
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

        // Clamp a wire-scale heading (0-255) to its valid range. Matches
        // #fixzoneheading (gm_commands/fixzoneheading.cpp:170-173) so a value the user
        // typed in /loc scale (e.g. 511) reduces to a legal wire value after the ÷2
        // conversion and doesn't produce out-of-range garbage at server × 2 fire time.
        static float ClampWireHeading(float wire)
        {
            if (wire < 0f)   return 0f;
            if (wire > 255f) return 255f;
            return wire;
        }

        // Owned zone-point heading — same /loc-scale display + wire-scale storage as
        // the incoming slider (RenderIncomingHeadingSlider). Buffer holds wire, display
        // is buffer * 2, actions record wire values so Apply/Revert write back to
        // zp.Row.Heading verbatim.
        private float _zpOwnedHeadingBuffer;
        private float _zpOwnedHeadingBeforeEdit;
        private int?  _zpOwnedHeadingRowId;
        private bool  _zpOwnedHeadingSliderWasActive;

        void RenderOwnedHeadingSlider(VisualEQ.ZonePointSystem.ZonePoint zp, bool editable)
        {
            if (!editable)
            {
                ImGui.Text($"  heading (0-511) = {zp.Row.Heading * 2f:F0}   (DB wire = {zp.Row.Heading:F0})");
                return;
            }

            var isSameRow = _zpOwnedHeadingRowId == zp.Row.Id;
            var drifted   = System.MathF.Abs(_zpOwnedHeadingBuffer - zp.Row.Heading) > 0.5f;
            if (!isSameRow || !_zpOwnedHeadingSliderWasActive || drifted)
            {
                _zpOwnedHeadingBuffer = zp.Row.Heading;
                _zpOwnedHeadingRowId  = zp.Row.Id;
            }

            ImGui.Text("heading (0-511, matches client /loc)");
            var displayVal = _zpOwnedHeadingBuffer * 2f;
            var changed = ImGui.SliderFloat($"###{Id}zpOwnHead", ref displayVal, 0f, 511f, "%.0f", 1f);
            var sliderActive = ImGui.IsAnyItemActive();

            if (changed)
            {
                _zpOwnedHeadingBuffer = ClampWireHeading(displayVal * 0.5f);
                zp.Row.Heading = _zpOwnedHeadingBuffer;
            }

            if (!_zpOwnedHeadingSliderWasActive && sliderActive)
                _zpOwnedHeadingBeforeEdit = zp.Row.Heading;

            if (_zpOwnedHeadingSliderWasActive && !sliderActive)
            {
                if (System.MathF.Abs(_zpOwnedHeadingBeforeEdit - _zpOwnedHeadingBuffer) > 0.5f)
                {
                    _view.Controller.RecordAction(
                        new VisualEQ.EditSystem.ZonePointFieldEditAction(
                            zp,
                            VisualEQ.EditSystem.ZonePointFieldEditAction.Field.Heading,
                            _zpOwnedHeadingBeforeEdit, _zpOwnedHeadingBuffer));
                }
                else
                {
                    zp.Row.Heading = _zpOwnedHeadingBeforeEdit;
                }
            }
            _zpOwnedHeadingSliderWasActive = sliderActive;
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

            RenderTargetZoneInputText(zp, current);
        }

        // Free-form target_zone entry — same byte-buffer + active-edit pattern as the
        // other InputText widgets. Preferred over a Combo so the user can type any
        // shortname (including zones not in the cached `zone` table).
        void RenderTargetZoneInputText(VisualEQ.ZonePointSystem.ZonePoint zp, string current)
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

            ImGui.Text("target_zone");
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

        // Full-fidelity NPC row inspector. Slice 2 turns Slice 1's read-only view into
        // a full editor — every non-visual, non-special-abilities column is editable in
        // edit mode. Foreign-key int fields (loottable, faction, merchant, spells,
        // effects) render with resolved names via ReferenceDataCache and open a modal
        // typeahead picker for edits.
        //
        // Data flow:
        //   SelectedSpawn → primary NPC id → async fetch via NpcRepository → NpcTypeFull
        //   → clone into _displayedNpc + overlay pending buffer edits → widgets read/write
        //   _displayedNpc for live feedback → activation-transition flush records one
        //   NpcFieldEditAction per widget-release cycle → PendingBuffer.Npcs → EditCommitter
        //   → UPDATE npc_types on Save.
        void RenderNpcDetailsSection(int index)
        {
            RenderReorderHandles(index, "nd");
            if (!ImGui.CollapsingHeader($"NPC Details###{Id}nd", 0))
                return;

            var sp = _view.SelectedSpawn;
            if (sp == null)
            {
                ImGui.Text("Click a spawn to view full NPC details.");
                return;
            }

            var primary = sp.Record.Entries
                .OrderByDescending(e => e.Entry.Chance)
                .FirstOrDefault();
            var npcId = primary?.Npc?.Id ?? 0;
            if (npcId == 0)
            {
                ImGui.Text("(no primary NPC in spawngroup)");
                return;
            }

            MaintainNpcDetailsFetch(npcId);

            // Loading / error / not-found precede the data render so we don't flash a stale
            // NPC's fields when the user selects a different spawn.
            if (_npcDetailsInFlightForId == npcId)
            {
                ImGui.Text("Loading NPC details…");
                return;
            }
            if (_npcDetailsFetchedForId == npcId && _npcDetailsError != null)
            {
                ImGui.Text($"Error: {_npcDetailsError}", new Vector4(0.95f, 0.35f, 0.25f, 1f));
                return;
            }
            if (_npcDetailsData == null || _npcDetailsData.Id != npcId)
            {
                ImGui.Text("NPC row not found in database.");
                return;
            }

            MaintainDisplayedNpc(npcId);
            if (_displayedNpc == null) return;

            var editable = _view.Controller.EditModeEnabled;
            RenderNpcDetailsBody(_displayedNpc, editable);
        }

        void MaintainNpcDetailsFetch(int npcId)
        {
            // Reap in-flight fetch first so the switch below sees a settled state.
            if (_npcDetailsFetchTask != null && _npcDetailsFetchTask.IsCompleted)
            {
                if (_npcDetailsFetchTask.IsFaulted)
                {
                    _npcDetailsError = _npcDetailsFetchTask.Exception?.GetBaseException().Message ?? "unknown error";
                    _npcDetailsData  = null;
                }
                else
                {
                    _npcDetailsError = null;
                    _npcDetailsData  = _npcDetailsFetchTask.Result;
                }
                _npcDetailsFetchedForId  = _npcDetailsInFlightForId;
                _npcDetailsInFlightForId = null;
                _npcDetailsFetchTask     = null;
            }

            // Kick off a new fetch when the target changed and nothing's in flight.
            // Selecting the same NPC again (or another spawn sharing the same npc_types
            // row) short-circuits — the cached row is reused.
            if (_npcDetailsFetchTask == null && _npcDetailsFetchedForId != npcId)
            {
                var factory = _view.Controller.DbFactory;
                if (factory == null)
                {
                    _npcDetailsError = "No database connection is configured.";
                    _npcDetailsData  = null;
                    _npcDetailsFetchedForId = npcId;
                    return;
                }
                _npcDetailsInFlightForId = npcId;
                var repo = new VisualEQ.Database.Repositories.NpcRepository(factory);
                _npcDetailsFetchTask = System.Threading.Tasks.Task.Run(async () =>
                    await repo.GetNpcByIdAsync(npcId));
            }

            // Slice 7a — usage count runs alongside details. Small query; keyed
            // on the same npc id so re-selecting a spawn with the same NPC
            // avoids a re-fetch. Reap independently — completes in parallel
            // with the details task, its result seeds _npcUsageCount.
            if (_npcUsageTask != null && _npcUsageTask.IsCompleted)
            {
                if (!_npcUsageTask.IsFaulted)
                    _npcUsageCount = _npcUsageTask.Result;
                _npcUsageFetchedForId = npcId;
                _npcUsageTask         = null;
            }
            if (_npcUsageTask == null && _npcUsageFetchedForId != npcId)
            {
                var factory = _view.Controller.DbFactory;
                if (factory != null)
                {
                    var repo = new VisualEQ.Database.Repositories.NpcRepository(factory);
                    _npcUsageTask = System.Threading.Tasks.Task.Run(async () =>
                        await repo.GetUsageCountAsync(npcId));
                }
            }
        }

        // Rebuild _displayedNpc from baseline + pending overlay when either changes.
        // Widgets mutate _displayedNpc directly during a drag/type interaction for live
        // feedback; those mutations DON'T touch the buffer (that's action-flush's job on
        // release). So the rebuild trigger has to skip mid-drag frames — we key it on the
        // buffer entry's LastModifiedAt Ticks + presence bit, which only advance when an
        // action actually fires. That way a slider drag keeps its intermediate value across
        // frames without getting clobbered by an idle overlay rebuild.
        void MaintainDisplayedNpc(int npcId)
        {
            if (_npcDetailsData == null || _npcDetailsData.Id != npcId) return;

            // Baseline swap (new NPC selected, or post-commit refetch reset _displayedNpc).
            if (_displayedNpc == null || _displayedNpc.Id != npcId)
            {
                _displayedNpc = CloneNpcTypeFull(_npcDetailsData);
                _npcDisplayedHadEdit = false;
                _npcDisplayedVersion = 0;
                OverlayPendingEdits(_displayedNpc, npcId);
                var b0 = _view.Controller.PendingBuffer;
                if (b0 != null && b0.Npcs.TryGetValue(npcId, out var e0))
                {
                    _npcDisplayedHadEdit = true;
                    _npcDisplayedVersion = e0.LastModifiedAt.Ticks;
                }
                SyncNpcTextBuffers(_displayedNpc);
                // Selecting a different NPC while the camera is locked to the previous
                // one's head/torso would leave the camera pointing at empty space (the
                // lock target is a fixed world point). Release the lock so mouse-look
                // control returns to the user until they focus a visual field again.
                Camera.ClearLookLock();
                return;
            }

            // Version-drift check — only rebuild if the pending buffer's entry changed
            // since the last overlay pass. Slider drags don't move the version because
            // the widget's write callback goes to _displayedNpc, not to the buffer.
            var buffer = _view.Controller.PendingBuffer;
            var hasEdit = buffer != null && buffer.Npcs.ContainsKey(npcId);
            long currentVer = 0;
            if (hasEdit) currentVer = buffer.Npcs[npcId].LastModifiedAt.Ticks;

            if (hasEdit != _npcDisplayedHadEdit || currentVer != _npcDisplayedVersion)
            {
                _displayedNpc = CloneNpcTypeFull(_npcDetailsData);
                OverlayPendingEdits(_displayedNpc, npcId);
                _npcDisplayedHadEdit = hasEdit;
                _npcDisplayedVersion = currentVer;
                SyncNpcTextBuffers(_displayedNpc);
            }
        }

        // Shallow-clone helper (NpcTypeFull is a flat property bag — no references to
        // copy defensively). Emitted per-property to survive future column additions
        // via compile-time errors rather than silently missing fields.
        static VisualEQ.Database.Models.NpcTypeFull CloneNpcTypeFull(VisualEQ.Database.Models.NpcTypeFull s) =>
            new VisualEQ.Database.Models.NpcTypeFull
            {
                Id = s.Id, Name = s.Name, LastName = s.LastName, Level = s.Level,
                Race = s.Race, Class = s.Class, BodyType = s.BodyType, Gender = s.Gender, Size = s.Size,
                Hp = s.Hp, Mana = s.Mana, Ac = s.Ac, MinDmg = s.MinDmg, MaxDmg = s.MaxDmg,
                Atk = s.Atk, Accuracy = s.Accuracy, Avoidance = s.Avoidance, SlowMitigation = s.SlowMitigation,
                AttackSpeed = s.AttackSpeed, AttackDelay = s.AttackDelay, AttackCount = s.AttackCount,
                HeroicStrikethrough = s.HeroicStrikethrough,
                HpRegenRate = s.HpRegenRate, HpRegenPerSecond = s.HpRegenPerSecond, ManaRegenRate = s.ManaRegenRate,
                Str = s.Str, Sta = s.Sta, Dex = s.Dex, Agi = s.Agi, Int_ = s.Int_, Wis = s.Wis, Cha = s.Cha,
                MR = s.MR, CR = s.CR, DR = s.DR, FR = s.FR, PR = s.PR, Corrup = s.Corrup, PhR = s.PhR,
                Texture = s.Texture, HelmTexture = s.HelmTexture, Face = s.Face,
                HerosForgeModel = s.HerosForgeModel, ArmTexture = s.ArmTexture, BracerTexture = s.BracerTexture,
                HandTexture = s.HandTexture, LegTexture = s.LegTexture, FeetTexture = s.FeetTexture,
                Light = s.Light, Model = s.Model, DMeleeTexture1 = s.DMeleeTexture1, DMeleeTexture2 = s.DMeleeTexture2,
                AmmoIdfile = s.AmmoIdfile, PrimMeleeType = s.PrimMeleeType, SecMeleeType = s.SecMeleeType,
                RangedType = s.RangedType,
                LuclinHairstyle = s.LuclinHairstyle, LuclinHaircolor = s.LuclinHaircolor,
                LuclinEyecolor = s.LuclinEyecolor, LuclinEyecolor2 = s.LuclinEyecolor2,
                LuclinBeardcolor = s.LuclinBeardcolor, LuclinBeard = s.LuclinBeard,
                DrakkinHeritage = s.DrakkinHeritage, DrakkinTattoo = s.DrakkinTattoo, DrakkinDetails = s.DrakkinDetails,
                ArmortintId = s.ArmortintId, ArmortintRed = s.ArmortintRed,
                ArmortintGreen = s.ArmortintGreen, ArmortintBlue = s.ArmortintBlue,
                AggroRadius = s.AggroRadius, AssistRadius = s.AssistRadius,
                Runspeed = s.Runspeed, Walkspeed = s.Walkspeed,
                SeeInvis = s.SeeInvis, SeeInvisUndead = s.SeeInvisUndead,
                SeeHide = s.SeeHide, SeeImprovedHide = s.SeeImprovedHide,
                NpcAggro = s.NpcAggro, AlwaysAggro = s.AlwaysAggro,
                Findable = s.Findable, Trackable = s.Trackable,
                RaidTarget = s.RaidTarget, NoTargetHotkey = s.NoTargetHotkey,
                Untargetable = s.Untargetable, ShowName = s.ShowName,
                PrivateCorpse = s.PrivateCorpse, UniqueSpawnByName = s.UniqueSpawnByName,
                Unique = s.Unique, Fixed = s.Fixed, IgnoreDespawn = s.IgnoreDespawn,
                StuckBehavior = s.StuckBehavior, Flymode = s.Flymode,
                RareSpawn = s.RareSpawn, Exclude = s.Exclude, IsBot = s.IsBot, IsQuest = s.IsQuest,
                Qglobal = s.Qglobal, EmoteId = s.EmoteId, Underwater = s.Underwater, SpawnLimit = s.SpawnLimit,
                LoottableId = s.LoottableId, MerchantId = s.MerchantId, Greed = s.Greed,
                AltCurrencyId = s.AltCurrencyId, NpcSpellsId = s.NpcSpellsId,
                NpcSpellsEffectsId = s.NpcSpellsEffectsId, NpcFactionId = s.NpcFactionId,
                AdventureTemplateId = s.AdventureTemplateId, TrapTemplate = s.TrapTemplate,
                FactionAmount = s.FactionAmount, KeepsSoldItems = s.KeepsSoldItems,
                IsParcelMerchant = s.IsParcelMerchant, MultiquestEnabled = s.MultiquestEnabled,
                SkipGlobalLoot = s.SkipGlobalLoot,
                Scalerate = s.Scalerate, Spellscale = s.Spellscale, Healscale = s.Healscale,
                ExpMod = s.ExpMod, Maxlevel = s.Maxlevel,
                CharmAc = s.CharmAc, CharmMinDmg = s.CharmMinDmg, CharmMaxDmg = s.CharmMaxDmg,
                CharmAttackDelay = s.CharmAttackDelay, CharmAccuracyRating = s.CharmAccuracyRating,
                CharmAvoidanceRating = s.CharmAvoidanceRating, CharmAtk = s.CharmAtk,
                NpcSpecialAttks = s.NpcSpecialAttks, SpecialAbilities = s.SpecialAbilities,
                Version = s.Version, PeqId = s.PeqId,
            };

        // Walk pending NpcEdit.CurrentValues and write each into the corresponding
        // property on _displayedNpc. Unknown fields (schema drift, legacy buffers) are
        // silently skipped rather than crashing the display.
        void OverlayPendingEdits(VisualEQ.Database.Models.NpcTypeFull dest, int npcId)
        {
            var buffer = _view.Controller.PendingBuffer;
            if (buffer == null) return;
            if (!buffer.Npcs.TryGetValue(npcId, out var edit)) return;
            foreach (var kv in edit.CurrentValues)
                ApplyNpcFieldValue(dest, kv.Key, kv.Value);
        }

        // Parses a stringified value from the buffer catalog and writes it onto the
        // matching NpcTypeFull property. Kept as a switch rather than reflection so a
        // typo caught by the compiler and per-field bug fixes don't need dynamic dispatch.
        static void ApplyNpcFieldValue(VisualEQ.Database.Models.NpcTypeFull dest, string field, string stringValue)
        {
            var def = VisualEQ.EditSystem.NpcFieldCatalog.Get(field);
            if (def == null) return;
            object v = VisualEQ.EditSystem.NpcFieldCatalog.ParseValue(stringValue, def.Kind);
            switch (field)
            {
                case "name":                   dest.Name = (string)v; break;
                case "lastname":               dest.LastName = (string)v; break;
                case "level":                  dest.Level = (int)v; break;
                case "race":                   dest.Race = (int)v; break;
                case "class":                  dest.Class = (int)v; break;
                case "bodytype":               dest.BodyType = (int)v; break;
                case "gender":                 dest.Gender = (int)v; break;
                case "size":                   dest.Size = (float)v; break;
                case "texture":                dest.Texture = (int)v; break;
                case "helmtexture":            dest.HelmTexture = (int)v; break;
                case "face":                   dest.Face = (int)v; break;
                case "hp":                     dest.Hp = (long)v; break;
                case "mana":                   dest.Mana = (long)v; break;
                case "AC":                     dest.Ac = (int)v; break;
                case "mindmg":                 dest.MinDmg = (int)v; break;
                case "maxdmg":                 dest.MaxDmg = (int)v; break;
                case "ATK":                    dest.Atk = (int)v; break;
                case "Accuracy":               dest.Accuracy = (int)v; break;
                case "Avoidance":              dest.Avoidance = (int)v; break;
                case "slow_mitigation":        dest.SlowMitigation = (int)v; break;
                case "attack_speed":           dest.AttackSpeed = (float)v; break;
                case "attack_delay":           dest.AttackDelay = (int)v; break;
                case "attack_count":           dest.AttackCount = (int)v; break;
                case "heroic_strikethrough":   dest.HeroicStrikethrough = (int)v; break;
                case "hp_regen_rate":          dest.HpRegenRate = (long)v; break;
                case "hp_regen_per_second":    dest.HpRegenPerSecond = (long)v; break;
                case "mana_regen_rate":        dest.ManaRegenRate = (long)v; break;
                case "STR":                    dest.Str = (int)v; break;
                case "STA":                    dest.Sta = (int)v; break;
                case "DEX":                    dest.Dex = (int)v; break;
                case "AGI":                    dest.Agi = (int)v; break;
                case "_INT":                   dest.Int_ = (int)v; break;
                case "WIS":                    dest.Wis = (int)v; break;
                case "CHA":                    dest.Cha = (int)v; break;
                case "MR":                     dest.MR = (int)v; break;
                case "CR":                     dest.CR = (int)v; break;
                case "DR":                     dest.DR = (int)v; break;
                case "FR":                     dest.FR = (int)v; break;
                case "PR":                     dest.PR = (int)v; break;
                case "Corrup":                 dest.Corrup = (int)v; break;
                case "PhR":                    dest.PhR = (int)v; break;
                case "aggroradius":            dest.AggroRadius = (int)v; break;
                case "assistradius":           dest.AssistRadius = (int)v; break;
                case "runspeed":               dest.Runspeed = (float)v; break;
                case "walkspeed":              dest.Walkspeed = (int)v; break;
                case "see_invis":              dest.SeeInvis = (int)v; break;
                case "see_invis_undead":       dest.SeeInvisUndead = (int)v; break;
                case "see_hide":               dest.SeeHide = (int)v; break;
                case "see_improved_hide":      dest.SeeImprovedHide = (int)v; break;
                case "npc_aggro":              dest.NpcAggro = (int)v; break;
                case "always_aggro":           dest.AlwaysAggro = (int)v; break;
                case "findable":               dest.Findable = (int)v; break;
                case "trackable":              dest.Trackable = (int)v; break;
                case "raid_target":            dest.RaidTarget = (int)v; break;
                case "no_target_hotkey":       dest.NoTargetHotkey = (int)v; break;
                case "untargetable":           dest.Untargetable = (int)v; break;
                case "show_name":              dest.ShowName = (int)v; break;
                case "private_corpse":         dest.PrivateCorpse = (int)v; break;
                case "unique_spawn_by_name":   dest.UniqueSpawnByName = (int)v; break;
                case "unique_":                dest.Unique = (int)v; break;
                case "fixed":                  dest.Fixed = (int)v; break;
                case "ignore_despawn":         dest.IgnoreDespawn = (int)v; break;
                case "stuck_behavior":         dest.StuckBehavior = (int)v; break;
                case "flymode":                dest.Flymode = (int)v; break;
                case "rare_spawn":             dest.RareSpawn = (int?)v; break;
                case "exclude":                dest.Exclude = (int)v; break;
                case "isbot":                  dest.IsBot = (int)v; break;
                case "isquest":                dest.IsQuest = (int)v; break;
                case "qglobal":                dest.Qglobal = (int)v; break;
                case "emoteid":                dest.EmoteId = (int)v; break;
                case "underwater":             dest.Underwater = (int)v; break;
                case "spawn_limit":            dest.SpawnLimit = (int)v; break;
                case "loottable_id":           dest.LoottableId = (int)v; break;
                case "merchant_id":            dest.MerchantId = (int)v; break;
                case "greed":                  dest.Greed = (int)v; break;
                case "alt_currency_id":        dest.AltCurrencyId = (int)v; break;
                case "npc_spells_id":          dest.NpcSpellsId = (int)v; break;
                case "npc_spells_effects_id":  dest.NpcSpellsEffectsId = (int)v; break;
                case "npc_faction_id":         dest.NpcFactionId = (int)v; break;
                case "adventure_template_id":  dest.AdventureTemplateId = (int)v; break;
                case "trap_template":          dest.TrapTemplate = (int?)v; break;
                case "faction_amount":         dest.FactionAmount = (int)v; break;
                case "keeps_sold_items":       dest.KeepsSoldItems = (int)v; break;
                case "is_parcel_merchant":     dest.IsParcelMerchant = (int)v; break;
                case "multiquest_enabled":     dest.MultiquestEnabled = (int)v; break;
                case "skip_global_loot":       dest.SkipGlobalLoot = (int?)v; break;
                case "scalerate":              dest.Scalerate = (int)v; break;
                case "spellscale":             dest.Spellscale = (float)v; break;
                case "healscale":              dest.Healscale = (float)v; break;
                case "exp_mod":                dest.ExpMod = (int)v; break;
                case "maxlevel":               dest.Maxlevel = (int)v; break;
                case "charm_ac":               dest.CharmAc = (int?)v; break;
                case "charm_min_dmg":          dest.CharmMinDmg = (int?)v; break;
                case "charm_max_dmg":          dest.CharmMaxDmg = (int?)v; break;
                case "charm_attack_delay":     dest.CharmAttackDelay = (int?)v; break;
                case "charm_accuracy_rating":  dest.CharmAccuracyRating = (int?)v; break;
                case "charm_avoidance_rating": dest.CharmAvoidanceRating = (int?)v; break;
                case "charm_atk":              dest.CharmAtk = (int?)v; break;
                case "special_abilities":      dest.SpecialAbilities = (string)v; break;
            }
        }

        // Text-field byte buffers. InputText writes into these directly; we mirror to
        // _displayedNpc each frame so the widget-driven changes flow through the same
        // overlay/activation-flush path as the numeric fields. Buffer sizes match the
        // schema varchar caps with a little slack for the null terminator.
        private readonly byte[] _npcNameBuf     = new byte[128];
        private readonly byte[] _npcLastNameBuf = new byte[64];
        private readonly byte[] _npcAmmoIdBuf   = new byte[32];
        private int? _npcTextBuffersForId;

        void SyncNpcTextBuffers(VisualEQ.Database.Models.NpcTypeFull n)
        {
            WriteStringToBuffer(_npcNameBuf, n.Name);
            WriteStringToBuffer(_npcLastNameBuf, n.LastName);
            WriteStringToBuffer(_npcAmmoIdBuf, n.AmmoIdfile);
            _npcTextBuffersForId = n.Id;
        }

        static void WriteStringToBuffer(byte[] dst, string s)
        {
            System.Array.Clear(dst, 0, dst.Length);
            if (string.IsNullOrEmpty(s)) return;
            var bytes = System.Text.Encoding.UTF8.GetBytes(s);
            var n = System.Math.Min(bytes.Length, dst.Length - 1); // reserve null terminator
            System.Array.Copy(bytes, dst, n);
        }

        // (numeric byte-buffer state removed — numeric widgets now use DragFloat, which
        // owns its own display state, so we don't need per-field byte buffers or the
        // reset-on-npc-change dance.)

        // ───────── NPC field widget helpers ───────────────────────────

        // Visual-affecting fields — the ones whose edits should trigger a live model
        // rebuild via Controller.RefreshNpcVisualForNpc. Race + gender rebuild the
        // AniModel from a new chr code; size rebuilds Scale on the same instance;
        // texture/helm/face swap the material variant on the same code.
        static readonly System.Collections.Generic.HashSet<string> _npcVisualFields =
            new System.Collections.Generic.HashSet<string>(System.StringComparer.OrdinalIgnoreCase)
            {
                "race", "gender", "size", "texture", "helmtexture", "face",
            };

        static bool IsNpcVisualField(string field) => _npcVisualFields.Contains(field);

        // Single funnel for every NPC-field edit action. Records the action AND, for
        // visual-affecting fields, immediately re-runs Controller.RefreshNpcVisualForNpc
        // so the on-screen model reflects the new value without waiting for save. If the
        // instance actually got a new AniModel or a new Scale, re-frames the camera so
        // the look-lock targets the new head/torso positions instead of the pre-swap ones.
        void RecordNpcFieldEdit(int npcId, string field, object from, object to, string display)
        {
            _view.Controller.RecordAction(
                new VisualEQ.EditSystem.NpcFieldEditAction(npcId, field, from, to, display));
            if (IsNpcVisualField(field) && _displayedNpc != null && _displayedNpc.Id == npcId)
            {
                var changed = _view.Controller.RefreshNpcVisualForNpc(npcId, _displayedNpc);
                if (changed)
                {
                    var sp = _view.SelectedSpawn;
                    if (sp != null) FrameNpcForField(sp, field);
                }
            }
        }

        // Framing hints for the auto-camera. FullBody pulls back so the whole silhouette
        // is in frame; Face zooms into the head; UpperBody is a middle ground for helm
        // edits where you want to see head + shoulders.
        enum NpcFramingHint { Face, UpperBody, FullBody }

        static NpcFramingHint FramingHintForField(string field)
        {
            switch (field)
            {
                case "face":         return NpcFramingHint.Face;
                case "helmtexture":  return NpcFramingHint.UpperBody;
                default:             return NpcFramingHint.FullBody; // texture, size, race, gender
            }
        }

        // Compute a "in-front-of-NPC" camera pose for the given hint and fly there with
        // a look-lock so scrubbing texture/face/helm keeps the camera on the subject.
        // Face-height / torso-height offsets match the spine-cylinder heuristic used by
        // ModelSelector.
        void FrameNpcForField(SpawnPoint sp, string field)
        {
            if (sp?.Model == null) return;
            var hint  = FramingHintForField(field);
            var pos   = sp.Model.Position;
            var scale = System.Math.Max(0.1f, sp.Model.Scale);
            // Use the race-specific authored mesh height so halflings/dwarves (mesh
            // authored at 4 units) don't get framed above their heads. Fall back to 6
            // when we don't have the effective NpcTypeFull (shouldn't happen — this is
            // called from field widgets that always have _displayedNpc — but guard just
            // in case).
            var meshHeight = _displayedNpc != null
                ? VisualEQ.SpawnSystem.SpawnManager.MeshHeightForRace(_displayedNpc.Race)
                : 6f;
            var head  = pos + new Vector3(0, 0, meshHeight * scale);
            var torso = pos + new Vector3(0, 0, (meshHeight * 2f / 3f) * scale);

            Vector3 target;
            float distance;
            switch (hint)
            {
                case NpcFramingHint.Face:      target = head;  distance = 4f  + 2f * scale; break;
                case NpcFramingHint.UpperBody: target = head;  distance = 8f  + 3f * scale; break;
                default:                       target = torso; distance = 12f + 5f * scale; break;
            }

            // NPC's forward vector — camera sits in front of the face, looking back.
            var facing = Vector3.Transform(new Vector3(0, 1, 0), sp.Model.Rotation);
            facing.Z = 0;
            if (facing.LengthSquared() < 0.0001f) facing = new Vector3(0, 1, 0);
            facing = Vector3.Normalize(facing);

            var cameraPos = target + facing * distance;
            // FpsCamera.Update adds CameraHeight to Position before the LookAt matrix, so
            // pre-subtract it here to land the eye AT target-height (not target + 5.5).
            cameraPos.Z -= VisualEQ.Engine.FpsCamera.CameraHeight;

            Camera.FlyToLookAt(cameraPos, target, 0.35f);
        }


        // Records the from/to values via NpcFieldEditAction on widget deactivation.
        // Single-slot activation tracker — ImGui only allows one active item at a time so
        // a stale field's flush fires whenever focus moves to another field. beforeValueIfStarting
        // is the value at the *start* of the interaction; the reader lambda produces the
        // value at flush time (usually the current displayed-npc property).
        void HandleNpcActivation(int npcId, string field, object beforeValueIfStarting, Func<object> readCurrent)
        {
            // ImGui.NET 0.4.6's IsAnyItemActive is unreliable for InputText — it's true
            // on the click frame + during actual keystroke frames, but returns false in
            // between (while the InputText still holds focus). Using it as the sole
            // defocus signal fires false flushes, which clear our tracking state, so
            // the next frame's !isMe branch resyncs the buffer to expected and wipes
            // whatever the user typed. Symptom: typed values silently revert to the
            // pre-focus value.
            //
            // Fix: combine IsAnyItemActive (reliable rising-edge signal for "focus just
            // captured this frame") with Gui.KeyboardWanted (reliable "some InputText
            // is still receiving keyboard input"). Consider the field defocused only
            // when BOTH are false — the keyboard-wanted bit stays true as long as any
            // text input is focused, so transient IsAnyItemActive=false readings during
            // idle-typing frames don't fire a false flush.
            var isActive       = ImGui.IsAnyItemActive();
            var keyboardWanted = _view.Controller.Engine.Gui.KeyboardWanted;
            var wasThisFieldActive =
                _npcActiveEditForId == npcId &&
                _npcActiveEditField == field;

            if (isActive && !wasThisFieldActive)
            {
                // Focus moved onto this field. If some other field was mid-flush, flush it
                // first with its own reader (matches the WP pattern — never fire an action
                // for field A using field B's reader).
                if (_npcActiveEditForId.HasValue)
                    FlushNpcActiveEditIfChanged();
                _npcActiveEditForId      = npcId;
                _npcActiveEditField      = field;
                _npcActiveEditBeforeValue = beforeValueIfStarting;
                _npcActiveEditReader      = readCurrent;

                // Auto-frame when focus first lands on a visual field, so the user sees
                // what they're editing before typing anything. The look-lock stays active
                // until the user drags mouse-look (FpsCamera.Look clears it).
                var sp = _view.SelectedSpawn;
                if (sp != null && IsNpcVisualField(field))
                    FrameNpcForField(sp, field);
            }
            else if (wasThisFieldActive && !isActive && !keyboardWanted)
            {
                // Only flush when BOTH ImGui-item-active and keyboard-wanted are false —
                // that's the real defocus. Transient !isActive during focused typing
                // (with keyboardWanted still true) is ignored.
                FlushNpcActiveEditIfChanged();
            }
        }

        void FlushNpcActiveEditIfChanged()
        {
            if (!_npcActiveEditForId.HasValue || _npcActiveEditReader == null) return;
            var npcId  = _npcActiveEditForId.Value;
            var field  = _npcActiveEditField;
            var before = _npcActiveEditBeforeValue;
            var after  = _npcActiveEditReader();

            bool changed;
            if (before is float bf && after is float af) changed = System.Math.Abs(bf - af) > 0.0001f;
            else if (before is int bi && after is int ai) changed = bi != ai;
            else if (before is long bl && after is long al) changed = bl != al;
            else changed = !object.Equals(before ?? "", after ?? "");

            if (changed)
            {
                var display = _displayedNpc?.Name ?? "?";
                RecordNpcFieldEdit(npcId, field, before, after, display);
            }

            _npcActiveEditForId       = null;
            _npcActiveEditField       = null;
            _npcActiveEditBeforeValue = null;
            _npcActiveEditReader      = null;
        }

        // Numeric widgets use DragFloat with dragSpeed=0 (drag disabled — users
        // click and type, no accidental drag). Recording is INLINE: DragFloat returns
        // `changed=true` only for the widget the user actually interacted with, so we
        // record one action per commit without the HandleNpcActivation state-tracking
        // dance that cross-contaminated fields via IsAnyItemActive's global scope.
        //
        // The earlier state-tracking approach (isMe / _npcActiveEditForId flushed per
        // field) fell apart because IsAnyItemActive is not scoped to the last-rendered
        // widget — every field's HandleNpcActivation saw isActive=true whenever ANY
        // widget was active, treated itself as "just activated", and thrashed the
        // state slot. That let a subsequent field's flush read a WRONG reader lambda
        // and record spurious walk-back actions, which in turn cleaned up
        // buffer.Npcs entries mid-edit — reverting previously-committed field values.
        void NpcInt(int npcId, string field, string label,
            System.Func<int> read, System.Action<int> write, bool editable,
            int minValue = int.MinValue, int maxValue = int.MaxValue)
        {
            var current = read();
            if (!editable)
            {
                ImGui.Text($"  {label}: {current}");
                return;
            }
            ImGui.Text(label);
            var val = (float)current;
            var changed = ImGui.DragFloat($"###{Id}ni{field}", ref val, 0f, 0f, 1f, "%.0f", 1f);
            if (changed)
            {
                var asInt = (int)System.Math.Round(val);
                if (asInt < minValue) asInt = minValue;
                if (asInt > maxValue) asInt = maxValue;
                if (asInt != current)
                {
                    write(asInt);
                    RecordNpcFieldEdit(npcId, field, current, asInt, _displayedNpc?.Name ?? "?");
                }
            }
        }

        void NpcLong(int npcId, string field, string label,
            System.Func<long> read, System.Action<long> write, bool editable)
        {
            var current = read();
            if (!editable)
            {
                ImGui.Text($"  {label}: {current}");
                return;
            }
            ImGui.Text(label);
            // DragFloat carries ~7 significant digits; values > ~10^7 lose low-bit
            // precision on edit. Practical HP/mana/regen ranges fit fine.
            var val = (float)current;
            var changed = ImGui.DragFloat($"###{Id}nl{field}", ref val, 0f, 0f, 1f, "%.0f", 1f);
            if (changed)
            {
                var asLong = (long)System.Math.Round((double)val);
                if (asLong < 0) asLong = 0;
                if (asLong != current)
                {
                    write(asLong);
                    RecordNpcFieldEdit(npcId, field, current, asLong, _displayedNpc?.Name ?? "?");
                }
            }
        }

        void NpcFloat(int npcId, string field, string label,
            System.Func<float> read, System.Action<float> write, bool editable, string fmt = "F2")
        {
            var current = read();
            if (!editable)
            {
                var display = current.ToString(fmt, System.Globalization.CultureInfo.InvariantCulture);
                ImGui.Text($"  {label}: {display}");
                return;
            }
            ImGui.Text(label);
            var val = current;
            var dfFmt = fmt.StartsWith("F", System.StringComparison.Ordinal)
                ? "%." + fmt.Substring(1) + "f"
                : "%.2f";
            var changed = ImGui.DragFloat($"###{Id}nf{field}", ref val, 0f, 0f, 1f, dfFmt, 1f);
            if (changed && System.Math.Abs(val - current) > 0.0001f)
            {
                write(val);
                RecordNpcFieldEdit(npcId, field, current, val, _displayedNpc?.Name ?? "?");
            }
        }

        void NpcText(int npcId, string field, string label,
            byte[] buffer, System.Func<string> read, System.Action<string> write, bool editable)
        {
            var current = read() ?? "";
            if (!editable)
            {
                ImGui.Text($"  {label}: {current}");
                return;
            }
            ImGui.Text(label);
            ImGui.InputText($"###{Id}nt{field}", buffer, (uint)buffer.Length, InputTextFlags.Default, null);
            var bufStr = ReadBuffer(buffer);
            if (!string.Equals(bufStr, current, System.StringComparison.Ordinal))
                write(bufStr);
            HandleNpcActivation(npcId, field, (string)current, () => (object)read());
        }

        // 0/1 int rendered as checkbox. On write, the widget flips between 0 and 1 —
        // matches how findable / trackable / show_name etc. are stored in npc_types.
        void NpcCheckbox(int npcId, string field, string label,
            System.Func<int> read, System.Action<int> write, bool editable)
        {
            var current = read();
            if (!editable)
            {
                ImGui.Text($"  {label}: {(current != 0 ? "yes" : "no")}");
                return;
            }
            var val = current != 0;
            if (ImGui.Checkbox($"{label}###{Id}nc{field}", ref val))
            {
                var before = current;
                var after  = val ? 1 : 0;
                if (before != after)
                {
                    // Checkbox activation is instantaneous — no drag-then-release cycle to
                    // coalesce over, so record inline (still routed through the visual-
                    // refresh funnel).
                    write(after);
                    var display = _displayedNpc?.Name ?? "?";
                    RecordNpcFieldEdit(npcId, field, before, after, display);
                }
            }
        }

        // Combo over an int → label dict. Options are keys of `labels` in insertion order.
        // Unknown current values render an extra "(current N)" note above the combo so a
        // legacy DB value still shows something rather than being silently reset to the
        // first option.
        void NpcEnumCombo(int npcId, string field, string label,
            System.Func<int> read, System.Action<int> write,
            IList<int> optionValues, IList<string> optionLabels, bool editable)
        {
            var current = read();
            var currentIdx = -1;
            for (int i = 0; i < optionValues.Count; i++)
                if (optionValues[i] == current) { currentIdx = i; break; }

            if (!editable)
            {
                var name = currentIdx >= 0 ? optionLabels[currentIdx] : $"({current})";
                ImGui.Text($"  {label}: {name} ({current})");
                return;
            }
            ImGui.Text(label);
            if (currentIdx < 0)
            {
                ImGui.Text($"  (current DB value {current} is outside preset list — pick to change)");
                currentIdx = 0;
            }
            var refIdx = currentIdx;
            var arr = new string[optionLabels.Count];
            for (int i = 0; i < arr.Length; i++) arr[i] = optionLabels[i];
            if (ImGui.Combo($"###{Id}ne{field}", ref refIdx, arr))
            {
                var after = optionValues[refIdx];
                if (after != current)
                {
                    write(after);
                    var display = _displayedNpc?.Name ?? "?";
                    RecordNpcFieldEdit(npcId, field, current, after, display);
                }
            }
        }

        // Nullable int — checkbox toggles between "null" and a numeric value. When checked
        // for the first time, initial value is 0. Uncheck → set to null. Numeric drag
        // routes through the standard activation-flush pattern.
        void NpcNullableInt(int npcId, string field, string label,
            System.Func<int?> read, System.Action<int?> write, bool editable)
        {
            var current = read();
            if (!editable)
            {
                ImGui.Text($"  {label}: {(current?.ToString() ?? "(null)")}");
                return;
            }

            var isSet = current.HasValue;
            if (ImGui.Checkbox($"Set {label}###{Id}nnc{field}", ref isSet))
            {
                if (isSet && !current.HasValue)
                {
                    write(0);
                    var display = _displayedNpc?.Name ?? "?";
                    RecordNpcFieldEdit(npcId, field, (int?)null, (int?)0, display);
                }
                else if (!isSet && current.HasValue)
                {
                    var before = current;
                    write(null);
                    var display = _displayedNpc?.Name ?? "?";
                    RecordNpcFieldEdit(npcId, field, before, (int?)null, display);
                }
            }
            if (isSet)
            {
                var cur = current ?? 0;
                var val = (float)cur;
                var changed = ImGui.DragFloat($"  {label}###{Id}nni{field}", ref val, 0f, 0f, 1f, "%.0f", 1f);
                if (changed)
                {
                    var asInt = (int)System.Math.Round(val);
                    if (asInt != cur)
                    {
                        write(asInt);
                        RecordNpcFieldEdit(npcId, field, cur, asInt, _displayedNpc?.Name ?? "?");
                    }
                }
            }
        }

        // FK picker — displays "<id> — <name>" resolved via cache, plus a Change button
        // that opens the modal typeahead. Read-only mode renders just the resolved text.
        void NpcIdPicker(int npcId, string field, string label,
            VisualEQ.SpawnSystem.ReferenceDataCache.Table table,
            System.Func<int> read, bool editable)
        {
            var cache = _view.Controller.ReferenceData;
            var current = read();
            var resolved = cache != null ? cache.ResolveLabel(table, current) : current.ToString();

            if (!editable)
            {
                ImGui.Text($"  {label}: {resolved}");
                return;
            }
            ImGui.Text($"{label}: {resolved}");
            ImGui.SameLine();
            if (ImGui.Button($"Change###{Id}nip{field}", new Vector2(90, 22)))
                BeginFkPicker(table, field, label, npcId, current);
            ImGui.SameLine();
            if (current != 0 && ImGui.Button($"Clear###{Id}nipc{field}", new Vector2(60, 22)))
            {
                // Explicit clear sets the FK to 0 (EQEmu's "no assignment" sentinel).
                var display = _displayedNpc?.Name ?? "?";
                RecordNpcFieldEdit(npcId, field, current, 0, display);
            }
        }

        // ── Compact / grid-friendly variants of Npc{Int,Long,Float} ────────────
        // These render label + DragFloat on ONE line with an explicit input width,
        // so callers can pack them into 2-, 3-, or 4-column grids via SameLine(x)
        // between cells. Field IDs are byte-identical to the block-form helpers
        // (###{Id}ni{field}, ###{Id}nl{field}, ###{Id}nf{field}) — HandleNpcActivation
        // slots key on those and MUST stay unchanged. Read-only branch renders
        // "label: value" inline so grids don't collapse when EditMode is off.
        void NpcIntInline(int npcId, string field, string label,
            System.Func<int> read, System.Action<int> write, bool editable,
            float inputWidth,
            int minValue = int.MinValue, int maxValue = int.MaxValue)
        {
            var current = read();
            if (!editable)
            {
                ImGui.Text($"{label}: {current}");
                return;
            }
            ImGui.Text(label);
            ImGui.SameLine();
            NsimGui.CimguiRaw.igPushItemWidth(inputWidth);
            var val = (float)current;
            var changed = ImGui.DragFloat($"###{Id}ni{field}", ref val, 0f, 0f, 1f, "%.0f", 1f);
            NsimGui.CimguiRaw.igPopItemWidth();
            if (changed)
            {
                var asInt = (int)System.Math.Round(val);
                if (asInt < minValue) asInt = minValue;
                if (asInt > maxValue) asInt = maxValue;
                if (asInt != current)
                {
                    write(asInt);
                    RecordNpcFieldEdit(npcId, field, current, asInt, _displayedNpc?.Name ?? "?");
                }
            }
        }

        void NpcLongInline(int npcId, string field, string label,
            System.Func<long> read, System.Action<long> write, bool editable,
            float inputWidth)
        {
            var current = read();
            if (!editable)
            {
                ImGui.Text($"{label}: {current}");
                return;
            }
            ImGui.Text(label);
            ImGui.SameLine();
            NsimGui.CimguiRaw.igPushItemWidth(inputWidth);
            var val = (float)current;
            var changed = ImGui.DragFloat($"###{Id}nl{field}", ref val, 0f, 0f, 1f, "%.0f", 1f);
            NsimGui.CimguiRaw.igPopItemWidth();
            if (changed)
            {
                var asLong = (long)System.Math.Round((double)val);
                if (asLong < 0) asLong = 0;
                if (asLong != current)
                {
                    write(asLong);
                    RecordNpcFieldEdit(npcId, field, current, asLong, _displayedNpc?.Name ?? "?");
                }
            }
        }

        void NpcFloatInline(int npcId, string field, string label,
            System.Func<float> read, System.Action<float> write, bool editable,
            float inputWidth, string fmt = "F2")
        {
            var current = read();
            if (!editable)
            {
                var display = current.ToString(fmt, System.Globalization.CultureInfo.InvariantCulture);
                ImGui.Text($"{label}: {display}");
                return;
            }
            ImGui.Text(label);
            ImGui.SameLine();
            NsimGui.CimguiRaw.igPushItemWidth(inputWidth);
            var val = current;
            var dfFmt = fmt.StartsWith("F", System.StringComparison.Ordinal)
                ? "%." + fmt.Substring(1) + "f"
                : "%.2f";
            var changed = ImGui.DragFloat($"###{Id}nf{field}", ref val, 0f, 0f, 1f, dfFmt, 1f);
            NsimGui.CimguiRaw.igPopItemWidth();
            if (changed && System.Math.Abs(val - current) > 0.0001f)
            {
                write(val);
                RecordNpcFieldEdit(npcId, field, current, val, _displayedNpc?.Name ?? "?");
            }
        }

        // Special-abilities editor (Slice 4). One row per SpecialAbilityCatalog entry
        // with an enable checkbox + a value input (int, DragFloat with drag disabled).
        // Any ability that has non-zero params 0..8 shows them read-only inline —
        // param editing is out of MVP scope; users who need it can bypass the widget
        // by editing the raw string via server-side tooling. Unknown ability ids (in
        // raw string but not in the server catalog we scraped) get a "Custom (id=N)"
        // read-only row at the bottom and are preserved verbatim on save.
        //
        // Every change re-serializes the whole entries dict and fires one
        // NpcFieldEditAction — same commit path as any other npc_types field.
        // Faction-entries editor (Slice 5). Modeled on peqphpeditor's npc/faction
        // view — a compact header (set name + primary faction + assist flag) over
        // a fixed-column table of hits: Faction | Value | Reaction | Temp | X.
        //
        // Column widths are absolute pixels via igSetNextItemWidth so the columns
        // line up regardless of the widest faction name. The name column truncates
        // if the sidebar is narrower than expected — full name is on the row's
        // tooltip via ImGui.Text (implicit hover doesn't kick in here, so it's
        // shown inline).
        //
        // npc_value is a tri-state enum (Aggressive / Passive / Assist), not a
        // signed byte editable via drag. temp is a 4-state enum (Perm / Temp / …),
        // not a boolean checkbox. This corrects two guesses from the initial cut.
        //
        // Fetch runs two queries in parallel: npc_faction_entries (the rows) and
        // npc_faction (the parent set metadata). FactionList reference cache is
        // force-warmed here so the very first render already has names — no more
        // "you see just IDs until the async fetch lands" flash.
        //
        // Requires npc_faction_id > 0. NPCs without a faction set get a hint
        // pointing to the References section where the FK picker is.
        void NpcFactionEntriesEditor(int npcId, int npcFactionId, bool editable)
        {
            if (npcFactionId <= 0)
            {
                ImGui.Text("(no faction set assigned — pick one in the References section)");
                return;
            }

            // Faction names come inline via the LEFT JOIN on faction_list — no
            // reliance on ReferenceDataCache for row rendering. Cache is still
            // used by the Add-faction picker further down (its own concern).
            var cache = _view.Controller.ReferenceData;

            MaintainFactionEntriesFetch(npcFactionId);
            if (_factionEntriesInFlightFor == npcFactionId)
            {
                ImGui.Text("Loading faction entries…");
                return;
            }
            if (_factionEntriesFetchedFor == npcFactionId && _factionEntriesError != null)
            {
                ImGui.Text($"Error: {_factionEntriesError}", new Vector4(0.95f, 0.35f, 0.25f, 1f));
                return;
            }
            if (_factionEntriesFetchedFor != npcFactionId || _factionEntriesData == null)
            {
                ImGui.Text("Waiting for faction entries…");
                return;
            }

            // ── Header: set name + primary faction + ignore-assist flag ──────
            // Slice 7b: header fields are editable in edit mode via a dedicated
            // dynamic UPDATE loop against npc_faction. Overlay the pending edit
            // dict onto the fetched set metadata so widgets always render the
            // effective (post-edit) value.
            var effectiveSet = OverlayEffectiveNpcFactionSet(npcFactionId, _factionSetData);
            var setName = string.IsNullOrWhiteSpace(effectiveSet?.Name) ? "(unnamed set)" : effectiveSet.Name;

            if (editable && effectiveSet != null)
            {
                RenderNpcFactionHeaderEditRow(npcId, npcFactionId, effectiveSet);
            }
            else
            {
                ImGui.Text($"Set #{npcFactionId} — \"{setName}\"");
                string primaryLine;
                if (effectiveSet == null)
                    primaryLine = "(loading…)";
                else if (effectiveSet.PrimaryFaction == 0)
                    primaryLine = "(none)";
                else if (!string.IsNullOrWhiteSpace(effectiveSet.PrimaryFactionName))
                    primaryLine = $"{effectiveSet.PrimaryFactionName} (#{effectiveSet.PrimaryFaction})";
                else
                    primaryLine = $"? (#{effectiveSet.PrimaryFaction})";
                ImGui.Text($"Primary faction: {primaryLine}");
                var ignoreAssistRO = effectiveSet != null && effectiveSet.IgnorePrimaryAssist != 0;
                ImGui.Text($"Ignore primary assist: {(ignoreAssistRO ? "Yes" : "No")}");
            }

            ImGui.Separator();

            // ── Table: hit rows ───────────────────────────────────────────
            var effective = ComputeEffectiveFactionEntries(npcFactionId);

            // Column geometry, sized to the actual content region so the layout
            // adapts when the user widens the sidebar. Widget widths are fixed
            // (combos need to fit their widest label — "Aggressive"); the faction
            // name column absorbs whatever's left. Minimums stop things from
            // collapsing at the ~320px minimum sidebar width.
            var contentW = ImGui.GetContentRegionAvailable().X;
            if (contentW < 320f) contentW = 320f;

            const float wValue    = 55f;    // "-2000" fits
            const float wReaction = 100f;   // "Aggressive" + arrow
            const float wTemp     = 85f;    // "Perm/NM" + arrow
            const float wRemove   = 22f;    // small "X" button
            const float colGap    = 6f;

            var xRemove   = contentW - wRemove;
            var xTemp     = xRemove - colGap - wTemp;
            var xReaction = xTemp   - colGap - wReaction;
            var xValue    = xReaction - colGap - wValue;
            // Minimum faction-name column width so ultra-narrow sidebars don't
            // squeeze names into 20px. If we ever hit this, columns overflow to
            // the right (still readable, just no longer perfectly aligned).
            if (xValue < 90f) xValue = 90f;

            // Header row.
            ImGui.Text("Faction");
            ImGui.SameLine(xValue);    ImGui.Text("Adj.");
            ImGui.SameLine(xReaction); ImGui.Text("Reaction");
            ImGui.SameLine(xTemp);     ImGui.Text("Temp");
            ImGui.Separator();

            if (effective.Count == 0)
            {
                ImGui.Text("  (no faction hits in this set)");
            }

            foreach (var entry in effective.OrderBy(e =>
                string.IsNullOrWhiteSpace(e.FactionName) ? "￿" + e.FactionId : e.FactionName))
            {
                // Format: "Name (#id)". Name from the LEFT JOIN, falls back to "?"
                // when faction_list has no row for the id (dangling fk — user still
                // sees the id, so they can look it up).
                var factionName = !string.IsNullOrWhiteSpace(entry.FactionName)
                    ? $"{entry.FactionName} (#{entry.FactionId})"
                    : $"? (#{entry.FactionId})";

                // Faction name column — plain text; ID follows the name so the row
                // reads at a glance and the id is available for anyone jumping into
                // faction_list directly.
                ImGui.Text(factionName);

                if (!editable)
                {
                    // Read-only view: same columns, just labels.
                    ImGui.SameLine(xValue);
                    ImGui.Text(entry.Value.ToString());

                    ImGui.SameLine(xReaction);
                    ImGui.Text(ReactionLabel(entry.NpcValue));

                    ImGui.SameLine(xTemp);
                    ImGui.Text(TempLabel(entry.Temp));
                    continue;
                }

                // Value: numeric input, ~70px wide. Push/Pop per widget mimics the
                // (unavailable) SetNextItemWidth API — see CimguiRaw comment.
                ImGui.SameLine(xValue);
                NsimGui.CimguiRaw.igPushItemWidth(wValue);
                var val = (float)entry.Value;
                var vChanged = ImGui.DragFloat($"##{Id}nfeV{npcFactionId}_{entry.FactionId}",
                    ref val, 0f, 0f, 0f, "%.0f", 1f);
                NsimGui.CimguiRaw.igPopItemWidth();
                if (vChanged)
                {
                    var newVal = (int)System.Math.Round(val);
                    if (newVal != entry.Value)
                        RecordFactionEntryEdit(entry, e => e.Value = newVal, factionName);
                }

                // Reaction: tri-state combo. Fall back to first slot if the DB has
                // an out-of-range value (shouldn't happen, but keeps the combo sane).
                ImGui.SameLine(xReaction);
                NsimGui.CimguiRaw.igPushItemWidth(wReaction);
                var reactionIdx = System.Array.IndexOf(_reactionVals, (int)entry.NpcValue);
                if (reactionIdx < 0) reactionIdx = 1; // default to Passive
                var refReactionIdx = reactionIdx;
                var reactionChanged = ImGui.Combo($"##{Id}nfeR{npcFactionId}_{entry.FactionId}", ref refReactionIdx, _reactionLabels);
                NsimGui.CimguiRaw.igPopItemWidth();
                if (reactionChanged)
                {
                    var newNv = (sbyte)_reactionVals[refReactionIdx];
                    if (newNv != entry.NpcValue)
                        RecordFactionEntryEdit(entry, e => e.NpcValue = newNv, factionName);
                }

                // Temp: 4-state combo.
                ImGui.SameLine(xTemp);
                NsimGui.CimguiRaw.igPushItemWidth(wTemp);
                var tempIdx = System.Array.IndexOf(_tempVals, (int)entry.Temp);
                if (tempIdx < 0) tempIdx = 0;
                var refTempIdx = tempIdx;
                var tempChanged = ImGui.Combo($"##{Id}nfeT{npcFactionId}_{entry.FactionId}", ref refTempIdx, _tempLabels);
                NsimGui.CimguiRaw.igPopItemWidth();
                if (tempChanged)
                {
                    var newTemp = (sbyte)_tempVals[refTempIdx];
                    if (newTemp != entry.Temp)
                        RecordFactionEntryEdit(entry, e => e.Temp = newTemp, factionName);
                }

                // Remove button.
                ImGui.SameLine(xRemove);
                if (ImGui.Button($"X##{Id}nfeX{npcFactionId}_{entry.FactionId}", new Vector2(24, 22)))
                    RecordFactionEntryDelete(npcFactionId, entry, factionName);
            }

            if (editable)
            {
                ImGui.Separator();
                if (ImGui.Button($"+ Add faction hit…###{Id}nfeAdd{npcFactionId}", new Vector2(160, 24)))
                {
                    // Open the FK picker over faction_list with a callback that inserts
                    // a new entry (value=0 defaults) instead of writing an NpcFieldEditAction.
                    var currentEffective = effective;
                    BeginFkPicker(
                        VisualEQ.SpawnSystem.ReferenceDataCache.Table.FactionList,
                        "faction_entry_add", "Add faction hit to set",
                        npcId, 0,
                        onPicked: pickedFactionId =>
                        {
                            // Guard against re-adding an existing faction (composite PK
                            // is (npc_faction_id, faction_id) — a duplicate would fail
                            // the commit's INSERT).
                            if (currentEffective.Any(e => e.FactionId == pickedFactionId)) return;
                            var newSnap = new NpcFactionEntrySnapshot { Value = 0, NpcValue = 0, Temp = 0 };
                            var cache2 = _view.Controller.ReferenceData;
                            var picked = cache2?.ResolveLabel(
                                VisualEQ.SpawnSystem.ReferenceDataCache.Table.FactionList, pickedFactionId)
                                ?? pickedFactionId.ToString();
                            _view.Controller.RecordAction(new NpcFactionEntryEditAction(
                                npcFactionId, pickedFactionId, null, newSnap, picked));
                        });
                }
            }
        }

        static string ReactionLabel(sbyte npcValue)
        {
            var idx = System.Array.IndexOf(_reactionVals, (int)npcValue);
            return idx >= 0 ? _reactionLabels[idx] : $"({npcValue})";
        }

        static string TempLabel(sbyte temp)
        {
            var idx = System.Array.IndexOf(_tempVals, (int)temp);
            return idx >= 0 ? _tempLabels[idx] : $"({temp})";
        }

        // Kick lazy fetches for both npc_faction_entries AND the parent npc_faction
        // set row. Cached against the last-fetched id so switching NPCs that share
        // a faction set is free. The set-metadata task is fire-and-forget: renders
        // "(unknown)" gracefully if it hasn't landed yet, and always polls-in on
        // the same tick as the entries task.
        void MaintainFactionEntriesFetch(int npcFactionId)
        {
            // Reap set-metadata task first (independent of entries task; both were
            // spawned together but land in whatever order the DB replies).
            if (_factionSetTask != null && _factionSetTask.IsCompleted)
            {
                _factionSetData = _factionSetTask.IsFaulted ? null : _factionSetTask.Result;
                _factionSetTask = null;
            }

            if (_factionEntriesTask != null && _factionEntriesTask.IsCompleted)
            {
                if (_factionEntriesTask.IsFaulted)
                {
                    _factionEntriesError = _factionEntriesTask.Exception?.GetBaseException().Message ?? "unknown error";
                    _factionEntriesData  = null;
                }
                else
                {
                    _factionEntriesError = null;
                    _factionEntriesData  = _factionEntriesTask.Result;
                }
                _factionEntriesFetchedFor  = _factionEntriesInFlightFor;
                _factionEntriesInFlightFor = null;
                _factionEntriesTask        = null;
            }
            if (_factionEntriesTask == null && _factionEntriesFetchedFor != npcFactionId)
            {
                var factory = _view.Controller.DbFactory;
                if (factory == null)
                {
                    _factionEntriesError = "No database connection is configured.";
                    _factionEntriesData  = null;
                    _factionEntriesFetchedFor = npcFactionId;
                    return;
                }
                _factionEntriesInFlightFor = npcFactionId;
                _factionSetData            = null;
                var repo = new VisualEQ.Database.Repositories.FactionRepository(factory);
                _factionEntriesTask = System.Threading.Tasks.Task.Run(async () =>
                    (await repo.GetNpcFactionEntriesAsync(npcFactionId)).ToList());
                _factionSetTask     = System.Threading.Tasks.Task.Run(async () =>
                    await repo.GetNpcFactionSetAsync(npcFactionId));
            }
        }

        // Merge fetched baseline + pending buffer ops → the effective in-memory list
        // the widget renders. Inserts add new entries, updates modify existing, deletes
        // remove. Result is a fresh List so callers can mutate freely.
        List<VisualEQ.Database.Models.NpcFactionEntry> ComputeEffectiveFactionEntries(int npcFactionId)
        {
            var byId = new Dictionary<int, VisualEQ.Database.Models.NpcFactionEntry>();
            if (_factionEntriesData != null)
                foreach (var e in _factionEntriesData)
                    byId[e.FactionId] = new VisualEQ.Database.Models.NpcFactionEntry
                    {
                        NpcFactionId = e.NpcFactionId,
                        FactionId    = e.FactionId,
                        Value        = e.Value,
                        NpcValue     = e.NpcValue,
                        Temp         = e.Temp,
                        FactionName  = e.FactionName,
                    };

            var buffer = _view.Controller.PendingBuffer;
            if (buffer != null)
            {
                foreach (var kv in buffer.NpcFactionEntries)
                {
                    var op = kv.Value;
                    if (op.NpcFactionId != npcFactionId) continue;
                    if (op.Current == null)
                    {
                        byId.Remove(op.FactionId);
                    }
                    else
                    {
                        // Preserve baseline name on update; fall back to the op's
                        // captured name (from picker or edit-time snapshot) so pure-
                        // insert rows still display a name in the editor.
                        byId.TryGetValue(op.FactionId, out var existing);
                        var name = existing != null && !string.IsNullOrEmpty(existing.FactionName)
                            ? existing.FactionName
                            : op.FactionName;
                        byId[op.FactionId] = new VisualEQ.Database.Models.NpcFactionEntry
                        {
                            NpcFactionId = npcFactionId,
                            FactionId    = op.FactionId,
                            Value        = op.Current.Value,
                            NpcValue     = op.Current.NpcValue,
                            Temp         = op.Current.Temp,
                            FactionName  = name,
                        };
                    }
                }
            }
            return byId.Values.ToList();
        }

        // Record a mutation to an existing faction entry. Captures the "from"
        // snapshot at call time (either the buffer's current-if-present, or the DB
        // baseline for first-touch), applies `mutate` to a new "to" snapshot, fires
        // the action. Repeated edits on the same entry keep the true DB baseline as
        // Original inside the buffer (walk-back cleanup then works correctly).
        void RecordFactionEntryEdit(VisualEQ.Database.Models.NpcFactionEntry current,
            System.Action<NpcFactionEntrySnapshot> mutate, string factionName)
        {
            var from = new NpcFactionEntrySnapshot
            {
                Value    = current.Value,
                NpcValue = current.NpcValue,
                Temp     = current.Temp,
            };
            var to = new NpcFactionEntrySnapshot
            {
                Value    = current.Value,
                NpcValue = current.NpcValue,
                Temp     = current.Temp,
            };
            mutate(to);
            _view.Controller.RecordAction(new NpcFactionEntryEditAction(
                current.NpcFactionId, current.FactionId, from, to, factionName));
        }

        // Delete-entry action. `from` is the current snapshot; `to` is null.
        void RecordFactionEntryDelete(int npcFactionId,
            VisualEQ.Database.Models.NpcFactionEntry current, string factionName)
        {
            var from = new NpcFactionEntrySnapshot
            {
                Value    = current.Value,
                NpcValue = current.NpcValue,
                Temp     = current.Temp,
            };
            _view.Controller.RecordAction(new NpcFactionEntryEditAction(
                npcFactionId, current.FactionId, from, null, factionName));
        }

        // ── Slice 6a: read-only loot editor ─────────────────────────────
        // Renders the NPC's assigned loottable → its lootdrops → each lootdrop's
        // item list. Fetch is keyed on loottable id (not npc id) so NPCs that
        // share a loottable share the fetch. `items.Name` and `lootdrop.name`
        // come inline via LEFT JOIN — same pattern as Slice 5's faction editor.
        //
        // Requires loottable_id > 0. NPCs without one get a hint pointing at
        // References (where the loottable FK picker lives). Editing arrives in
        // Slice 6b: full CRUD on loottable_entries + lootdrop_entries via the
        // shared EditBuffer path. Overlay merge composes DB baseline + pending
        // ops so the widget shows the "effective" state pre-commit; per-row
        // widgets emit LootTableEntryEditAction / LootDropEntryEditAction; the
        // Add flows open the search picker and INSERT with sensible defaults.
        //
        // Assigning a loottable to an NPC (npc_types.loottable_id itself) is
        // handled by the existing FK picker in References — no duplication here.
        void NpcLootEditor(int npcId, int loottableId, bool editable)
        {
            if (loottableId <= 0)
            {
                // Slice 6c fix: don't dead-end the user. Offer both an
                // existing-loottable picker AND the create-new flow directly
                // from the loot section — otherwise "no loottable assigned"
                // becomes a black hole they can only escape via References,
                // and the create-new path (which lives here) isn't reachable
                // at all until they've assigned one.
                ImGui.Text("No loottable assigned.");
                if (!editable)
                {
                    ImGui.Text("(read-only mode — flip to edit to assign one)");
                    return;
                }
                if (ImGui.Button($"Pick existing loottable…###{Id}ndPickLT0", new Vector2(200, 24)))
                {
                    BeginFkPicker(
                        VisualEQ.SpawnSystem.ReferenceDataCache.Table.LootTable,
                        "loottable_id", "Loot table",
                        npcId, 0);
                }
                ImGui.SameLine();
                if (ImGui.Button($"+ New empty loottable…###{Id}ndNewLT0", new Vector2(200, 24)))
                    BeginCreateEmptyLootTable(npcId, 0);
                return;
            }

            MaintainLootFetch(loottableId);
            if (_lootInFlightForLoottableId == loottableId)
            {
                ImGui.Text("Loading loot…");
                return;
            }
            if (_lootFetchedForLoottableId == loottableId && _lootError != null)
            {
                ImGui.Text($"Error: {_lootError}", new Vector4(0.95f, 0.35f, 0.25f, 1f));
                return;
            }
            if (_lootFetchedForLoottableId != loottableId || _lootData == null)
            {
                ImGui.Text("Waiting for loot data…");
                return;
            }

            var data  = _lootData;
            var table = data.LootTable;

            // ── Header block ────────────────────────────────────────────
            // Overlay merged view: the fetched LootTable is the DB baseline;
            // pending LootTables buffer op (if any) shadows the four editable
            // fields. Widgets always render the effective (post-overlay) value
            // so a mid-edit re-render reflects the latest keystroke.
            var effectiveTable = OverlayEffectiveLootTable(loottableId, table);
            var ltName = string.IsNullOrWhiteSpace(effectiveTable?.Name) ? "(unnamed)" : effectiveTable.Name;
            ImGui.Text($"Loottable #{loottableId} — \"{ltName}\"");
            if (effectiveTable != null)
            {
                if (editable)
                {
                    RenderLootTableHeaderEditRow(loottableId, effectiveTable, ltName);
                }
                else
                {
                    ImGui.Text($"Cash: min {effectiveTable.MinCash} / max {effectiveTable.MaxCash} / avg {effectiveTable.AvgCoin}");
                }
                if (effectiveTable.MinExpansion != -1 || effectiveTable.MaxExpansion != -1)
                    ImGui.Text($"Expansion window: {effectiveTable.MinExpansion} .. {effectiveTable.MaxExpansion}");
                if (!string.IsNullOrEmpty(effectiveTable.ContentFlags))
                    ImGui.Text($"Content flags: {effectiveTable.ContentFlags}");
                if (!string.IsNullOrEmpty(effectiveTable.ContentFlagsDisabled))
                    ImGui.Text($"Content flags disabled: {effectiveTable.ContentFlagsDisabled}");
            }
            ImGui.Text($"Used by: {data.UsageCount} NPC(s)");

            // Slice 6c — shared-loottable warning + clone button. Only surfaces
            // when > 1 NPC points at this loottable (any edit here silently
            // affects everyone).
            if (editable && data.UsageCount > 1)
            {
                ImGui.Text("⚠ Shared — edits affect every NPC above.", new Vector4(0.95f, 0.75f, 0.25f, 1f));
                if (ImGui.Button($"Clone loottable for this NPC###{Id}ndCloneLT{loottableId}", new Vector2(240, 22)))
                    BeginCloneLootTable(loottableId, table?.Name, data.UsageCount, npcId);
            }
            if (editable)
            {
                if (ImGui.Button($"+ New empty loottable…###{Id}ndNewLT{loottableId}", new Vector2(200, 22)))
                    BeginCreateEmptyLootTable(npcId, loottableId);
            }
            ImGui.Separator();

            // ── Per-lootdrop cards (with overlay merge) ─────────────────
            var effectiveLTE = ComputeEffectiveLootTableEntries(loottableId, data);

            if (effectiveLTE.Count == 0)
                ImGui.Text("(no lootdrops in this loottable)");

            foreach (var entry in effectiveLTE)
            {
                RenderLootTableEntryRow(loottableId, entry, data, editable);
            }

            if (editable)
            {
                ImGui.Separator();
                if (ImGui.Button($"+ Add lootdrop to loottable…###{Id}ndAddLD{loottableId}", new Vector2(220, 24)))
                {
                    var factory = _view.Controller.DbFactory;
                    if (factory != null)
                    {
                        var repo = new VisualEQ.Database.Repositories.LootRepository(factory);
                        BeginSearchPicker(
                            $"Add lootdrop to loottable #{loottableId}",
                            async filter => (await repo.SearchLootdropsAsync(filter, 200)).ToList(),
                            picked =>
                            {
                                // Duplicate-guard: if the lootdrop is already in the
                                // effective list (baseline + pending inserts), no-op.
                                if (effectiveLTE.Any(e => e.LootdropId == picked.Id)) return;
                                var newSnap = new LootTableEntrySnapshot
                                {
                                    Multiplier  = 1,
                                    DropLimit   = 1,
                                    MinDrop     = 1,
                                    Probability = 100f,
                                };
                                _view.Controller.RecordAction(new LootTableEntryEditAction(
                                    loottableId, picked.Id, null, newSnap, picked.Name));
                            });
                    }
                }
                ImGui.SameLine();
                if (ImGui.Button($"+ New empty lootdrop…###{Id}ndNewLD{loottableId}", new Vector2(200, 24)))
                    BeginCreateEmptyLootDrop(loottableId);
            }
        }

        void RenderLootTableEntryRow(int loottableId,
            VisualEQ.Database.Models.LootTableEntry entry,
            LootFetchResult data, bool editable)
        {
            var dropName = string.IsNullOrWhiteSpace(entry.LootdropName) ? "(unnamed)" : entry.LootdropName;
            var items = data.ItemsByLootdrop.TryGetValue(entry.LootdropId, out var baseline)
                ? baseline
                : new List<VisualEQ.Database.Models.LootDropEntry>();
            var effectiveItems = ComputeEffectiveLootDropEntries(entry.LootdropId, items);

            var header = $"{dropName} (#{entry.LootdropId})  — {effectiveItems.Count} item(s), prob {entry.Probability:0.##}%###{Id}ndLD{entry.LootdropId}";
            if (!ImGui.CollapsingHeader(header, 0)) return;

            // Slice 6c — per-lootdrop shared-warning + clone button. The
            // usage-count lookup is per-lootdrop across all loottables (batched
            // fetch); if > 1, editing items here quietly changes loot for
            // every other loottable that references the same lootdrop.
            var lootdropUsage = 1;
            if (data.LootdropUsage != null)
                data.LootdropUsage.TryGetValue(entry.LootdropId, out lootdropUsage);
            if (lootdropUsage <= 0) lootdropUsage = 1;

            if (editable && lootdropUsage > 1)
            {
                ImGui.Text($"  ⚠ Shared with {lootdropUsage - 1} other loottable(s) — item edits affect them all.",
                    new Vector4(0.95f, 0.75f, 0.25f, 1f));
                if (ImGui.Button($"  Clone lootdrop for this loottable###{Id}ndCloneLD{entry.LootdropId}", new Vector2(260, 22)))
                    BeginCloneLootDrop(entry.LootdropId, dropName, lootdropUsage, loottableId);
            }

            // Roll-param edit row: mindrop / droplimit / mult / probability + X.
            if (editable)
            {
                RenderLootTableEntryEditRow(loottableId, entry, dropName);
            }
            else
            {
                ImGui.Text($"  mindrop {entry.MinDrop}   droplimit {entry.DropLimit}   mult {entry.Multiplier}   prob {entry.Probability:0.##}");
            }

            // Items table — column headers + rows + add button.
            var contentW = ImGui.GetContentRegionAvailable().X;
            if (contentW < 320f) contentW = 320f;

            const float wChance = 55f;
            const float wMult   = 35f;
            const float wEqp    = 35f;
            const float wRemove = 22f;
            const float colGap  = 6f;
            var xRemove = contentW - wRemove;
            var xEqp    = xRemove - colGap - wEqp;
            var xMult   = xEqp    - colGap - wMult;
            var xChance = xMult   - colGap - wChance;

            ImGui.Text("  Item");
            ImGui.SameLine(xChance); ImGui.Text("Chance");
            ImGui.SameLine(xMult);   ImGui.Text("Mult");
            ImGui.SameLine(xEqp);    ImGui.Text("Eqp");
            ImGui.Separator();

            if (effectiveItems.Count == 0)
            {
                ImGui.Text("  (no items)");
            }

            foreach (var it in effectiveItems)
            {
                RenderLootDropEntryRow(entry.LootdropId, entry.Probability, it, xChance, xMult, xEqp, xRemove, editable);
            }

            if (editable)
            {
                if (ImGui.Button($"+ Add item to lootdrop…###{Id}ndAddIt{entry.LootdropId}", new Vector2(200, 22)))
                {
                    var factory = _view.Controller.DbFactory;
                    if (factory != null)
                    {
                        var repo = new VisualEQ.Database.Repositories.ItemRepository(factory);
                        var currentEffective = effectiveItems;
                        var lootdropId = entry.LootdropId;
                        BeginSearchPicker(
                            $"Add item to lootdrop #{lootdropId}",
                            async filter => (await repo.SearchItemsAsync(filter, 200)).ToList(),
                            picked =>
                            {
                                if (currentEffective.Any(e => e.ItemId == picked.Id)) return;
                                var newSnap = new LootDropEntrySnapshot
                                {
                                    ItemCharges = 1,
                                    EquipItem   = 0,
                                    Chance      = 100f,
                                    Multiplier  = 1,
                                };
                                _view.Controller.RecordAction(new LootDropEntryEditAction(
                                    lootdropId, picked.Id, null, newSnap, picked.Name));
                            });
                    }
                }
            }
        }

        // loottable_entries roll-param edit row. mindrop / droplimit / mult are
        // byte fields (0..255); probability is float (0..100). All emit
        // LootTableEntryEditAction — same buffer op, per-field mutation.
        void RenderLootTableEntryEditRow(int loottableId,
            VisualEQ.Database.Models.LootTableEntry entry, string dropName)
        {
            ImGui.Text("  Roll:");
            ImGui.SameLine();
            var minDropF = (float)entry.MinDrop;
            NsimGui.CimguiRaw.igPushItemWidth(55f);
            var minDropChanged = ImGui.DragFloat($"min##{Id}lteMD{loottableId}_{entry.LootdropId}",
                ref minDropF, 0f, 0f, 0f, "%.0f", 1f);
            NsimGui.CimguiRaw.igPopItemWidth();

            ImGui.SameLine();
            var dropLimF = (float)entry.DropLimit;
            NsimGui.CimguiRaw.igPushItemWidth(55f);
            var dropLimChanged = ImGui.DragFloat($"lim##{Id}lteDL{loottableId}_{entry.LootdropId}",
                ref dropLimF, 0f, 0f, 0f, "%.0f", 1f);
            NsimGui.CimguiRaw.igPopItemWidth();

            ImGui.SameLine();
            var multF = (float)entry.Multiplier;
            NsimGui.CimguiRaw.igPushItemWidth(45f);
            var multChanged = ImGui.DragFloat($"mult##{Id}lteM{loottableId}_{entry.LootdropId}",
                ref multF, 0f, 0f, 0f, "%.0f", 1f);
            NsimGui.CimguiRaw.igPopItemWidth();

            ImGui.SameLine();
            var probF = entry.Probability;
            NsimGui.CimguiRaw.igPushItemWidth(65f);
            var probChanged = ImGui.DragFloat($"%##{Id}lteP{loottableId}_{entry.LootdropId}",
                ref probF, 0f, 0f, 100f, "%.2f", 1f);
            NsimGui.CimguiRaw.igPopItemWidth();

            ImGui.SameLine();
            var removePressed = ImGui.Button($"Remove lootdrop##{Id}lteX{loottableId}_{entry.LootdropId}", new Vector2(130, 22));

            if (minDropChanged || dropLimChanged || multChanged || probChanged)
            {
                var newSnap = new LootTableEntrySnapshot
                {
                    Multiplier  = (byte)ClampByte((int)System.Math.Round(multF)),
                    DropLimit   = (byte)ClampByte((int)System.Math.Round(dropLimF)),
                    MinDrop     = (byte)ClampByte((int)System.Math.Round(minDropF)),
                    Probability = System.Math.Max(0f, System.Math.Min(100f, probF)),
                };
                if (!SnapshotEqualsLTE(newSnap, entry))
                {
                    var from = new LootTableEntrySnapshot
                    {
                        Multiplier  = entry.Multiplier,
                        DropLimit   = entry.DropLimit,
                        MinDrop     = entry.MinDrop,
                        Probability = entry.Probability,
                    };
                    _view.Controller.RecordAction(new LootTableEntryEditAction(
                        loottableId, entry.LootdropId, from, newSnap, dropName));
                }
            }

            if (removePressed)
            {
                var from = new LootTableEntrySnapshot
                {
                    Multiplier  = entry.Multiplier,
                    DropLimit   = entry.DropLimit,
                    MinDrop     = entry.MinDrop,
                    Probability = entry.Probability,
                };
                _view.Controller.RecordAction(new LootTableEntryEditAction(
                    loottableId, entry.LootdropId, from, null, dropName));
            }
        }

        void RenderLootDropEntryRow(int lootdropId, float lootdropProbability,
            VisualEQ.Database.Models.LootDropEntry it,
            float xChance, float xMult, float xEqp, float xRemove, bool editable)
        {
            var itemName = string.IsNullOrWhiteSpace(it.ItemName) ? "?" : it.ItemName;
            ImGui.Text($"  {itemName} (#{it.ItemId})");

            if (!editable)
            {
                var effective = lootdropProbability >= 100f || lootdropProbability <= 0f
                    ? it.Chance
                    : (it.Chance / 100f) * (lootdropProbability / 100f) * 100f;
                ImGui.SameLine(xChance); ImGui.Text($"{effective:0.##}");
                ImGui.SameLine(xMult);   ImGui.Text(it.Multiplier.ToString());
                ImGui.SameLine(xEqp);    ImGui.Text(it.EquipItem != 0 ? "Yes" : "No");
                return;
            }

            // Editable row — inline DragFloat widgets + checkbox + X. Fields
            // outside the four primary columns (charges, trivial min/max, npc
            // min/max, disabled_chance) are preserved via the snapshot copy
            // but not exposed inline; they can be edited by removing + re-
            // adding the row via the picker (advanced) or via a future polish
            // slice.
            var chanceF = it.Chance;
            ImGui.SameLine(xChance);
            NsimGui.CimguiRaw.igPushItemWidth(55f);
            var chChanged = ImGui.DragFloat($"##{Id}ldeCh{lootdropId}_{it.ItemId}",
                ref chanceF, 0f, 0f, 0f, "%.2f", 1f);
            NsimGui.CimguiRaw.igPopItemWidth();

            var multF = (float)it.Multiplier;
            ImGui.SameLine(xMult);
            NsimGui.CimguiRaw.igPushItemWidth(35f);
            var multChanged = ImGui.DragFloat($"##{Id}ldeMu{lootdropId}_{it.ItemId}",
                ref multF, 0f, 0f, 0f, "%.0f", 1f);
            NsimGui.CimguiRaw.igPopItemWidth();

            var eqpOn = it.EquipItem != 0;
            ImGui.SameLine(xEqp);
            var eqpChanged = ImGui.Checkbox($"##{Id}ldeEq{lootdropId}_{it.ItemId}", ref eqpOn);

            ImGui.SameLine(xRemove);
            var removePressed = ImGui.Button($"X##{Id}ldeX{lootdropId}_{it.ItemId}", new Vector2(22, 22));

            if (chChanged || multChanged || eqpChanged)
            {
                var newSnap = SnapshotLDE(it);
                newSnap.Chance     = System.Math.Max(0f, chanceF);
                newSnap.Multiplier = (byte)ClampByte((int)System.Math.Round(multF));
                newSnap.EquipItem  = (byte)(eqpOn ? 1 : 0);
                if (!SnapshotEqualsLDE(newSnap, it))
                {
                    _view.Controller.RecordAction(new LootDropEntryEditAction(
                        lootdropId, it.ItemId, SnapshotLDE(it), newSnap, itemName));
                }
            }

            if (removePressed)
            {
                _view.Controller.RecordAction(new LootDropEntryEditAction(
                    lootdropId, it.ItemId, SnapshotLDE(it), null, itemName));
            }
        }

        static int ClampByte(int v) => v < 0 ? 0 : (v > 255 ? 255 : v);

        static LootDropEntrySnapshot SnapshotLDE(VisualEQ.Database.Models.LootDropEntry e) =>
            new LootDropEntrySnapshot
            {
                ItemCharges     = e.ItemCharges,
                EquipItem       = e.EquipItem,
                Chance          = e.Chance,
                DisabledChance  = e.DisabledChance,
                TrivialMinLevel = e.TrivialMinLevel,
                TrivialMaxLevel = e.TrivialMaxLevel,
                Multiplier      = e.Multiplier,
                NpcMinLevel     = e.NpcMinLevel,
                NpcMaxLevel     = e.NpcMaxLevel,
            };

        static bool SnapshotEqualsLTE(LootTableEntrySnapshot s, VisualEQ.Database.Models.LootTableEntry e) =>
            s.Multiplier  == e.Multiplier
         && s.DropLimit   == e.DropLimit
         && s.MinDrop     == e.MinDrop
         && s.Probability == e.Probability;

        static bool SnapshotEqualsLDE(LootDropEntrySnapshot s, VisualEQ.Database.Models.LootDropEntry e) =>
            s.ItemCharges     == e.ItemCharges
         && s.EquipItem       == e.EquipItem
         && s.Chance          == e.Chance
         && s.DisabledChance  == e.DisabledChance
         && s.TrivialMinLevel == e.TrivialMinLevel
         && s.TrivialMaxLevel == e.TrivialMaxLevel
         && s.Multiplier      == e.Multiplier
         && s.NpcMinLevel     == e.NpcMinLevel
         && s.NpcMaxLevel     == e.NpcMaxLevel;

        // Slice 6c follow-up — clone the DB baseline loottable and overlay the
        // pending buffer op (if any) so widgets see the effective (post-edit)
        // values. Returns null if baseline is null (loottable row missing).
        // Slice 7b — overlay pending NpcFactions edits onto the fetched set
        // metadata so the header widgets always render the effective (post-
        // edit) value. Returns null if baseline is null.
        VisualEQ.Database.Models.NpcFactionSet OverlayEffectiveNpcFactionSet(
            int npcFactionId, VisualEQ.Database.Models.NpcFactionSet baseline)
        {
            if (baseline == null) return null;
            var eff = new VisualEQ.Database.Models.NpcFactionSet
            {
                Id                  = baseline.Id,
                Name                = baseline.Name,
                PrimaryFaction      = baseline.PrimaryFaction,
                IgnorePrimaryAssist = baseline.IgnorePrimaryAssist,
                PrimaryFactionName  = baseline.PrimaryFactionName,
            };
            var buffer = _view.Controller.PendingBuffer;
            if (buffer != null && buffer.NpcFactions.TryGetValue(npcFactionId, out var edit))
            {
                foreach (var kv in edit.CurrentValues)
                {
                    switch (kv.Key)
                    {
                        case "name":
                            eff.Name = kv.Value;
                            break;
                        case "primaryfaction":
                            eff.PrimaryFaction = int.Parse(kv.Value, System.Globalization.CultureInfo.InvariantCulture);
                            // Try to resolve the name from the FactionList cache;
                            // fall back to null so the render shows "? (#id)".
                            var cache = _view.Controller.ReferenceData;
                            var newName = cache?.GetNameLookup(VisualEQ.SpawnSystem.ReferenceDataCache.Table.FactionList);
                            eff.PrimaryFactionName = newName != null && newName.TryGetValue(eff.PrimaryFaction, out var pn) ? pn : null;
                            break;
                        case "ignore_primary_assist":
                            eff.IgnorePrimaryAssist = (sbyte)int.Parse(kv.Value, System.Globalization.CultureInfo.InvariantCulture);
                            break;
                    }
                }
            }
            return eff;
        }

        // Slice 7b — inline header editor for one npc_faction row. Primary
        // faction opens the FactionList FK picker via a callback path (writes
        // directly to a buffered NpcFactionFieldEditAction instead of an
        // NpcFieldEditAction). Ignore-assist is a checkbox that fires on click.
        // Name editing deferred — needs the NpcText-style deferred-write
        // machinery, punted for scope.
        void RenderNpcFactionHeaderEditRow(int npcId, int npcFactionId,
            VisualEQ.Database.Models.NpcFactionSet effective)
        {
            var setName = string.IsNullOrWhiteSpace(effective.Name) ? "(unnamed set)" : effective.Name;
            ImGui.Text($"Set #{npcFactionId} — \"{setName}\"");

            // Primary faction row: read-only label + Change / Clear buttons.
            var primaryText = effective.PrimaryFaction == 0
                ? "(none)"
                : (!string.IsNullOrWhiteSpace(effective.PrimaryFactionName)
                    ? $"{effective.PrimaryFactionName} (#{effective.PrimaryFaction})"
                    : $"? (#{effective.PrimaryFaction})");
            ImGui.Text($"Primary faction: {primaryText}");
            ImGui.SameLine();
            if (ImGui.Button($"Change###{Id}nfPfBtn{npcFactionId}", new Vector2(80, 22)))
            {
                var currentPf = effective.PrimaryFaction;
                var displayName = string.IsNullOrWhiteSpace(effective.Name) ? "?" : effective.Name;
                BeginFkPicker(
                    VisualEQ.SpawnSystem.ReferenceDataCache.Table.FactionList,
                    "primaryfaction", "Primary faction",
                    npcId, currentPf,
                    onPicked: pickedFactionId =>
                    {
                        if (pickedFactionId == currentPf) return;
                        _view.Controller.RecordAction(new NpcFactionFieldEditAction(
                            npcFactionId, "primaryfaction", currentPf, pickedFactionId, displayName));
                    });
            }
            if (effective.PrimaryFaction != 0)
            {
                ImGui.SameLine();
                if (ImGui.Button($"Clear###{Id}nfPfClr{npcFactionId}", new Vector2(60, 22)))
                {
                    var currentPf = effective.PrimaryFaction;
                    var displayName = string.IsNullOrWhiteSpace(effective.Name) ? "?" : effective.Name;
                    _view.Controller.RecordAction(new NpcFactionFieldEditAction(
                        npcFactionId, "primaryfaction", currentPf, 0, displayName));
                }
            }

            // Ignore-primary-assist checkbox.
            var ignoreOn = effective.IgnorePrimaryAssist != 0;
            if (ImGui.Checkbox($"Ignore primary assist###{Id}nfIpa{npcFactionId}", ref ignoreOn))
            {
                var beforeI = (int)effective.IgnorePrimaryAssist;
                var afterI  = ignoreOn ? 1 : 0;
                if (beforeI != afterI)
                {
                    var displayName = string.IsNullOrWhiteSpace(effective.Name) ? "?" : effective.Name;
                    _view.Controller.RecordAction(new NpcFactionFieldEditAction(
                        npcFactionId, "ignore_primary_assist", beforeI, afterI, displayName));
                }
            }

            // Slice 7b — open the pop-out browser for cross-NPC set management.
            if (ImGui.Button($"Manage faction sets…###{Id}nfMgr{npcFactionId}", new Vector2(180, 22)))
                BeginManageFactionSets(npcId, npcFactionId);
        }

        VisualEQ.Database.Models.LootTable OverlayEffectiveLootTable(
            int loottableId, VisualEQ.Database.Models.LootTable baseline)
        {
            if (baseline == null) return null;
            var eff = new VisualEQ.Database.Models.LootTable
            {
                Id                    = baseline.Id,
                Name                  = baseline.Name,
                MinCash               = baseline.MinCash,
                MaxCash               = baseline.MaxCash,
                AvgCoin               = baseline.AvgCoin,
                MinExpansion          = baseline.MinExpansion,
                MaxExpansion          = baseline.MaxExpansion,
                ContentFlags          = baseline.ContentFlags,
                ContentFlagsDisabled  = baseline.ContentFlagsDisabled,
            };
            var buffer = _view.Controller.PendingBuffer;
            if (buffer != null && buffer.LootTables.TryGetValue(loottableId, out var edit))
            {
                foreach (var kv in edit.CurrentValues)
                {
                    switch (kv.Key)
                    {
                        case "name":    eff.Name    = kv.Value; break;
                        case "mincash": eff.MinCash = int.Parse(kv.Value, System.Globalization.CultureInfo.InvariantCulture); break;
                        case "maxcash": eff.MaxCash = int.Parse(kv.Value, System.Globalization.CultureInfo.InvariantCulture); break;
                        case "avgcoin": eff.AvgCoin = int.Parse(kv.Value, System.Globalization.CultureInfo.InvariantCulture); break;
                    }
                }
            }
            return eff;
        }

        // Slice 6c follow-up — inline editors for the loottable header fields
        // (mincash / maxcash / avgcoin). Name edit deferred to avoid crowding
        // the header; when needed, use `+ New empty loottable` with the desired
        // name, then re-point via the FK picker. Emits LootTableFieldEditAction
        // per changed field so undo/redo works one field at a time.
        void RenderLootTableHeaderEditRow(int loottableId,
            VisualEQ.Database.Models.LootTable effective, string displayName)
        {
            ImGui.Text("Cash:");
            ImGui.SameLine();
            var minF = (float)effective.MinCash;
            NsimGui.CimguiRaw.igPushItemWidth(80f);
            var minChanged = ImGui.DragFloat($"min##{Id}lthMin{loottableId}",
                ref minF, 0f, 0f, 0f, "%.0f", 1f);
            NsimGui.CimguiRaw.igPopItemWidth();

            ImGui.SameLine();
            var maxF = (float)effective.MaxCash;
            NsimGui.CimguiRaw.igPushItemWidth(80f);
            var maxChanged = ImGui.DragFloat($"max##{Id}lthMax{loottableId}",
                ref maxF, 0f, 0f, 0f, "%.0f", 1f);
            NsimGui.CimguiRaw.igPopItemWidth();

            ImGui.SameLine();
            var avgF = (float)effective.AvgCoin;
            NsimGui.CimguiRaw.igPushItemWidth(80f);
            var avgChanged = ImGui.DragFloat($"avg##{Id}lthAvg{loottableId}",
                ref avgF, 0f, 0f, 0f, "%.0f", 1f);
            NsimGui.CimguiRaw.igPopItemWidth();

            if (minChanged)
            {
                var newVal = (int)System.Math.Max(0, System.Math.Round(minF));
                if (newVal != effective.MinCash)
                    _view.Controller.RecordAction(new LootTableFieldEditAction(
                        loottableId, "mincash", effective.MinCash, newVal, displayName));
            }
            if (maxChanged)
            {
                var newVal = (int)System.Math.Max(0, System.Math.Round(maxF));
                if (newVal != effective.MaxCash)
                    _view.Controller.RecordAction(new LootTableFieldEditAction(
                        loottableId, "maxcash", effective.MaxCash, newVal, displayName));
            }
            if (avgChanged)
            {
                var newVal = (int)System.Math.Max(0, System.Math.Round(avgF));
                if (newVal != effective.AvgCoin)
                    _view.Controller.RecordAction(new LootTableFieldEditAction(
                        loottableId, "avgcoin", effective.AvgCoin, newVal, displayName));
            }
        }

        // Merge baseline loottable_entries + pending buffer ops → effective list.
        // Same overlay-merge shape as ComputeEffectiveFactionEntries; propagates
        // LootdropName from baseline JOIN on update, from op.LootdropName on
        // insert.
        List<VisualEQ.Database.Models.LootTableEntry> ComputeEffectiveLootTableEntries(
            int loottableId, LootFetchResult data)
        {
            var byId = new Dictionary<int, VisualEQ.Database.Models.LootTableEntry>();
            foreach (var e in data.Entries)
                byId[e.LootdropId] = new VisualEQ.Database.Models.LootTableEntry
                {
                    LoottableId = e.LoottableId,
                    LootdropId  = e.LootdropId,
                    Multiplier  = e.Multiplier,
                    DropLimit   = e.DropLimit,
                    MinDrop     = e.MinDrop,
                    Probability = e.Probability,
                    LootdropName = e.LootdropName,
                };

            var buffer = _view.Controller.PendingBuffer;
            if (buffer != null)
            {
                foreach (var kv in buffer.LootTableEntries)
                {
                    var op = kv.Value;
                    if (op.LoottableId != loottableId) continue;
                    if (op.Current == null)
                    {
                        byId.Remove(op.LootdropId);
                    }
                    else
                    {
                        byId.TryGetValue(op.LootdropId, out var existing);
                        var name = existing != null && !string.IsNullOrEmpty(existing.LootdropName)
                            ? existing.LootdropName
                            : op.LootdropName;
                        byId[op.LootdropId] = new VisualEQ.Database.Models.LootTableEntry
                        {
                            LoottableId  = loottableId,
                            LootdropId   = op.LootdropId,
                            Multiplier   = op.Current.Multiplier,
                            DropLimit    = op.Current.DropLimit,
                            MinDrop      = op.Current.MinDrop,
                            Probability  = op.Current.Probability,
                            LootdropName = name,
                        };
                    }
                }
            }
            return byId.Values.OrderBy(e => string.IsNullOrWhiteSpace(e.LootdropName)
                ? "�" + e.LootdropId : e.LootdropName).ToList();
        }

        List<VisualEQ.Database.Models.LootDropEntry> ComputeEffectiveLootDropEntries(
            int lootdropId, List<VisualEQ.Database.Models.LootDropEntry> baseline)
        {
            var byId = new Dictionary<int, VisualEQ.Database.Models.LootDropEntry>();
            if (baseline != null)
                foreach (var e in baseline)
                    byId[e.ItemId] = new VisualEQ.Database.Models.LootDropEntry
                    {
                        LootdropId      = e.LootdropId,
                        ItemId          = e.ItemId,
                        ItemCharges     = e.ItemCharges,
                        EquipItem       = e.EquipItem,
                        Chance          = e.Chance,
                        DisabledChance  = e.DisabledChance,
                        TrivialMinLevel = e.TrivialMinLevel,
                        TrivialMaxLevel = e.TrivialMaxLevel,
                        Multiplier      = e.Multiplier,
                        NpcMinLevel     = e.NpcMinLevel,
                        NpcMaxLevel     = e.NpcMaxLevel,
                        ItemName        = e.ItemName,
                    };

            var buffer = _view.Controller.PendingBuffer;
            if (buffer != null)
            {
                foreach (var kv in buffer.LootDropEntries)
                {
                    var op = kv.Value;
                    if (op.LootdropId != lootdropId) continue;
                    if (op.Current == null)
                    {
                        byId.Remove(op.ItemId);
                    }
                    else
                    {
                        byId.TryGetValue(op.ItemId, out var existing);
                        var name = existing != null && !string.IsNullOrEmpty(existing.ItemName)
                            ? existing.ItemName
                            : op.ItemName;
                        byId[op.ItemId] = new VisualEQ.Database.Models.LootDropEntry
                        {
                            LootdropId      = lootdropId,
                            ItemId          = op.ItemId,
                            ItemCharges     = op.Current.ItemCharges,
                            EquipItem       = op.Current.EquipItem,
                            Chance          = op.Current.Chance,
                            DisabledChance  = op.Current.DisabledChance,
                            TrivialMinLevel = op.Current.TrivialMinLevel,
                            TrivialMaxLevel = op.Current.TrivialMaxLevel,
                            Multiplier      = op.Current.Multiplier,
                            NpcMinLevel     = op.Current.NpcMinLevel,
                            NpcMaxLevel     = op.Current.NpcMaxLevel,
                            ItemName        = name,
                        };
                    }
                }
            }
            return byId.Values.OrderBy(e => string.IsNullOrWhiteSpace(e.ItemName)
                ? "�" + e.ItemId : e.ItemName).ToList();
        }

        // Fetch pump for the loot editor. One Task.Run spawns all three queries
        // (loottable header, entries with lootdrop-name JOIN, items batched
        // across all lootdrops with items.Name JOIN) and composes them into one
        // LootFetchResult so the widget's render path is single-null-check.
        //
        // Batches items via IN (@Ids) to keep the fetch at 4 queries total no
        // matter how many lootdrops the table has — no N+1 for wide tables.
        void MaintainLootFetch(int loottableId)
        {
            if (_lootTask != null && _lootTask.IsCompleted)
            {
                if (_lootTask.IsFaulted)
                {
                    _lootError = _lootTask.Exception?.GetBaseException().Message ?? "unknown error";
                    _lootData  = null;
                }
                else
                {
                    _lootError = null;
                    _lootData  = _lootTask.Result;
                }
                _lootFetchedForLoottableId  = _lootInFlightForLoottableId;
                _lootInFlightForLoottableId = null;
                _lootTask                   = null;
            }
            if (_lootTask == null && _lootFetchedForLoottableId != loottableId)
            {
                var factory = _view.Controller.DbFactory;
                if (factory == null)
                {
                    _lootError = "No database connection is configured.";
                    _lootData  = null;
                    _lootFetchedForLoottableId = loottableId;
                    return;
                }
                _lootInFlightForLoottableId = loottableId;
                var repo = new VisualEQ.Database.Repositories.LootRepository(factory);
                _lootTask = System.Threading.Tasks.Task.Run(async () =>
                {
                    var header = await repo.GetLootTableAsync(loottableId);
                    var entries = (await repo.GetLootTableEntriesAsync(loottableId)).ToList();
                    var usage  = await repo.GetLootTableUsageCountAsync(loottableId);

                    var lootdropIds = entries.Select(e => e.LootdropId).Distinct().ToList();
                    var itemsFlat = (await repo.GetLootDropEntriesBatchAsync(lootdropIds)).ToList();
                    var itemsByDrop = itemsFlat
                        .GroupBy(i => i.LootdropId)
                        .ToDictionary(g => g.Key, g => g.ToList());
                    var lootdropUsage = await repo.GetLootDropUsageCountsAsync(lootdropIds);

                    return new LootFetchResult
                    {
                        LootTable       = header,
                        Entries         = entries,
                        ItemsByLootdrop = itemsByDrop,
                        UsageCount      = usage,
                        LootdropUsage   = lootdropUsage,
                    };
                });
            }
        }

        void NpcSpecialAbilitiesEditor(int npcId, System.Func<string> read, System.Action<string> write, bool editable)
        {
            var current = read() ?? "";
            var parsed  = VisualEQ.EditSystem.SpecialAbilityString.Parse(current);

            ImGui.Text($"Special abilities  ({parsed.Count} active)");
            if (!editable)
            {
                if (parsed.Count == 0)
                {
                    ImGui.Text("  (none)");
                    return;
                }
                foreach (var e in parsed.Values.OrderBy(x => x.AbilityId))
                {
                    var entry = VisualEQ.EditSystem.SpecialAbilityCatalog.Get(e.AbilityId);
                    var name = entry != null ? entry.Name : $"Custom (id={e.AbilityId})";
                    ImGui.Text($"  {name}: value={e.Value}{FormatSaParams(e.Params)}");
                }
                return;
            }

            // Scrollable list — 57 known abilities fits comfortably at ~18px per row
            // inside a bounded child. Height is tall enough to show ~15 rows without
            // scrolling; users scroll for the rest.
            ImGui.BeginChild($"###{Id}saList", new Vector2(0, 300), true, WindowFlags.Default);
            var display = _displayedNpc?.Name ?? "?";
            bool dirty = false;

            foreach (var abilityEntry in VisualEQ.EditSystem.SpecialAbilityCatalog.All)
            {
                var abilityId = abilityEntry.Id;
                var hasEntry = parsed.TryGetValue(abilityId, out var e);
                var chk = hasEntry;

                if (ImGui.Checkbox($"###{Id}saChk{abilityId}", ref chk))
                {
                    if (chk && !hasEntry)
                    {
                        parsed[abilityId] = new VisualEQ.EditSystem.SpecialAbilityString.Entry
                        {
                            AbilityId = abilityId,
                            Value     = 1,
                        };
                        dirty = true;
                        hasEntry = true;
                        e = parsed[abilityId];
                    }
                    else if (!chk && hasEntry)
                    {
                        parsed.Remove(abilityId);
                        dirty = true;
                        hasEntry = false;
                    }
                }

                ImGui.SameLine();
                ImGui.Text(abilityEntry.Name);

                if (hasEntry && !abilityEntry.IsBoolean)
                {
                    // Non-boolean ability — value carries magnitude (chance %, HP
                    // threshold, distance, etc. depending on ability). Expose input.
                    ImGui.SameLine();
                    ImGui.Text("value");
                    ImGui.SameLine();
                    var val = (float)e.Value;
                    // dragSpeed=1 so Ctrl+Click text entry works (same trap from Slice 3).
                    var changed = ImGui.DragFloat($"###{Id}saVal{abilityId}", ref val, 0f, 0f, 1f, "%.0f", 1f);
                    if (changed)
                    {
                        var newV = (int)System.Math.Round(val);
                        if (newV != e.Value)
                        {
                            e.Value = newV;
                            dirty = true;
                        }
                    }
                }
                // Boolean abilities: checkbox alone is enough. Value stays 1 when
                // enabled (set by the checkbox path above), 0 when disabled (entry
                // removed from `parsed`).

                if (hasEntry && HasAnyParam(e.Params))
                {
                    // Read-only inline params for any ability with non-zero params.
                    // Rare — MVP surfaces them as text; per-param editor is deferred.
                    ImGui.Text($"    params:{FormatSaParams(e.Params)}");
                }
            }

            // Unknown ability ids surface at the bottom of the list so they're not
            // silently dropped. Preserved on serialize because they live in `parsed`
            // — the checkbox loop doesn't remove them (only iterates known ids).
            var unknownIds = parsed.Keys.Where(id => !VisualEQ.EditSystem.SpecialAbilityCatalog.IsKnown(id)).ToList();
            if (unknownIds.Count > 0)
            {
                ImGui.Separator();
                ImGui.Text("Unknown ability ids (preserved on save):");
                foreach (var id in unknownIds)
                {
                    var e = parsed[id];
                    ImGui.Text($"  id={id}: value={e.Value}{FormatSaParams(e.Params)}");
                }
            }

            ImGui.EndChild();

            if (dirty)
            {
                var newRaw = VisualEQ.EditSystem.SpecialAbilityString.Serialize(parsed);
                if (newRaw != current)
                {
                    write(newRaw);
                    RecordNpcFieldEdit(npcId, "special_abilities", current, newRaw, display);
                }
            }
        }

        static bool HasAnyParam(int[] p)
        {
            for (int i = 0; i < p.Length; i++) if (p[i] != 0) return true;
            return false;
        }

        static string FormatSaParams(int[] p)
        {
            var sb = new System.Text.StringBuilder();
            int lastNonZero = -1;
            for (int i = 0; i < p.Length; i++) if (p[i] != 0) lastNonZero = i;
            for (int i = 0; i <= lastNonZero; i++)
            {
                sb.Append(' ');
                sb.Append('p'); sb.Append(i); sb.Append('=');
                sb.Append(p[i]);
            }
            return sb.ToString();
        }

        // ───────── FK picker modal ────────────────────────────────────

        void BeginFkPicker(VisualEQ.SpawnSystem.ReferenceDataCache.Table table,
            string fieldName, string label, int npcId, int currentValue,
            System.Action<int> onPicked = null)
        {
            _fkPickerActive       = true;
            _fkPickerTable        = table;
            _fkPickerFieldName    = fieldName;
            _fkPickerLabel        = label;
            _fkPickerNpcId        = npcId;
            _fkPickerCurrentValue = currentValue;
            _fkPickerNpcDisplayName = _displayedNpc?.Name ?? "?";
            _fkPickerOnPicked     = onPicked;
            System.Array.Clear(_fkPickerFilterBuf, 0, _fkPickerFilterBuf.Length);
            _fkPickerSelectedIdx = -1;
        }

        void EndFkPicker()
        {
            _fkPickerActive = false;
            _fkPickerFieldName = null;
            _fkPickerOnPicked  = null;
        }

        // ── Slice 6b: SEARCH picker (server-side LIKE fetch per keystroke) ──
        //
        // Caller supplies a `fetch` delegate that takes the current filter and
        // returns matching rows. This picker fires the delegate whenever the
        // filter changes; stale results are discarded by comparing the fetching
        // filter to the current filter when a task completes. No debounce timer
        // — on a local DB the queries land fast enough that keystroke-lag is
        // negligible, and the stale-result check keeps race conditions clean.
        void BeginSearchPicker(string label,
            System.Func<string, System.Threading.Tasks.Task<System.Collections.Generic.List<VisualEQ.Database.Models.ReferenceItem>>> fetch,
            System.Action<VisualEQ.Database.Models.ReferenceItem> onPicked)
        {
            _searchPickerActive     = true;
            _searchPickerLabel      = label ?? "Search";
            _searchPickerFetch      = fetch;
            _searchPickerOnPicked   = onPicked;
            _searchPickerResults    = null;
            _searchPickerTask       = null;
            _searchPickerTaskFilter = null;
            _searchPickerLastFilter = null; // forces the first-frame fetch
            _searchPickerError      = null;
            _searchPickerSelectedIdx = -1;
            System.Array.Clear(_searchPickerFilterBuf, 0, _searchPickerFilterBuf.Length);
        }

        void EndSearchPicker()
        {
            _searchPickerActive   = false;
            _searchPickerFetch    = null;
            _searchPickerOnPicked = null;
            _searchPickerResults  = null;
            _searchPickerTask     = null;
        }

        void RenderSearchPickerDialog(Gui gui)
        {
            const float dlgW = 560f;
            const float dlgH = 480f;
            var pos = new Vector2((gui.Dimensions.X - dlgW) / 2, (gui.Dimensions.Y - dlgH) / 2);

            ImGui.SetNextWindowPos(pos, Condition.Always, Vector2.Zero);
            ImGui.SetNextWindowSize(new Vector2(dlgW, dlgH), Condition.Always);

            const WindowFlags flags = WindowFlags.NoTitleBar | WindowFlags.NoMove
                                    | WindowFlags.NoResize   | WindowFlags.NoCollapse
                                    | WindowFlags.NoSavedSettings;

            ImGui.BeginWindow($"###{Id}SearchPickerDlg", flags);

            ImGui.Text(_searchPickerLabel);
            ImGui.Separator();

            ImGui.Text("Filter (substring, name or id):");
            ImGui.InputText($"###{Id}spF", _searchPickerFilterBuf, (uint)_searchPickerFilterBuf.Length, InputTextFlags.Default, null);
            var filter = ReadBuffer(_searchPickerFilterBuf).Trim();

            // Keystroke → new fetch. Compares against last-issued filter so we
            // don't re-fire the same query every frame. Task-completion path
            // discards stale results (older filter than the one currently in
            // the buffer).
            if (_searchPickerFetch != null && filter != _searchPickerLastFilter)
            {
                _searchPickerLastFilter = filter;
                _searchPickerTaskFilter = filter;
                _searchPickerError      = null;
                var capturedFilter = filter;
                var capturedFetch   = _searchPickerFetch;
                _searchPickerTask = System.Threading.Tasks.Task.Run(async () =>
                {
                    var result = await capturedFetch(capturedFilter);
                    return result ?? new System.Collections.Generic.List<VisualEQ.Database.Models.ReferenceItem>();
                });
            }

            // Reap the in-flight task; discard results if the filter has moved
            // on since we launched.
            if (_searchPickerTask != null && _searchPickerTask.IsCompleted)
            {
                if (_searchPickerTask.IsFaulted)
                {
                    _searchPickerError = _searchPickerTask.Exception?.GetBaseException().Message ?? "unknown error";
                }
                else if (_searchPickerTaskFilter == _searchPickerLastFilter)
                {
                    _searchPickerResults = _searchPickerTask.Result;
                    _searchPickerSelectedIdx = -1;
                }
                _searchPickerTask = null;
            }

            if (_searchPickerError != null)
            {
                ImGui.Text($"Error: {_searchPickerError}", new Vector4(0.95f, 0.35f, 0.25f, 1f));
            }
            else if (_searchPickerTask != null || _searchPickerResults == null)
            {
                ImGui.Text("Searching…");
            }
            else
            {
                ImGui.Text($"{_searchPickerResults.Count} match(es) shown");
            }

            ImGui.BeginChild($"###{Id}spList", new Vector2(0, 340), true, WindowFlags.Default);
            if (_searchPickerResults != null)
            {
                for (int i = 0; i < _searchPickerResults.Count; i++)
                {
                    var it = _searchPickerResults[i];
                    var lbl = $"{it.Id}  {it.Name}###{Id}spR{it.Id}";
                    if (ImGui.Selectable(lbl, i == _searchPickerSelectedIdx))
                        _searchPickerSelectedIdx = i;
                }
            }
            ImGui.EndChild();

            ImGui.Separator();
            var pickEnabled = _searchPickerResults != null
                              && _searchPickerSelectedIdx >= 0
                              && _searchPickerSelectedIdx < _searchPickerResults.Count;
            if (pickEnabled)
            {
                if (ImGui.Button($"Select###{Id}spOk", new Vector2(140, 28)))
                {
                    var picked = _searchPickerResults[_searchPickerSelectedIdx];
                    var cb = _searchPickerOnPicked;
                    EndSearchPicker();
                    cb?.Invoke(picked);
                    ImGui.EndWindow();
                    return;
                }
            }
            else
            {
                ImGui.Text("(pick a row to enable Select)");
            }
            ImGui.SameLine();
            if (ImGui.Button($"Cancel###{Id}spX", new Vector2(140, 28)))
                EndSearchPicker();

            ImGui.EndWindow();
        }

        // ── Slice 6c: clone / create-empty modals ───────────────────────
        //
        // BeginCloneLootTable / BeginCloneLootDrop open the same confirm modal
        // with different context. The modal fires the clone on Confirm and,
        // after the async DB write lands, records the follow-up buffered edit
        // (NPC repoint / LTE repoint). Success closes the modal + invalidates
        // the loot cache.
        void BeginCloneLootTable(int loottableId, string loottableName, int usageCount, int npcId)
        {
            _cloneConfirmActive              = true;
            _cloneConfirmKind                = "loottable";
            _cloneConfirmSourceId            = loottableId;
            _cloneConfirmSourceName          = loottableName ?? "?";
            _cloneConfirmSourceUsage         = usageCount;
            _cloneConfirmContextNpcId        = npcId;
            _cloneConfirmContextOldLoottableId = loottableId;
            _cloneConfirmContextLoottableId  = 0;
            _cloneTask                       = null;
            _cloneError                      = null;
        }

        void BeginCloneLootDrop(int lootdropId, string lootdropName, int usageCount, int owningLoottableId)
        {
            _cloneConfirmActive               = true;
            _cloneConfirmKind                 = "lootdrop";
            _cloneConfirmSourceId             = lootdropId;
            _cloneConfirmSourceName           = lootdropName ?? "?";
            _cloneConfirmSourceUsage          = usageCount;
            _cloneConfirmContextLoottableId   = owningLoottableId;
            _cloneConfirmContextNpcId         = 0;
            _cloneConfirmContextOldLoottableId = 0;
            _cloneTask                        = null;
            _cloneError                       = null;
        }

        void EndCloneConfirm()
        {
            _cloneConfirmActive = false;
            _cloneTask          = null;
        }

        void RenderCloneConfirmDialog(Gui gui)
        {
            const float dlgW = 500f;
            const float dlgH = 240f;
            var pos = new Vector2((gui.Dimensions.X - dlgW) / 2, (gui.Dimensions.Y - dlgH) / 2);

            ImGui.SetNextWindowPos(pos, Condition.Always, Vector2.Zero);
            ImGui.SetNextWindowSize(new Vector2(dlgW, dlgH), Condition.Always);

            const WindowFlags flags = WindowFlags.NoTitleBar | WindowFlags.NoMove
                                    | WindowFlags.NoResize   | WindowFlags.NoCollapse
                                    | WindowFlags.NoSavedSettings;

            ImGui.BeginWindow($"###{Id}CloneCfmDlg", flags);

            if (_cloneConfirmKind == "loottable")
            {
                ImGui.Text($"Clone loottable \"{_cloneConfirmSourceName}\" (#{_cloneConfirmSourceId})?");
                ImGui.Separator();
                ImGui.Text($"A copy of this loottable and all its lootdrops will be created.");
                ImGui.Text($"This NPC will be re-pointed to the new loottable (buffered — commit to save).");
                ImGui.Text($"The other {System.Math.Max(0, _cloneConfirmSourceUsage - 1)} NPC(s) sharing this loottable will continue to use the original.");
            }
            else
            {
                ImGui.Text($"Clone lootdrop \"{_cloneConfirmSourceName}\" (#{_cloneConfirmSourceId})?");
                ImGui.Separator();
                ImGui.Text($"A copy of this lootdrop (with all its items) will be created.");
                ImGui.Text($"This loottable's entry will be re-pointed to the clone immediately.");
                ImGui.Text($"The other {System.Math.Max(0, _cloneConfirmSourceUsage - 1)} loottable(s) sharing this lootdrop will continue to use the original.");
            }

            // Reap the in-flight task and apply the follow-up.
            if (_cloneTask != null && _cloneTask.IsCompleted)
            {
                if (_cloneTask.IsFaulted)
                {
                    _cloneError = _cloneTask.Exception?.GetBaseException().Message ?? "unknown error";
                    _cloneTask  = null;
                }
                else
                {
                    var newId = _cloneTask.Result;
                    _cloneTask = null;
                    ApplyCloneFollowUp(newId);
                    EndCloneConfirm();
                    ImGui.EndWindow();
                    return;
                }
            }

            if (_cloneError != null)
                ImGui.Text($"Error: {_cloneError}", new Vector4(0.95f, 0.35f, 0.25f, 1f));
            else if (_cloneTask != null)
                ImGui.Text("Cloning…");

            ImGui.Separator();
            var busy = _cloneTask != null;
            if (!busy && ImGui.Button($"Clone###{Id}cloneOk", new Vector2(120, 28)))
            {
                var factory = _view.Controller.DbFactory;
                if (factory == null)
                {
                    _cloneError = "No database connection is configured.";
                }
                else
                {
                    _cloneError = null;
                    var repo = new VisualEQ.Database.Repositories.LootRepository(factory);
                    var sourceId = _cloneConfirmSourceId;
                    _cloneTask = _cloneConfirmKind == "loottable"
                        ? System.Threading.Tasks.Task.Run(() => repo.CloneLootTableAsync(sourceId))
                        : System.Threading.Tasks.Task.Run(() => repo.CloneLootDropAsync(sourceId));
                }
            }
            ImGui.SameLine();
            if (ImGui.Button($"Cancel###{Id}cloneX", new Vector2(120, 28)))
                EndCloneConfirm();

            ImGui.EndWindow();
        }

        // Follow-up work after a clone's async DB write lands. For loottable
        // clone: fire a buffered NPC edit swapping loottable_id from old → new
        // (undoable). For lootdrop clone: run an immediate UPDATE on the
        // owning loottable's LTE row swapping its lootdrop_id (not buffered —
        // the DELETE-old + INSERT-new pattern isn't a good fit for an in-place
        // repoint since the composite PK would fight itself; a raw UPDATE
        // keeps roll params intact).
        void ApplyCloneFollowUp(int newId)
        {
            if (_cloneConfirmKind == "loottable")
            {
                // Buffered NPC edit: loottable_id = newId. Uses the same
                // pipeline as any other npc_types field edit; overlay merge
                // will make the widget see the new id, triggering a fresh
                // fetch of the clone's contents.
                var displayName = _displayedNpc?.Name ?? "?";
                RecordNpcFieldEdit(_cloneConfirmContextNpcId, "loottable_id",
                    _cloneConfirmContextOldLoottableId, newId, displayName);
            }
            else
            {
                // Immediate repoint of the owning loottable_entries row. Fire
                // and forget (any error surfaces as the next fetch's error).
                var factory = _view.Controller.DbFactory;
                if (factory != null)
                {
                    var repo = new VisualEQ.Database.Repositories.LootRepository(factory);
                    var lt = _cloneConfirmContextLoottableId;
                    var oldId = _cloneConfirmSourceId;
                    System.Threading.Tasks.Task.Run(async () =>
                        await repo.RepointLootTableEntryLootdropAsync(lt, oldId, newId));
                }
                // Force a re-fetch so the widget renders the swapped row.
                _lootFetchedForLoottableId = null;
                _lootData                  = null;
            }
        }

        // Name-entry modal for the "+ New empty loottable/lootdrop" flows. On
        // Confirm: async INSERT (returns new id) → follow-up wiring (buffered
        // NPC edit for loottable, buffered LTE insert for lootdrop) → close
        // + invalidate cache.
        void BeginCreateEmptyLootTable(int npcId, int currentLoottableId)
        {
            _createEmptyActive                  = true;
            _createEmptyKind                    = "loottable";
            _createEmptyContextNpcId            = npcId;
            _createEmptyContextOldLoottableId   = currentLoottableId;
            _createEmptyContextLoottableId      = 0;
            _createEmptyMinCash                 = 0;
            _createEmptyMaxCash                 = 0;
            _createEmptyAvgCoin                 = 0;
            _createEmptyTask                    = null;
            _createEmptyError                   = null;
            System.Array.Clear(_createEmptyNameBuf, 0, _createEmptyNameBuf.Length);
        }

        void BeginCreateEmptyLootDrop(int owningLoottableId)
        {
            _createEmptyActive                  = true;
            _createEmptyKind                    = "lootdrop";
            _createEmptyContextLoottableId      = owningLoottableId;
            _createEmptyContextNpcId            = 0;
            _createEmptyContextOldLoottableId   = 0;
            _createEmptyTask                    = null;
            _createEmptyError                   = null;
            System.Array.Clear(_createEmptyNameBuf, 0, _createEmptyNameBuf.Length);
        }

        void EndCreateEmpty()
        {
            _createEmptyActive = false;
            _createEmptyTask   = null;
        }

        void RenderCreateEmptyDialog(Gui gui)
        {
            const float dlgW = 460f;
            // Loottable modal grows to fit the extra cash inputs; lootdrop stays compact.
            float dlgH = _createEmptyKind == "loottable" ? 320f : 210f;
            var pos = new Vector2((gui.Dimensions.X - dlgW) / 2, (gui.Dimensions.Y - dlgH) / 2);

            ImGui.SetNextWindowPos(pos, Condition.Always, Vector2.Zero);
            ImGui.SetNextWindowSize(new Vector2(dlgW, dlgH), Condition.Always);

            const WindowFlags flags = WindowFlags.NoTitleBar | WindowFlags.NoMove
                                    | WindowFlags.NoResize   | WindowFlags.NoCollapse
                                    | WindowFlags.NoSavedSettings;

            ImGui.BeginWindow($"###{Id}CreateEmptyDlg", flags);

            ImGui.Text(_createEmptyKind == "loottable"
                ? "Create new empty loottable"
                : "Create new empty lootdrop");
            ImGui.Separator();

            ImGui.Text("Name:");
            ImGui.InputText($"###{Id}ceName", _createEmptyNameBuf, (uint)_createEmptyNameBuf.Length, InputTextFlags.Default, null);
            var name = ReadBuffer(_createEmptyNameBuf).Trim();

            // Loottable-specific: cash-range inputs. Skipped for lootdrops
            // (lootdrop table has no cash columns).
            if (_createEmptyKind == "loottable")
            {
                ImGui.Separator();
                ImGui.Text("Cash range:");
                NsimGui.CimguiRaw.igPushItemWidth(90f);
                ImGui.DragFloat($"min cash###{Id}ceMin", ref _createEmptyMinCash, 0f, 0f, 0f, "%.0f", 1f);
                NsimGui.CimguiRaw.igPopItemWidth();
                ImGui.SameLine();
                NsimGui.CimguiRaw.igPushItemWidth(90f);
                ImGui.DragFloat($"max cash###{Id}ceMax", ref _createEmptyMaxCash, 0f, 0f, 0f, "%.0f", 1f);
                NsimGui.CimguiRaw.igPopItemWidth();
                ImGui.SameLine();
                NsimGui.CimguiRaw.igPushItemWidth(90f);
                ImGui.DragFloat($"avg coin###{Id}ceAvg", ref _createEmptyAvgCoin, 0f, 0f, 0f, "%.0f", 1f);
                NsimGui.CimguiRaw.igPopItemWidth();
                ImGui.Separator();
                ImGui.Text("This NPC will be re-pointed to the new loottable (buffered — commit to save).");
            }
            else
                ImGui.Text("The new lootdrop will be added to this loottable (buffered — commit to save).");

            // Reap in-flight task.
            if (_createEmptyTask != null && _createEmptyTask.IsCompleted)
            {
                if (_createEmptyTask.IsFaulted)
                {
                    _createEmptyError = _createEmptyTask.Exception?.GetBaseException().Message ?? "unknown error";
                    _createEmptyTask  = null;
                }
                else
                {
                    var newId = _createEmptyTask.Result;
                    _createEmptyTask = null;
                    ApplyCreateEmptyFollowUp(newId, name);
                    EndCreateEmpty();
                    ImGui.EndWindow();
                    return;
                }
            }

            if (_createEmptyError != null)
                ImGui.Text($"Error: {_createEmptyError}", new Vector4(0.95f, 0.35f, 0.25f, 1f));
            else if (_createEmptyTask != null)
                ImGui.Text("Creating…");

            ImGui.Separator();
            var busy = _createEmptyTask != null;
            var canConfirm = !busy && !string.IsNullOrWhiteSpace(name);
            if (canConfirm)
            {
                if (ImGui.Button($"Create###{Id}ceOk", new Vector2(120, 28)))
                {
                    var factory = _view.Controller.DbFactory;
                    if (factory == null)
                    {
                        _createEmptyError = "No database connection is configured.";
                    }
                    else
                    {
                        _createEmptyError = null;
                        var repo = new VisualEQ.Database.Repositories.LootRepository(factory);
                        var capturedName = name;
                        var capturedMin  = (int)System.Math.Max(0, System.Math.Round(_createEmptyMinCash));
                        var capturedMax  = (int)System.Math.Max(0, System.Math.Round(_createEmptyMaxCash));
                        var capturedAvg  = (int)System.Math.Max(0, System.Math.Round(_createEmptyAvgCoin));
                        _createEmptyTask = _createEmptyKind == "loottable"
                            ? System.Threading.Tasks.Task.Run(() => repo.CreateEmptyLootTableAsync(capturedName, capturedMin, capturedMax, capturedAvg))
                            : System.Threading.Tasks.Task.Run(() => repo.CreateEmptyLootDropAsync(capturedName));
                    }
                }
            }
            else
            {
                ImGui.Text(busy ? "(waiting for DB…)" : "(name required)");
            }
            ImGui.SameLine();
            if (ImGui.Button($"Cancel###{Id}ceX", new Vector2(120, 28)))
                EndCreateEmpty();

            ImGui.EndWindow();
        }

        void ApplyCreateEmptyFollowUp(int newId, string name)
        {
            if (_createEmptyKind == "loottable")
            {
                var displayName = _displayedNpc?.Name ?? "?";
                RecordNpcFieldEdit(_createEmptyContextNpcId, "loottable_id",
                    _createEmptyContextOldLoottableId, newId, displayName);
            }
            else
            {
                // Buffered LTE insert linking the new lootdrop to the owning
                // loottable, defaults matching the "+ Add lootdrop" flow.
                var newSnap = new LootTableEntrySnapshot
                {
                    Multiplier  = 1,
                    DropLimit   = 1,
                    MinDrop     = 1,
                    Probability = 100f,
                };
                _view.Controller.RecordAction(new LootTableEntryEditAction(
                    _createEmptyContextLoottableId, newId, null, newSnap, name ?? "?"));
            }
        }

        // ── Slice 7a: duplicate NPC ─────────────────────────────────────
        //
        // Sequence:
        //   1. RequestDuplicateForSelectedSpawn — captures the owning spawn's
        //      spawngroupID and opens the confirm modal.
        //   2. Modal fires DuplicateAsync + RepointSpawnEntryAsync in one
        //      Task.Run chain so the two writes travel together.
        //   3. On completion, ApplyDuplicateFollowUp mutates the in-memory
        //      SpawnEntryWithNpc so the sidebar's "primary NPC" now resolves
        //      to the clone, and drops the NPC-details / usage caches so the
        //      next render fetches the fresh row.
        //
        // Immediate DB writes (not buffered) — same rationale as the loot
        // clone flows. Undo isn't wired for this action (users can delete the
        // clone manually if they change their mind).
        void RequestDuplicateForSelectedSpawn(int currentNpcId, string npcName)
        {
            var sp = _view.SelectedSpawn;
            if (sp?.Record?.Spawn == null) return;
            BeginDuplicateConfirm(currentNpcId, npcName, sp.Record.Spawn.SpawnGroupId, _npcUsageCount);
        }

        void BeginDuplicateConfirm(int npcId, string name, int spawnGroupId, int usage)
        {
            _duplicateConfirmActive       = true;
            _duplicateConfirmSourceNpcId  = npcId;
            _duplicateConfirmSourceName   = name ?? "?";
            _duplicateConfirmSpawnGroupId = spawnGroupId;
            _duplicateConfirmUsage        = usage;
            _duplicateTask                = null;
            _duplicateError               = null;
        }

        void EndDuplicateConfirm()
        {
            _duplicateConfirmActive = false;
            _duplicateTask          = null;
        }

        void RenderDuplicateConfirmDialog(Gui gui)
        {
            const float dlgW = 520f;
            const float dlgH = 260f;
            var pos = new Vector2((gui.Dimensions.X - dlgW) / 2, (gui.Dimensions.Y - dlgH) / 2);

            ImGui.SetNextWindowPos(pos, Condition.Always, Vector2.Zero);
            ImGui.SetNextWindowSize(new Vector2(dlgW, dlgH), Condition.Always);

            const WindowFlags flags = WindowFlags.NoTitleBar | WindowFlags.NoMove
                                    | WindowFlags.NoResize   | WindowFlags.NoCollapse
                                    | WindowFlags.NoSavedSettings;

            ImGui.BeginWindow($"###{Id}DupCfmDlg", flags);

            ImGui.Text($"Duplicate NPC \"{_duplicateConfirmSourceName}\" (#{_duplicateConfirmSourceNpcId})?");
            ImGui.Separator();
            ImGui.Text($"A copy of this npc_types row will be created (name suffixed with \" (clone)\").");
            ImGui.Text($"This spawn (spawngroup #{_duplicateConfirmSpawnGroupId}) will point at the clone.");
            var others = System.Math.Max(0, _duplicateConfirmUsage - 1);
            if (others > 0)
            {
                ImGui.Text($"The other {others} spawn entr{(others == 1 ? "y" : "ies")} using the source keep pointing at it.");
            }
            else
            {
                // usage <= 1 means this spawn is the only user — duplicating
                // will leave the source npc_types row unreferenced. Not broken
                // (unreferenced rows cost nothing), just wasteful. Users may
                // still want this as a "clone as template" flow.
                ImGui.Text("Note: this NPC has no other spawn entries — the source row will be left orphaned.",
                    new Vector4(0.95f, 0.75f, 0.25f, 1f));
            }

            // Reap async result. Task returns the new npc id after BOTH
            // DuplicateAsync and RepointSpawnEntryAsync have committed.
            if (_duplicateTask != null && _duplicateTask.IsCompleted)
            {
                if (_duplicateTask.IsFaulted)
                {
                    _duplicateError = _duplicateTask.Exception?.GetBaseException().Message ?? "unknown error";
                    _duplicateTask  = null;
                }
                else
                {
                    var newId = _duplicateTask.Result;
                    _duplicateTask = null;
                    ApplyDuplicateFollowUp(newId);
                    EndDuplicateConfirm();
                    ImGui.EndWindow();
                    return;
                }
            }

            if (_duplicateError != null)
                ImGui.Text($"Error: {_duplicateError}", new Vector4(0.95f, 0.35f, 0.25f, 1f));
            else if (_duplicateTask != null)
                ImGui.Text("Duplicating…");

            ImGui.Separator();
            var busy = _duplicateTask != null;
            if (!busy && ImGui.Button($"Duplicate###{Id}dupOk", new Vector2(140, 28)))
            {
                var factory = _view.Controller.DbFactory;
                if (factory == null)
                {
                    _duplicateError = "No database connection is configured.";
                }
                else
                {
                    _duplicateError = null;
                    var repo = new VisualEQ.Database.Repositories.NpcRepository(factory);
                    var sourceId = _duplicateConfirmSourceNpcId;
                    var sgId     = _duplicateConfirmSpawnGroupId;
                    _duplicateTask = System.Threading.Tasks.Task.Run(async () =>
                    {
                        var newId = await repo.DuplicateAsync(sourceId);
                        await repo.RepointSpawnEntryAsync(sgId, sourceId, newId);
                        return newId;
                    });
                }
            }
            ImGui.SameLine();
            if (ImGui.Button($"Cancel###{Id}dupX", new Vector2(140, 28)))
                EndDuplicateConfirm();

            ImGui.EndWindow();
        }

        // Point the in-memory SpawnRecord at the freshly-cloned npc_types row
        // and drop the sidebar's NPC caches so the next render fetches the
        // clone (with " (clone)" name suffix). Only the primary entry's NpcId
        // needs updating — the source-npc's other spawnentries in this
        // spawngroup (if any) still reference the original.
        void ApplyDuplicateFollowUp(int newNpcId)
        {
            var sp = _view.SelectedSpawn;
            if (sp == null) return;

            foreach (var e in sp.Record.Entries)
            {
                if (e.Entry != null && e.Entry.NpcId == _duplicateConfirmSourceNpcId)
                {
                    e.Entry.NpcId = newNpcId;
                    if (e.Npc != null) e.Npc.Id = newNpcId;
                }
            }

            // Drop caches so the sidebar re-fetches the clone. Prefer nulling
            // _displayedNpc + _npcDetailsData rather than just clearing the
            // "fetched-for" id — otherwise the render would flash "NPC row
            // not found" for a frame (id-mismatch check) before the fetch
            // lands. Nulling shows "Loading NPC details…" instead.
            _npcDetailsFetchedForId = null;
            _npcDetailsData         = null;
            _displayedNpc           = null;
            _npcUsageFetchedForId   = null;
            _npcUsageCount          = 0;
        }

        // ── Slice 7b: Manage Faction Sets pop-out browser ────────────────
        //
        // Filterable list of npc_faction rows sourced from ReferenceDataCache
        // (preloaded, 20k rows). Row actions: "Assign to current NPC" fires a
        // buffered NPC edit for npc_faction_id, exactly like an FK picker
        // selection would. An inline "+ New faction set" form creates a fresh
        // npc_faction row (name + optional primaryfaction + ignore flag),
        // then routes the assign through the same buffered path.
        //
        // Coexists with the FK picker: opening the primaryfaction picker
        // inside the create form temporarily hides this window (modal
        // dispatch order — FK picker is earlier). Control returns after Select
        // or Cancel.
        void BeginManageFactionSets(int npcId, int currentNpcFactionId)
        {
            _factionMgrActive                 = true;
            _factionMgrTargetNpcId            = npcId;
            _factionMgrTargetOldNpcFactionId  = currentNpcFactionId;
            _factionMgrTargetNpcName          = _displayedNpc?.Name ?? "?";
            _factionMgrSelectedIdx            = -1;
            _factionMgrCreateFormOpen         = false;
            _factionMgrCreatePrimaryFaction   = 0;
            _factionMgrCreateIgnoreAssist     = false;
            _factionMgrCreateTask             = null;
            _factionMgrCreateError            = null;
            System.Array.Clear(_factionMgrFilterBuf,     0, _factionMgrFilterBuf.Length);
            System.Array.Clear(_factionMgrCreateNameBuf, 0, _factionMgrCreateNameBuf.Length);
        }

        void EndManageFactionSets()
        {
            _factionMgrActive = false;
            _factionMgrCreateTask = null;
        }

        void RenderManageFactionSetsDialog(Gui gui)
        {
            const float dlgW = 560f;
            float dlgH = _factionMgrCreateFormOpen ? 620f : 500f;
            var pos = new Vector2((gui.Dimensions.X - dlgW) / 2, (gui.Dimensions.Y - dlgH) / 2);

            ImGui.SetNextWindowPos(pos, Condition.Always, Vector2.Zero);
            ImGui.SetNextWindowSize(new Vector2(dlgW, dlgH), Condition.Always);

            const WindowFlags flags = WindowFlags.NoTitleBar | WindowFlags.NoMove
                                    | WindowFlags.NoResize   | WindowFlags.NoCollapse
                                    | WindowFlags.NoSavedSettings;

            ImGui.BeginWindow($"###{Id}FacMgrDlg", flags);

            ImGui.Text($"Manage Faction Sets (target: '{_factionMgrTargetNpcName}')");
            ImGui.Separator();

            var cache = _view.Controller.ReferenceData;
            if (cache == null)
            {
                ImGui.Text("No database connection is configured.", new Vector4(0.95f, 0.35f, 0.25f, 1f));
                if (ImGui.Button($"Close###{Id}fmClose", new Vector2(120, 28)))
                    EndManageFactionSets();
                ImGui.EndWindow();
                return;
            }

            var state = cache.GetState(VisualEQ.SpawnSystem.ReferenceDataCache.Table.NpcFaction);
            if (state == VisualEQ.SpawnSystem.ReferenceDataCache.LoadState.NotLoaded ||
                state == VisualEQ.SpawnSystem.ReferenceDataCache.LoadState.Loading)
            {
                cache.GetItems(VisualEQ.SpawnSystem.ReferenceDataCache.Table.NpcFaction); // force-warm
                ImGui.Text("Loading faction sets…");
                if (ImGui.Button($"Close###{Id}fmClose", new Vector2(120, 28)))
                    EndManageFactionSets();
                ImGui.EndWindow();
                return;
            }

            var items = cache.GetItems(VisualEQ.SpawnSystem.ReferenceDataCache.Table.NpcFaction);

            ImGui.Text("Filter (id or name substring):");
            ImGui.InputText($"###{Id}fmF", _factionMgrFilterBuf, (uint)_factionMgrFilterBuf.Length, InputTextFlags.Default, null);
            var filter = ReadBuffer(_factionMgrFilterBuf).Trim();

            var filtered = new List<VisualEQ.Database.Models.ReferenceItem>(256);
            if (string.IsNullOrEmpty(filter))
            {
                for (int i = 0; i < items.Count && filtered.Count < 250; i++)
                    filtered.Add(items[i]);
            }
            else
            {
                int filterId;
                bool filterIsInt = int.TryParse(filter, out filterId);
                foreach (var it in items)
                {
                    if (filterIsInt && it.Id == filterId) { filtered.Add(it); continue; }
                    if (!string.IsNullOrEmpty(it.Name) &&
                        it.Name.IndexOf(filter, System.StringComparison.OrdinalIgnoreCase) >= 0)
                        filtered.Add(it);
                    if (filtered.Count >= 500) break;
                }
            }

            ImGui.Text($"{filtered.Count} match(es) shown");
            ImGui.BeginChild($"###{Id}fmList", new Vector2(0, _factionMgrCreateFormOpen ? 220 : 320), true, WindowFlags.Default);
            for (int i = 0; i < filtered.Count; i++)
            {
                var it = filtered[i];
                var lbl = $"{it.Id}  {it.Name}###{Id}fmR{it.Id}";
                if (ImGui.Selectable(lbl, i == _factionMgrSelectedIdx))
                    _factionMgrSelectedIdx = i;
            }
            ImGui.EndChild();

            ImGui.Separator();

            // Assign action for the currently-highlighted row.
            var canAssign = _factionMgrSelectedIdx >= 0 && _factionMgrSelectedIdx < filtered.Count;
            if (canAssign)
            {
                var picked = filtered[_factionMgrSelectedIdx];
                if (ImGui.Button($"Assign #{picked.Id} to this NPC###{Id}fmOk", new Vector2(220, 28)))
                {
                    if (picked.Id != _factionMgrTargetOldNpcFactionId)
                    {
                        RecordNpcFieldEdit(_factionMgrTargetNpcId, "npc_faction_id",
                            _factionMgrTargetOldNpcFactionId, picked.Id, _factionMgrTargetNpcName);
                    }
                    EndManageFactionSets();
                    ImGui.EndWindow();
                    return;
                }
            }
            else
            {
                ImGui.Text("(pick a row to enable Assign)");
            }
            ImGui.SameLine();
            if (ImGui.Button($"Close###{Id}fmX", new Vector2(120, 28)))
            {
                EndManageFactionSets();
                ImGui.EndWindow();
                return;
            }

            ImGui.Separator();

            // Create sub-form. Collapsed by default; opens inline within the
            // same window rather than as a nested modal.
            if (!_factionMgrCreateFormOpen)
            {
                if (ImGui.Button($"+ New faction set…###{Id}fmNewOpen", new Vector2(180, 24)))
                    _factionMgrCreateFormOpen = true;
            }
            else
            {
                ImGui.Text("Create new faction set:");
                ImGui.Text("Name:");
                ImGui.InputText($"###{Id}fmNewName", _factionMgrCreateNameBuf, (uint)_factionMgrCreateNameBuf.Length, InputTextFlags.Default, null);
                var newName = ReadBuffer(_factionMgrCreateNameBuf).Trim();

                // Primary faction: read-only label + Change button that opens
                // the FactionList FK picker via a callback.
                var pfLabel = _factionMgrCreatePrimaryFaction == 0
                    ? "(none)"
                    : (cache.ResolveLabel(VisualEQ.SpawnSystem.ReferenceDataCache.Table.FactionList, _factionMgrCreatePrimaryFaction));
                ImGui.Text($"Primary faction: {pfLabel}");
                ImGui.SameLine();
                if (ImGui.Button($"Change###{Id}fmNewPfBtn", new Vector2(80, 22)))
                {
                    var currentPf = _factionMgrCreatePrimaryFaction;
                    BeginFkPicker(
                        VisualEQ.SpawnSystem.ReferenceDataCache.Table.FactionList,
                        "primaryfaction_new", "Primary faction (new set)",
                        _factionMgrTargetNpcId, currentPf,
                        onPicked: pickedFactionId => { _factionMgrCreatePrimaryFaction = pickedFactionId; });
                }
                if (_factionMgrCreatePrimaryFaction != 0)
                {
                    ImGui.SameLine();
                    if (ImGui.Button($"Clear###{Id}fmNewPfClr", new Vector2(60, 22)))
                        _factionMgrCreatePrimaryFaction = 0;
                }

                ImGui.Checkbox($"Ignore primary assist###{Id}fmNewIpa", ref _factionMgrCreateIgnoreAssist);

                // Reap the create task on completion.
                if (_factionMgrCreateTask != null && _factionMgrCreateTask.IsCompleted)
                {
                    if (_factionMgrCreateTask.IsFaulted)
                    {
                        _factionMgrCreateError = _factionMgrCreateTask.Exception?.GetBaseException().Message ?? "unknown error";
                        _factionMgrCreateTask  = null;
                    }
                    else
                    {
                        var newId = _factionMgrCreateTask.Result;
                        _factionMgrCreateTask = null;
                        // Buffered assign to current NPC. Reload of the cache
                        // isn't strictly needed — the sidebar just uses the id;
                        // but for accuracy, drop the NpcFaction cache so the
                        // row shows in the next open of this window.
                        RecordNpcFieldEdit(_factionMgrTargetNpcId, "npc_faction_id",
                            _factionMgrTargetOldNpcFactionId, newId, _factionMgrTargetNpcName);
                        EndManageFactionSets();
                        ImGui.EndWindow();
                        return;
                    }
                }

                if (_factionMgrCreateError != null)
                    ImGui.Text($"Error: {_factionMgrCreateError}", new Vector4(0.95f, 0.35f, 0.25f, 1f));
                else if (_factionMgrCreateTask != null)
                    ImGui.Text("Creating…");

                var busy = _factionMgrCreateTask != null;
                var canCreate = !busy && !string.IsNullOrWhiteSpace(newName);
                if (canCreate && ImGui.Button($"Create + assign###{Id}fmNewOk", new Vector2(180, 24)))
                {
                    var factory = _view.Controller.DbFactory;
                    if (factory == null)
                    {
                        _factionMgrCreateError = "No database connection is configured.";
                    }
                    else
                    {
                        _factionMgrCreateError = null;
                        var repo = new VisualEQ.Database.Repositories.FactionRepository(factory);
                        var capturedName    = newName;
                        var capturedPf      = _factionMgrCreatePrimaryFaction;
                        var capturedIgnore  = _factionMgrCreateIgnoreAssist ? 1 : 0;
                        _factionMgrCreateTask = System.Threading.Tasks.Task.Run(async () =>
                            await repo.CreateEmptyNpcFactionAsync(capturedName, capturedPf, capturedIgnore));
                    }
                }
                else if (!canCreate)
                {
                    ImGui.Text(busy ? "(waiting for DB…)" : "(name required)");
                }
                ImGui.SameLine();
                if (ImGui.Button($"Cancel new###{Id}fmNewX", new Vector2(120, 24)))
                    _factionMgrCreateFormOpen = false;
            }

            ImGui.EndWindow();
        }

        void RenderFkPickerDialog(Gui gui)
        {
            const float dlgW = 520f;
            const float dlgH = 460f;
            var pos = new Vector2((gui.Dimensions.X - dlgW) / 2, (gui.Dimensions.Y - dlgH) / 2);

            ImGui.SetNextWindowPos(pos, Condition.Always, Vector2.Zero);
            ImGui.SetNextWindowSize(new Vector2(dlgW, dlgH), Condition.Always);

            const WindowFlags flags = WindowFlags.NoTitleBar | WindowFlags.NoMove
                                    | WindowFlags.NoResize   | WindowFlags.NoCollapse
                                    | WindowFlags.NoSavedSettings;

            ImGui.BeginWindow($"###{Id}FkPickerDlg", flags);

            var cache = _view.Controller.ReferenceData;
            ImGui.Text($"Pick {_fkPickerLabel} for '{_fkPickerNpcDisplayName}'");
            ImGui.Text($"Current: {(cache != null ? cache.ResolveLabel(_fkPickerTable, _fkPickerCurrentValue) : _fkPickerCurrentValue.ToString())}");
            ImGui.Separator();

            var state = cache?.GetState(_fkPickerTable) ?? VisualEQ.SpawnSystem.ReferenceDataCache.LoadState.NotLoaded;
            if (cache == null)
            {
                ImGui.Text("No database connection is configured.", new Vector4(0.95f, 0.35f, 0.25f, 1f));
                if (ImGui.Button($"Close###{Id}fkClose", new Vector2(120, 28)))
                    EndFkPicker();
                ImGui.EndWindow();
                return;
            }
            if (state == VisualEQ.SpawnSystem.ReferenceDataCache.LoadState.Loading ||
                state == VisualEQ.SpawnSystem.ReferenceDataCache.LoadState.NotLoaded)
            {
                ImGui.Text("Loading reference data…");
                if (ImGui.Button($"Close###{Id}fkClose", new Vector2(120, 28)))
                    EndFkPicker();
                ImGui.EndWindow();
                return;
            }
            if (state == VisualEQ.SpawnSystem.ReferenceDataCache.LoadState.Error)
            {
                ImGui.Text("Failed to load reference data.", new Vector4(0.95f, 0.35f, 0.25f, 1f));
                if (ImGui.Button($"Close###{Id}fkClose", new Vector2(120, 28)))
                    EndFkPicker();
                ImGui.EndWindow();
                return;
            }

            var items = cache.GetItems(_fkPickerTable);
            ImGui.Text("Filter (id or name substring):");
            ImGui.InputText($"###{Id}fkF", _fkPickerFilterBuf, (uint)_fkPickerFilterBuf.Length, InputTextFlags.Default, null);
            var filter = ReadBuffer(_fkPickerFilterBuf).Trim();

            var filtered = new List<VisualEQ.Database.Models.ReferenceItem>(256);
            if (string.IsNullOrEmpty(filter))
            {
                // Empty filter: show first 250 items unfiltered so the list isn't overwhelming
                // (loottable has 15k rows). Users typing a substring get an unbounded but
                // usually-small result set.
                for (int i = 0; i < items.Count && filtered.Count < 250; i++)
                    filtered.Add(items[i]);
            }
            else
            {
                int filterId;
                bool filterIsInt = int.TryParse(filter, out filterId);
                foreach (var it in items)
                {
                    if (filterIsInt && it.Id == filterId) { filtered.Add(it); continue; }
                    if (!string.IsNullOrEmpty(it.Name) &&
                        it.Name.IndexOf(filter, System.StringComparison.OrdinalIgnoreCase) >= 0)
                        filtered.Add(it);
                    if (filtered.Count >= 500) break;
                }
            }

            ImGui.Text($"{filtered.Count} match(es) shown");
            ImGui.BeginChild($"###{Id}fkList", new Vector2(0, 260), true, WindowFlags.Default);
            for (int i = 0; i < filtered.Count; i++)
            {
                var it = filtered[i];
                var label = $"{it.Id}  {it.Name}###{Id}fkR{it.Id}";
                if (ImGui.Selectable(label, i == _fkPickerSelectedIdx))
                    _fkPickerSelectedIdx = i;
            }
            ImGui.EndChild();

            ImGui.Separator();
            var pickEnabled = _fkPickerSelectedIdx >= 0 && _fkPickerSelectedIdx < filtered.Count;
            if (pickEnabled)
            {
                if (ImGui.Button($"Select###{Id}fkOk", new Vector2(140, 28)))
                {
                    var picked = filtered[_fkPickerSelectedIdx];
                    // Callback owns what to do with the pick (faction-entry-add path).
                    // Default: write an NpcFieldEditAction to the field the picker
                    // was opened for (loottable_id / npc_faction_id / etc).
                    if (_fkPickerOnPicked != null)
                    {
                        _fkPickerOnPicked(picked.Id);
                    }
                    else if (picked.Id != _fkPickerCurrentValue)
                    {
                        RecordNpcFieldEdit(
                            _fkPickerNpcId, _fkPickerFieldName,
                            _fkPickerCurrentValue, picked.Id,
                            _fkPickerNpcDisplayName);
                    }
                    EndFkPicker();
                }
            }
            else
            {
                ImGui.Text("(pick a row to enable Select)");
            }
            ImGui.SameLine();
            if (ImGui.Button($"Cancel###{Id}fkX", new Vector2(140, 28)))
                EndFkPicker();

            ImGui.EndWindow();
        }

        // ───────── NPC Details body (editable) ────────────────────────

        // Preset value/label pairs for enum-combo widgets. Kept as static readonly arrays
        // so the combo doesn't reallocate every frame. Order = display order in the combo.
        static readonly int[]    _npcGenderVals   = { 0, 1, 2 };
        static readonly string[] _npcGenderLabels = { "Male", "Female", "Neuter" };

        static readonly int[]    _npcStuckVals   = { 0, 1, 2, 3 };
        static readonly string[] _npcStuckLabels = { "Run to target", "Warp to target", "Take no action", "Evade combat" };

        static readonly int[]    _npcFlymodeVals   = { -1, 0, 1, 2, 3, 4, 5 };
        static readonly string[] _npcFlymodeLabels = { "Default (race)", "Grounded", "Flying", "Levitating", "Water", "Floating (no gravity)", "Levitating over water" };

        static readonly int[]    _npcMeleeVals   = { 0, 1, 2, 3, 7, 8, 10, 21, 23, 26, 28, 30, 36, 38, 45, 51 };
        static readonly string[] _npcMeleeLabels = { "1H Blunt", "1H Slashing", "2H Blunt", "2H Slashing", "Archery", "Backstab", "Bash", "Dragon Punch", "Eagle Strike", "Flying Kick", "Hand to Hand", "Kick", "1H Piercing", "Round Kick", "2H Piercing", "Throwing" };

        // Race / Class / BodyType combo options. Built once at class-init from the
        // SpawnInfoLookups dicts, ordered by numeric id (server-side convention — race 1
        // = Human, class 1 = Warrior, etc., so scanning by id is what OPs already know).
        // Labels are formatted "Name (id)" so both are visible in the dropdown.
        static readonly int[]    _npcRaceVals;
        static readonly string[] _npcRaceLabels;
        static readonly int[]    _npcClassVals;
        static readonly string[] _npcClassLabels;
        static readonly int[]    _npcBodyTypeVals;
        static readonly string[] _npcBodyTypeLabels;

        static SidebarWidget()
        {
            BuildEnumComboOptions(SpawnInfoLookups.AllRaces,     out _npcRaceVals,     out _npcRaceLabels);
            BuildEnumComboOptions(SpawnInfoLookups.AllClasses,   out _npcClassVals,   out _npcClassLabels);
            BuildEnumComboOptions(SpawnInfoLookups.AllBodyTypes, out _npcBodyTypeVals, out _npcBodyTypeLabels);
        }

        static void BuildEnumComboOptions(System.Collections.Generic.IReadOnlyDictionary<int, string> src,
            out int[] values, out string[] labels)
        {
            var pairs = src.Select(kv => new { Id = kv.Key, Label = kv.Value })
                           .OrderBy(p => p.Id)
                           .ToArray();
            values = pairs.Select(p => p.Id).ToArray();
            labels = pairs.Select(p => $"{p.Label} ({p.Id})").ToArray();
        }

        void RenderNpcDetailsBody(VisualEQ.Database.Models.NpcTypeFull n, bool editable)
        {
            var npcId = n.Id;

            // ── Identity ───────────────────────────────────────────
            ImGui.Text($"[id {n.Id}]");

            // Slice 7a — shared-record indicator + Ctrl+D duplicate. Usage > 1
            // means edits here silently affect every other spawn using this
            // npc_types row. The Duplicate button clones the row and repoints
            // THIS spawn's spawnentry so subsequent edits scope to it alone.
            var usageReady = _npcUsageFetchedForId == npcId;
            if (usageReady)
            {
                if (_npcUsageCount > 1)
                    ImGui.Text($"⚠ Used by {_npcUsageCount} spawn entr{(_npcUsageCount == 1 ? "y" : "ies")} — edits affect them all.",
                        new Vector4(0.95f, 0.75f, 0.25f, 1f));
                else
                    ImGui.Text($"Used by {_npcUsageCount} spawn entr{(_npcUsageCount == 1 ? "y" : "ies")}.");
            }
            else
            {
                ImGui.Text("Usage: (loading…)");
            }

            if (editable)
            {
                // Button-only (no hotkey): Ctrl+D is already bound to spawn2
                // row duplication in EngineCore. Two separate operations —
                // this one clones the npc_types row + re-points THIS spawn's
                // spawnentry so subsequent edits scope to it alone.
                if (ImGui.Button($"Duplicate NPC (for this spawn)###{Id}ndDup{npcId}", new Vector2(240, 22)))
                    RequestDuplicateForSelectedSpawn(npcId, n.Name);
            }

            NpcText(npcId, "name", "Name", _npcNameBuf, () => n.Name, v => n.Name = v, editable);
            NpcText(npcId, "lastname", "Last name", _npcLastNameBuf, () => n.LastName, v => n.LastName = v, editable);
            NpcInt(npcId, "level", "Level", () => n.Level, v => n.Level = v, editable, 1, 127);
            NpcEnumCombo(npcId, "race",     "Race",      () => n.Race,     v => n.Race     = v, _npcRaceVals,     _npcRaceLabels,     editable);
            NpcEnumCombo(npcId, "class",    "Class",     () => n.Class,    v => n.Class    = v, _npcClassVals,    _npcClassLabels,    editable);
            NpcEnumCombo(npcId, "bodytype", "Body type", () => n.BodyType, v => n.BodyType = v, _npcBodyTypeVals, _npcBodyTypeLabels, editable);
            NpcEnumCombo(npcId, "gender", "Gender", () => n.Gender, v => n.Gender = v,
                _npcGenderVals, _npcGenderLabels, editable);
            NpcFloat(npcId, "size", "Size", () => n.Size, v => n.Size = v, editable);

            // Sub-sections wrapped in CollapsingHeader so the sidebar's scrollable
            // content stays under ImGui.NET 0.4.6's ~one-page scroll cap. Defaults
            // biased toward the fields OPs edit most (Combat, Visual, Special abilities);
            // the noisier full-schema sections start collapsed.

            // ── Combat ─────────────────────────────────────────────
            // 3-column grid @ 115px cell pitch (fits 380px default sidebar); labels
            // shortened to survive tight cells ("Min damage"→"Min dmg", "Attack
            // delay"→"Atk delay", "HP regen (per tick)"→"HP/tick", etc.). Field
            // IDs unchanged so undo history + activation slots stay consistent.
            if (ImGui.CollapsingHeader($"Combat###{Id}ndCombat", 0))
            {
            const float CombatCell  = 115f;
            const float CombatInput = 48f;
            NpcLongInline(npcId, "hp",   "HP",   () => n.Hp,   v => n.Hp   = v, editable, CombatInput);
            ImGui.SameLine(CombatCell);
            NpcLongInline(npcId, "mana", "Mana", () => n.Mana, v => n.Mana = v, editable, CombatInput);
            ImGui.SameLine(CombatCell * 2);
            NpcIntInline (npcId, "AC",   "AC",   () => n.Ac,   v => n.Ac   = v, editable, CombatInput);

            NpcIntInline(npcId, "ATK",  "ATK",   () => n.Atk,       v => n.Atk       = v, editable, CombatInput);
            ImGui.SameLine(CombatCell);
            NpcIntInline(npcId, "Accuracy",  "Accur", () => n.Accuracy,  v => n.Accuracy  = v, editable, CombatInput);
            ImGui.SameLine(CombatCell * 2);
            NpcIntInline(npcId, "Avoidance", "Avoid", () => n.Avoidance, v => n.Avoidance = v, editable, CombatInput);

            NpcIntInline(npcId, "mindmg", "Min dmg", () => n.MinDmg, v => n.MinDmg = v, editable, CombatInput, 0);
            ImGui.SameLine(CombatCell);
            NpcIntInline(npcId, "maxdmg", "Max dmg", () => n.MaxDmg, v => n.MaxDmg = v, editable, CombatInput, 0);
            ImGui.SameLine(CombatCell * 2);
            NpcIntInline(npcId, "attack_delay", "Atk dly", () => n.AttackDelay, v => n.AttackDelay = v, editable, CombatInput, 0);

            NpcIntInline  (npcId, "attack_count", "Atk cnt", () => n.AttackCount, v => n.AttackCount = v, editable, CombatInput);
            ImGui.SameLine(CombatCell);
            NpcFloatInline(npcId, "attack_speed", "Atk spd", () => n.AttackSpeed, v => n.AttackSpeed = v, editable, CombatInput);
            ImGui.SameLine(CombatCell * 2);
            NpcIntInline  (npcId, "slow_mitigation", "Slow mit", () => n.SlowMitigation, v => n.SlowMitigation = v, editable, CombatInput);

            NpcIntInline (npcId, "heroic_strikethrough", "Heroic ST", () => n.HeroicStrikethrough, v => n.HeroicStrikethrough = v, editable, CombatInput);
            ImGui.SameLine(CombatCell);
            NpcLongInline(npcId, "hp_regen_rate",        "HP/tick",   () => n.HpRegenRate,         v => n.HpRegenRate         = v, editable, CombatInput);
            ImGui.SameLine(CombatCell * 2);
            NpcLongInline(npcId, "hp_regen_per_second",  "HP/sec",    () => n.HpRegenPerSecond,    v => n.HpRegenPerSecond    = v, editable, CombatInput);

            NpcLongInline(npcId, "mana_regen_rate",      "Mana/tick", () => n.ManaRegenRate,       v => n.ManaRegenRate       = v, editable, CombatInput);
            }

            // ── Attributes (Stats + Resistances) ───────────────────
            // 4-column grid @ 88px cell pitch. Merged from two separate headers
            // (Stats + Resistances) since both are short-label int fields with
            // identical widget shape — the merge shaves a whole click and heading
            // line. DefaultOpen after compaction: 14 fields in 4 rows is cheap
            // to show. Field IDs unchanged so undo history keeps working.
            if (ImGui.CollapsingHeader($"Attributes###{Id}ndStats", 0))
            {
            const float AttrCell  = 88f;
            const float AttrInput = 40f;
            NpcIntInline(npcId, "STR",  "STR", () => n.Str,  v => n.Str  = v, editable, AttrInput, 0);
            ImGui.SameLine(AttrCell);
            NpcIntInline(npcId, "STA",  "STA", () => n.Sta,  v => n.Sta  = v, editable, AttrInput, 0);
            ImGui.SameLine(AttrCell * 2);
            NpcIntInline(npcId, "DEX",  "DEX", () => n.Dex,  v => n.Dex  = v, editable, AttrInput, 0);
            ImGui.SameLine(AttrCell * 3);
            NpcIntInline(npcId, "AGI",  "AGI", () => n.Agi,  v => n.Agi  = v, editable, AttrInput, 0);

            NpcIntInline(npcId, "_INT", "INT", () => n.Int_, v => n.Int_ = v, editable, AttrInput, 0);
            ImGui.SameLine(AttrCell);
            NpcIntInline(npcId, "WIS",  "WIS", () => n.Wis,  v => n.Wis  = v, editable, AttrInput, 0);
            ImGui.SameLine(AttrCell * 2);
            NpcIntInline(npcId, "CHA",  "CHA", () => n.Cha,  v => n.Cha  = v, editable, AttrInput, 0);

            NpcIntInline(npcId, "MR", "MR", () => n.MR, v => n.MR = v, editable, AttrInput);
            ImGui.SameLine(AttrCell);
            NpcIntInline(npcId, "CR", "CR", () => n.CR, v => n.CR = v, editable, AttrInput);
            ImGui.SameLine(AttrCell * 2);
            NpcIntInline(npcId, "DR", "DR", () => n.DR, v => n.DR = v, editable, AttrInput);
            ImGui.SameLine(AttrCell * 3);
            NpcIntInline(npcId, "FR", "FR", () => n.FR, v => n.FR = v, editable, AttrInput);

            NpcIntInline(npcId, "PR", "PR", () => n.PR, v => n.PR = v, editable, AttrInput);
            ImGui.SameLine(AttrCell);
            NpcIntInline(npcId, "Corrup", "Corr", () => n.Corrup, v => n.Corrup = v, editable, AttrInput);
            ImGui.SameLine(AttrCell * 2);
            NpcIntInline(npcId, "PhR",    "PhR",  () => n.PhR,    v => n.PhR    = v, editable, AttrInput, 0);
            }

            // ── Visual ─────────────────────────────────────────────
            // Texture / helm / face are the "live preview" trio — editing any of them
            // triggers Controller.RefreshNpcVisualForNpc which cache-swaps the AniModel
            // on every scene instance backed by this npc_types row. Focusing any of
            // these fields also auto-frames the camera (see HandleNpcActivation).
            if (ImGui.CollapsingHeader($"Visual###{Id}ndVis", 0))
            {
            const float VisualCell  = 120f;
            const float VisualInput = 44f;
            NpcIntInline(npcId, "texture",     "Body", () => n.Texture,     v => n.Texture     = v, editable, VisualInput, 0, 15);
            ImGui.SameLine(VisualCell);
            NpcIntInline(npcId, "helmtexture", "Helm", () => n.HelmTexture, v => n.HelmTexture = v, editable, VisualInput, 0, 15);
            ImGui.SameLine(VisualCell * 2);
            NpcIntInline(npcId, "face",        "Face", () => n.Face,        v => n.Face        = v, editable, VisualInput, 0, 15);

            // Cosmetic / luclin / drakkin fields — not wired into the live-preview
            // refresh (RaceModelMapper doesn't consult them for the Trilogy client this
            // editor targets). Nested behind a TreeNode so they don't dominate the
            // panel when unused; opens with a click when a Luclin-era fork needs
            // them for reference. TreeNode not nested CollapsingHeader — the latter
            // renders poorly nested in ImGui.NET 0.4.6.
            if (ImGui.TreeNode($"Cosmetic / Luclin / Drakkin (read-only)###{Id}ndVisMore"))
            {
                ImGui.Text($"Extra: arm {n.ArmTexture}  bracer {n.BracerTexture}  hand {n.HandTexture}  leg {n.LegTexture}  feet {n.FeetTexture}");
                ImGui.Text($"Weapons: d_melee1 {n.DMeleeTexture1}   d_melee2 {n.DMeleeTexture2}   ammo {n.AmmoIdfile ?? ""}");
                ImGui.Text($"Melee types: prim {SpawnInfoLookups.MeleeTypeName(n.PrimMeleeType)} ({n.PrimMeleeType})  sec {SpawnInfoLookups.MeleeTypeName(n.SecMeleeType)} ({n.SecMeleeType})  ranged {SpawnInfoLookups.MeleeTypeName(n.RangedType)} ({n.RangedType})");
                ImGui.Text($"Model {n.Model}   HerosForge {n.HerosForgeModel}   Light {n.Light}");
                ImGui.Text($"Luclin: hair {n.LuclinHairstyle}/{n.LuclinHaircolor}   eyes {n.LuclinEyecolor}/{n.LuclinEyecolor2}   beard {n.LuclinBeard}/{n.LuclinBeardcolor}");
                ImGui.Text($"Drakkin: heritage {n.DrakkinHeritage}  tattoo {n.DrakkinTattoo}  details {n.DrakkinDetails}");
                ImGui.Text($"Armor tint: id {n.ArmortintId}   RGB ({n.ArmortintRed},{n.ArmortintGreen},{n.ArmortintBlue})");
                ImGui.TreePop();
            }
            }

            // ── AI & Behavior ──────────────────────────────────────
            if (ImGui.CollapsingHeader($"AI & Behavior###{Id}ndAI", 0))
            {
            NpcInt(npcId, "aggroradius",  "Aggro radius",  () => n.AggroRadius,  v => n.AggroRadius  = v, editable, 0);
            NpcInt(npcId, "assistradius", "Assist radius", () => n.AssistRadius, v => n.AssistRadius = v, editable, 0);
            NpcInt(npcId, "npc_aggro",    "npc_aggro",     () => n.NpcAggro,     v => n.NpcAggro     = v, editable);
            NpcCheckbox(npcId, "always_aggro", "always_aggro", () => n.AlwaysAggro, v => n.AlwaysAggro = v, editable);
            NpcFloat(npcId, "runspeed",  "Run speed",  () => n.Runspeed,  v => n.Runspeed  = v, editable);
            NpcInt(npcId, "walkspeed", "Walk speed", () => n.Walkspeed, v => n.Walkspeed = v, editable, 0);
            NpcInt(npcId, "see_invis",         "See invis",         () => n.SeeInvis,        v => n.SeeInvis        = v, editable);
            NpcInt(npcId, "see_invis_undead",  "See invis undead",  () => n.SeeInvisUndead,  v => n.SeeInvisUndead  = v, editable);
            NpcInt(npcId, "see_hide",          "See hide",          () => n.SeeHide,         v => n.SeeHide         = v, editable);
            NpcInt(npcId, "see_improved_hide", "See improved hide", () => n.SeeImprovedHide, v => n.SeeImprovedHide = v, editable);
            NpcEnumCombo(npcId, "stuck_behavior", "Stuck behavior",
                () => n.StuckBehavior, v => n.StuckBehavior = v,
                _npcStuckVals, _npcStuckLabels, editable);
            NpcEnumCombo(npcId, "flymode", "Flymode",
                () => n.Flymode, v => n.Flymode = v,
                _npcFlymodeVals, _npcFlymodeLabels, editable);
            NpcInt(npcId, "underwater", "Underwater", () => n.Underwater, v => n.Underwater = v, editable);
            NpcNullableInt(npcId, "rare_spawn", "Rare spawn", () => n.RareSpawn, v => n.RareSpawn = v, editable);
            NpcInt(npcId, "spawn_limit", "Spawn limit", () => n.SpawnLimit, v => n.SpawnLimit = v, editable, 0);
            NpcInt(npcId, "qglobal",  "Qglobal",  () => n.Qglobal,  v => n.Qglobal  = v, editable);
            NpcInt(npcId, "emoteid",  "Emote id", () => n.EmoteId,  v => n.EmoteId  = v, editable);
            ImGui.Text("Flags");
            // 2-column grid @ 170px cell pitch. Two columns (not three) because
            // "unique_spawn_by_name" alone is ~150px wide; a 3rd column would
            // regularly clip. Cell positions: 0 and FlagCell.
            const float FlagCell = 170f;
            NpcCheckbox(npcId, "findable",              "findable",              () => n.Findable,           v => n.Findable           = v, editable);
            ImGui.SameLine(FlagCell);
            NpcCheckbox(npcId, "trackable",             "trackable",             () => n.Trackable,          v => n.Trackable          = v, editable);

            NpcCheckbox(npcId, "show_name",             "show_name",             () => n.ShowName,           v => n.ShowName           = v, editable);
            ImGui.SameLine(FlagCell);
            NpcCheckbox(npcId, "no_target_hotkey",      "no_target_hotkey",      () => n.NoTargetHotkey,     v => n.NoTargetHotkey     = v, editable);

            NpcCheckbox(npcId, "untargetable",          "untargetable",          () => n.Untargetable,       v => n.Untargetable       = v, editable);
            ImGui.SameLine(FlagCell);
            NpcCheckbox(npcId, "raid_target",           "raid_target",           () => n.RaidTarget,         v => n.RaidTarget         = v, editable);

            NpcCheckbox(npcId, "private_corpse",        "private_corpse",        () => n.PrivateCorpse,      v => n.PrivateCorpse      = v, editable);
            ImGui.SameLine(FlagCell);
            NpcCheckbox(npcId, "unique_spawn_by_name",  "unique_spawn_by_name",  () => n.UniqueSpawnByName,  v => n.UniqueSpawnByName  = v, editable);

            NpcCheckbox(npcId, "unique_",               "unique",                () => n.Unique,             v => n.Unique             = v, editable);
            ImGui.SameLine(FlagCell);
            NpcCheckbox(npcId, "fixed",                 "fixed",                 () => n.Fixed,              v => n.Fixed              = v, editable);

            NpcCheckbox(npcId, "ignore_despawn",        "ignore_despawn",        () => n.IgnoreDespawn,      v => n.IgnoreDespawn      = v, editable);
            ImGui.SameLine(FlagCell);
            NpcCheckbox(npcId, "isbot",                 "isbot",                 () => n.IsBot,              v => n.IsBot              = v, editable);

            NpcCheckbox(npcId, "isquest",               "isquest",               () => n.IsQuest,            v => n.IsQuest            = v, editable);
            ImGui.SameLine(FlagCell);
            NpcCheckbox(npcId, "exclude",               "exclude",               () => n.Exclude,            v => n.Exclude            = v, editable);
            }

            // ── Special abilities (Slice 4 — friendly checkbox editor) ─────
            if (ImGui.CollapsingHeader($"Special Abilities###{Id}ndSA", 0))
            {
            NpcSpecialAbilitiesEditor(npcId, () => n.SpecialAbilities, v => n.SpecialAbilities = v, editable);

            // npcspecialattks is the legacy per-letter format (S=Summon, E=Enrage,
            // R=Rampage, ...). Server auto-migrated it into special_abilities long ago
            // (see database_update_manifest.cpp lines 184+). Kept read-only here — a
            // fork that still writes to it can add editing later; the mainstream path
            // is special_abilities.
            ImGui.Text($"npcspecialattks (legacy): {(string.IsNullOrEmpty(n.NpcSpecialAttks) ? "(none)" : n.NpcSpecialAttks)}");
            }

            // ── References (typeahead pickers for the FK fields) ───
            if (ImGui.CollapsingHeader($"References###{Id}ndRef", 0))
            {
            NpcIdPicker(npcId, "loottable_id",          "Loot table",
                VisualEQ.SpawnSystem.ReferenceDataCache.Table.LootTable,
                () => n.LoottableId, editable);
            NpcIdPicker(npcId, "npc_faction_id",        "Faction set",
                VisualEQ.SpawnSystem.ReferenceDataCache.Table.NpcFaction,
                () => n.NpcFactionId, editable);
            NpcIdPicker(npcId, "merchant_id",           "Merchant",
                VisualEQ.SpawnSystem.ReferenceDataCache.Table.Merchant,
                () => n.MerchantId, editable);
            NpcIdPicker(npcId, "npc_spells_id",         "Spell set",
                VisualEQ.SpawnSystem.ReferenceDataCache.Table.NpcSpellSet,
                () => n.NpcSpellsId, editable);
            NpcIdPicker(npcId, "npc_spells_effects_id", "Spell effects set",
                VisualEQ.SpawnSystem.ReferenceDataCache.Table.NpcSpellEffectSet,
                () => n.NpcSpellsEffectsId, editable);
            NpcInt(npcId, "greed",                 "Greed",                 () => n.Greed,               v => n.Greed               = v, editable);
            NpcInt(npcId, "alt_currency_id",       "Alt currency id",       () => n.AltCurrencyId,       v => n.AltCurrencyId       = v, editable);
            NpcInt(npcId, "adventure_template_id", "Adventure template id", () => n.AdventureTemplateId, v => n.AdventureTemplateId = v, editable);
            NpcNullableInt(npcId, "trap_template", "Trap template",         () => n.TrapTemplate,        v => n.TrapTemplate        = v, editable);
            NpcInt(npcId, "faction_amount",        "Faction amount",        () => n.FactionAmount,       v => n.FactionAmount       = v, editable);
            NpcCheckbox(npcId, "keeps_sold_items",     "keeps_sold_items",   () => n.KeepsSoldItems,     v => n.KeepsSoldItems     = v, editable);
            NpcCheckbox(npcId, "is_parcel_merchant",   "is_parcel_merchant", () => n.IsParcelMerchant,   v => n.IsParcelMerchant   = v, editable);
            NpcCheckbox(npcId, "multiquest_enabled",   "multiquest_enabled", () => n.MultiquestEnabled,  v => n.MultiquestEnabled  = v, editable);
            NpcNullableInt(npcId, "skip_global_loot",  "Skip global loot",   () => n.SkipGlobalLoot,     v => n.SkipGlobalLoot     = v, editable);
            }

            // ── Faction entries (Slice 5 — inline edits over npc_faction_entries) ─
            // Right after References so the just-picked faction set is visually close
            // to its entries. Default-open because it's a common edit target.
            if (ImGui.CollapsingHeader($"Faction entries###{Id}ndFacE", 0))
            {
                NpcFactionEntriesEditor(npcId, n.NpcFactionId, editable);
            }

            // ── Loot (Slice 6a — read-only 3-level view; CRUD in 6b) ──────
            // Directly below Faction entries so the two "what happens when this
            // NPC dies" sections sit together. Default-collapsed because a wide
            // loottable can push everything else off-screen.
            if (ImGui.CollapsingHeader($"Loot###{Id}ndLoot", 0))
            {
                NpcLootEditor(npcId, n.LoottableId, editable);
            }

            // ── Scaling ────────────────────────────────────────────
            if (ImGui.CollapsingHeader($"Scaling###{Id}ndScale", 0))
            {
            NpcInt(npcId, "scalerate",  "Scale rate", () => n.Scalerate,  v => n.Scalerate  = v, editable);
            NpcFloat(npcId, "spellscale", "Spell scale", () => n.Spellscale, v => n.Spellscale = v, editable, "F1");
            NpcFloat(npcId, "healscale",  "Heal scale",  () => n.Healscale,  v => n.Healscale  = v, editable, "F1");
            NpcInt(npcId, "exp_mod",    "Exp mod",   () => n.ExpMod,   v => n.ExpMod   = v, editable);
            NpcInt(npcId, "maxlevel",   "Max level", () => n.Maxlevel, v => n.Maxlevel = v, editable, 0, 127);
            }

            // ── Charm overrides ────────────────────────────────────
            if (ImGui.CollapsingHeader($"Charm overrides (null = use base stat)###{Id}ndCharm", 0))
            {
            NpcNullableInt(npcId, "charm_ac",               "Charm AC",                () => n.CharmAc,               v => n.CharmAc               = v, editable);
            NpcNullableInt(npcId, "charm_atk",              "Charm ATK",               () => n.CharmAtk,              v => n.CharmAtk              = v, editable);
            NpcNullableInt(npcId, "charm_min_dmg",          "Charm min damage",        () => n.CharmMinDmg,           v => n.CharmMinDmg           = v, editable);
            NpcNullableInt(npcId, "charm_max_dmg",          "Charm max damage",        () => n.CharmMaxDmg,           v => n.CharmMaxDmg           = v, editable);
            NpcNullableInt(npcId, "charm_attack_delay",     "Charm attack delay",      () => n.CharmAttackDelay,      v => n.CharmAttackDelay      = v, editable);
            NpcNullableInt(npcId, "charm_accuracy_rating", "Charm accuracy rating",    () => n.CharmAccuracyRating,   v => n.CharmAccuracyRating   = v, editable);
            NpcNullableInt(npcId, "charm_avoidance_rating","Charm avoidance rating",   () => n.CharmAvoidanceRating,  v => n.CharmAvoidanceRating  = v, editable);
            }

            // ── Provenance ─────────────────────────────────────────
            if (ImGui.CollapsingHeader($"Provenance###{Id}ndProv", 0))
            {
            ImGui.Text($"Version {n.Version}   PEQ id {n.PeqId}");
            }
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
