using NsimGui;
using NsimGui.Widgets;
using VisualEQ.Engine;

namespace VisualEQ.Views
{
    public class StatusView : BaseView
    {
        private Gui _gui;
        private Window _window;
        private bool _windowShown;

        public StatusView(Controller controller) : base(controller)
        {
            controller.ZoneChanged += OnZoneChanged;
        }

        public override void Setup(Gui gui)
        {
            _gui = gui;
            _window = new Window("Status") {
                new Size(500, 140),
                new Text(() => $"Position {Globals.Camera.Position}"),
                new Text(() => $"FPS {Controller.Engine.FPS}"),
                new Text(() => Controller.DbFactory != null
                    ? $"DB: Connected ({Controller.Settings.Database.Server}/{Controller.Settings.Database.Database})"
                    : "DB: Not connected"),
                new Text(() => $"Spawns: {Controller.SpawnManager.SpawnPoints.Count}" +
                    (Controller.SpawnManager.DirtyCount > 0 ? $"  [{Controller.SpawnManager.DirtyCount} unsaved]" : ""))
            };
            if (Controller.CurrentZoneName != null) ShowWindow();
        }

        void OnZoneChanged(string zone)
        {
            if (zone == null) HideWindow();
            else ShowWindow();
        }

        void ShowWindow()
        {
            if (_windowShown || _gui == null || _window == null) return;
            _gui.Add(_window);
            _windowShown = true;
        }

        void HideWindow()
        {
            if (!_windowShown || _gui == null || _window == null) return;
            _gui.Remove(_window);
            _windowShown = false;
        }
    }
}
