using System;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using ImGuiNET;
using MySqlConnector;
using NsimGui;
using NsimGui.Widgets;
using VisualEQ.Database.Configuration;
using VisualEQ.Settings;

namespace VisualEQ.Views
{
    public class DatabaseConnectionView : BaseView
    {
        private readonly DatabaseFormWidget _widget;

        public DatabaseConnectionView(Controller controller) : base(controller)
        {
            _widget = new DatabaseFormWidget(controller);
        }

        public override void Setup(Gui gui)
        {
            gui.Add(_widget);
        }
    }

    // Renders a self-contained ImGui window for DB connection configuration.
    // Bypasses the NsimGui declarative widget tree because it needs mutable byte[]
    // buffers, async feedback, and conditional layout that don't fit the static widget model.
    internal class DatabaseFormWidget : BaseWidget
    {
        private readonly Controller _controller;

        private readonly byte[] _host     = new byte[256];
        private readonly byte[] _port     = new byte[16];
        private readonly byte[] _database = new byte[64];
        private readonly byte[] _username = new byte[64];
        private readonly byte[] _password = new byte[128];

        private string _status = "";
        private bool   _statusOk;
        private Task<(bool Success, string Error)> _pendingTest;

        // Position/size set once on first render.
        private bool _positioned;

        public DatabaseFormWidget(Controller controller)
        {
            _controller = controller;
            var db = controller.Settings.Database;
            Write(_host,     db.Server   ?? "localhost");
            Write(_port,     (db.Port > 0 ? db.Port : 3306).ToString());
            Write(_database, db.Database ?? "");
            Write(_username, db.Username ?? "");
            Write(_password, db.Password ?? "");
        }

        // ---- byte[] helpers ----

        private static void Write(byte[] buf, string value)
        {
            Array.Clear(buf, 0, buf.Length);
            var bytes = Encoding.UTF8.GetBytes(value ?? "");
            Array.Copy(bytes, buf, Math.Min(bytes.Length, buf.Length - 1));
        }

        private static string Read(byte[] buf) =>
            Encoding.UTF8.GetString(buf).TrimEnd('\0');

        // ---- build settings from current buffer values ----

        private DatabaseSettings CurrentSettings() => new DatabaseSettings
        {
            Server            = Read(_host),
            Port              = int.TryParse(Read(_port), out var p) ? p : 3306,
            Database          = Read(_database),
            Username          = Read(_username),
            Password          = Read(_password),
            ConnectionTimeout = 10
        };

        // ---- ImGui render ----

        public override void Render(Gui gui)
        {
            // Collect completed async test result.
            if (_pendingTest != null && _pendingTest.IsCompleted)
            {
                var r = _pendingTest.Result;
                _status   = r.Success ? "Connection successful!" : $"Error: {r.Error}";
                _statusOk = r.Success;
                _pendingTest = null;
            }

            if (!_positioned)
            {
                ImGui.SetNextWindowSize(new Vector2(340, 300), Condition.Always);
                ImGui.SetNextWindowPos(new Vector2(100, 200), Condition.Always, Vector2.Zero);
                _positioned = true;
            }

            ImGui.BeginWindow($"Database Connection###{Id}");

            ImGui.Text("EQEmu MySQL Connection");
            ImGui.Separator();

            ImGui.InputText($"Host###{Id}h",     _host,     (uint)_host.Length,     InputTextFlags.Default, null);
            ImGui.InputText($"Port###{Id}p",     _port,     (uint)_port.Length,     InputTextFlags.Default, null);
            ImGui.InputText($"Database###{Id}d", _database, (uint)_database.Length, InputTextFlags.Default, null);
            ImGui.InputText($"Username###{Id}u", _username, (uint)_username.Length, InputTextFlags.Default, null);
            ImGui.InputText($"Password###{Id}pw",_password, (uint)_password.Length, InputTextFlags.Default,   null);

            ImGui.Separator();

            if (_pendingTest != null)
            {
                ImGui.Text("Testing connection...");
            }
            else
            {
                if (ImGui.Button($"Test###{Id}t", new Vector2(100, 28)))
                    BeginTest();

                ImGui.SameLine();

                if (ImGui.Button($"Save & Connect###{Id}s", new Vector2(185, 28)))
                    SaveAndConnect();
            }

            if (_status != "")
            {
                var col = _statusOk
                    ? new Vector4(0.2f, 0.9f, 0.2f, 1f)
                    : new Vector4(0.9f, 0.2f, 0.2f, 1f);
                ImGui.Text(_status, col);
            }

            ImGui.EndWindow();
        }

        // ---- actions ----

        private void BeginTest()
        {
            _status = "";
            var factory = new MySqlConnectionFactory(CurrentSettings());
            _pendingTest = factory.TestConnectionAsync();
        }

        private void SaveAndConnect()
        {
            var db = CurrentSettings();
            _controller.Settings.Database = db;
            SettingsManager.Save(_controller.Settings);
            _controller.SetDbConnection(db);
            _status   = "Settings saved.";
            _statusOk = true;
        }
    }
}
