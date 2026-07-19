using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using VisualEQ.Database.Configuration;
using VisualEQ.Database.Repositories;
using VisualEQ.EditSystem;
using VisualEQ.Engine;
using VisualEQ.Settings;
using VisualEQ.SpawnSystem;
using VisualEQ.Views;
using VisualEQ.ZonePointSystem;
using static VisualEQ.Engine.Globals;

namespace VisualEQ
{
    public class Controller : Engine.IController
    {
        public readonly EngineCore Engine = new EngineCore();

        readonly List<BaseView> Views = new List<BaseView>();
        readonly List<AniModelInstance> CharacterModels = new List<AniModelInstance>();

        // Shared model cache so the same AniModel is never loaded twice across spawns.
        readonly Dictionary<string, AniModel> _modelCache = new Dictionary<string, AniModel>();

        // Cached availableModels for the current zone. Populated by every code path that
        // calls SpawnManager.LoadFromRecords / LoadBatch so post-load spawn creates
        // (duplicate, session-recovery replay) don't have to re-scan every chr zip.
        // Empty until the first spawn load succeeds.
        Dictionary<string, string> _availableModels = new Dictionary<string, string>();
        internal Dictionary<string, string> AvailableModels => _availableModels;

        // Per-zone snapshot cache — populated on ClearCurrentZone so that F10 → pick the
        // same zone again skips DB round-trips and restores the camera pose. Rows are only
        // cached when PendingBuffer is empty (a dirty buffer means the in-memory rows have
        // been mutated relative to DB, so re-feeding them would leak edits across the
        // re-load). Camera pose is always cached — it's independent of edit state. Key is
        // zone shortname (case-insensitive). Session-lifetime; cleared on Shutdown.
        readonly Dictionary<string, ZoneSnapshot> _zoneSnapshots =
            new Dictionary<string, ZoneSnapshot>(StringComparer.OrdinalIgnoreCase);

        // Session-wide cache of every zone shortname in the DB — global to the DB, not per
        // zone. Populated on the first LoadZonePointsSync that has DbFactory != null and
        // reused by every subsequent zone load so we don't re-query zone.short_name every
        // time.
        List<string> _cachedZoneShortNames;

        sealed class ZoneSnapshot
        {
            // Always populated.
            public Vector3 CameraPosition;
            public float CameraPitch;
            public float CameraYaw;

            // Populated only when the buffer was empty at ClearCurrentZone time. Null
            // otherwise (forces a DB re-fetch on re-visit).
            public List<Database.Models.SpawnRecord> SpawnRecords;
            public Dictionary<string, string> AvailableModels;
            public List<Database.Models.ZoneGridRecord> ZoneGrids;
            public int? CurrentZoneId;
            public List<Database.Models.TrilogyZonePoint> ZonePointsOutgoing;
            public List<Database.Models.TrilogyZonePoint> ZonePointsIncoming;
            public Dictionary<string, List<Database.Models.TrilogyZonePoint>> PeerZoneRows;

            public bool HasRowData => SpawnRecords != null;
        }

        public AniModel LastModelLoaded;

        private ModelSelector modelSelector;
        object IController.ModelSelector => modelSelector;
        public ModelSelector ModelSelector => modelSelector;

        // IController plumbing for the drag-to-create pipeline. Implementations here
        // dispatch to the OnCreation* methods declared elsewhere in this class.
        bool IController.IsCreationActive => ActiveCreation != CreationMode.None;
        void IController.OnCreationMouseDown(Vector3 groundHit) => OnCreationMouseDown(groundHit);
        void IController.OnCreationMouseMove(Vector3 groundHit) => OnCreationMouseMove(groundHit);
        void IController.OnCreationMouseUp() => OnCreationMouseUp();
        void IController.CancelCreation() => CancelCreation();

        public AppSettings Settings { get; }

        // Resolved directory holding converted zone/chr `*_oes.zip` files. Sourced from
        // AppSettings.ConvertedAssetsPath so views (MainMenuView) can read a single value
        // without importing Settings directly.
        public string ConvertedAssetsDir => Settings.ConvertedAssetsPath;

        // Non-null once the user has saved a valid DB connection.
        public MySqlConnectionFactory DbFactory { get; private set; }

        public SpawnManager SpawnManager { get; } = new SpawnManager();

        public ZonePointManager ZonePointManager { get; } = new ZonePointManager();

        // Sandwich detection results keyed by owned row id. Recomputed each frame from
        // ZonePointManager.PeerZoneRows so edits (drag, inspector field changes) reflect
        // instantly without an explicit invalidation step. Detection is cheap — dozens of
        // rows × dozens of peer rows per zone — so per-frame is fine.
        public Dictionary<int, ZonePointSystem.SandwichDetector.Result> SandwichResults { get; }
            = new Dictionary<int, ZonePointSystem.SandwichDetector.Result>();

        // Cached list of every zone shortname in the DB — populated on zone load,
        // powers the target-zone dropdown in the inspector. Empty when no DB is
        // configured; the inspector falls back to a free-text field in that case.
        public List<string> ZoneShortNames { get; } = new List<string>();

        // Every grid in the current zone — attached AND orphan (no spawn2 references it).
        // Loaded alongside spawns; SpawnCount is filled after spawn records land so the
        // sidebar can tag each row A/O. The Grid List sidebar section reads this; when
        // a grid is picked from that list, SelectedGridId drives UpdatePathGrids to
        // render THAT grid's polyline instead of the selected spawn's.
        public List<Database.Models.ZoneGridRecord> ZoneGrids { get; } = new List<Database.Models.ZoneGridRecord>();

        public int? SelectedGridId { get; private set; }
        public event Action<int?> GridSelectedChanged;

        // Sidebar entry point for grid picks. Passing null clears the selection.
        public void SelectGrid(int? gridId)
        {
            if (SelectedGridId == gridId) return;
            SelectedGridId = gridId;
            GridSelectedChanged?.Invoke(gridId);
        }

        // Numeric zoneidnumber of the current zone (from the zone table). Populated by
        // LoadZoneGridsSync; needed by GridInsertAction so it can stamp new grid rows
        // with the correct FK without a fresh DB round-trip.
        public int? CurrentZoneId { get; private set; }

        // Negative temp-id counter for pending grid inserts — mirrors
        // ZonePointManager.NextTempId. Real grid ids are always positive, so a negative
        // sentinel unambiguously flags a pre-commit grid throughout scene + buffer.
        int _nextTempGridId = -1;
        public int NextTempGridId() => _nextTempGridId--;

        // Grid Mode — sub-mode of Edit Mode. When active, EngineCore intercepts LMB
        // double-clicks on collision geometry and routes them to OnGridModeDoubleClick
        // for waypoint placement. Auto-exits when EditModeEnabled goes false so a stale
        // Grid Mode never outlives its parent.
        bool _gridModeActive;
        public bool GridModeActive => _gridModeActive;
        public event Action<bool> GridModeChanged;

        public void EnterGridMode()
        {
            if (_gridModeActive) return;
            if (!EditModeEnabled)
            {
                Console.WriteLine("[Controller] EnterGridMode ignored — edit mode is off.");
                return;
            }
            _gridModeActive = true;
            GridModeChanged?.Invoke(true);
        }

        public void ExitGridMode()
        {
            if (!_gridModeActive) return;
            _gridModeActive = false;
            GridModeChanged?.Invoke(false);
        }

        public void ToggleGridMode()
        {
            if (_gridModeActive) ExitGridMode();
            else EnterGridMode();
        }

        // ─── Drag-to-create state ────────────────────────────────────────────────────
        // When active, the mouse pipeline is intercepted before zone-point / waypoint /
        // spawn selectors so left-click-drag on the ground plane draws a preview
        // rectangle (box) or line (plane). Mouse-up commits the new zone_point via a
        // ZonePointInsertAction and returns to CreationMode.None.
        public enum CreationMode { None, DrawBox, DrawPlane }
        public CreationMode ActiveCreation { get; private set; } = CreationMode.None;

        // Ground-plane drag anchor + current point. Both null when not actively dragging.
        Vector3? _creationDragStart;
        Vector3? _creationDragCurrent;

        public Vector3? CreationDragStart   => _creationDragStart;
        public Vector3? CreationDragCurrent => _creationDragCurrent;

        public event Action<CreationMode> CreationModeChanged;

        public void EnterCreationMode(CreationMode m)
        {
            if (m == ActiveCreation) return;
            ActiveCreation = m;
            _creationDragStart = _creationDragCurrent = null;
            CreationModeChanged?.Invoke(m);
        }

        public void CancelCreation()
        {
            if (ActiveCreation == CreationMode.None && _creationDragStart == null) return;
            ActiveCreation = CreationMode.None;
            _creationDragStart = null;
            _creationDragCurrent = null;
            CreationModeChanged?.Invoke(CreationMode.None);
        }

        // Called by EngineCore.MouseDown when creation mode is active. scenePos is the
        // mouse ray's intersection with the ground plane (Z = camera-anchor Z, or the
        // collider's floor if a hit is found).
        public void OnCreationMouseDown(Vector3 scenePos)
        {
            if (ActiveCreation == CreationMode.None) return;
            _creationDragStart = _creationDragCurrent = scenePos;
        }

        public void OnCreationMouseMove(Vector3 scenePos)
        {
            if (ActiveCreation == CreationMode.None) return;
            if (_creationDragStart.HasValue) _creationDragCurrent = scenePos;
        }

        public void OnCreationMouseUp()
        {
            if (ActiveCreation == CreationMode.None) return;
            if (_creationDragStart.HasValue && _creationDragCurrent.HasValue)
            {
                var start = _creationDragStart.Value;
                var end   = _creationDragCurrent.Value;
                if (Vector3.DistanceSquared(new Vector3(start.X, start.Y, 0), new Vector3(end.X, end.Y, 0)) > 1f)
                {
                    if (ActiveCreation == CreationMode.DrawBox)   CreateBoxFromDrag(start, end);
                    else if (ActiveCreation == CreationMode.DrawPlane) CreatePlaneFromDrag(start, end);
                }
            }
            _creationDragStart = null;
            _creationDragCurrent = null;
            ActiveCreation = CreationMode.None;
            CreationModeChanged?.Invoke(CreationMode.None);
        }

        // Ground-rectangle → box. Zrange = half of max(width, height) — the schema is a
        // single scalar so the resulting box is square-fit (spec §6). Center = rectangle
        // midpoint.
        void CreateBoxFromDrag(Vector3 sceneStart, Vector3 sceneEnd)
        {
            var centerScene = new Vector3(
                (sceneStart.X + sceneEnd.X) * 0.5f,
                (sceneStart.Y + sceneEnd.Y) * 0.5f,
                (sceneStart.Z + sceneEnd.Z) * 0.5f);
            var halfW = System.MathF.Abs(sceneEnd.X - sceneStart.X) * 0.5f;
            var halfH = System.MathF.Abs(sceneEnd.Y - sceneStart.Y) * 0.5f;
            var zrange = (int)System.MathF.Max(1f, System.MathF.Max(halfW, halfH));

            var row = NewRowTemplate();
            row.X            = centerScene.Y;   // DB X = scene Y
            row.Y            = centerScene.X;   // DB Y = scene X
            row.Z            = centerScene.Z;
            row.Zrange       = zrange;
            row.UseNewZoning = 0;

            RecordAction(new EditSystem.ZonePointInsertAction(row));
            SelectFreshInsert(row.Id);
        }

        // Ground-line → plane crossing. Midpoint's X or Y becomes the trigger coord
        // (depending on which axis has the larger delta — we choose the plane
        // perpendicular to the SHORTER axis so the line hugs the intended edge). Line
        // endpoints become MinVert/MaxVert on the perpendicular axis.
        void CreatePlaneFromDrag(Vector3 sceneStart, Vector3 sceneEnd)
        {
            var mid = new Vector3(
                (sceneStart.X + sceneEnd.X) * 0.5f,
                (sceneStart.Y + sceneEnd.Y) * 0.5f,
                (sceneStart.Z + sceneEnd.Z) * 0.5f);
            var dx = System.MathF.Abs(sceneEnd.X - sceneStart.X);
            var dy = System.MathF.Abs(sceneEnd.Y - sceneStart.Y);

            var row = NewRowTemplate();
            row.Z = mid.Z;
            if (dx >= dy)
            {
                // Line runs along scene X (= DB Y). Perpendicular axis is scene Y (= DB X).
                // → DB mode 2 (Y-plane); plane at DB Y = mid's DB Y (= mid.X in scene).
                row.UseNewZoning = 2;
                row.X = 0f;             // ungated on DB X (bounds control extent)
                row.Y = mid.X;          // DB Y = scene X
                row.MinVert = System.MathF.Min(sceneStart.Y, sceneEnd.Y); // scene Y = DB X
                row.MaxVert = System.MathF.Max(sceneStart.Y, sceneEnd.Y);
            }
            else
            {
                // Line runs along scene Y (= DB X). Perpendicular axis is scene X (= DB Y).
                // → DB mode 1 (X-plane); plane at DB X = mid's DB X (= mid.Y in scene).
                row.UseNewZoning = 1;
                row.X = mid.Y;          // DB X = scene Y
                row.Y = 0f;             // ungated on DB Y (bounds control extent)
                row.MinVert = System.MathF.Min(sceneStart.X, sceneEnd.X); // scene X = DB Y
                row.MaxVert = System.MathF.Max(sceneStart.X, sceneEnd.X);
            }

            RecordAction(new EditSystem.ZonePointInsertAction(row));
            SelectFreshInsert(row.Id);
        }

        Database.Models.TrilogyZonePoint NewRowTemplate() => new Database.Models.TrilogyZonePoint
        {
            Id           = ZonePointManager.NextTempId(),
            Zone         = CurrentZoneName,
            Heading      = 0f,
            TargetZone   = ZoneShortNames.FirstOrDefault(z =>
                                !string.Equals(z, CurrentZoneName, StringComparison.OrdinalIgnoreCase))
                            ?? CurrentZoneName,
            Zrange       = 15,
            MaxZDiff     = 0,
            UseNewZoning = 0,
        };

        void SelectFreshInsert(int id)
        {
            var created = ZonePointManager.ZonePoints.FirstOrDefault(z => z.Row.Id == id);
            if (created != null) ZonePointManager.Select(created);
        }

        // Zone name set when LoadZone is called; triggers spawn load on later DB connect.
        public string CurrentZoneName { get; private set; }

        // Fires whenever the loaded zone changes (including cleared → null). Used by views
        // that need to react to zone swaps (e.g. MainMenuView shows/hides itself).
        public event Action<string> ZoneChanged;

        // Fires when the user hit F10 while there are unsaved pending edits. The sidebar
        // subscribes and shows an unsaved-changes warning modal instead of clearing.
        public event Action UnsavedChangesOnClearRequested;

        // Called by EngineCore.OnKeyDown F10. If there are pending edits, defers to a UI
        // prompt via the event above; otherwise clears immediately.
        public void RequestClearCurrentZone()
        {
            if (PendingBuffer != null && !PendingBuffer.IsEmpty && UnsavedChangesOnClearRequested != null)
            {
                UnsavedChangesOnClearRequested.Invoke();
                return;
            }
            ClearCurrentZone();
        }

        // Edit mode gate: drag mutations are only allowed when true. Persists via settings.
        // Views + ModelSelector observe this via EditModeChanged.
        public bool EditModeEnabled
        {
            get => Settings.EditModeEnabled;
            set
            {
                if (Settings.EditModeEnabled == value) return;
                Settings.EditModeEnabled = value;
                SettingsManager.Save(Settings);
                if (!value) ExitGridMode();  // Grid Mode is a sub-mode; can't outlive Edit Mode.
                EditModeChanged?.Invoke(value);
            }
        }
        public event Action<bool> EditModeChanged;

        // Called by EngineCore.OnKeyDown when the user hits E. No-op when no zone is loaded
        // (edit mode is meaningless without a scene).
        public void ToggleEditMode()
        {
            if (CurrentZoneName == null) return;
            EditModeEnabled = !EditModeEnabled;
        }

        // Registered by App at startup; F1 flips this window's visibility. No-op if
        // App skipped registering (headless test runs or a variant with no help view).
        public HelpView HelpView;
        public void ToggleHelp() => HelpView?.Toggle();

        // ─── Pending edit buffer ───────────────────────────────────────────────────────

        // The in-memory buffer for the currently-loaded zone. Non-null while a zone is loaded.
        // Fresh (empty) by default; may be populated from disk during zone load if a previous
        // session left pending edits.
        public EditBuffer PendingBuffer { get; private set; }

        // Session-only undo/redo history — see UndoStack for rationale. Cleared on zone unload.
        public UndoStack UndoStack { get; } = new UndoStack();

        // Fires whenever the buffer's contents change (add, remove, or item update). Sidebar
        // subscribes to refresh its Pending Changes list; ModelSelector uses it to fire
        // updates for the orange dirty markers.
        public event Action BufferChanged;

        // Debounced auto-save. Every mutation calls MarkBufferDirty(); a background timer
        // in UpdateFrame writes to disk after a short lull so drags don't hammer the FS.
        bool _bufferDirty;
        float _bufferDirtyAt;
        const float BufferSaveDebounceSec = 0.5f;

        public void MarkBufferDirty()
        {
            _bufferDirty = true;
            _bufferDirtyAt = FrameTime;
            BufferChanged?.Invoke();
        }

        void FlushBufferIfNeeded()
        {
            if (!_bufferDirty || PendingBuffer == null) return;
            if (FrameTime - _bufferDirtyAt < BufferSaveDebounceSec) return;

            if (PendingBuffer.IsEmpty)
                EditBufferManager.DeleteForZone(PendingBuffer.Zone);
            else
                EditBufferManager.SaveForZone(PendingBuffer);
            _bufferDirty = false;
        }

        // Applies every SpawnEdit + GridEntryEdit in a buffer to the currently-loaded scene.
        // Called on user's "Restore" during session recovery.
        public void ApplyPendingBuffer(EditBuffer buffer)
        {
            if (buffer == null || buffer.IsEmpty) return;

            foreach (var kv in buffer.Spawns)
            {
                var sp = SpawnManager.SpawnPoints.FirstOrDefault(p => p.Record.Spawn.Id == kv.Key);
                if (sp == null) continue;
                var e = kv.Value;
                // Positions in the buffer are DB-space; the scene swaps X/Y.
                var scenePos = new Vector3(e.CurrentY, e.CurrentX, e.CurrentZ);
                sp.MarkMoved(scenePos, e.CurrentHeading);
            }

            // Pending spawn deletes — hide each matching just-loaded SpawnPoint so the
            // scene reflects the buffer. Applies AFTER spawn moves so a pending move
            // + pending delete on the same id still gets the hide (the move was dropped
            // when the delete happened, so buffer.Spawns won't have an entry, but this
            // ordering also handles a hypothetical race).
            foreach (var kv in buffer.SpawnDeletes)
            {
                var sp = SpawnManager.SpawnPoints.FirstOrDefault(p => p.Record.Spawn.Id == kv.Key);
                if (sp == null) continue;
                HideSpawnFromScene(sp);
            }

            // Pending spawn inserts — rebuild each temp SpawnPoint from the SpawnInsert
            // fields + npc_types lookup so a session-recovered buffer materialises the
            // same in-scene node the user had before the F10 / restart. Advances
            // _nextTempSpawnId so a fresh Duplicate() doesn't collide with recovered temps.
            foreach (var kv in buffer.SpawnInserts)
            {
                var ins = kv.Value;
                if (SpawnManager.SpawnPoints.Any(p => p.Record.Spawn.Id == ins.TempSpawnId)) continue;

                // Reconstruct the SpawnRecord — need NPC rows to know race / size / textures.
                // Batch-load via SpawnRepository so N inserts = 1 query. If DbFactory is null
                // (offline recovery), skip: the insert stays in the buffer, will re-Apply next
                // time the zone loads with a DB connection.
                if (DbFactory == null) continue;

                var repo = new Database.Repositories.SpawnRepository(DbFactory);
                var npcIds = ins.Entries.Select(e => e.NpcId).ToList();
                if (npcIds.Count == 0) continue;

                // Fetching one insert at a time is fine — session recovery typically has
                // a handful of pending inserts, not thousands. Reuses the batch query
                // signature which happily accepts a small IN-list.
                var npcs = repo.GetNpcTypesBatchAsync(npcIds).GetAwaiter().GetResult().ToList();
                var npcById = npcs.ToDictionary(n => n.Id);

                var entries = ins.Entries
                    .Where(e => npcById.ContainsKey(e.NpcId))
                    .Select(e => new Database.Models.SpawnEntryWithNpc
                    {
                        Entry = new Database.Models.SpawnEntry { NpcId = e.NpcId, Chance = e.Chance },
                        Npc   = npcById[e.NpcId],
                    })
                    .ToList();

                var record = new Database.Models.SpawnRecord
                {
                    Spawn = new Database.Models.Spawn2
                    {
                        Id             = ins.TempSpawnId,
                        SpawnGroupId   = 0,
                        Zone           = ins.Zone,
                        Version        = ins.Version,
                        X              = ins.X,
                        Y              = ins.Y,
                        Z              = ins.Z,
                        Heading        = ins.Heading,
                        RespawnTime    = ins.RespawnTime,
                        Variance       = ins.Variance,
                        PathGrid       = ins.PathGrid,
                        Animation      = ins.Animation,
                        SpawnGroupName = ins.SpawnGroupName,
                    },
                    Entries   = entries,
                    Waypoints = new List<Database.Models.GridEntry>(),
                    Grid      = null,
                };

                SpawnPendingInsertFromSnapshot(record);
            }

            // Grid entry field edits — mutate every SpawnRecord.Waypoints copy that
            // references this waypoint so rendering picks up the pending state. Also
            // mutate the ZoneGrids parallel copy so orphan grids (unreferenced by any
            // spawn2 row) reflect pending edits when picked via the Grid List sidebar.
            foreach (var kv in buffer.GridEntries)
            {
                var e = kv.Value;
                foreach (var sp in SpawnManager.SpawnPoints)
                {
                    foreach (var wp in sp.Record.Waypoints)
                    {
                        if (wp.GridId != e.GridId || wp.Number != e.Number) continue;
                        wp.X          = e.CurrentX;
                        wp.Y          = e.CurrentY;
                        wp.Z          = e.CurrentZ;
                        wp.Heading    = e.CurrentHeading;
                        wp.Pause      = e.CurrentPause;
                        wp.Centerpoint = e.CurrentCenterpoint;
                    }
                }
                foreach (var zg in ZoneGrids)
                {
                    foreach (var wp in zg.Waypoints)
                    {
                        if (wp.GridId != e.GridId || wp.Number != e.Number) continue;
                        wp.X          = e.CurrentX;
                        wp.Y          = e.CurrentY;
                        wp.Z          = e.CurrentZ;
                        wp.Heading    = e.CurrentHeading;
                        wp.Pause      = e.CurrentPause;
                        wp.Centerpoint = e.CurrentCenterpoint;
                    }
                }
            }

            // Grid metadata (type/type2).
            foreach (var kv in buffer.Grids)
            {
                var e = kv.Value;
                foreach (var sp in SpawnManager.SpawnPoints)
                {
                    var g = sp.Record.Grid;
                    if (g == null || g.Id != e.Id || g.ZoneId != e.ZoneId) continue;
                    g.Type  = e.CurrentType;
                    g.Type2 = e.CurrentType2;
                }
                foreach (var zg in ZoneGrids)
                {
                    if (zg.Grid == null || zg.Grid.Id != e.Id || zg.Grid.ZoneId != e.ZoneId) continue;
                    zg.Grid.Type  = e.CurrentType;
                    zg.Grid.Type2 = e.CurrentType2;
                }
            }

            // Pending whole-grid inserts — recreate each temp ZoneGridRecord in the scene
            // (must happen BEFORE GridEntryInserts so the seed waypoint has a matching
            // ZoneGrid to attach to). Also advance _nextTempGridId past the recovered
            // temps so a fresh CreateNewGridAtCamera() call won't collide.
            foreach (var kv in buffer.GridInserts)
            {
                var ins = kv.Value;
                if (ZoneGrids.Any(g => g.Grid != null && g.Grid.Id == ins.TempId)) continue;
                ZoneGrids.Add(new Database.Models.ZoneGridRecord
                {
                    Grid       = new Database.Models.Grid { Id = ins.TempId, ZoneId = ins.ZoneId, Type = ins.Type, Type2 = ins.Type2 },
                    Waypoints  = new List<Database.Models.GridEntry>(),  // seed waypoint arrives via GridEntryInserts below
                    SpawnCount = 0,
                });
                if (ins.TempId <= _nextTempGridId)
                    _nextTempGridId = ins.TempId - 1;
            }

            // Pending inserts — add each temp waypoint to every referring spawn's list and
            // to the matching ZoneGrid (orphan grids only reach here via the latter).
            foreach (var kv in buffer.GridEntryInserts)
            {
                var ins = kv.Value;
                foreach (var sp in SpawnManager.SpawnPoints)
                {
                    if (sp.Record.Spawn.PathGrid != ins.GridId) continue;
                    if (sp.Record.Waypoints.Any(w => w.GridId == ins.GridId && w.Number == ins.Number))
                        continue;
                    sp.Record.Waypoints.Add(new Database.Models.GridEntry
                    {
                        GridId      = ins.GridId,
                        Number      = ins.Number,
                        X           = ins.X,
                        Y           = ins.Y,
                        Z           = ins.Z,
                        Heading     = ins.Heading,
                        Pause       = ins.Pause,
                        Centerpoint = ins.Centerpoint,
                    });
                }
                foreach (var zg in ZoneGrids)
                {
                    if (zg.Grid == null || zg.Grid.Id != ins.GridId) continue;
                    if (zg.Waypoints.Any(w => w.GridId == ins.GridId && w.Number == ins.Number))
                        continue;
                    zg.Waypoints.Add(new Database.Models.GridEntry
                    {
                        GridId      = ins.GridId,
                        Number      = ins.Number,
                        X           = ins.X,
                        Y           = ins.Y,
                        Z           = ins.Z,
                        Heading     = ins.Heading,
                        Pause       = ins.Pause,
                        Centerpoint = ins.Centerpoint,
                    });
                }
            }

            // Pending deletes — remove the row from every referring spawn's list AND from
            // ZoneGrids so orphan-grid deletes render correctly.
            foreach (var kv in buffer.GridEntryDeletes)
            {
                var del = kv.Value;
                foreach (var sp in SpawnManager.SpawnPoints)
                {
                    sp.Record.Waypoints.RemoveAll(w =>
                        w.GridId == del.GridId && w.Number == del.Number);
                }
                foreach (var zg in ZoneGrids)
                {
                    zg.Waypoints.RemoveAll(w =>
                        w.GridId == del.GridId && w.Number == del.Number);
                }
            }

            foreach (var kv in buffer.ZonePoints)
            {
                // Incoming rows (owned by other zones) can also carry pending heading edits,
                // so check both lists via AllPoints().
                var zp = ZonePointManager.AllPoints().FirstOrDefault(p => p.Row.Id == kv.Key);
                if (zp == null) continue;
                var e = kv.Value;

                // v2 → v3 migration: earlier buffers stored only position + size, so the
                // scalar Original* fields deserialise as zero/null. Seed them from the live
                // row now so any future field-edit's revert-to-baseline check has real values
                // to compare against. Also copy the live row into Current* for fields the
                // v2 buffer didn't know about — otherwise the commit would blank them out.
                if (!e.ScalarOriginalsSeeded)
                {
                    e.OriginalHeading       = zp.OriginalHeading;
                    e.OriginalTargetZone    = zp.OriginalTargetZone;
                    e.OriginalTargetX       = zp.OriginalTargetX;
                    e.OriginalTargetY       = zp.OriginalTargetY;
                    e.OriginalTargetZ       = zp.OriginalTargetZ;
                    e.OriginalUseNewZoning  = zp.OriginalUseNewZoning;
                    e.OriginalMinVert       = zp.OriginalMinVert;
                    e.OriginalMaxVert       = zp.OriginalMaxVert;
                    e.OriginalCenterPoint   = zp.OriginalCenterPoint;
                    e.OriginalKeepX         = zp.OriginalKeepX;
                    e.OriginalKeepY         = zp.OriginalKeepY;
                    e.OriginalKeepZ         = zp.OriginalKeepZ;

                    e.CurrentHeading        = zp.OriginalHeading;
                    e.CurrentTargetZone     = zp.OriginalTargetZone;
                    e.CurrentTargetX        = zp.OriginalTargetX;
                    e.CurrentTargetY        = zp.OriginalTargetY;
                    e.CurrentTargetZ        = zp.OriginalTargetZ;
                    e.CurrentUseNewZoning   = zp.OriginalUseNewZoning;
                    e.CurrentMinVert        = zp.OriginalMinVert;
                    e.CurrentMaxVert        = zp.OriginalMaxVert;
                    e.CurrentCenterPoint    = zp.OriginalCenterPoint;
                    e.CurrentKeepX          = zp.OriginalKeepX;
                    e.CurrentKeepY          = zp.OriginalKeepY;
                    e.CurrentKeepZ          = zp.OriginalKeepZ;

                    e.ScalarOriginalsSeeded = true;
                }

                var scenePos = new Vector3(e.CurrentY, e.CurrentX, e.CurrentZ);
                zp.MarkMoved(scenePos);
                zp.MarkResized(e.CurrentZrange, e.CurrentMaxZDiff);
                // Apply scalar Current* into the live row so the inspector opens with the
                // recovered values, not the DB baseline.
                zp.SetHeading(e.CurrentHeading);
                zp.SetTargetZone(e.CurrentTargetZone);
                zp.SetTargetX(e.CurrentTargetX);
                zp.SetTargetY(e.CurrentTargetY);
                zp.SetTargetZ(e.CurrentTargetZ);
                zp.SetUseNewZoning(e.CurrentUseNewZoning);
                zp.SetMinVert(e.CurrentMinVert);
                zp.SetMaxVert(e.CurrentMaxVert);
                zp.SetCenterPoint(e.CurrentCenterPoint);
                zp.SetKeepX(e.CurrentKeepX);
                zp.SetKeepY(e.CurrentKeepY);
                zp.SetKeepZ(e.CurrentKeepZ);
            }

            PendingBuffer = buffer;
            BufferChanged?.Invoke();
            // No MarkBufferDirty — buffer is already on disk from the previous session.
        }

        // Wipes the current buffer + on-disk file. Restores all dirty spawns to their
        // original position/heading via SpawnPoint.Revert(). Waypoints don't have a
        // per-object Revert() — they're shared across spawns — so we restore each
        // GridEntry field-by-field from the buffer's Original* snapshot before the
        // buffer is cleared.
        public void DiscardPendingBuffer()
        {
            if (PendingBuffer == null) return;

            foreach (var sp in SpawnManager.SpawnPoints)
                if (sp.IsDirty) sp.Revert();
            foreach (var zp in ZonePointManager.AllPoints())
                if (zp.IsDirty) zp.Revert();

            // Pending spawn deletes — re-attach every hidden SpawnPoint. Snapshot the ids
            // first because RestoreSpawnToScene mutates HiddenSpawnPoints.
            var pendingDeleteIds = PendingBuffer.SpawnDeletes.Keys.ToList();
            foreach (var id in pendingDeleteIds)
                RestoreSpawnToScene(id);

            // Pending spawn inserts — detach the temp SpawnPoint from every scene
            // register. Snapshot the ids first because DetachPendingInsertSpawn mutates
            // SpawnManager.SpawnPoints.
            var pendingInsertIds = PendingBuffer.SpawnInserts.Keys.ToList();
            foreach (var id in pendingInsertIds)
            {
                var sp = SpawnManager.SpawnPoints.FirstOrDefault(p => p.Record.Spawn.Id == id);
                if (sp != null) DetachPendingInsertSpawn(sp);
            }

            // Waypoint revert — GridWaypointMoveAction/GridEntryFieldEditAction mutate
            // sp.Record.Waypoints (and ZoneGrids.Waypoints) in place, so without these
            // loops the polyline stays at the moved position after Discard.
            foreach (var edit in PendingBuffer.GridEntries.Values)
            {
                foreach (var sp in SpawnManager.SpawnPoints)
                {
                    foreach (var wp in sp.Record.Waypoints)
                    {
                        if (wp.GridId != edit.GridId || wp.Number != edit.Number) continue;
                        wp.X          = edit.OriginalX;
                        wp.Y          = edit.OriginalY;
                        wp.Z          = edit.OriginalZ;
                        wp.Heading    = edit.OriginalHeading;
                        wp.Pause      = edit.OriginalPause;
                        wp.Centerpoint = edit.OriginalCenterpoint;
                    }
                }
                foreach (var zg in ZoneGrids)
                {
                    foreach (var wp in zg.Waypoints)
                    {
                        if (wp.GridId != edit.GridId || wp.Number != edit.Number) continue;
                        wp.X          = edit.OriginalX;
                        wp.Y          = edit.OriginalY;
                        wp.Z          = edit.OriginalZ;
                        wp.Heading    = edit.OriginalHeading;
                        wp.Pause      = edit.OriginalPause;
                        wp.Centerpoint = edit.OriginalCenterpoint;
                    }
                }
            }

            // Grid metadata revert — same in-place mutation as waypoints.
            foreach (var edit in PendingBuffer.Grids.Values)
            {
                foreach (var sp in SpawnManager.SpawnPoints)
                {
                    var g = sp.Record.Grid;
                    if (g == null || g.Id != edit.Id || g.ZoneId != edit.ZoneId) continue;
                    g.Type  = edit.OriginalType;
                    g.Type2 = edit.OriginalType2;
                }
                foreach (var zg in ZoneGrids)
                {
                    if (zg.Grid == null || zg.Grid.Id != edit.Id || zg.Grid.ZoneId != edit.ZoneId) continue;
                    zg.Grid.Type  = edit.OriginalType;
                    zg.Grid.Type2 = edit.OriginalType2;
                }
            }

            // Pending inserts — pop the temp waypoints out of every spawn's list AND every
            // ZoneGrid so the polyline collapses back to the DB-known state.
            foreach (var ins in PendingBuffer.GridEntryInserts.Values)
            {
                foreach (var sp in SpawnManager.SpawnPoints)
                {
                    sp.Record.Waypoints.RemoveAll(w =>
                        w.GridId == ins.GridId && w.Number == ins.Number);
                }
                foreach (var zg in ZoneGrids)
                {
                    zg.Waypoints.RemoveAll(w =>
                        w.GridId == ins.GridId && w.Number == ins.Number);
                }
            }

            // Pending whole-grid inserts — drop each temp ZoneGridRecord from the scene.
            // (GridEntryInserts loop above already stripped any seed waypoints referring
            // to these temp ids.)
            foreach (var ins in PendingBuffer.GridInserts.Values)
            {
                ZoneGrids.RemoveAll(g => g.Grid != null && g.Grid.Id == ins.TempId);
                if (SelectedGridId == ins.TempId)
                {
                    SelectedGridId = null;
                    GridSelectedChanged?.Invoke(null);
                }
            }

            // Pending deletes — re-insert the snapshot rows so they render again until
            // the user re-issues the delete.
            foreach (var del in PendingBuffer.GridEntryDeletes.Values)
            {
                foreach (var sp in SpawnManager.SpawnPoints)
                {
                    if (sp.Record.Spawn.PathGrid != del.GridId) continue;
                    if (sp.Record.Waypoints.Any(w => w.GridId == del.GridId && w.Number == del.Number))
                        continue;
                    sp.Record.Waypoints.Add(new Database.Models.GridEntry
                    {
                        GridId      = del.GridId,
                        Number      = del.Number,
                        X           = del.X,
                        Y           = del.Y,
                        Z           = del.Z,
                        Heading     = del.Heading,
                        Pause       = del.Pause,
                        Centerpoint = del.Centerpoint,
                    });
                }
                foreach (var zg in ZoneGrids)
                {
                    if (zg.Grid == null || zg.Grid.Id != del.GridId) continue;
                    if (zg.Waypoints.Any(w => w.GridId == del.GridId && w.Number == del.Number))
                        continue;
                    zg.Waypoints.Add(new Database.Models.GridEntry
                    {
                        GridId      = del.GridId,
                        Number      = del.Number,
                        X           = del.X,
                        Y           = del.Y,
                        Z           = del.Z,
                        Heading     = del.Heading,
                        Pause       = del.Pause,
                        Centerpoint = del.Centerpoint,
                    });
                }
            }

            EditBufferManager.DeleteForZone(PendingBuffer.Zone);
            PendingBuffer = new EditBuffer { Zone = CurrentZoneName, CreatedAt = DateTime.UtcNow };
            _bufferDirty = false;
            BufferChanged?.Invoke();
        }

        public Controller(AppSettings settings)
        {
            Settings = settings;

            // Make sure the assets dir exists so the in-app converter can write into it and
            // so directory-scan sites (MainMenuView, SpawnManager.BuildAvailableModels)
            // don't have to null-check the path on every call.
            try
            {
                System.IO.Directory.CreateDirectory(settings.ConvertedAssetsPath);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Controller] Failed to create assets dir '{settings.ConvertedAssetsPath}': {ex.Message}");
            }

            // If the user has legacy assets sitting in the old ../ConverterApp location AND
            // the new assets dir is empty, print a one-line hint. Manual move is safer than
            // auto-migration (permissions, symlinks, partial copies). Upgrade to a Move button
            // if this actually trips people up.
            try
            {
                const string legacyDir = "../ConverterApp";
                if (System.IO.Directory.Exists(legacyDir) &&
                    System.IO.Directory.EnumerateFiles(legacyDir, "*_oes.zip").Any() &&
                    !System.IO.Directory.EnumerateFiles(settings.ConvertedAssetsPath, "*_oes.zip").Any())
                {
                    Console.WriteLine(
                        $"[Controller] Legacy assets detected in '{legacyDir}'. " +
                        $"Move *_oes.zip into '{settings.ConvertedAssetsPath}' or re-run Decode from the main menu.");
                }
            }
            catch
            {
                // Hint is best-effort — never let it interfere with startup.
            }

            if (!string.IsNullOrEmpty(settings.Database?.Server) &&
                !string.IsNullOrEmpty(settings.Database?.Database))
            {
                DbFactory = new MySqlConnectionFactory(settings.Database);
            }

            Engine.UpdateFrame += (s, e) =>
            {
                Views.ForEach(view => view.Update(Engine.Gui));
                UpdateSpawnMarkers();
                UpdatePathGrids();
                UpdateZonePoints();
                FlushBufferIfNeeded();
            };
            modelSelector = new ModelSelector(Engine, CharacterModels);
            modelSelector.EditModeEnabled = Settings.EditModeEnabled;
            Engine.Controller = this;

            // Forward model selection to SpawnManager.
            modelSelector.OnSelectionChanged += SpawnManager.Select;

            // Keep ModelSelector in sync with the mode toggle.
            EditModeChanged += enabled => modelSelector.EditModeEnabled = enabled;

            // Turn a completed drag into a SpawnMoveAction and apply it. In 5.4 this will
            // also push onto the undo stack.
            modelSelector.OnDragCompleted += HandleDragCompleted;

            // Waypoint editor mirrors the same wiring.
            Engine.WaypointEditor.EditModeEnabled = Settings.EditModeEnabled;
            EditModeChanged += enabled => Engine.WaypointEditor.EditModeEnabled = enabled;
            Engine.WaypointEditor.OnDragCompleted += HandleWaypointDragCompleted;

            // Zone-point editor mirrors the same wiring pattern.
            Engine.ZonePointEditor.EditModeEnabled = Settings.EditModeEnabled;
            EditModeChanged += enabled => Engine.ZonePointEditor.EditModeEnabled = enabled;
            Engine.ZonePointEditor.OnZonePointClicked += HandleZonePointClicked;
            Engine.ZonePointEditor.OnDragCompleted += HandleZonePointDragCompleted;
            // Lookup delegate: hands the editor a live view of the row + apply callbacks.
            // The editor never touches the domain model directly — keeps EngineCore
            // agnostic of ZonePointSystem types.
            Engine.ZonePointEditor.SetLookup(id =>
            {
                var zp = ZonePointManager.AllPoints().FirstOrDefault(z => z.Row.Id == id);
                if (zp == null) return null;

                // Incoming rows: the spatial anchor in this zone is the landing pad
                // (SceneTarget), and center-drag mutates target_x/y/z — the source coord
                // lives in another zone and is meaningless to drag from here.
                if (zp.IsIncoming)
                {
                    return new Engine.ZonePointEditor.ZonePointDragTarget
                    {
                        Id                = zp.Row.Id,
                        SceneCenter       = zp.SceneTarget,
                        Zrange            = zp.Row.Zrange,
                        MaxZDiff          = zp.Row.MaxZDiff,
                        MinVert           = zp.Row.MinVert,
                        MaxVert           = zp.Row.MaxVert,
                        UseNewZoning      = zp.Row.UseNewZoning,
                        ApplyMove         = scenePos => { zp.SetTargetX(scenePos.Y); zp.SetTargetY(scenePos.X); zp.SetTargetZ(scenePos.Z); },
                        ApplyResize       = null,
                        ApplyPlaneBounds  = null,
                    };
                }

                return new Engine.ZonePointEditor.ZonePointDragTarget
                {
                    Id                = zp.Row.Id,
                    SceneCenter       = zp.SceneCenter,
                    Zrange            = zp.Row.Zrange,
                    MaxZDiff          = zp.Row.MaxZDiff,
                    MinVert           = zp.Row.MinVert,
                    MaxVert           = zp.Row.MaxVert,
                    UseNewZoning      = zp.Row.UseNewZoning,
                    ApplyMove         = scenePos      => zp.MarkMoved(scenePos),
                    ApplyResize       = (zr, mz)      => zp.MarkResized(zr, mz),
                    ApplyPlaneBounds  = (min, max)    => { zp.SetMinVert(min); zp.SetMaxVert(max); },
                };
            });
        }

        void HandleZonePointClicked(int zonePointId)
        {
            ZonePointManager.Select(zonePointId);
        }

        void HandleZonePointDragCompleted(ZonePointEditor.DragResult r)
        {
            var zp = ZonePointManager.AllPoints().FirstOrDefault(z => z.Row.Id == r.ZonePointId);
            if (zp == null) return;

            // Incoming rows: only the center handle is exposed, and it moves the landing
            // coord (target_x/y/z) — not the source coord (which lives in the other zone).
            if (zp.IsIncoming)
            {
                if (r.Kind != ZonePointEditor.HandleKind.Center) return;
                if (Vector3.DistanceSquared(r.FromCenter, r.ToCenter) < 0.0001f) return;
                RecordAction(new EditSystem.ZonePointTargetMoveAction(zp, r.FromCenter, r.ToCenter));
                return;
            }

            switch (r.Kind)
            {
                case ZonePointEditor.HandleKind.Center:
                    if (Vector3.DistanceSquared(r.FromCenter, r.ToCenter) < 0.0001f) return;
                    RecordAction(new EditSystem.ZonePointMoveAction(zp, r.FromCenter, r.ToCenter));
                    break;

                case ZonePointEditor.HandleKind.CornerXmYm:
                case ZonePointEditor.HandleKind.CornerXpYm:
                case ZonePointEditor.HandleKind.CornerXpYp:
                case ZonePointEditor.HandleKind.CornerXmYp:
                case ZonePointEditor.HandleKind.FaceZm:
                case ZonePointEditor.HandleKind.FaceZp:
                    if (r.FromZrange == r.ToZrange && r.FromMaxZDiff == r.ToMaxZDiff) return;
                    RecordAction(new EditSystem.ZonePointResizeAction(zp, r.FromZrange, r.ToZrange, r.FromMaxZDiff, r.ToMaxZDiff));
                    break;

                case ZonePointEditor.HandleKind.PlaneEndMinVert:
                    if (System.Math.Abs(r.FromMinVert - r.ToMinVert) < 0.001f) return;
                    RecordAction(new EditSystem.ZonePointFieldEditAction(
                        zp, EditSystem.ZonePointFieldEditAction.Field.MinVert, r.FromMinVert, r.ToMinVert));
                    break;

                case ZonePointEditor.HandleKind.PlaneEndMaxVert:
                    if (System.Math.Abs(r.FromMaxVert - r.ToMaxVert) < 0.001f) return;
                    RecordAction(new EditSystem.ZonePointFieldEditAction(
                        zp, EditSystem.ZonePointFieldEditAction.Field.MaxVert, r.FromMaxVert, r.ToMaxVert));
                    break;
            }
        }

        void HandleWaypointDragCompleted(int gridId, int number, Vector3 fromScenePos, Vector3 toScenePos)
        {
            if (Vector3.DistanceSquared(fromScenePos, toScenePos) < 0.0001f) return;
            var action = new EditSystem.GridWaypointMoveAction(gridId, number, fromScenePos, toScenePos);
            RecordAction(action);
        }

        void HandleDragCompleted(AniModelInstance instance, Vector3 fromScenePos, Vector3 toScenePos)
        {
            // No-op if the position didn't actually change (surface-stick may snap a drag
            // back onto its starting Z — treat as no-edit).
            if (Vector3.DistanceSquared(fromScenePos, toScenePos) < 0.0001f) return;

            var sp = SpawnManager.SpawnPoints.FirstOrDefault(p => p.Model == instance);
            if (sp == null) return;

            var action = new EditSystem.SpawnMoveAction(sp, fromScenePos, toScenePos);
            RecordAction(action);
        }

        // Central intake for edit actions. Runs Apply and records for undo. Also called
        // by future action sources (rotation UI, waypoint drag).
        public void RecordAction(IEditAction action)
        {
            action.Apply(this);
            UndoStack.Record(action);
        }

        // Public wrappers for hotkey / sidebar button use. Return true if something changed.
        public bool TryUndo()
        {
            var a = UndoStack.Undo(this);
            if (a != null) Console.WriteLine($"[Undo] {a.Description}");
            return a != null;
        }

        public bool TryRedo()
        {
            var a = UndoStack.Redo(this);
            if (a != null) Console.WriteLine($"[Redo] {a.Description}");
            return a != null;
        }

        // Wired to Escape. Clears model selection (which cascades to SpawnManager via the
        // OnSelectionChanged event chain) and the waypoint sub-selection.
        public void ClearSelection()
        {
            ModelSelector?.ClearSelection();
            Engine?.WaypointEditor?.ClearSelection();
            Engine?.ZonePointEditor?.ClearSelection();
            ZonePointManager?.ClearSelection();
        }

        // Wired to F. Puts the camera face-to-face with the selected spawn using the same
        // wall-aware logic as the sidebar list's click-to-fly. Called by the F hotkey.
        public void FrameSelection()
        {
            var sp = SpawnManager.Selected;
            if (sp == null) return;

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

        // Zone-point analogue of FrameSelection — flies the camera to look at the currently
        // selected zone point's center handle from a comfortable distance. No wall-cast
        // shortening: zone-point centers can sit inside geometry (fall-through triggers) and
        // clamping to the near wall would leave the camera in the floor.
        public void FrameZonePointSelection()
        {
            var zp = ZonePointManager.Selected;
            if (zp == null) return;

            var forward = Vector3.Normalize(Vector3.Transform(new Vector3(0, 1, 0), Camera.LookRotation));
            // Incoming rows' spatial anchor is the landing pad at SceneTarget (not the
            // source coord, which is in another zone anyway).
            var anchor  = zp.IsIncoming ? zp.SceneTarget : zp.SceneCenter;
            Camera.Position = anchor - forward * 60f + new Vector3(0, 0, 20f);
            Camera.LookAt(anchor);
        }

        // Fires a Task that writes the buffer to the DB in a single transaction. The
        // sidebar's commit dialog owns the Task and polls it; this method just returns it
        // so the widget can display progress. Returns null if there's nothing to commit
        // or the DB isn't configured.
        public Task<EditSystem.EditCommitter.Result> CommitPendingChangesAsync()
        {
            if (PendingBuffer == null || PendingBuffer.IsEmpty) return null;
            if (DbFactory == null)
            {
                return Task.FromResult(new EditSystem.EditCommitter.Result
                {
                    Success = false,
                    Error   = "No database connection is configured. Open Settings to configure it."
                });
            }

            var bufferSnapshot = PendingBuffer;
            var zoneName = CurrentZoneName;
            return Task.Run(() => EditSystem.EditCommitter.CommitAsync(bufferSnapshot, zoneName, DbFactory));
        }

        // Called by the sidebar after a successful commit. Clears the buffer + undo stack
        // and drops the on-disk file. Kept separate from CommitPendingChangesAsync so the
        // sidebar can show the result dialog before wiping state.
        public void OnCommitSucceeded()
        {
            OnCommitSucceeded(null);
        }

        // Overload that also stitches DB-assigned AUTO_INCREMENT ids back onto pending-
        // insert ZonePoints (whose Row.Id was previously a negative temp id). Rows the
        // user was editing keep their (now-real) identity, so undo history for post-
        // commit field-edits remains coherent. Also drops any deleted rows from the
        // in-memory manager list.
        public void OnCommitSucceeded(EditSystem.EditCommitter.Result result)
        {
            if (PendingBuffer != null && !string.IsNullOrEmpty(PendingBuffer.Zone))
                EditSystem.EditBufferManager.DeleteForZone(PendingBuffer.Zone);

            if (result != null)
            {
                if (result.InsertedIdMap != null)
                {
                    foreach (var kv in result.InsertedIdMap)
                    {
                        var zp = ZonePointManager.ZonePoints.FirstOrDefault(z => z.Row.Id == kv.Key);
                        if (zp != null) zp.Row.Id = kv.Value;
                    }
                }
                // Deletes were already applied in-memory when the DeleteAction ran; no
                // scene mutation needed here.

                // Spawn inserts: remap the negative temp spawn2 ids on in-memory
                // SpawnPoints (and their SpawnRecord.Spawn.Id) to the AUTO_INCREMENT ids
                // returned by the commit. Also stamps the resolved spawngroup id back on
                // Spawn.SpawnGroupId so any post-commit UPDATE (a subsequent move) targets
                // the correct row. Undo entries for post-commit edits stay coherent
                // because they look up by the (now-real) id.
                if (result.SpawnInsertedIdMap != null)
                {
                    foreach (var kv in result.SpawnInsertedIdMap)
                    {
                        var sp = SpawnManager.SpawnPoints.FirstOrDefault(p => p.Record.Spawn.Id == kv.Key);
                        if (sp == null) continue;
                        sp.Record.Spawn.Id = kv.Value.SpawnId;
                        sp.Record.Spawn.SpawnGroupId = kv.Value.SpawnGroupId;
                        // Update child spawnentry rows' SpawnGroupId too — kept in sync
                        // so the sidebar's "Spawngroup entries" list reflects the
                        // real group id post-commit.
                        foreach (var e in sp.Record.Entries)
                            e.Entry.SpawnGroupId = kv.Value.SpawnGroupId;
                    }
                }

                // Grid inserts get real ids assigned during commit. Simpler than remapping
                // every ZoneGridRecord/waypoint that referenced a temp id: reload the whole
                // Grid List from DB. Cheap (small table), keeps the Grid List and any
                // subsequent selection coherent. Clear any dangling temp-id selection.
                if (result.GridInsertsWritten > 0)
                {
                    if (SelectedGridId.HasValue && SelectedGridId.Value < 0)
                    {
                        SelectedGridId = null;
                        GridSelectedChanged?.Invoke(null);
                    }
                    LoadZoneGridsSync(CurrentZoneName);
                }
            }

            // Hidden SpawnPoints held pending-delete rows for revert. After a successful
            // commit those rows are gone from the DB, so drop the in-memory parked copies
            // to release the AniModelInstance references (the underlying AniModel stays
            // shared in _modelCache regardless).
            SpawnManager.HiddenSpawnPoints.Clear();

            PendingBuffer = new EditBuffer
            {
                Zone       = CurrentZoneName,
                CreatedAt  = DateTime.UtcNow,
            };
            _bufferDirty = false;
            UndoStack.Clear();
            BufferChanged?.Invoke();
        }

        public void AddView(BaseView view)
        {
            Views.Add(view);
            view.Setup(Engine.Gui);
        }

        public void RemoveView(BaseView view)
        {
            Views.Remove(view);
            view.Teardown(Engine.Gui);
        }

        public void LoadZone(string name)
        {
            CurrentZoneName = name;
            Loader.LoadZoneFile(System.IO.Path.Combine(ConvertedAssetsDir, $"{name}_oes.zip"), Engine);
            Engine.RebuildCollision();
            // Start with a fresh buffer — the state-machine phase CheckRecovery may replace
            // it with one loaded from disk after prompting the user.
            PendingBuffer = new EditBuffer { Zone = name, CreatedAt = DateTime.UtcNow };
            _bufferDirty = false;
            ZoneChanged?.Invoke(name);
        }

        // Tears down the current zone's scene state so a new zone can be loaded on top.
        // Safe to call when nothing is loaded. Ensures any pending buffer edits are flushed
        // to disk before we drop the reference — protects against losing work on F10.
        public void ClearCurrentZone()
        {
            // Snapshot the current zone's state BEFORE tearing down so a later re-visit
            // can skip the DB round-trips and restore the camera pose. Rows are only
            // captured when the buffer is empty — otherwise the in-memory rows have been
            // mutated relative to DB and re-feeding them would leak edits across reload.
            if (!string.IsNullOrEmpty(CurrentZoneName))
                CaptureZoneSnapshot(CurrentZoneName);

            if (_bufferDirty && PendingBuffer != null && !PendingBuffer.IsEmpty)
                EditBufferManager.SaveForZone(PendingBuffer);
            _bufferDirty = false;
            PendingBuffer = null;
            UndoStack.Clear();

            // Drop selections BEFORE the SpawnPoints list is emptied. Otherwise SpawnManager.
            // Selected keeps a dangling reference and UpdatePathGrids happily renders the
            // dead record's waypoints against the black post-clear scene until a new zone
            // loads. Same story for WaypointEditor's selected handle.
            modelSelector?.ClearSelection();
            Engine.WaypointEditor?.ClearSelection();
            Engine.WaypointEditor?.SetCandidates(System.Array.Empty<Engine.WaypointEditor.Handle>());
            Engine.ZonePointEditor?.SetCandidates(System.Array.Empty<Engine.ZonePointEditor.Handle>());
            // Belt-and-suspenders: some code paths (Escape from the sidebar list) don't
            // route through ModelSelector, so hard-null SpawnManager.Selected too.
            SpawnManager.Select(null);

            // Empty every dynamic line/tri VBO the renderer owns so the last frame's
            // primitives don't render on top of the cleared scene. UpdatePathGrids /
            // UpdateSpawnMarkers / UpdateZonePoints would zero these next frame anyway,
            // but a stale render can flash between clear and next frame boot.
            var emptyLines = System.Array.Empty<(Vector3, Vector3, Vector4)>();
            var emptyTris  = System.Array.Empty<(Vector3, Vector3, Vector3, Vector4)>();
            Engine.SetSpawnMarkerLines(emptyLines);
            Engine.SetPathGridLines(emptyLines);
            Engine.SetZonePointPrimitives(emptyLines, emptyTris);

            Engine.ClearScene();
            CharacterModels.Clear();
            _modelCache.Clear();
            _availableModels = new Dictionary<string, string>();
            LastModelLoaded = null;
            SpawnManager.SpawnPoints.Clear();
            SpawnManager.HiddenSpawnPoints.Clear();
            ZonePointManager.Clear();
            ZoneShortNames.Clear();
            ZoneGrids.Clear();
            CurrentZoneId = null;
            _nextTempGridId = -1;
            if (SelectedGridId != null)
            {
                SelectedGridId = null;
                GridSelectedChanged?.Invoke(null);
            }
            CurrentZoneName = null;
            ZoneChanged?.Invoke(null);
        }

        // Captures the current zone's state into _zoneSnapshots so a re-visit can skip DB
        // work + restore the camera pose. Camera pose is always captured; rows only when
        // PendingBuffer is empty (guards against caching mutated-relative-to-DB rows).
        void CaptureZoneSnapshot(string zoneName)
        {
            var snap = new ZoneSnapshot
            {
                CameraPosition = Camera.Position,
                CameraPitch    = Camera.Pitch,
                CameraYaw      = Camera.Yaw,
            };

            var bufferEmpty = PendingBuffer == null || PendingBuffer.IsEmpty;
            if (bufferEmpty)
            {
                // SpawnPoints + HiddenSpawnPoints together = every SpawnRecord ever fetched
                // for this zone. Both are needed so a re-visit rebuilds any pending-delete
                // rows too (their delete lives in the buffer, but the buffer is empty here
                // so there are no hidden points to begin with — kept for symmetry).
                var records = SpawnManager.SpawnPoints.Select(sp => sp.Record).ToList();
                records.AddRange(SpawnManager.HiddenSpawnPoints.Values.Select(sp => sp.Record));
                snap.SpawnRecords    = records;
                snap.AvailableModels = new Dictionary<string, string>(_availableModels);
                snap.ZoneGrids       = ZoneGrids.ToList();
                snap.CurrentZoneId   = CurrentZoneId;
                snap.ZonePointsOutgoing = ZonePointManager.ZonePoints.Select(z => z.Row).ToList();
                snap.ZonePointsIncoming = ZonePointManager.IncomingPoints.Select(z => z.Row).ToList();
                snap.PeerZoneRows       = new Dictionary<string, List<Database.Models.TrilogyZonePoint>>(
                    ZonePointManager.PeerZoneRows, StringComparer.OrdinalIgnoreCase);
            }

            _zoneSnapshots[zoneName] = snap;
        }

        // Restores the camera pose from a prior snapshot for zoneName. Returns true iff
        // a snapshot existed. MainMenuView calls this in place of the (0,0,1000) default
        // reset so re-visiting a zone lands the user back at their last viewpoint.
        public bool TryRestoreCameraForZone(string zoneName)
        {
            if (string.IsNullOrEmpty(zoneName)) return false;
            if (!_zoneSnapshots.TryGetValue(zoneName, out var snap)) return false;
            Camera.SetPose(snap.CameraPosition, snap.CameraPitch, snap.CameraYaw);
            return true;
        }

        // App-exit hook. Called from EngineCore.OnUnload while the GL context is still
        // current. Two responsibilities:
        //  1. Flush any pending edit buffer immediately (bypass the 500 ms debounce) so
        //     an Alt-F4 within a debounce window doesn't drop the last few edits.
        //  2. Drain the session-lifetime GL caches synchronously — otherwise thousands
        //     of Buffer/Vao/Texture finalizers pile up on the finalizer thread at
        //     process teardown with no current GL context, which is where the multi-
        //     second exit stall (worst on Parallels/ARM64) was coming from.
        public void Shutdown()
        {
            try
            {
                if (_bufferDirty && PendingBuffer != null && !PendingBuffer.IsEmpty)
                    EditBufferManager.SaveForZone(PendingBuffer);
                _bufferDirty = false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Controller.Shutdown] buffer flush failed: {ex.Message}");
            }

            _modelCache.Clear();
            _zoneSnapshots.Clear();
            _cachedZoneShortNames = null;

            try
            {
                Loader.ClearAllCaches();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Controller.Shutdown] loader cache clear failed: {ex.Message}");
            }
        }

        // Loads a zone by name after the engine has already started. Clears any previously
        // loaded zone, loads geometry + default characters + spawns, and repositions the camera.
        // Fully synchronous — Mesh/AnimatedMesh constructors upload buffers to GL immediately,
        // so every step here MUST run on the GL thread. Callers block briefly while it runs.
        public void LoadZoneFromMenu(string name)
        {
            if (string.IsNullOrEmpty(name)) return;

            ClearCurrentZone();

            LoadZone(name);

            // Pick a default character model for the fallback slot used by SpawnManager.
            try
            {
                LoadDefaultCharacterForZone(name);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Controller] Default character load failed: {ex.Message}");
            }

            Camera.Position = new Vector3(0, 0, 1000);

            LoadZoneSpawnsSync(name);
            LoadZonePointsSync(name);
        }

        // Fetches trilogy_zone_points for the zone and hands them to ZonePointManager.
        // No GL work — safe to call on any thread, but we call it on the GL thread for
        // consistency with the sibling spawn-load call (§7 GL-thread rule in CLAUDE.md).
        public void LoadZonePointsSync(string zoneName)
        {
            if (DbFactory == null)
            {
                Console.WriteLine("[Controller] Zone point load skipped — no DB connection.");
                return;
            }

            // Snapshot hit — feed cached rows straight into the managers and skip the
            // 3 zone-point queries. Shortnames use the session-wide cache (below) since
            // they're global-to-DB, not per-zone.
            if (_zoneSnapshots.TryGetValue(zoneName, out var snap) && snap.HasRowData)
            {
                ZonePointManager.LoadFromRows(snap.ZonePointsOutgoing);
                ZonePointManager.LoadIncomingFromRows(snap.ZonePointsIncoming);
                foreach (var kv in snap.PeerZoneRows)
                    foreach (var row in kv.Value)
                    {
                        if (!ZonePointManager.PeerZoneRows.TryGetValue(kv.Key, out var list))
                        {
                            list = new List<Database.Models.TrilogyZonePoint>();
                            ZonePointManager.PeerZoneRows[kv.Key] = list;
                        }
                        list.Add(row);
                    }
                if (_cachedZoneShortNames != null && ZoneShortNames.Count == 0)
                    ZoneShortNames.AddRange(_cachedZoneShortNames);
                Console.WriteLine($"[Controller] Zone point cache hit for '{zoneName}' — skipped DB.");
                return;
            }

            try
            {
                var repo = new ZonePointRepository(DbFactory);
                var rows = repo.GetZonePointsAsync(zoneName).GetAwaiter().GetResult();
                ZonePointManager.LoadFromRows(rows);

                // Incoming rows — foreign zones that land in this zone. Shown as landing
                // pads + heading arrows so users can see where arriving players face.
                var incoming = repo.GetIncomingZonePointsAsync(zoneName).GetAwaiter().GetResult();
                ZonePointManager.LoadIncomingFromRows(incoming);

                // Peer-zone rows — every row in each destination zone reached by an
                // owned outgoing row. Used only by the sandwich detector.
                var destinations = ZonePointManager.ZonePoints
                    .Select(z => z.Row.TargetZone)
                    .Where(t => !string.IsNullOrEmpty(t)
                                && !string.Equals(t, zoneName, StringComparison.OrdinalIgnoreCase))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
                if (destinations.Count > 0)
                {
                    var peers = repo.GetZonePointsForZonesAsync(destinations).GetAwaiter().GetResult();
                    ZonePointManager.LoadPeerRows(peers);
                    Console.WriteLine($"[Controller] Loaded peer-zone rows for {ZonePointManager.PeerZoneRows.Count} destination zones (sandwich detector).");
                }

                // Populate the dropdown-backing list once per zone load. Cached because
                // it's used every inspector-render frame. Session-wide _cachedZoneShortNames
                // survives F10 → new zone so subsequent zones skip the query too.
                if (ZoneShortNames.Count == 0)
                {
                    if (_cachedZoneShortNames != null)
                    {
                        ZoneShortNames.AddRange(_cachedZoneShortNames);
                    }
                    else
                    {
                        var shortnames = repo.GetAllZoneShortNamesAsync().GetAwaiter().GetResult()
                            .Where(s => !string.IsNullOrEmpty(s)).ToList();
                        ZoneShortNames.AddRange(shortnames);
                        _cachedZoneShortNames = shortnames;
                        Console.WriteLine($"[Controller] Cached {ZoneShortNames.Count} zone shortnames for target dropdown.");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Controller] Zone point load error: {ex}");
            }
        }

        // Creates a new trilogy_zone_points row at the camera's current ground projection
        // (X/Y from Camera.Position, Z from a downward collision probe so it lands on the
        // floor). Wraps in a ZonePointInsertAction so the create is undoable, selects the
        // new row so the inspector focuses it, and flies the camera to give the user a
        // visual anchor.
        public void CreateZonePoint(byte useNewZoning)
        {
            if (CurrentZoneName == null) return;

            // Scene camera position → DB coords (swap XY). Ground-snap Z so the new box
            // isn't floating.
            var cam = Camera.Position;
            float dbX = cam.Y;
            float dbY = cam.X;
            float dbZ = cam.Z;
            if (Collider != null)
            {
                var probe = new Vector3(cam.X, cam.Y, cam.Z + 5f);
                var hit = Collider.FindIntersection(probe, new Vector3(0, 0, -1), 0.5f);
                if (hit.HasValue) dbZ = hit.Value.Item2.Z + 5f;
            }

            var row = new Database.Models.TrilogyZonePoint
            {
                Id           = ZonePointManager.NextTempId(),
                Zone         = CurrentZoneName,
                X            = dbX,
                Y            = dbY,
                Z            = dbZ,
                Heading      = 0f,
                TargetZone   = ZoneShortNames.FirstOrDefault(z =>
                                    !string.Equals(z, CurrentZoneName, StringComparison.OrdinalIgnoreCase))
                                ?? CurrentZoneName,
                TargetX      = 0f,
                TargetY      = 0f,
                TargetZ      = 0f,
                Zrange       = 15,
                MaxZDiff     = 0,
                UseNewZoning = useNewZoning,
                MinVert      = 0f,
                MaxVert      = 0f,
                CenterPoint  = 0f,
                KeepX        = 0,
                KeepY        = 0,
                KeepZ        = 0,
                ToZoneId     = 0,
            };

            RecordAction(new EditSystem.ZonePointInsertAction(row));

            // Select the fresh row so the inspector focuses on it and the user sees a
            // handle to tune.
            var created = ZonePointManager.ZonePoints.FirstOrDefault(z => z.Row.Id == row.Id);
            if (created != null) ZonePointManager.Select(created);
        }

        // Grid Mode double-click handler — called by EngineCore when the user double-
        // clicks a collision surface with Grid Mode active. Target grid resolution:
        //   1. If SelectedGridId is set → append waypoint there (sidebar-picked grid).
        //   2. Else if a waypoint is selected in the WaypointEditor → target its grid.
        //   3. Else → create a new grid + first waypoint at the hit point.
        // hitPoint is scene-space (x=east, y=north-in-scene, z=up); the DB swap happens
        // here so downstream code always sees DB coords.
        public void OnGridModeDoubleClick(Vector3 hitPoint)
        {
            if (CurrentZoneName == null || CurrentZoneId == null)
            {
                Console.WriteLine("[Controller] Grid Mode double-click ignored — no zone/zoneId.");
                return;
            }

            var dbX = hitPoint.Y;
            var dbY = hitPoint.X;
            var dbZ = hitPoint.Z;

            var targetGridId = SelectedGridId;
            if (targetGridId == null)
            {
                var wp = Engine.WaypointEditor?.Selected;
                if (wp.HasValue) targetGridId = wp.Value.GridId;
            }

            if (targetGridId.HasValue)
            {
                // Compute next waypoint number for the target grid (scan both SpawnPoints
                // and ZoneGrids — matches RenderWaypointAddButton's behaviour).
                var gridId = targetGridId.Value;
                int maxNumber = 0;
                foreach (var sp in SpawnManager.SpawnPoints)
                    foreach (var w in sp.Record.Waypoints)
                        if (w.GridId == gridId && w.Number > maxNumber)
                            maxNumber = w.Number;
                foreach (var zg in ZoneGrids)
                    foreach (var w in zg.Waypoints)
                        if (w.GridId == gridId && w.Number > maxNumber)
                            maxNumber = w.Number;

                RecordAction(new EditSystem.GridEntryInsertAction(
                    gridId, CurrentZoneId.Value, maxNumber + 1,
                    (float)dbX, (float)dbY, (float)dbZ,
                    heading: 0f, pause: 0, centerpoint: 0));
                return;
            }

            // No grid targeted → create a fresh one at the hit point.
            var tempId = NextTempGridId();
            RecordAction(new EditSystem.GridInsertAction(
                tempId, CurrentZoneId.Value,
                type: 0, type2: 0,
                seedX: (float)dbX, seedY: (float)dbY, seedZ: (float)dbZ));
        }

        // Grid List sidebar entry point — creates a fresh grid with one seed waypoint at
        // the camera XY (ground-snapped Z). Defaults: type=0 (Circular), type2=0 (Half-
        // random pause) — the two most common EQEmu wander settings. User can edit via
        // the Grid Info combos in the Waypoint Info section post-creation. No-op if no
        // zone is loaded, no DB connection, or the zone's numeric id wasn't resolved.
        public void CreateNewGridAtCamera()
        {
            if (CurrentZoneName == null || CurrentZoneId == null)
            {
                Console.WriteLine("[Controller] CreateNewGridAtCamera skipped — no zone loaded or CurrentZoneId unresolved.");
                return;
            }

            // Scene camera position → DB coords (swap XY). Ground-snap Z so the seed
            // waypoint sits on terrain, not floating.
            var cam = Camera.Position;
            float dbX = cam.Y;
            float dbY = cam.X;
            float dbZ = cam.Z;
            if (Collider != null)
            {
                var probe = new Vector3(cam.X, cam.Y, cam.Z + 5f);
                var hit = Collider.FindIntersection(probe, new Vector3(0, 0, -1), 0.5f);
                if (hit.HasValue) dbZ = hit.Value.Item2.Z;
            }

            var tempId = NextTempGridId();
            RecordAction(new EditSystem.GridInsertAction(
                tempId, CurrentZoneId.Value,
                type: 0, type2: 0,
                seedX: dbX, seedY: dbY, seedZ: dbZ));
        }

        // Removes the currently-selected zone_point (persisted rows go to buffer.Deletes,
        // pending-insert rows get their Insert entry dropped). No-op when nothing is
        // selected.
        public void DeleteSelectedZonePoint()
        {
            var zp = ZonePointManager.Selected;
            if (zp == null) return;
            RecordAction(new EditSystem.ZonePointDeleteAction(zp));
        }

        // Marks the currently-selected spawn as pending-delete. Gated on edit mode: same
        // convention as move/rotate actions so the Delete key doesn't fire in read-only
        // sessions. No-op when nothing is selected or the spawn is already pending-delete.
        // Wired to the Delete key by EngineCore.OnKeyDown.
        public void DeleteSelectedSpawn()
        {
            if (!EditModeEnabled) return;
            var sp = SpawnManager.Selected;
            if (sp == null) return;
            // Idempotency: if this id is already in the buffer, don't push a duplicate action.
            if (PendingBuffer != null && PendingBuffer.SpawnDeletes.ContainsKey(sp.Record.Spawn.Id)) return;

            // Pending-insert path: pass the current SpawnInsert entry so Revert can put
            // it back verbatim. Detected by the negative-id convention (real spawn2 ids
            // are always positive AUTO_INCREMENT).
            EditSystem.SpawnInsert insertSnapshot = null;
            if (sp.Record.Spawn.Id < 0 && PendingBuffer != null)
            {
                PendingBuffer.SpawnInserts.TryGetValue(sp.Record.Spawn.Id, out insertSnapshot);
            }
            RecordAction(new EditSystem.SpawnDeleteAction(sp, insertSnapshot));
        }

        // Detaches a spawn from every scene register (SpawnManager visible list, engine
        // model list, character-models pick list). Called by SpawnDeleteAction.Apply and
        // by ApplyPendingBuffer during session recovery. The SpawnPoint itself is parked
        // in SpawnManager.HiddenSpawnPoints so Restore can splice it back with no DB call.
        public void HideSpawnFromScene(SpawnPoint sp)
        {
            if (sp == null) return;
            Engine.Remove(sp.Model);
            CharacterModels.Remove(sp.Model);
            SpawnManager.Hide(sp);
        }

        // Inverse of HideSpawnFromScene — pulls the parked SpawnPoint out of
        // HiddenSpawnPoints and re-registers it with the engine + selector list. No-op
        // when the id isn't hidden (idempotent).
        public void RestoreSpawnToScene(int spawnId)
        {
            var sp = SpawnManager.Restore(spawnId);
            if (sp == null) return;
            Engine.Add(sp.Model);
            CharacterModels.Add(sp.Model);
        }

        // ─── Slice 3: place-new-spawn from terrain via NPC picker ────────────────────

        // Scene-space hit point captured when the user Ctrl+double-clicks terrain.
        // Held so the NPC picker modal (opened via PendingPlacementRequested) can
        // fetch it on Confirm. Null when no placement is pending.
        public Vector3? PendingPlacementScenePos { get; private set; }

        // Fires when the user Ctrl+double-clicks terrain (and edit mode is on + DB is
        // configured). SidebarView subscribes to open the NPC picker modal. The event
        // carries no payload — the sidebar reads PendingPlacementScenePos on demand.
        public event Action PendingPlacementRequested;

        // Called by EngineCore.OnMouseDown when the user Ctrl+double-clicks a
        // terrain hit. Gated on edit mode + DB configured + availableModels ready.
        // Records the scene hit and lets the sidebar open its picker.
        public void OnCtrlDoubleClickTerrain(Vector3 sceneHitPos)
        {
            if (!EditModeEnabled) return;
            if (CurrentZoneName == null) return;
            if (DbFactory == null)
            {
                Console.WriteLine("[Controller] Ctrl+double-click ignored — no DB connection (open Settings to configure).");
                return;
            }
            if (_availableModels == null || _availableModels.Count == 0)
            {
                Console.WriteLine("[Controller] Ctrl+double-click ignored — availableModels not yet populated.");
                return;
            }
            PendingPlacementScenePos = sceneHitPos;
            PendingPlacementRequested?.Invoke();
        }

        // Cleared when the picker modal is dismissed (either confirmed or cancelled).
        public void ClearPendingPlacement() => PendingPlacementScenePos = null;

        // Places a fresh spawn at `sceneHitPos` for the picked NPC. Builds a new
        // spawngroup + one spawnentry (100% chance) + spawn2 row, wraps it in a
        // SpawnInsertAction so the commit path + undo stack treat it identically to
        // a duplicated spawn. Called by the NPC picker's Confirm button.
        public void PlaceNewSpawn(Database.Models.NpcType npc, Vector3 sceneHitPos)
        {
            if (!EditModeEnabled) return;
            if (npc == null) return;
            if (CurrentZoneName == null) return;
            if (_availableModels == null || _availableModels.Count == 0) return;

            // Scene → DB coord swap (X = east/west, Y = north/south, Z = up).
            float dbX = sceneHitPos.Y;
            float dbY = sceneHitPos.X;
            float dbZ = sceneHitPos.Z;

            var tempId = SpawnManager.NextTempSpawnId();

            // Spawngroup name — no source to clone from, so use "vqp_<npcname>_<hex>"
            // as a discoverable prefix. UNIQUE-safe within a ~1/65k Guid-suffix window
            // (same reasoning as DuplicateSelectedSpawn); on collision the commit
            // rolls back and the user retries.
            var baseName = (npc.Name ?? "npc").Trim();
            var suffix = "_new" + Guid.NewGuid().ToString("N").Substring(0, 4);
            const int maxLen = 30;
            var prefixMax = maxLen - "vqp_".Length - suffix.Length;
            if (prefixMax < 1) prefixMax = 1;
            var prefix = baseName.Length > prefixMax ? baseName.Substring(0, prefixMax) : baseName;
            var newGroupName = "vqp_" + prefix + suffix;

            var record = new Database.Models.SpawnRecord
            {
                Spawn = new Database.Models.Spawn2
                {
                    Id             = tempId,
                    SpawnGroupId   = 0,        // resolved at commit
                    Zone           = CurrentZoneName,
                    Version        = 0,        // most spawn2 rows use version 0 in EQEmu classic data
                    X              = dbX,
                    Y              = dbY,
                    Z              = dbZ,
                    Heading        = 0f,       // face north; user can rotate afterwards
                    RespawnTime    = 600,      // 10 min — reasonable default per EQEmu convention
                    Variance       = 0,
                    PathGrid       = 0,        // no path — user can assign later
                    Animation      = 0,
                    SpawnGroupName = newGroupName,
                },
                Entries = new System.Collections.Generic.List<Database.Models.SpawnEntryWithNpc>
                {
                    new Database.Models.SpawnEntryWithNpc
                    {
                        Entry = new Database.Models.SpawnEntry
                        {
                            SpawnGroupId = 0,  // resolved at commit
                            NpcId        = npc.Id,
                            Chance       = 100,
                        },
                        Npc = npc,
                    }
                },
                Waypoints = new System.Collections.Generic.List<Database.Models.GridEntry>(),
                Grid = null,
            };

            var insert = new EditSystem.SpawnInsert
            {
                TempSpawnId    = tempId,
                SourceSpawnId  = 0, // no source — placed from picker
                DisplayName    = npc.Name ?? "?",
                SpawnGroupName = newGroupName,
                Zone           = record.Spawn.Zone,
                Version        = record.Spawn.Version,
                X              = record.Spawn.X,
                Y              = record.Spawn.Y,
                Z              = record.Spawn.Z,
                Heading        = record.Spawn.Heading,
                RespawnTime    = record.Spawn.RespawnTime,
                Variance       = record.Spawn.Variance,
                PathGrid       = record.Spawn.PathGrid,
                Animation      = record.Spawn.Animation,
                Entries        = new System.Collections.Generic.List<EditSystem.SpawnInsertEntry>
                {
                    new EditSystem.SpawnInsertEntry { NpcId = npc.Id, Chance = 100 }
                },
                CreatedAt      = DateTime.UtcNow,
            };

            RecordAction(new EditSystem.SpawnInsertAction(record, insert));

            var created = SpawnManager.SpawnPoints.FirstOrDefault(p => p.Record.Spawn.Id == tempId);
            if (created != null) SpawnManager.Select(created.Model);
        }

        // Duplicates the currently-selected spawn: clones spawngroup + spawnentries +
        // spawn2 into a pending-insert SpawnPoint at the camera's ground-projected
        // position (matches the CreateNewGridAtCamera / CreateZonePoint convention).
        // Gated on edit mode + a selection existing + at least one available model
        // (otherwise even the fallback can't render). Wired to Ctrl+D + sidebar button.
        public void DuplicateSelectedSpawn()
        {
            if (!EditModeEnabled) return;
            var src = SpawnManager.Selected;
            if (src == null) return;
            if (_availableModels == null || _availableModels.Count == 0)
            {
                Console.WriteLine("[Controller] Duplicate skipped — availableModels not yet populated (no zone load?).");
                return;
            }

            // Near-source placement — 5 units offset from the source in the horizontal
            // direction of the camera (so the duplicate is always inside the camera's
            // line of sight to the source). Keeps the source's Z, which is a known-good
            // ground level. Reasoning: camera-anchor placement was cumbersome (WASD-only
            // navigation), and a downward-probe from camera Z lands inside geometry in
            // small zones like cshome.
            var srcScene = src.Model.Position;
            var camScene = Camera.Position;
            var dx = camScene.X - srcScene.X;
            var dy = camScene.Y - srcScene.Y;
            var lenSq = dx * dx + dy * dy;
            Vector3 sceneOffset;
            if (lenSq > 0.001f)
            {
                var invLen = 1f / System.MathF.Sqrt(lenSq);
                sceneOffset = new Vector3(dx * invLen * 5f, dy * invLen * 5f, 0f);
            }
            else
            {
                sceneOffset = new Vector3(5f, 0f, 0f); // camera directly above source
            }
            var newScene = srcScene + sceneOffset;

            // Scene → DB coord swap (X = east/west, Y = north/south, Z = up).
            float dbX = newScene.Y;
            float dbY = newScene.X;
            float dbZ = newScene.Z;

            // Fresh negative temp id — never collides with real spawn2 ids
            // (AUTO_INCREMENT is always positive) and never with other temp ids in-session.
            var tempId = SpawnManager.NextTempSpawnId();

            // spawngroup name has a UNIQUE constraint. Cloning the source's name
            // verbatim would guarantee a commit failure. Truncate source name + suffix
            // with 4 hex digits from a fresh Guid (~1-in-65k collision within the same
            // spawngroup name prefix — accept the retry cost on the extremely rare hit).
            var sourceGroupName = src.Record.Spawn.SpawnGroupName ?? "spawngroup";
            var suffix = "_dup" + Guid.NewGuid().ToString("N").Substring(0, 4);
            const int maxLen = 30;
            var prefix = sourceGroupName;
            if (prefix.Length + suffix.Length > maxLen)
                prefix = prefix.Substring(0, maxLen - suffix.Length);
            var newGroupName = prefix + suffix;

            // Build the cloned SpawnRecord. Position uses camera anchor; heading + other
            // fields copied verbatim so the duplicate behaves identically until edited.
            var cloned = new Database.Models.SpawnRecord
            {
                Spawn = new Database.Models.Spawn2
                {
                    Id             = tempId,
                    SpawnGroupId   = 0,  // resolved at commit time
                    Zone           = src.Record.Spawn.Zone,
                    Version        = src.Record.Spawn.Version,
                    X              = dbX,
                    Y              = dbY,
                    Z              = dbZ,
                    Heading        = src.Record.Spawn.Heading,
                    RespawnTime    = src.Record.Spawn.RespawnTime,
                    Variance       = src.Record.Spawn.Variance,
                    PathGrid       = src.Record.Spawn.PathGrid,
                    Animation      = src.Record.Spawn.Animation,
                    SpawnGroupName = newGroupName,
                },
                Entries = src.Record.Entries
                    .Select(e => new Database.Models.SpawnEntryWithNpc
                    {
                        Entry = new Database.Models.SpawnEntry
                        {
                            SpawnGroupId = 0, // resolved at commit time
                            NpcId        = e.Entry.NpcId,
                            Chance       = e.Entry.Chance,
                        },
                        Npc = e.Npc, // shared — read-only for rendering
                    })
                    .ToList(),
                Waypoints = new System.Collections.Generic.List<Database.Models.GridEntry>(),
                Grid = null,
            };

            var insert = new EditSystem.SpawnInsert
            {
                TempSpawnId    = tempId,
                SourceSpawnId  = src.Record.Spawn.Id,
                DisplayName    = cloned.Entries.OrderByDescending(e => e.Entry.Chance).FirstOrDefault()?.Npc?.Name ?? "?",
                SpawnGroupName = newGroupName,
                Zone           = cloned.Spawn.Zone,
                Version        = cloned.Spawn.Version,
                X              = cloned.Spawn.X,
                Y              = cloned.Spawn.Y,
                Z              = cloned.Spawn.Z,
                Heading        = cloned.Spawn.Heading,
                RespawnTime    = cloned.Spawn.RespawnTime,
                Variance       = cloned.Spawn.Variance,
                PathGrid       = cloned.Spawn.PathGrid,
                Animation      = cloned.Spawn.Animation,
                Entries        = cloned.Entries
                    .Select(e => new EditSystem.SpawnInsertEntry { NpcId = e.Entry.NpcId, Chance = e.Entry.Chance })
                    .ToList(),
                CreatedAt      = DateTime.UtcNow,
            };

            RecordAction(new EditSystem.SpawnInsertAction(cloned, insert));

            // Auto-select the new spawn so the sidebar focuses on it + drag pipeline
            // grabs it on the very next left-click.
            var created = SpawnManager.SpawnPoints.FirstOrDefault(p => p.Record.Spawn.Id == tempId);
            if (created != null) SpawnManager.Select(created.Model);
        }

        // Builds the in-scene SpawnPoint for a pending-insert action's snapshot record.
        // Called by SpawnInsertAction.Apply. Uses the same model-resolution pipeline as
        // initial zone load (SpawnManager.LoadSingle) so armor/helm/face textures + race
        // scaling match. Attaches to engine + CharacterModels so the model renders and
        // is pickable.
        public void SpawnPendingInsertFromSnapshot(Database.Models.SpawnRecord record)
        {
            var sp = SpawnManager.LoadSingle(record, Engine, _modelCache, _availableModels, LastModelLoaded);
            if (sp == null)
            {
                Console.WriteLine($"[Controller] SpawnPendingInsertFromSnapshot: LoadSingle returned null (no model + no fallback) for temp #{record.Spawn.Id}");
                return;
            }
            CharacterModels.Add(sp.Model);
        }

        // Detaches a pending-insert SpawnPoint from every scene register. Different from
        // HideSpawnFromScene: no HiddenSpawnPoints stash — the row was never in the DB,
        // so if the action is later Reverted we re-Apply via ReattachPendingInsertSpawn
        // (which re-adds the same AniModelInstance).
        public void DetachPendingInsertSpawn(SpawnPoint sp)
        {
            if (sp == null) return;
            if (SpawnManager.Selected == sp)
                SpawnManager.Select(null);
            Engine.Remove(sp.Model);
            CharacterModels.Remove(sp.Model);
            SpawnManager.SpawnPoints.Remove(sp);
        }

        // Inverse of DetachPendingInsertSpawn — re-attach a previously-detached snapshot
        // node. Used by SpawnDeleteAction.Revert on the pending-insert path.
        public void ReattachPendingInsertSpawn(SpawnPoint sp)
        {
            if (sp == null) return;
            if (SpawnManager.SpawnPoints.Any(p => p.Record.Spawn.Id == sp.Record.Spawn.Id))
                return; // already attached — idempotent
            SpawnManager.SpawnPoints.Add(sp);
            Engine.Add(sp.Model);
            CharacterModels.Add(sp.Model);
        }

        // Sandwich fix: shift the landing coord (target_x/y) 50 units directly away from
        // the offending peer row's source coord. Uses ZonePointFieldEditAction on TargetX
        // + TargetY so the edit shows up as two undoable steps (fine per existing
        // convention — user hits Ctrl+Z twice to fully revert).
        public void ShiftLandingAwayFromSandwich(int zonePointId)
        {
            if (!SandwichResults.TryGetValue(zonePointId, out var r) || !r.Sandwiched) return;
            var zp = ZonePointManager.ZonePoints.FirstOrDefault(z => z.Row.Id == zonePointId);
            if (zp == null) return;

            var lx = zp.Row.TargetX;
            var ly = zp.Row.TargetY;
            var ox = r.OffendingRow.X;
            var oy = r.OffendingRow.Y;
            var dx = lx - ox;
            var dy = ly - oy;
            var len = System.MathF.Sqrt(dx * dx + dy * dy);
            if (len < 0.001f)
            {
                // Landing sits exactly on the offender — pick an arbitrary +X direction so
                // the shift is deterministic.
                dx = 1f; dy = 0f; len = 1f;
            }
            const float ShiftDistance = 50f;
            var nx = dx / len;
            var ny = dy / len;
            var newX = lx + nx * ShiftDistance;
            var newY = ly + ny * ShiftDistance;

            RecordAction(new EditSystem.ZonePointFieldEditAction(
                zp, EditSystem.ZonePointFieldEditAction.Field.TargetX, lx, newX));
            RecordAction(new EditSystem.ZonePointFieldEditAction(
                zp, EditSystem.ZonePointFieldEditAction.Field.TargetY, ly, newY));
        }

        // Preloads the placeholder-fallback model for the zone. Sets LastModelLoaded so
        // SpawnManager has something to instance for spawns whose race can't be resolved.
        // Does NOT add a visible character to the scene — that was the "phantom orc" bug.
        public void LoadDefaultCharacterForZone(string zoneName)
        {
            var candidates = new[] { $"{zoneName}_chr", "gfaydark_chr" };
            foreach (var prefix in candidates)
            {
                var path = System.IO.Path.Combine(ConvertedAssetsDir, $"{prefix}_oes.zip");
                if (!System.IO.File.Exists(path)) continue;

                var models = Loader.GetAvailableCharacterModels(path);
                if (models.Count == 0) continue;

                var pick = models.Contains("ORC") ? "ORC" : System.Linq.Enumerable.First(models);
                // Same idle-animation treatment as SpawnManager (see SpawnAnimations there).
                LastModelLoaded = Loader.LoadCharacter(path, pick, new System.Collections.Generic.HashSet<string> { "P01" }, singleFrame: true);
                _modelCache[pick] = LastModelLoaded;
                return;
            }
        }

        public void LoadCharacter(string filename, string name)
        {
            var model = LastModelLoaded = Loader.LoadCharacter(System.IO.Path.Combine(ConvertedAssetsDir, $"{filename}_oes.zip"), name);
            var instance = new AniModelInstance(model) { Animation = "C05", Position = vec3(-153, 149, 80) };
            Engine.Add(instance);
            CharacterModels.Add(instance);
        }

        // Fetches all spawn data for zoneName and places model instances in the scene.
        public async Task LoadZoneSpawnsAsync(string zoneName)
        {
            if (DbFactory == null)
            {
                Console.WriteLine("[Controller] Spawn load skipped — no DB connection.");
                return;
            }

            if (SpawnManager.SpawnPoints.Count > 0)
            {
                Console.WriteLine("[Controller] Spawns already loaded — skipping.");
                return;
            }

            try
            {
                IEnumerable<Database.Models.SpawnRecord> records;
                if (_zoneSnapshots.TryGetValue(zoneName, out var snap) && snap.HasRowData)
                {
                    records = snap.SpawnRecords;
                    _availableModels = new Dictionary<string, string>(snap.AvailableModels);
                    Console.WriteLine($"[Controller] Spawn cache hit for '{zoneName}' — skipped DB.");
                }
                else
                {
                    var repo = new SpawnRepository(DbFactory);
                    records = await repo.GetZoneSpawnsFullAsync(zoneName);
                    _availableModels = SpawnManager.BuildAvailableModels(zoneName, ConvertedAssetsDir);
                }
                SpawnManager.LoadFromRecords(records, Engine, _modelCache, _availableModels, LastModelLoaded);

                // Register spawn instances with the model selector so they are clickable.
                foreach (var sp in SpawnManager.SpawnPoints)
                    CharacterModels.Add(sp.Model);

                LoadZoneGridsSync(zoneName);
                LoadZonePointsSync(zoneName);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Controller] Spawn load error: {ex}");
            }
        }

        // Step-based spawn loading, used by the menu widget so a progress bar can animate.
        // BeginSpawnLoad → repeated ContinueSpawnLoad(chunkSize) → FinishSpawnLoad.
        List<Database.Models.SpawnRecord> _spawnLoadRecords;
        Dictionary<string, string> _spawnLoadAvailable;
        int _spawnLoadIndex;

        public int SpawnLoadTotal     => _spawnLoadRecords?.Count ?? 0;
        public int SpawnLoadProcessed => _spawnLoadIndex;
        public bool SpawnLoadDone     => _spawnLoadRecords == null || _spawnLoadIndex >= _spawnLoadRecords.Count;

        // Fetches records + available models on the calling thread. Returns false if the
        // DB isn't configured or spawns are already loaded.
        public bool BeginSpawnLoad(string zoneName)
        {
            if (DbFactory == null)
            {
                Console.WriteLine("[Controller] Spawn load skipped — no DB connection.");
                return false;
            }
            if (SpawnManager.SpawnPoints.Count > 0)
            {
                Console.WriteLine("[Controller] Spawns already loaded — skipping.");
                return false;
            }

            if (_zoneSnapshots.TryGetValue(zoneName, out var snap) && snap.HasRowData)
            {
                _spawnLoadRecords   = snap.SpawnRecords.ToList();
                _spawnLoadAvailable = new Dictionary<string, string>(snap.AvailableModels);
                Console.WriteLine($"[Controller] Spawn cache hit for '{zoneName}' — skipped DB.");
            }
            else
            {
                var repo = new SpawnRepository(DbFactory);
                _spawnLoadRecords   = repo.GetZoneSpawnsFullAsync(zoneName).GetAwaiter().GetResult().ToList();
                _spawnLoadAvailable = SpawnManager.BuildAvailableModels(zoneName, ConvertedAssetsDir);
            }
            _availableModels = _spawnLoadAvailable;
            SpawnManager.PrepareForLoad();
            _spawnLoadIndex = 0;
            return true;
        }

        // Processes up to chunkSize records from where we left off.
        public void ContinueSpawnLoad(int chunkSize)
        {
            if (SpawnLoadDone) return;
            int end = Math.Min(_spawnLoadIndex + chunkSize, _spawnLoadRecords.Count);
            SpawnManager.LoadBatch(
                _spawnLoadRecords.GetRange(_spawnLoadIndex, end - _spawnLoadIndex),
                Engine, _modelCache, _spawnLoadAvailable, LastModelLoaded);
            _spawnLoadIndex = end;
        }

        // Emits the summary log, registers spawn models with the selector, drops load state.
        public void FinishSpawnLoad()
        {
            if (_spawnLoadRecords == null) return;
            SpawnManager.FinishLoad();
            foreach (var sp in SpawnManager.SpawnPoints)
                CharacterModels.Add(sp.Model);
            _spawnLoadRecords = null;
            _spawnLoadAvailable = null;
            _spawnLoadIndex = 0;

            // Grid + zone-point tables are small (grid: dozens per zone, zone_point: dozens);
            // load synchronously right after spawns so the menu flow's Done phase sees the
            // full scene.
            LoadZoneGridsSync(CurrentZoneName);
            LoadZonePointsSync(CurrentZoneName);
        }

        // Same as LoadZoneSpawnsAsync but blocks the caller. Used by the in-app zone loader
        // so all GL-touching work (LoadFromRecords → AnimatedMesh ctors) stays on the GL thread.
        // The DB fetch's internal continuations still run on ThreadPool, but they don't touch GL.
        public void LoadZoneSpawnsSync(string zoneName)
        {
            if (DbFactory == null)
            {
                Console.WriteLine("[Controller] Spawn load skipped — no DB connection.");
                return;
            }

            if (SpawnManager.SpawnPoints.Count > 0)
            {
                Console.WriteLine("[Controller] Spawns already loaded — skipping.");
                return;
            }

            try
            {
                IEnumerable<Database.Models.SpawnRecord> records;
                if (_zoneSnapshots.TryGetValue(zoneName, out var snap) && snap.HasRowData)
                {
                    records = snap.SpawnRecords;
                    _availableModels = new Dictionary<string, string>(snap.AvailableModels);
                    Console.WriteLine($"[Controller] Spawn cache hit for '{zoneName}' — skipped DB.");
                }
                else
                {
                    var repo = new SpawnRepository(DbFactory);
                    records = repo.GetZoneSpawnsFullAsync(zoneName).GetAwaiter().GetResult();
                    _availableModels = SpawnManager.BuildAvailableModels(zoneName, ConvertedAssetsDir);
                }
                SpawnManager.LoadFromRecords(records, Engine, _modelCache, _availableModels, LastModelLoaded);

                foreach (var sp in SpawnManager.SpawnPoints)
                    CharacterModels.Add(sp.Model);

                LoadZoneGridsSync(zoneName);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Controller] Spawn load error: {ex}");
            }
        }

        // Loads every grid (with waypoints) for the zone — attached AND orphan. Runs after
        // spawns land so we can fill in each record's SpawnCount from SpawnManager.SpawnPoints
        // without a second DB round trip. No GL work; safe on any thread. Silently no-ops
        // when there's no DB connection.
        public void LoadZoneGridsSync(string zoneName)
        {
            if (DbFactory == null) return;

            // Snapshot hit — reuse cached grid records (and CurrentZoneId) so we skip the
            // GetZoneGridsAsync round-trip. SpawnCount is recomputed against the freshly-
            // loaded SpawnPoints since edits may have moved spawns between grids.
            if (_zoneSnapshots.TryGetValue(zoneName, out var snap) && snap.HasRowData)
            {
                ZoneGrids.Clear();
                foreach (var g in snap.ZoneGrids)
                {
                    g.SpawnCount = SpawnManager.SpawnPoints.Count(sp => sp.Record.Spawn.PathGrid == g.Grid.Id);
                    ZoneGrids.Add(g);
                }
                CurrentZoneId = snap.CurrentZoneId;
                Console.WriteLine($"[Controller] Zone grid cache hit for '{zoneName}' — skipped DB.");
                return;
            }

            try
            {
                var repo = new SpawnRepository(DbFactory);
                var records = repo.GetZoneGridsAsync(zoneName).GetAwaiter().GetResult().ToList();

                ZoneGrids.Clear();
                foreach (var g in records)
                {
                    g.SpawnCount = SpawnManager.SpawnPoints.Count(sp => sp.Record.Spawn.PathGrid == g.Grid.Id);
                    ZoneGrids.Add(g);
                }

                // Cache CurrentZoneId for GridInsertAction. First prefer any loaded
                // grid's ZoneId (free — no extra round-trip); fall back to a direct
                // GetZoneId query for the edge case where the zone has zero grids.
                if (records.Count > 0)
                    CurrentZoneId = records[0].Grid.ZoneId;
                else
                {
                    using (var conn = DbFactory.CreateConnection())
                    {
                        conn.Open();
                        var zid = Dapper.SqlMapper.QueryFirstOrDefault<int>(
                            conn, Database.Constants.SqlQueries.GetZoneId,
                            new { ZoneName = zoneName });
                        CurrentZoneId = zid == 0 ? (int?)null : zid;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Controller] Zone grid load error: {ex}");
            }
        }

        // Called by DatabaseConnectionView after the user saves valid credentials.
        public void SetDbConnection(DatabaseSettings db)
        {
            Settings.Database = db;
            DbFactory = new MySqlConnectionFactory(db);
            Console.WriteLine($"[DB] Connection configured: {db.Server}:{db.Port}/{db.Database}");

            // If a zone is already loaded, kick off spawn loading immediately.
            if (CurrentZoneName != null)
                _ = LoadZoneSpawnsAsync(CurrentZoneName);
        }

        public void Start()
        {
            Engine.Start();
        }

        public IReadOnlyList<AniModelInstance> GetCharacterModels() => CharacterModels;

        // Builds the per-frame list of spawn marker lines (vertical spikes above spawns in
        // non-normal states). Colors:
        //   selected  → bright cyan, taller line
        //   dirty     → orange
        //   placeholder → yellow
        // Normal (modelled, in-DB spawns) get no marker to keep the scene readable.
        void UpdateSpawnMarkers()
        {
            var lines = new List<(Vector3 A, Vector3 B, Vector4 Color)>();
            var selected = SpawnManager.Selected;

            var showSelected    = Settings.ShowSelectedMarker;
            var showDirty       = Settings.ShowDirtyMarkers;
            var showPlaceholder = Settings.ShowPlaceholderMarkers;

            foreach (var sp in SpawnManager.SpawnPoints)
            {
                bool isSelected = sp == selected;
                bool isDirty = sp.IsDirty;
                bool isPlaceholder = sp.IsPlaceholder;

                // Priority: dirty > selected > placeholder. Dirty must win over selected so
                // that dragging a spawn (which stays selected) still flips the marker orange.
                Vector4 color;
                float height;
                if (isDirty && showDirty)
                {
                    // Selected + dirty gets a taller marker so the user still sees "this is
                    // the one I have selected" while the color reflects the dirty state.
                    color = new Vector4(1f, 0.55f, 0.15f, 0.95f); // orange
                    height = isSelected ? 60f : 40f;
                }
                else if (isSelected && showSelected)
                {
                    color = new Vector4(0.3f, 1f, 1f, 1f);   // cyan
                    height = 60f;
                }
                else if (isPlaceholder && showPlaceholder)
                {
                    color = new Vector4(1f, 0.9f, 0.2f, 0.75f);  // yellow
                    height = 40f;
                }
                else continue;

                var basePos = sp.Model.Position;
                lines.Add((basePos, basePos + new Vector3(0, 0, height), color));
            }

            Engine.SetSpawnMarkerLines(lines);
        }

        // Builds path grid line list for the current grid source. Priority: sidebar-picked
        // grid (SelectedGridId, from the Grid List section, may be orphan → magenta) →
        // selected spawn's grid (amber) → empty. Also pushes the current waypoint set into
        // WaypointEditor so clicks near a crosshair can grab the waypoint before the spawn
        // selection.
        void UpdatePathGrids()
        {
            var lines = new List<(Vector3 A, Vector3 B, Vector4 Color)>();
            var candidates = new List<Engine.WaypointEditor.Handle>();

            List<Database.Models.GridEntry> sourceWaypoints = null;
            var baseColor = new Vector4(1f, 0.85f, 0.2f, 1f); // amber (attached)
            if (Settings.ShowPathGrids)
            {
                if (SelectedGridId.HasValue)
                {
                    var zg = ZoneGrids.FirstOrDefault(g => g.Grid.Id == SelectedGridId.Value);
                    if (zg != null && zg.Waypoints.Count > 0)
                    {
                        sourceWaypoints = zg.Waypoints;
                        if (zg.SpawnCount == 0)
                            baseColor = new Vector4(0.9f, 0.3f, 0.9f, 1f); // magenta (orphan)
                    }
                }
                else
                {
                    var sp = SpawnManager.Selected;
                    if (sp != null && sp.Record.Waypoints.Count > 0)
                        sourceWaypoints = sp.Record.Waypoints;
                }
            }

            if (sourceWaypoints != null)
            {
                var green      = new Vector4(0.3f, 1f, 0.3f, 1f);
                var brightGreen= new Vector4(0.5f, 1f, 0.5f, 1f);
                var selectedHandle = Engine.WaypointEditor.Selected;
                var wpEditor   = Engine.WaypointEditor;
                var isDragging = wpEditor.IsDragging;

                var waypoints = sourceWaypoints.OrderBy(w => w.Number).ToList();

                Vector3 ToScene(Database.Models.GridEntry g) => new Vector3(g.Y, g.X, g.Z);

                for (int i = 0; i + 1 < waypoints.Count; i++)
                {
                    var a = ToScene(waypoints[i]);
                    var b = ToScene(waypoints[i + 1]);
                    // If either endpoint is the actively-dragged waypoint, pull the live
                    // ScenePos from the editor so the polyline follows the crosshair.
                    if (isDragging && selectedHandle.HasValue)
                    {
                        if (selectedHandle.Value.GridId == waypoints[i].GridId &&
                            selectedHandle.Value.Number == waypoints[i].Number)
                            a = selectedHandle.Value.ScenePos;
                        if (selectedHandle.Value.GridId == waypoints[i + 1].GridId &&
                            selectedHandle.Value.Number == waypoints[i + 1].Number)
                            b = selectedHandle.Value.ScenePos;
                    }
                    lines.Add((a, b, baseColor));
                }

                // Waypoint markers were getting lost on bright surfaces (cshome marble
                // floor, freporte cobblestone). Two changes: (1) bumped arm length so
                // the ground-plane cross has more presence; (2) the vertical arm is now
                // an upward pole only, so the marker protrudes into the air above the
                // floor instead of half-burying itself.
                const float armLength = 8f;
                const float selectedArmLength = 12f;
                const float draggingArmLength = 20f;
                const float PoleMultiplier = 2.5f;   // vertical pole = arm × this
                const float FloorLift = 0.5f;        // lift X/Y arms this much above WP to avoid z-fighting with the floor
                foreach (var wp in waypoints)
                {
                    var isSelected = selectedHandle.HasValue
                        && selectedHandle.Value.GridId == wp.GridId
                        && selectedHandle.Value.Number == wp.Number;

                    // Live drag position for the selected+dragging waypoint; static for the rest.
                    var scenePos = (isSelected && isDragging)
                        ? selectedHandle.Value.ScenePos
                        : ToScene(wp);

                    Vector4 color;
                    float arm;
                    if (isSelected && isDragging) { color = brightGreen; arm = draggingArmLength; }
                    else if (isSelected)          { color = green;       arm = selectedArmLength; }
                    else                          { color = baseColor;   arm = armLength; }

                    var xyLevel = scenePos + new Vector3(0, 0, FloorLift);
                    lines.Add((xyLevel - new Vector3(arm, 0, 0), xyLevel + new Vector3(arm, 0, 0), color));
                    lines.Add((xyLevel - new Vector3(0, arm, 0), xyLevel + new Vector3(0, arm, 0), color));
                    // Upward pole only (previously ±arm on Z, but the negative half was
                    // buried in the floor). Now every waypoint has a visible vertical
                    // beacon even on flat, brightly-lit surfaces.
                    lines.Add((scenePos, scenePos + new Vector3(0, 0, arm * PoleMultiplier), color));

                    // Heading arrow — points in the direction the NPC will face at this
                    // waypoint. Same 0-511 convention as spawn2.heading (grid_entries.heading
                    // uses the same scale), so we can hand it straight to HeadingToRotation.
                    // Longer arrow when selected/dragged so the highlighted waypoint stays
                    // legible against terrain.
                    //
                    // Skip only the arrow — not the whole waypoint — when heading is EQEmu's
                    // -1 sentinel ("no rotation on arrival"; mob keeps its incoming facing,
                    // no direction to draw) or a clearly-invalid legacy value. Crosshair,
                    // vertical pole, and the candidates-list entry must still emit so the
                    // waypoint stays visible and clickable.
                    var showArrow = wp.Heading >= -0.001f && wp.Heading <= 512f;
                    if (showArrow)
                    {
                        float arrowLen = (isSelected && isDragging) ? 22f
                                       : isSelected                 ? 14f
                                                                    : 10f;
                        var rot = SpawnSystem.SpawnManager.HeadingToRotation(wp.Heading);
                        var forward = Vector3.Normalize(Vector3.Transform(new Vector3(0, 1, 0), rot));
                        var arrowStart = scenePos;
                        var arrowEnd = arrowStart + forward * arrowLen;
                        lines.Add((arrowStart, arrowEnd, color));

                        // Arrowhead: two short lines splaying back from the tip.
                        var perp = new Vector3(-forward.Y, forward.X, 0);   // 90° CCW around Z
                        var head = arrowLen * 0.28f;
                        lines.Add((arrowEnd, arrowEnd - forward * head + perp * head, color));
                        lines.Add((arrowEnd, arrowEnd - forward * head - perp * head, color));
                    }

                    // Vertical pole from ground straight up to a tall marker so the dragged
                    // waypoint never disappears against textured floors.
                    if (isSelected && isDragging)
                    {
                        var ground = scenePos;
                        if (Collider != null)
                        {
                            var probe = scenePos + new Vector3(0, 0, 20f);
                            var hit = Collider.FindIntersection(probe, new Vector3(0, 0, -1), 0.5f);
                            if (hit.HasValue) ground = new Vector3(scenePos.X, scenePos.Y, hit.Value.Item2.Z);
                        }
                        var top = scenePos + new Vector3(0, 0, 80f);
                        lines.Add((ground, top, brightGreen));
                    }

                    candidates.Add(new Engine.WaypointEditor.Handle
                    {
                        GridId   = wp.GridId,
                        Number   = wp.Number,
                        // Give WaypointEditor the current authoritative position so a fresh
                        // click hits the crosshair where it's actually drawn.
                        ScenePos = (isSelected && isDragging) ? selectedHandle.Value.ScenePos : ToScene(wp),
                    });
                }
            }

            Engine.WaypointEditor.SetCandidates(candidates);
            Engine.SetPathGridLines(lines);
        }

        // Builds and pushes the zone-point primitive lists AND the editor's candidate
        // handle set. Runs every frame — every row renders at its health color; the
        // selected row also gets corner + Z-face handles; and the editor's candidate
        // list is refreshed so a click near any glyph engages the drag.
        void UpdateZonePoints()
        {
            var emptyLines = System.Array.Empty<(Vector3, Vector3, Vector4)>();
            var emptyTris  = System.Array.Empty<(Vector3, Vector3, Vector3, Vector4)>();

            if (ZonePointManager.ZonePoints.Count == 0 && ZonePointManager.IncomingPoints.Count == 0)
            {
                Engine.SetZonePointPrimitives(emptyLines, emptyTris);
                Engine.ZonePointEditor.SetCandidates(System.Array.Empty<ZonePointEditor.Handle>());
                return;
            }

            var selected = ZonePointManager.Selected;

            // Recompute sandwich status for every owned row. Cheap enough to run per frame
            // (dozens × dozens ≤ a few hundred comparisons); avoids invalidation bookkeeping
            // and reflects target-coord edits immediately.
            SandwichResults.Clear();
            foreach (var zp in ZonePointManager.ZonePoints)
            {
                var r = ZonePointSystem.SandwichDetector.Check(zp, ZonePointManager.PeerZoneRows);
                if (r.Sandwiched) SandwichResults[zp.Row.Id] = r;
            }

            var built = ZonePointSystem.ZonePointPrimitiveBuilder.Build(
                ZonePointManager.ZonePoints,
                ZonePointManager.IncomingPoints,
                selected,
                Engine.ZoneBounds,
                SandwichResults);

            // Handle-glyph lines + candidates. Small crosses at center handles for every
            // zone point; larger contrast crosses at corner/face handles for the selected.
            var handleColor        = new Vector4(0.85f, 0.95f, 1f, 1f);
            var selectedHandleClr  = new Vector4(0.30f, 1.00f, 1.00f, 1f);
            var incomingHandleClr  = new Vector4(0.30f, 0.95f, 1.00f, 1f);
            var candidates = new List<ZonePointEditor.Handle>(
                ZonePointManager.ZonePoints.Count * 3 + ZonePointManager.IncomingPoints.Count);

            const float centerArm = 4f;
            const float handleArm = 6f;

            foreach (var zp in ZonePointManager.ZonePoints)
            {
                // Skip source-wildcard rows for now — their "center" sits at the sentinel
                // coord and dragging it is meaningless without an inspector-level toggle.
                if (zp.HasSourceWildcard) continue;

                var c = zp.SceneCenter;
                built.Lines.Add(new ZonePointSystem.ZonePointPrimitiveBuilder.Line(
                    c - new Vector3(centerArm, 0, 0), c + new Vector3(centerArm, 0, 0), handleColor));
                built.Lines.Add(new ZonePointSystem.ZonePointPrimitiveBuilder.Line(
                    c - new Vector3(0, centerArm, 0), c + new Vector3(0, centerArm, 0), handleColor));
                built.Lines.Add(new ZonePointSystem.ZonePointPrimitiveBuilder.Line(
                    c - new Vector3(0, 0, centerArm), c + new Vector3(0, 0, centerArm), handleColor));

                candidates.Add(new Engine.ZonePointEditor.Handle
                {
                    ZonePointId = zp.Row.Id,
                    Kind        = ZonePointEditor.HandleKind.Center,
                    ScenePos    = c,
                });

                if (ReferenceEquals(zp, selected) && zp.Row.UseNewZoning == 0)
                {
                    var xy = System.MathF.Max(1f, zp.Row.Zrange);
                    // Draw corner + face handles at the row's center Z so they stay visible
                    // even inside a very tall (infinite-Z) volume.
                    var corners = new[]
                    {
                        (ZonePointEditor.HandleKind.CornerXmYm, new Vector3(c.X - xy, c.Y - xy, c.Z)),
                        (ZonePointEditor.HandleKind.CornerXpYm, new Vector3(c.X + xy, c.Y - xy, c.Z)),
                        (ZonePointEditor.HandleKind.CornerXpYp, new Vector3(c.X + xy, c.Y + xy, c.Z)),
                        (ZonePointEditor.HandleKind.CornerXmYp, new Vector3(c.X - xy, c.Y + xy, c.Z)),
                    };
                    foreach (var (kind, pos) in corners)
                    {
                        AddHandleCross(built.Lines, pos, handleArm, selectedHandleClr);
                        candidates.Add(new ZonePointEditor.Handle { ZonePointId = zp.Row.Id, Kind = kind, ScenePos = pos });
                    }

                    var zHalf = zp.Row.MaxZDiff == 0 ? 2000f : System.MathF.Max(1f, zp.Row.MaxZDiff);
                    var faceZm = new Vector3(c.X, c.Y, c.Z - zHalf);
                    var faceZp = new Vector3(c.X, c.Y, c.Z + zHalf);
                    AddHandleCross(built.Lines, faceZm, handleArm, selectedHandleClr);
                    AddHandleCross(built.Lines, faceZp, handleArm, selectedHandleClr);
                    candidates.Add(new ZonePointEditor.Handle { ZonePointId = zp.Row.Id, Kind = ZonePointEditor.HandleKind.FaceZm, ScenePos = faceZm });
                    candidates.Add(new ZonePointEditor.Handle { ZonePointId = zp.Row.Id, Kind = ZonePointEditor.HandleKind.FaceZp, ScenePos = faceZp });
                }
                else if (ReferenceEquals(zp, selected) && (zp.Row.UseNewZoning == 1 || zp.Row.UseNewZoning == 2))
                {
                    // Plane-crossing end-cap handles. Position depends on mode:
                    //   mode 1 (X-plane, perpendicular = scene X axis) → handles at (min/maxVert, c.Y, c.Z)
                    //   mode 2 (Y-plane, perpendicular = scene Y axis) → handles at (c.X, min/maxVert, c.Z)
                    Vector3 minPos, maxPos;
                    if (zp.Row.UseNewZoning == 1)
                    {
                        minPos = new Vector3(zp.Row.MinVert, c.Y, c.Z);
                        maxPos = new Vector3(zp.Row.MaxVert, c.Y, c.Z);
                    }
                    else
                    {
                        minPos = new Vector3(c.X, zp.Row.MinVert, c.Z);
                        maxPos = new Vector3(c.X, zp.Row.MaxVert, c.Z);
                    }
                    AddHandleCross(built.Lines, minPos, handleArm, selectedHandleClr);
                    AddHandleCross(built.Lines, maxPos, handleArm, selectedHandleClr);
                    candidates.Add(new ZonePointEditor.Handle { ZonePointId = zp.Row.Id, Kind = ZonePointEditor.HandleKind.PlaneEndMinVert, ScenePos = minPos });
                    candidates.Add(new ZonePointEditor.Handle { ZonePointId = zp.Row.Id, Kind = ZonePointEditor.HandleKind.PlaneEndMaxVert, ScenePos = maxPos });
                }
            }

            // Incoming rows: only a center-handle candidate (at the landing coord) so
            // clicking the pad selects the row for inspector heading edit. IsIncoming=true
            // makes TrySelect prefer this over any overlapping owned-box handles that
            // would otherwise win the depth sort.
            foreach (var zp in ZonePointManager.IncomingPoints)
            {
                candidates.Add(new ZonePointEditor.Handle
                {
                    ZonePointId = zp.Row.Id,
                    Kind        = ZonePointEditor.HandleKind.Center,
                    ScenePos    = zp.SceneTarget,
                    IsIncoming  = true,
                });
            }

            // Drag-to-create preview. When the user is mid-drag with an active creation
            // mode, sketch the pending shape in a distinct yellow so they can see what
            // they're about to commit.
            if (ActiveCreation != CreationMode.None
                && _creationDragStart.HasValue && _creationDragCurrent.HasValue)
            {
                var start = _creationDragStart.Value;
                var end   = _creationDragCurrent.Value;
                var previewWire = new Vector4(1.0f, 0.85f, 0.20f, 1.0f);
                var previewFill = new Vector4(1.0f, 0.85f, 0.20f, 0.20f);

                if (ActiveCreation == CreationMode.DrawBox)
                {
                    // Ground-hugging rectangle. Preview the SQUARE-fit that the box will
                    // actually snap to (max of width/height) — that's what commit produces.
                    var cx = (start.X + end.X) * 0.5f;
                    var cy = (start.Y + end.Y) * 0.5f;
                    var cz = (start.Z + end.Z) * 0.5f;
                    var half = System.MathF.Max(1f, System.MathF.Max(
                        System.MathF.Abs(end.X - start.X) * 0.5f,
                        System.MathF.Abs(end.Y - start.Y) * 0.5f));
                    var min = new Vector3(cx - half, cy - half, cz - 5f);
                    var max = new Vector3(cx + half, cy + half, cz + 5f);
                    var v000 = new Vector3(min.X, min.Y, min.Z);
                    var v100 = new Vector3(max.X, min.Y, min.Z);
                    var v110 = new Vector3(max.X, max.Y, min.Z);
                    var v010 = new Vector3(min.X, max.Y, min.Z);
                    built.Lines.Add(new ZonePointSystem.ZonePointPrimitiveBuilder.Line(v000, v100, previewWire));
                    built.Lines.Add(new ZonePointSystem.ZonePointPrimitiveBuilder.Line(v100, v110, previewWire));
                    built.Lines.Add(new ZonePointSystem.ZonePointPrimitiveBuilder.Line(v110, v010, previewWire));
                    built.Lines.Add(new ZonePointSystem.ZonePointPrimitiveBuilder.Line(v010, v000, previewWire));
                    built.Tris.Add(new ZonePointSystem.ZonePointPrimitiveBuilder.Tri(v000, v100, v110, previewFill));
                    built.Tris.Add(new ZonePointSystem.ZonePointPrimitiveBuilder.Tri(v000, v110, v010, previewFill));
                }
                else if (ActiveCreation == CreationMode.DrawPlane)
                {
                    // A tall thin wall along the drag line. Height = a modest preview
                    // (real plane extends to zone bounds; showing that at 2000px scale
                    // during drag is visually noisy).
                    var mid = new Vector3(
                        (start.X + end.X) * 0.5f,
                        (start.Y + end.Y) * 0.5f,
                        (start.Z + end.Z) * 0.5f);
                    var height = 80f;
                    var top   = mid + new Vector3(0, 0, height);
                    var bot   = mid - new Vector3(0, 0, height);
                    built.Lines.Add(new ZonePointSystem.ZonePointPrimitiveBuilder.Line(start, end, previewWire));
                    built.Lines.Add(new ZonePointSystem.ZonePointPrimitiveBuilder.Line(
                        new Vector3(start.X, start.Y, start.Z + height),
                        new Vector3(end.X, end.Y, end.Z + height), previewWire));
                    built.Lines.Add(new ZonePointSystem.ZonePointPrimitiveBuilder.Line(
                        new Vector3(start.X, start.Y, start.Z),
                        new Vector3(start.X, start.Y, start.Z + height), previewWire));
                    built.Lines.Add(new ZonePointSystem.ZonePointPrimitiveBuilder.Line(
                        new Vector3(end.X, end.Y, end.Z),
                        new Vector3(end.X, end.Y, end.Z + height), previewWire));
                }
            }

            var lines = new (Vector3, Vector3, Vector4)[built.Lines.Count];
            for (int i = 0; i < built.Lines.Count; i++)
                lines[i] = (built.Lines[i].A, built.Lines[i].B, built.Lines[i].Color);

            var tris = new (Vector3, Vector3, Vector3, Vector4)[built.Tris.Count];
            for (int i = 0; i < built.Tris.Count; i++)
                tris[i] = (built.Tris[i].A, built.Tris[i].B, built.Tris[i].C, built.Tris[i].Color);

            // Body candidates for the click hit-test fallback — every owned zone-point
            // box contributes its AABB (using the SAME clamping the renderer applies for
            // infinite-Z boxes so visible and clickable stay in sync). Clicking the box
            // wireframe/fill selects the box even though the tiny 12px center handle is
            // otherwise the only precise target.
            var bodies = new List<ZonePointEditor.BoxBody>(ZonePointManager.ZonePoints.Count);
            foreach (var zp in ZonePointManager.ZonePoints)
            {
                var aabb = ZonePointSystem.ZonePointPrimitiveBuilder.TryGetBoxSceneAabb(zp, Engine.ZoneBounds);
                if (!aabb.HasValue) continue;
                bodies.Add(new ZonePointEditor.BoxBody
                {
                    ZonePointId = zp.Row.Id,
                    Min         = aabb.Value.Min,
                    Max         = aabb.Value.Max,
                    Center      = zp.SceneCenter,
                });
            }
            Engine.ZonePointEditor.SetBodyCandidates(bodies);

            Engine.SetZonePointPrimitives(lines, tris);
            Engine.ZonePointEditor.SetCandidates(candidates);
        }

        static void AddHandleCross(List<ZonePointSystem.ZonePointPrimitiveBuilder.Line> lines, Vector3 pos, float arm, Vector4 color)
        {
            lines.Add(new ZonePointSystem.ZonePointPrimitiveBuilder.Line(pos - new Vector3(arm, 0, 0), pos + new Vector3(arm, 0, 0), color));
            lines.Add(new ZonePointSystem.ZonePointPrimitiveBuilder.Line(pos - new Vector3(0, arm, 0), pos + new Vector3(0, arm, 0), color));
            lines.Add(new ZonePointSystem.ZonePointPrimitiveBuilder.Line(pos - new Vector3(0, 0, arm), pos + new Vector3(0, 0, arm), color));
        }
    }
}
