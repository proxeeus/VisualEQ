using System.Collections.Generic;
using VisualEQ.Engine;
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

        // ModelSelector field
        private ModelSelector modelSelector;

        // Implement the IController.ModelSelector property
        object IController.ModelSelector => modelSelector;

        // Public property for ease of use
        public ModelSelector ModelSelector => modelSelector;

        public Controller()
        {
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

        public void Start()
        {
            Engine.Start();
        }

        // Get all loaded character models
        public IReadOnlyList<AniModelInstance> GetCharacterModels()
        {
            return CharacterModels;
        }
    }
}
