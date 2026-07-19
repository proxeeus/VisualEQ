using System.Numerics;
using System.Reflection;
using ImGuiNET;
using NsimGui;
using NsimGui.Widgets;
using VisualEQ.Engine;

namespace VisualEQ.Views
{
    // F1 cheat sheet. Registered once at App startup; the widget is added to the
    // GUI unconditionally but self-gates on Visible so we're free to toggle it
    // from the F1 hotkey without add/remove churn every keypress. See
    // Controller.ToggleHelp / EngineCore.OnKeyDown Key.F1.
    public class HelpView : BaseView
    {
        private readonly HelpWidget _widget;

        public HelpView(Controller controller) : base(controller)
        {
            _widget = new HelpWidget(controller.Engine);
        }

        public override void Setup(Gui gui) => gui.Add(_widget);

        public void Toggle() => _widget.Toggle();
    }

    // Standalone ImGui window. Non-declarative because it needs conditional
    // visibility and reads live engine state each frame for the footer.
    internal class HelpWidget : BaseWidget
    {
        private readonly EngineCore _engine;
        private bool _visible;
        private bool _positioned;

        // Cached at construction; runtime probes Assembly.GetExecutingAssembly()
        // once. We show the informational version so publish-release.ps1's
        // AssemblyInformationalVersion (which carries the v-tag when built by CI)
        // renders instead of the placeholder 1.0.0.
        private readonly string _version;

        public HelpWidget(EngineCore engine)
        {
            _engine = engine;
            var asm = Assembly.GetExecutingAssembly();
            _version = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                       ?? asm.GetName().Version?.ToString()
                       ?? "unknown";
        }

        public void Toggle() => _visible = !_visible;

        public override void Render(Gui gui)
        {
            if (!_visible) return;

            if (!_positioned)
            {
                ImGui.SetNextWindowSize(new Vector2(460, 640), Condition.FirstUseEver);
                ImGui.SetNextWindowPos(new Vector2(20, 60), Condition.FirstUseEver, Vector2.Zero);
                _positioned = true;
            }

            ImGui.BeginWindow($"Cheat Sheet (F1 to close)###{Id}");

            SectionHeader("Keyboard");
            Row("F1",           "Show / hide this cheat sheet");
            Row("F8",           "Show / hide foliage (trees, pines, palms)");
            Row("F9",           "Toggle back-face culling (regression aid)");
            Row("F10",          "Return to main menu (prompts if unsaved)");
            Row("L",            "Toggle deferred lighting pass");
            Row("E",            "Toggle edit mode");
            Row("P",            "Toggle physics / gravity");
            Row("Space",        "Fly camera to cursor");
            Row("Home",         "Reset camera Z to 1000");
            Row("F",            "Frame selected spawn");
            Row("Delete",       "Mark selected spawn for deletion (edit mode)");
            Row("Escape",       "Cancel drag / exit grid mode / clear selection");
            Row("Ctrl+Z / Y",   "Undo / redo (Ctrl+W also works on AZERTY)");
            Row("Ctrl+D",       "Duplicate selected spawn (edit mode)");
            Row("Ctrl+LMB dbl", "Place new NPC at terrain hit (edit mode)");
            Row("~",            "Quit application");

            ImGui.Spacing();
            SectionHeader("Mouse");
            Row("Right-drag",   "Rotate camera (cursor hidden)");
            Row("Left-click",   "Select model (spawn / zone point / waypoint)");
            Row("Left-drag",    "Move selection along ground / surface");
            Row("Ctrl+drag",    "Move without Z snap (keep current altitude)");
            Row("Alt+drag",     "Move on camera-perpendicular plane");
            Row("Wheel + drag", "Push / pull selection along drag ray");
            Row("Dbl-click",    "In grid mode: place waypoint on terrain");

            ImGui.Spacing();
            SectionHeader("Workflow");
            ImGui.Text("- Press E to enter edit mode before moving or saving spawns.");
            ImGui.Text("- Sidebar (right) lists spawns; click to jump-frame, drag to");
            ImGui.Text("  move, Commit to save to the DB.");
            ImGui.Text("- Grid mode (sidebar toggle) turns LMB double-clicks into");
            ImGui.Text("  waypoint placements on the selected grid.");
            ImGui.Text("- Configure the EQEmu DB via the Database Connection window.");
            ImGui.Text("- Ctrl+Z undoes any spawn edit before commit.");
            ImGui.Text("- F8 hides Kunark trees when they block your view.");

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Text($"VisualEQ {_version}   FPS {_engine.FPS:F0}");
            ImGui.Text($"Deferred: {(_engine.DeferredLightingOn ? "ON" : "OFF")}   " +
                       $"Culling: {(_engine.BackfaceCullingOn ? "ON" : "OFF")}   " +
                       $"Foliage: {(_engine.FoliageHidden ? "HIDDEN" : "VISIBLE")}");

            ImGui.EndWindow();
        }

        private static void SectionHeader(string title)
        {
            ImGui.Text(title);
            ImGui.Separator();
        }

        // Two-column row: fixed-width key on the left, description on the right.
        // ImGui.NET 0.4.6 has no LabelText overload we need — plain SameLine
        // with a fixed X offset is enough and lines the columns up cleanly.
        private static void Row(string key, string desc)
        {
            ImGui.Text(key);
            ImGui.SameLine(150);
            ImGui.Text(desc);
        }
    }
}
