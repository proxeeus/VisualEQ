using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using VisualEQ.Database.Configuration;
using VisualEQ.Database.Repositories;
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

        public Controller(AppSettings settings)
        {
            Settings = settings;

            if (!string.IsNullOrEmpty(settings.Database?.Server) &&
                !string.IsNullOrEmpty(settings.Database?.Database))
            {
                DbFactory = new MySqlConnectionFactory(settings.Database);
            }

            Engine.UpdateFrame += (s, e) => Views.ForEach(view => view.Update(Engine.Gui));
            modelSelector = new ModelSelector(Engine, CharacterModels);
            Engine.Controller = this;

            // Forward model selection to SpawnManager.
            modelSelector.OnSelectionChanged += SpawnManager.Select;
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
            ZoneChanged?.Invoke(name);
        }

        // Tears down the current zone's scene state so a new zone can be loaded on top.
        // Safe to call when nothing is loaded.
        public void ClearCurrentZone()
        {
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
        void LoadDefaultCharacterForZone(string zoneName)
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
    }
}
