using System.Collections.Generic;
using VisualEQ.Database.Configuration;
using VisualEQ.Engine;
using VisualEQ.Settings;
using VisualEQ.Views;
using static VisualEQ.Engine.Globals;

namespace VisualEQ
{
    public class Controller : Engine.IController
    {
        public readonly EngineCore Engine = new EngineCore();

        readonly List<BaseView> Views = new List<BaseView>();
        readonly List<AniModelInstance> CharacterModels = new List<AniModelInstance>();

        public AniModel LastModelLoaded;

        private ModelSelector modelSelector;
        object IController.ModelSelector => modelSelector;
        public ModelSelector ModelSelector => modelSelector;

        // Settings loaded at startup and persisted on change.
        public AppSettings Settings { get; }

        // Non-null once the user has saved a valid DB connection.
        public MySqlConnectionFactory DbFactory { get; private set; }

        public Controller(AppSettings settings)
        {
            Settings = settings;

            // Restore a saved connection if credentials are present.
            if (!string.IsNullOrEmpty(settings.Database?.Server) &&
                !string.IsNullOrEmpty(settings.Database?.Database))
            {
                DbFactory = new MySqlConnectionFactory(settings.Database);
            }

            Engine.UpdateFrame += (s, e) => Views.ForEach(view => view.Update(Engine.Gui));
            modelSelector = new ModelSelector(Engine, CharacterModels);
            Engine.Controller = this;
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
            // TODO: Unload old contents
            Loader.LoadZoneFile($"../ConverterApp/{name}_oes.zip", Engine);
        }

        public void LoadCharacter(string filename, string name)
        {
            var model = LastModelLoaded = Loader.LoadCharacter($"../ConverterApp/{filename}_oes.zip", name);
            var instance = new AniModelInstance(model) { Animation = "C05", Position = vec3(-153, 149, 80) };
            Engine.Add(instance);
            CharacterModels.Add(instance);
        }

        // Called by DatabaseConnectionView after the user saves valid credentials.
        public void SetDbConnection(DatabaseSettings db)
        {
            Settings.Database = db;
            DbFactory = new MySqlConnectionFactory(db);
            System.Console.WriteLine($"[DB] Connection configured: {db.Server}:{db.Port}/{db.Database}");
        }

        public void Start()
        {
            Engine.Start();
        }

        public IReadOnlyList<AniModelInstance> GetCharacterModels() => CharacterModels;
    }
}
