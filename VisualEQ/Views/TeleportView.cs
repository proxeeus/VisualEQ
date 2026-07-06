using System;
using System.Numerics;
using NsimGui;
using NsimGui.Widgets;
using VisualEQ.Engine;
using static VisualEQ.Engine.Globals;

namespace VisualEQ.Views
{
    public class TeleportView : BaseView
    {
        // ORC character coordinates
        private const float ORC_X = -153f;
        private const float ORC_Y = 149f;
        private const float ORC_Z = 80f;

        private string statusMessage = "";
        private float messageTimer = 0;
        private float lastFrameTime = 0;

        private Gui _gui;
        private Window _window;
        private bool _windowShown;

        public TeleportView(Controller controller) : base(controller)
        {
            controller.ZoneChanged += OnZoneChanged;
        }

        public override void Setup(Gui gui)
        {
            _gui = gui;
            _window = new Window("Teleport") {
                new Size(200, 140),
                new Button("Teleport to ORC", (180, 30)) { _ => TeleportToOrc() },
                new Text(() => $"Current position:\n{Camera.Position}"),
                new Text(() => statusMessage)
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

        public override void Update(Gui gui)
        {
            // Calculate delta time manually using FrameTime
            float deltaTime = FrameTime - lastFrameTime;
            lastFrameTime = FrameTime;

            // Clear status message after 3 seconds
            if (statusMessage != "" && messageTimer > 0)
            {
                messageTimer -= deltaTime;
                if (messageTimer <= 0)
                {
                    statusMessage = "";
                }
            }
        }

        private void TeleportToOrc()
        {
            try
            {
                // Set the camera position to the ORC coordinates
                Camera.Position = new Vector3(ORC_X, ORC_Y, ORC_Z);

                // Show success message
                statusMessage = $"Teleported to ORC at ({ORC_X}, {ORC_Y}, {ORC_Z})";
                messageTimer = 3.0f; // Show message for 3 seconds
            }
            catch (Exception)
            {
                // Show error message
                statusMessage = "Teleport failed!";
                messageTimer = 3.0f;
            }
        }
    }
}
