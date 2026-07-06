using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using ImGuiNET;
using NsimGui;
using NsimGui.Widgets;
using VisualEQ.ConverterCore;
using VisualEQ.Settings;

namespace VisualEQ.Views
{
    // Full-screen main menu shown when no zone is loaded. Lists convertible zones,
    // opens the decode workflow (Phase 5), and exposes settings shortcuts.
    public class MainMenuView : BaseView
    {
        private readonly MainMenuWidget _widget;

        public MainMenuView(Controller controller) : base(controller)
        {
            _widget = new MainMenuWidget(controller);
        }

        public override void Setup(Gui gui)
        {
            gui.Add(_widget);
        }
    }

    internal class MainMenuWidget : BaseWidget
    {
        // Directory where ConverterApp writes converted zones. Kept in sync with Loader.LoadZoneFile,
        // Controller.LoadCharacter, SpawnManager.BuildAvailableModels — see CLAUDE.md §4.
        private const string ConvertedZoneDir = "../ConverterApp";

        private readonly Controller _controller;
        private readonly DatabaseFormWidget _dbForm;

        private readonly byte[] _eqPath  = new byte[512];
        private readonly byte[] _decodeZone = new byte[64];

        private List<ZoneEntry> _zones = new List<ZoneEntry>();
        private DateTime _lastScan = DateTime.MinValue;

        private string _status = "";
        private bool   _statusOk;

        // Zone-load handshake. Mesh/AnimatedMesh constructors upload to GL immediately,
        // so the load must happen on the GL thread (inside Render). To also let the
        // "Loading…" message paint before we block the frame, we defer the actual load
        // by one frame: frame A paints the message, frame B runs the load.
        private string _loadingZone;
        private bool   _loadingMessagePainted;

        // Decoder state. Converter is pure file I/O + CPU — safe to run on ThreadPool.
        private Task<DecodeResult> _decodeTask;
        private string _decodeLabel;

        public MainMenuWidget(Controller controller)
        {
            _controller = controller;
            _dbForm = new DatabaseFormWidget(controller);
            Write(_eqPath, controller.Settings.EqInstallPath ?? "");
        }

        public override void Render(Gui gui)
        {
            // Menu is hidden once a zone is loaded (unless a load is in flight).
            if (_controller.CurrentZoneName != null && _loadingZone == null) return;

            ReapDecodeResult();
            EnsureZoneList();

            ImGui.SetNextWindowSize(new Vector2(520, 560), Condition.FirstUseEver);
            ImGui.SetNextWindowPos(new Vector2(60, 60), Condition.FirstUseEver, Vector2.Zero);

            ImGui.BeginWindow($"VisualEQ###{Id}");

            ImGui.Text("VisualEQ — spawn editor for EQEmu");
            ImGui.Text("Tip: press F10 while a zone is loaded to return here.");
            ImGui.Separator();

            // Zone-load handshake — see field comments.
            if (_loadingZone != null)
            {
                ImGui.Text($"Loading zone '{_loadingZone}'…");
                ImGui.EndWindow();

                if (!_loadingMessagePainted)
                {
                    // First frame after click: let ImGui finish this frame so the user
                    // sees the message before we block the GL thread on the load.
                    _loadingMessagePainted = true;
                    return;
                }

                // Second frame: run the load synchronously on the GL thread.
                var zoneName = _loadingZone;
                _loadingZone = null;
                _loadingMessagePainted = false;

                try
                {
                    _controller.LoadZoneFromMenu(zoneName);
                    // Menu hides itself now that CurrentZoneName is set.
                }
                catch (Exception ex)
                {
                    _status   = $"Load failed: {ex.Message}";
                    _statusOk = false;
                    Console.WriteLine($"[MainMenu] Load '{zoneName}' failed: {ex}");
                }
                return;
            }

            RenderZonesSection();
            RenderDecodeSection();
            RenderSettingsSection();

            if (_status != "")
            {
                ImGui.Separator();
                var col = _statusOk
                    ? new Vector4(0.2f, 0.9f, 0.2f, 1f)
                    : new Vector4(0.9f, 0.2f, 0.2f, 1f);
                ImGui.Text(_status, col);
            }

            ImGui.EndWindow();
        }

        void RenderZonesSection()
        {
            if (!ImGui.CollapsingHeader($"Available Zones ({_zones.Count})###{Id}z", TreeNodeFlags.DefaultOpen))
                return;

            if (_zones.Count == 0)
            {
                ImGui.Text("No converted zones found in ConverterApp/.");
                ImGui.Text("Use the 'Decode New Zone' section below to convert one.");
                return;
            }

            ImGui.BeginChild($"zonelist###{Id}zl", new Vector2(0, 260), true, WindowFlags.Default);
            foreach (var entry in _zones)
            {
                var label = $"{entry.Name}   ({entry.LastModified:yyyy-MM-dd HH:mm})";
                if (ImGui.Selectable(label))
                {
                    _status = "";
                    _loadingZone = entry.Name;
                    _loadingMessagePainted = false;
                }
            }
            ImGui.EndChild();

            if (ImGui.Button($"Refresh###{Id}zr", new Vector2(100, 26)))
                _lastScan = DateTime.MinValue;
        }

        void RenderDecodeSection()
        {
            if (!ImGui.CollapsingHeader($"Decode New Zone###{Id}d", 0))
                return;

            ImGui.Text("EverQuest install path:");
            ImGui.InputText($"###{Id}dp", _eqPath, (uint)_eqPath.Length, InputTextFlags.Default, null);

            ImGui.Text("Zone short name (e.g. gfaydark, oasis, freportn):");
            ImGui.InputText($"###{Id}dz", _decodeZone, (uint)_decodeZone.Length, InputTextFlags.Default, null);

            if (_decodeTask != null)
            {
                ImGui.Text($"Decoding {_decodeLabel}…");
                ImGui.Text("(This can take 10–60 seconds. Check the console window for progress.)");
                return;
            }

            if (ImGui.Button($"Decode###{Id}dgo", new Vector2(180, 28)))
                BeginDecode();
        }

        void BeginDecode()
        {
            var eqPath = Read(_eqPath).Trim();
            var zoneName = Read(_decodeZone).Trim();

            if (string.IsNullOrEmpty(eqPath) || !Directory.Exists(eqPath))
            {
                _status   = "EverQuest install path is missing or does not exist.";
                _statusOk = false;
                return;
            }
            if (string.IsNullOrEmpty(zoneName))
            {
                _status   = "Zone short name is required.";
                _statusOk = false;
                return;
            }
            if (!Directory.Exists(ConvertedZoneDir))
            {
                _status   = $"Output directory '{ConvertedZoneDir}' does not exist.";
                _statusOk = false;
                return;
            }

            _status = "";
            _decodeLabel = zoneName;
            _decodeTask = Task.Run(() => RunDecode(eqPath, zoneName));
        }

        // Runs on ThreadPool. Converter is file I/O + CPU only — no GL calls, safe off-thread.
        static DecodeResult RunDecode(string eqPath, string zoneName)
        {
            var result = new DecodeResult { Zone = zoneName };
            try
            {
                var converter = new Converter(eqPath, ConvertedZoneDir);
                result.ZoneStatus = converter.Convert(zoneName);
                result.CharacterStatus = converter.Convert(zoneName + "_chr");
            }
            catch (Exception ex)
            {
                result.Error = ex.Message;
                Console.WriteLine($"[Decode] Failed: {ex}");
            }
            return result;
        }

        void ReapDecodeResult()
        {
            if (_decodeTask == null || !_decodeTask.IsCompleted) return;

            var r = _decodeTask.Result;
            _decodeTask = null;
            _decodeLabel = null;

            if (r.Error != null)
            {
                _status   = $"Decode failed: {r.Error}";
                _statusOk = false;
                return;
            }

            var msgs = new List<string>();
            msgs.Add(r.ZoneStatus == ConvertedType.None
                ? $"Zone '{r.Zone}' not found or not decodable."
                : $"Zone '{r.Zone}' decoded.");
            msgs.Add(r.CharacterStatus == ConvertedType.None
                ? $"No characters found for '{r.Zone}' (optional)."
                : $"Characters '{r.Zone}_chr' decoded.");

            _status   = string.Join(" ", msgs);
            _statusOk = r.ZoneStatus != ConvertedType.None;

            // Refresh the zone list so the new entry appears immediately.
            _lastScan = DateTime.MinValue;
        }

        struct DecodeResult
        {
            public string Zone;
            public ConvertedType ZoneStatus;
            public ConvertedType CharacterStatus;
            public string Error;
        }

        void RenderSettingsSection()
        {
            if (!ImGui.CollapsingHeader($"Settings###{Id}s", 0))
                return;

            ImGui.Text("EQ install path (used by the decoder):");
            ImGui.InputText($"###{Id}sp", _eqPath, (uint)_eqPath.Length, InputTextFlags.Default, null);

            if (ImGui.Button($"Save EQ Path###{Id}ss", new Vector2(160, 28)))
            {
                _controller.Settings.EqInstallPath = Read(_eqPath);
                SettingsManager.Save(_controller.Settings);
                _status   = "EQ path saved.";
                _statusOk = true;
            }

            ImGui.Separator();
            _dbForm.RenderInline();
        }

        void EnsureZoneList()
        {
            // Rescan on demand — cheap enough (directory listing) that we do it once per Refresh.
            if (_lastScan != DateTime.MinValue) return;

            _zones.Clear();

            if (!Directory.Exists(ConvertedZoneDir))
            {
                _lastScan = DateTime.UtcNow;
                return;
            }

            foreach (var path in Directory.EnumerateFiles(ConvertedZoneDir, "*_oes.zip"))
            {
                var file = Path.GetFileName(path);
                // Skip character zips — they aren't loadable zones.
                if (file.Contains("_chr_")) continue;

                var name = file.Substring(0, file.Length - "_oes.zip".Length);
                _zones.Add(new ZoneEntry(name, File.GetLastWriteTime(path)));
            }

            _zones = _zones.OrderByDescending(z => z.LastModified).ToList();
            _lastScan = DateTime.UtcNow;
        }

        static void Write(byte[] buf, string value)
        {
            Array.Clear(buf, 0, buf.Length);
            var bytes = Encoding.UTF8.GetBytes(value ?? "");
            Array.Copy(bytes, buf, Math.Min(bytes.Length, buf.Length - 1));
        }

        static string Read(byte[] buf) => Encoding.UTF8.GetString(buf).TrimEnd('\0');

        struct ZoneEntry
        {
            public readonly string Name;
            public readonly DateTime LastModified;
            public ZoneEntry(string name, DateTime lastModified) { Name = name; LastModified = lastModified; }
        }
    }
}
