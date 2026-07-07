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

        public AniModel LastModelLoaded;

        private ModelSelector modelSelector;
        object IController.ModelSelector => modelSelector;
        public ModelSelector ModelSelector => modelSelector;

        public AppSettings Settings { get; }

        // Non-null once the user has saved a valid DB connection.
        public MySqlConnectionFactory DbFactory { get; private set; }

        public SpawnManager SpawnManager { get; } = new SpawnManager();

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
            // Grid entries applied in Phase 5.6 when waypoint rendering picks up buffer state.

            PendingBuffer = buffer;
            BufferChanged?.Invoke();
            // No MarkBufferDirty — buffer is already on disk from the previous session.
        }

        // Wipes the current buffer + on-disk file. Restores all dirty spawns to their
        // original position/heading via SpawnPoint.Revert().
        public void DiscardPendingBuffer()
        {
            if (PendingBuffer == null) return;

            foreach (var sp in SpawnManager.SpawnPoints)
                if (sp.IsDirty) sp.Revert();

            EditBufferManager.DeleteForZone(PendingBuffer.Zone);
            PendingBuffer = new EditBuffer { Zone = CurrentZoneName, CreatedAt = DateTime.UtcNow };
            _bufferDirty = false;
            BufferChanged?.Invoke();
        }

        public Controller(AppSettings settings)
        {
            Settings = settings;

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
            if (PendingBuffer != null && !string.IsNullOrEmpty(PendingBuffer.Zone))
                EditSystem.EditBufferManager.DeleteForZone(PendingBuffer.Zone);

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
            Loader.LoadZoneFile($"../ConverterApp/{name}_oes.zip", Engine);
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
            if (_bufferDirty && PendingBuffer != null && !PendingBuffer.IsEmpty)
                EditBufferManager.SaveForZone(PendingBuffer);
            _bufferDirty = false;
            PendingBuffer = null;
            UndoStack.Clear();

            Engine.ClearScene();
            CharacterModels.Clear();
            _modelCache.Clear();
            LastModelLoaded = null;
            SpawnManager.SpawnPoints.Clear();
            CurrentZoneName = null;
            ZoneChanged?.Invoke(null);
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
        }

        // Preloads the placeholder-fallback model for the zone. Sets LastModelLoaded so
        // SpawnManager has something to instance for spawns whose race can't be resolved.
        // Does NOT add a visible character to the scene — that was the "phantom orc" bug.
        public void LoadDefaultCharacterForZone(string zoneName)
        {
            var candidates = new[] { $"{zoneName}_chr", "gfaydark_chr" };
            foreach (var prefix in candidates)
            {
                var path = $"../ConverterApp/{prefix}_oes.zip";
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
            var model = LastModelLoaded = Loader.LoadCharacter($"../ConverterApp/{filename}_oes.zip", name);
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
                var repo = new SpawnRepository(DbFactory);
                var records = await repo.GetZoneSpawnsFullAsync(zoneName);

                var availableModels = SpawnManager.BuildAvailableModels(zoneName);
                SpawnManager.LoadFromRecords(records, Engine, _modelCache, availableModels, LastModelLoaded);

                // Register spawn instances with the model selector so they are clickable.
                foreach (var sp in SpawnManager.SpawnPoints)
                    CharacterModels.Add(sp.Model);
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

            var repo = new SpawnRepository(DbFactory);
            _spawnLoadRecords = repo.GetZoneSpawnsFullAsync(zoneName).GetAwaiter().GetResult().ToList();
            _spawnLoadAvailable = SpawnManager.BuildAvailableModels(zoneName);
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
                var repo = new SpawnRepository(DbFactory);
                var records = repo.GetZoneSpawnsFullAsync(zoneName).GetAwaiter().GetResult();

                var availableModels = SpawnManager.BuildAvailableModels(zoneName);
                SpawnManager.LoadFromRecords(records, Engine, _modelCache, availableModels, LastModelLoaded);

                foreach (var sp in SpawnManager.SpawnPoints)
                    CharacterModels.Add(sp.Model);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Controller] Spawn load error: {ex}");
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

        // Builds path grid line list for the selected spawn only. Empty when nothing is
        // selected, the selected spawn has no grid, or the user has disabled path grids.
        // Also pushes the current waypoint set into WaypointEditor so clicks near a
        // crosshair can grab the waypoint before the spawn selection.
        void UpdatePathGrids()
        {
            var lines = new List<(Vector3 A, Vector3 B, Vector4 Color)>();
            var candidates = new List<Engine.WaypointEditor.Handle>();

            var sp = SpawnManager.Selected;
            if (sp != null && Settings.ShowPathGrids && sp.Record.Waypoints.Count > 0)
            {
                var amber = new Vector4(1f, 0.85f, 0.2f, 1f);
                var green = new Vector4(0.3f, 1f, 0.3f, 1f); // selected-waypoint highlight
                var selectedHandle = Engine.WaypointEditor.Selected;

                var waypoints = sp.Record.Waypoints.OrderBy(w => w.Number).ToList();

                Vector3 ToScene(Database.Models.GridEntry g) => new Vector3(g.Y, g.X, g.Z);

                for (int i = 0; i + 1 < waypoints.Count; i++)
                    lines.Add((ToScene(waypoints[i]), ToScene(waypoints[i + 1]), amber));

                const float armLength = 5f;
                const float selectedArmLength = 9f;
                foreach (var wp in waypoints)
                {
                    var scenePos = ToScene(wp);
                    var isSelected = selectedHandle.HasValue
                        && selectedHandle.Value.GridId == wp.GridId
                        && selectedHandle.Value.Number == wp.Number;
                    var color = isSelected ? green : amber;
                    var arm = isSelected ? selectedArmLength : armLength;

                    lines.Add((scenePos - new Vector3(arm, 0, 0), scenePos + new Vector3(arm, 0, 0), color));
                    lines.Add((scenePos - new Vector3(0, arm, 0), scenePos + new Vector3(0, arm, 0), color));
                    lines.Add((scenePos - new Vector3(0, 0, arm), scenePos + new Vector3(0, 0, arm), color));

                    candidates.Add(new Engine.WaypointEditor.Handle
                    {
                        GridId   = wp.GridId,
                        Number   = wp.Number,
                        ScenePos = scenePos,
                    });
                }
            }

            Engine.WaypointEditor.SetCandidates(candidates);
            Engine.SetPathGridLines(lines);
        }
    }
}
