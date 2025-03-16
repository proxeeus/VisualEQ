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

        public TeleportView(Controller controller) : base(controller) { }

        public override void Setup(Gui gui)
        {
            gui.Add(new Window("Teleport") {
                new Size(200, 140),
                
                // ORC teleport button
                new Button("Teleport to ORC", (180, 30)) { _ => TeleportToOrc() },
                
                // Current position display
                new Text(() => $"Current position:\n{Camera.Position}"),
                
                // Status message
                new Text(() => statusMessage)
            });
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
