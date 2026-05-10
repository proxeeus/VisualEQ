using System;
using System.Collections.Generic;
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
        private string _currentZoneName;

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
            _currentZoneName = name;
            Loader.LoadZoneFile($"../ConverterApp/{name}_oes.zip", Engine);
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

        // Called by DatabaseConnectionView after the user saves valid credentials.
        public void SetDbConnection(DatabaseSettings db)
        {
            Settings.Database = db;
            DbFactory = new MySqlConnectionFactory(db);
            Console.WriteLine($"[DB] Connection configured: {db.Server}:{db.Port}/{db.Database}");

            // If a zone is already loaded, kick off spawn loading immediately.
            if (_currentZoneName != null)
                _ = LoadZoneSpawnsAsync(_currentZoneName);
        }

        public void Start()
        {
            Engine.Start();
        }

        public IReadOnlyList<AniModelInstance> GetCharacterModels() => CharacterModels;
    }
}
