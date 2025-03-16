using System;
using System.Numerics;
using NsimGui;
using NsimGui.Widgets;
using VisualEQ.Engine;
using static VisualEQ.Engine.Globals;

namespace VisualEQ.Views
{
    public class ModelEditorView : BaseView
    {
        private string statusMessage = "";
        private float messageTimer = 0;
        private float lastFrameTime = 0;

        // Currently selected model
        private AniModelInstance selectedModel = null;

        // Position display strings
        private string posX = "0";
        private string posY = "0";
        private string posZ = "0";

        public ModelEditorView(Controller controller) : base(controller)
        {
            // Register for model selection change events
            controller.ModelSelector.OnSelectionChanged += OnModelSelectionChanged;
            controller.ModelSelector.OnPositionChanged += OnModelPositionChanged;
        }

        public override void Setup(Gui gui)
        {
            gui.Add(new Window("Model Editor") {
                new Size(280, 270),
                
                // Selection status
                new Text(() => selectedModel != null ?
                    "Selected: Character Model" :
                    "No model selected - Click on a model to select"),
                
                // Position display
                new Text(() => "Position:"),
                
                // X coordinate
                new HBox {
                    new Text("X:"),
                    new Text(() => posX)
                },
                
                // Y coordinate
                new HBox {
                    new Text("Y:"),
                    new Text(() => posY)
                },
                
                // Z coordinate
                new HBox {
                    new Text("Z:"),
                    new Text(() => posZ)
                },
                
                // Instructions section
                new Text("=== Controls ==="),
                new Text("Left click: select model"),
                new Text("Left drag: move model"),
                new Text("Mouse wheel while dragging: adjust depth"),
                new Text("Models will stick to surfaces below them"),
                
                // Status message
                new Text(() => statusMessage)
            });
        }

        public override void Update(Gui gui)
        {
            // Calculate delta time
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

        // Called when a model is selected/deselected
        private void OnModelSelectionChanged(AniModelInstance model)
        {
            selectedModel = model;

            if (model != null)
            {
                // Update position display
                UpdatePositionDisplay(model.Position);

                // Show status message
                statusMessage = "Model selected!";
                messageTimer = 3.0f;
            }
            else
            {
                // Clear position display
                posX = "0";
                posY = "0";
                posZ = "0";

                // Show status message
                statusMessage = "Model deselected";
                messageTimer = 3.0f;
            }
        }

        // Called when a model's position changes
        private void OnModelPositionChanged(AniModelInstance model, Vector3 newPosition)
        {
            // Update position display
            UpdatePositionDisplay(newPosition);
        }

        // Update the position display strings
        private void UpdatePositionDisplay(Vector3 position)
        {
            posX = position.X.ToString("0.00");
            posY = position.Y.ToString("0.00");
            posZ = position.Z.ToString("0.00");
        }
    }
}
