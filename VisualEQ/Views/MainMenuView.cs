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
using VisualEQ.EditSystem;
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
        private readonly Controller _controller;
        private readonly DatabaseFormWidget _dbForm;

        // Directory where the in-app converter writes `{zone}_oes.zip` and where the app
        // reads them from. Sourced from AppSettings via the controller so the value stays
        // in sync with the loader-side code paths (Controller.LoadZone,
        // SpawnManager.BuildAvailableModels).
        private string ConvertedZoneDir => _controller.ConvertedAssetsDir;

        private readonly byte[] _eqPath  = new byte[512];
        private readonly byte[] _decodeZone = new byte[64];

        private List<ZoneEntry> _zones = new List<ZoneEntry>();
        private DateTime _lastScan = DateTime.MinValue;

        private string _status = "";
        private bool   _statusOk;

        // Zone-load state machine. Mesh/AnimatedMesh constructors upload to GL immediately,
        // so the load must happen on the GL thread. To animate a progress bar between the
        // stages, we run one phase per frame — each frame paints the current progress, then
        // executes that phase's blocking work before returning.
        //
        // LoadGeometry and LoadDefault still block their whole frame (single monolithic
        // calls), but SpawnLoadChunk is repeated across many frames with a small chunk of
        // records per frame → the bar sweeps smoothly from ~68% to 98% for that phase.
        private enum LoadPhase
        {
            None,
            PaintMessage,
            ClearScene,
            LoadGeometry,
            LoadDefault,
            FetchSpawns,
            SpawnLoadChunk,
            FinishSpawns,
            CheckRecovery,   // 5.2 — detect pending buffer, show modal if non-empty
            Done
        }
        private const int SpawnLoadChunkSize = 10;
        private LoadPhase _loadPhase = LoadPhase.None;
        private string _loadingZone;
        private float _loadProgress;
        private string _loadLabel = "";

        // Recovery modal state during LoadPhase.CheckRecovery.
        private bool _recoveryChecked;
        private bool _recoveryModalActive;
        private EditBuffer _recoveryBuffer;

        // Decoder state. Converter is pure file I/O + CPU — safe to run on ThreadPool.
        private Task<DecodeResult> _decodeTask;
        private string _decodeLabel;

        // Batch decoder state for "Decode common globals". _batchProgress is updated by the
        // background task and read by the render thread — a simple lock keeps it consistent.
        private Task _batchTask;
        private readonly object _batchLock = new object();
        private string _batchProgress = "";
        private int _batchDone;
        private int _batchTotal;
        private int _batchSucceeded;

        // Two-click confirmation state for the destructive "delete decoded globals" action.
        private bool _deleteGlobalsPending;

        // Same pattern for per-zone deletes — null when nothing is pending, otherwise the
        // short-name of the zone whose row is currently showing Confirm/Cancel buttons.
        private string _deletePendingZone;

        public MainMenuWidget(Controller controller)
        {
            _controller = controller;
            _dbForm = new DatabaseFormWidget(controller);
            Write(_eqPath, controller.Settings.EqInstallPath ?? "");
        }

        public override void Render(Gui gui)
        {
            // Menu is hidden once a zone is loaded (unless a load is in flight).
            if (_controller.CurrentZoneName != null && _loadPhase == LoadPhase.None) return;

            ReapDecodeResult();
            ReapBatchResult();
            EnsureZoneList();

            // During load: only render the centered loading dialog. The main menu is hidden.
            if (_loadPhase != LoadPhase.None)
            {
                RenderLoadingDialog(gui);
                AdvanceLoadPhase();
                return;
            }

            ImGui.SetNextWindowSize(new Vector2(520, 560), Condition.FirstUseEver);
            ImGui.SetNextWindowPos(new Vector2(60, 60), Condition.FirstUseEver, Vector2.Zero);

            ImGui.BeginWindow($"VisualEQ###{Id}");

            ImGui.Text("VisualEQ — spawn editor for EQEmu");
            ImGui.Text("Tip: press F10 while a zone is loaded to return here.");
            ImGui.Separator();

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
                ImGui.Text($"No converted zones found in {ConvertedZoneDir}.");
                ImGui.Text("Use the 'Decode New Zone' section below to convert one.");
                return;
            }

            ImGui.BeginChild($"zonelist###{Id}zl", new Vector2(0, 260), true, WindowFlags.Default);
            foreach (var entry in _zones)
            {
                if (_deletePendingZone == entry.Name)
                {
                    ImGui.Text($"Delete {entry.Name}?");
                    ImGui.SameLine();
                    if (ImGui.Button($"Confirm###{Id}zdc_{entry.Name}", new Vector2(90, 22)))
                        DeleteZone(entry.Name);
                    ImGui.SameLine();
                    if (ImGui.Button($"Cancel###{Id}zdx_{entry.Name}", new Vector2(90, 22)))
                        _deletePendingZone = null;
                    continue;
                }

                // Small delete button leads the row; the Selectable takes the rest of the
                // width. Selectable comes first in ImGui without a size, so we intentionally
                // put the button first — SameLine after Selectable would overflow because
                // Selectable(label) spans the full column width by default.
                if (ImGui.SmallButton($"X###{Id}zd_{entry.Name}"))
                {
                    _deletePendingZone = entry.Name;
                    continue;
                }
                ImGui.SameLine();
                var label = $"{entry.Name}   ({entry.LastModified:yyyy-MM-dd HH:mm})";
                if (ImGui.Selectable(label))
                    BeginLoad(entry.Name);
            }
            ImGui.EndChild();

            if (ImGui.Button($"Refresh###{Id}zr", new Vector2(100, 26)))
                _lastScan = DateTime.MinValue;
        }

        // Removes {name}_oes.zip and (if present) {name}_chr_oes.zip from the assets dir.
        // Any pending edit buffer for the zone is left alone — the user may want to re-decode
        // later and pick their edits back up. Rescan + status line surface the result.
        void DeleteZone(string name)
        {
            var deletedFiles = new List<string>();
            var errors = new List<string>();
            foreach (var suffix in new[] { "_oes.zip", "_chr_oes.zip" })
            {
                var path = Path.Combine(ConvertedZoneDir, $"{name}{suffix}");
                if (!File.Exists(path)) continue;
                try { File.Delete(path); deletedFiles.Add(Path.GetFileName(path)); }
                catch (Exception ex)
                {
                    errors.Add($"{Path.GetFileName(path)}: {ex.Message}");
                    Console.WriteLine($"[DeleteZone] '{path}' failed: {ex.Message}");
                }
            }

            _deletePendingZone = null;
            _lastScan = DateTime.MinValue;

            // Drop any cached snapshot so a subsequent re-decode + load re-parses fresh.
            if (deletedFiles.Count > 0)
                _controller.InvalidateZoneSnapshot(name);

            if (errors.Count > 0)
            {
                _status = $"Delete failed: {string.Join("; ", errors)}";
                _statusOk = false;
            }
            else if (deletedFiles.Count > 0)
            {
                _status = $"Deleted {string.Join(", ", deletedFiles)}.";
                _statusOk = true;
            }
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
                ImGui.Text($"Decoding {_decodeLabel}...");
                // ImGui.NET 0.4.6's default font is Proggy Clean (ASCII only). Non-ASCII
                // glyphs (en dash '–', em dash '—', ellipsis '…') render as
                // '?' or truncate the string at the byte where the missing glyph starts —
                // "10-60" here previously used an en dash and got cut off at "10".
                ImGui.Text("(This can take 10 to 60 seconds. Check the console window for progress.)");
                return;
            }

            if (_batchTask != null)
            {
                string current;
                int done, total, ok;
                lock (_batchLock)
                {
                    current = _batchProgress;
                    done = _batchDone;
                    total = _batchTotal;
                    ok = _batchSucceeded;
                }
                ImGui.Text($"Batch decoding globals: {done}/{total} ({ok} succeeded)");
                if (current != "") ImGui.Text($"Current: {current}");
                ImGui.Text("(This can take several minutes. Progress in the console window.)");
                return;
            }

            if (ImGui.Button($"Decode###{Id}dgo", new Vector2(180, 28)))
                BeginDecode();

            ImGui.SameLine();

            if (ImGui.Button($"Decode common globals###{Id}dgb", new Vector2(220, 28)))
                BeginBatchDecodeGlobals();

            ImGui.Text("Batch: decodes global*_chr.s3d (playable races + monsters).");

            RenderDeleteGlobalsControls();
        }

        void RenderDeleteGlobalsControls()
        {
            var globalZips = Directory.Exists(ConvertedZoneDir)
                ? Directory.EnumerateFiles(ConvertedZoneDir, "global*_chr_oes.zip").ToList()
                : new List<string>();

            if (globalZips.Count == 0)
            {
                _deleteGlobalsPending = false;
                return;
            }

            ImGui.Separator();

            if (!_deleteGlobalsPending)
            {
                if (ImGui.Button($"Delete decoded globals ({globalZips.Count})###{Id}dgd", new Vector2(260, 26)))
                    _deleteGlobalsPending = true;
                ImGui.Text("Removes global*_chr_oes.zip so you can regenerate from a different EQ source.");
                return;
            }

            ImGui.Text($"Delete {globalZips.Count} global chr zips?");
            if (ImGui.Button($"Confirm delete###{Id}dgc", new Vector2(160, 28)))
            {
                int deleted = 0;
                foreach (var path in globalZips)
                {
                    try { File.Delete(path); deleted++; }
                    catch (Exception ex) { Console.WriteLine($"[DeleteGlobals] '{path}' failed: {ex.Message}"); }
                }
                _deleteGlobalsPending = false;
                _lastScan = DateTime.MinValue;
                _status = $"Deleted {deleted}/{globalZips.Count} global chr zips.";
                _statusOk = deleted == globalZips.Count;
            }
            ImGui.SameLine();
            if (ImGui.Button($"Cancel###{Id}dgx", new Vector2(120, 28)))
                _deleteGlobalsPending = false;
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

            // Pre-flight source check: catch typos ("kaladimROFL" vs "kaladima") before
            // launching a task that would silently return "not found or not decodable"
            // after a delay. Only enforced when the zone hasn't already been decoded —
            // an existing {zone}_oes.zip means the decode step is a no-op skip and we
            // don't care whether the source files are still on disk.
            var zoneAlreadyDecoded = File.Exists(Path.Combine(ConvertedZoneDir, $"{zoneName}_oes.zip"));
            if (!zoneAlreadyDecoded)
            {
                var wldPath = Path.Combine(eqPath, $"{zoneName}_obj.s3d");
                var eqgPath = Path.Combine(eqPath, $"{zoneName}.eqg");
                if (!File.Exists(wldPath) && !File.Exists(eqgPath))
                {
                    _status   = $"Zone '{zoneName}' not found in EQ install "
                              + $"(looked for {zoneName}_obj.s3d or {zoneName}.eqg). "
                              + "Check the zone short name and EQ install path.";
                    _statusOk = false;
                    return;
                }
            }

            _status = "";
            _decodeLabel = zoneName;
            _decodeTask = Task.Run(() => RunDecode(eqPath, zoneName));
        }

        // Runs on ThreadPool. Converter is file I/O + CPU only — no GL calls, safe off-thread.
        // Instance method (not static) so ConvertedZoneDir — now a controller-backed property —
        // resolves against `this`. The closure in the calling Task.Run captures `this` for us.
        DecodeResult RunDecode(string eqPath, string zoneName)
        {
            var result = new DecodeResult { Zone = zoneName };
            var converter = new Converter(eqPath, ConvertedZoneDir);

            // Skip conversion when the output zip already exists — decoding is expensive
            // (10-60s + several MB of I/O) and produces byte-identical output for the same
            // source. To force a re-decode, delete the entry from the zone list first.
            var zonePath = Path.Combine(ConvertedZoneDir, $"{zoneName}_oes.zip");
            var chrPath  = Path.Combine(ConvertedZoneDir, $"{zoneName}_chr_oes.zip");

            // Zone and character conversion each get their own try/catch so a chr failure
            // (e.g. the S3D header-count mismatch on Velious _chr archives) doesn't wipe
            // out an already-successful zone conversion. The zone _oes.zip has already
            // been written to disk by the time character conversion runs.
            if (File.Exists(zonePath))
            {
                result.ZoneSkipped = true;
            }
            else
            {
                try
                {
                    result.ZoneStatus = converter.Convert(zoneName);
                }
                catch (Exception ex)
                {
                    result.ZoneError = ex.Message;
                    Console.WriteLine($"[Decode] Zone '{zoneName}' failed: {ex}");
                }
            }

            if (File.Exists(chrPath))
            {
                result.CharacterSkipped = true;
            }
            else
            {
                try
                {
                    result.CharacterStatus = converter.Convert(zoneName + "_chr");
                }
                catch (Exception ex)
                {
                    result.CharacterError = ex.Message;
                    Console.WriteLine($"[Decode] Character '{zoneName}_chr' failed: {ex}");
                }
            }

            return result;
        }

        void ReapDecodeResult()
        {
            if (_decodeTask == null || !_decodeTask.IsCompleted) return;

            var r = _decodeTask.Result;
            _decodeTask = null;
            _decodeLabel = null;

            var msgs = new List<string>();
            if (r.ZoneSkipped)
                msgs.Add($"Zone '{r.Zone}' already decoded — skipped.");
            else if (r.ZoneError != null)
                msgs.Add($"Zone '{r.Zone}' failed: {r.ZoneError}");
            else
                msgs.Add(r.ZoneStatus == ConvertedType.None
                    ? $"Zone '{r.Zone}' not found or not decodable."
                    : $"Zone '{r.Zone}' decoded.");

            if (r.CharacterSkipped)
                msgs.Add($"Characters '{r.Zone}_chr' already decoded — skipped.");
            else if (r.CharacterError != null)
                msgs.Add($"Characters '{r.Zone}_chr' failed: {r.CharacterError}");
            else
                msgs.Add(r.CharacterStatus == ConvertedType.None
                    ? $"No characters found for '{r.Zone}' (optional)."
                    : $"Characters '{r.Zone}_chr' decoded.");

            _status   = string.Join(" ", msgs);
            // Success = zone was actually written OR the existing zone zip was left in place
            // (skipping is a valid successful no-op). A chr failure is a warning, not a hard
            // failure — the runtime falls back to gfaydark_chr for missing character meshes
            // so the zone is still usable.
            _statusOk = (r.ZoneStatus != ConvertedType.None || r.ZoneSkipped) && r.ZoneError == null;

            // Refresh the zone list so the new entry appears immediately.
            _lastScan = DateTime.MinValue;

            // Drop any stale geometry snapshot so the next load of this zone re-parses
            // the freshly-written OES instead of restoring the pre-decode Model instances.
            if (r.ZoneStatus != ConvertedType.None && r.ZoneError == null)
                _controller.InvalidateZoneSnapshot(r.Zone);
        }

        struct DecodeResult
        {
            public string Zone;
            public ConvertedType ZoneStatus;
            public ConvertedType CharacterStatus;
            public string ZoneError;
            public string CharacterError;
            public bool ZoneSkipped;
            public bool CharacterSkipped;
        }

        void BeginBatchDecodeGlobals()
        {
            var eqPath = Read(_eqPath).Trim();
            if (string.IsNullOrEmpty(eqPath) || !Directory.Exists(eqPath))
            {
                _status   = "EverQuest install path is missing or does not exist.";
                _statusOk = false;
                return;
            }

            // Collect unique global chr roots by stripping trailing digits from `global*_chr*.s3d`.
            var roots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var file in Directory.EnumerateFiles(eqPath, "global*_chr*.s3d"))
            {
                var name = Path.GetFileNameWithoutExtension(file);
                while (name.Length > 0 && char.IsDigit(name[name.Length - 1]))
                    name = name.Substring(0, name.Length - 1);
                if (name.EndsWith("_chr", StringComparison.OrdinalIgnoreCase))
                    roots.Add(name);
            }

            if (roots.Count == 0)
            {
                _status   = "No global*_chr*.s3d files found in the EQ install path.";
                _statusOk = false;
                return;
            }

            var ordered = roots.OrderBy(s => s, StringComparer.OrdinalIgnoreCase).ToList();

            lock (_batchLock)
            {
                _batchTotal = ordered.Count;
                _batchDone = 0;
                _batchSucceeded = 0;
                _batchProgress = "";
            }
            _status = "";
            _batchTask = Task.Run(() => RunBatchDecode(eqPath, ordered));
        }

        void RunBatchDecode(string eqPath, List<string> roots)
        {
            var converter = new Converter(eqPath, ConvertedZoneDir);
            foreach (var root in roots)
            {
                lock (_batchLock) _batchProgress = root;

                // Skip when the output already exists — a rerun of the batch decoder should
                // be idempotent for anything already on disk. Skipped items count towards
                // "succeeded" so the final tally reflects the total decoded population,
                // not just what got (re)written this run.
                if (File.Exists(Path.Combine(ConvertedZoneDir, $"{root}_oes.zip")))
                {
                    lock (_batchLock)
                    {
                        _batchSucceeded++;
                        _batchDone++;
                    }
                    continue;
                }

                try
                {
                    var status = converter.Convert(root);
                    if (status != ConvertedType.None)
                        lock (_batchLock) _batchSucceeded++;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[BatchDecode] '{root}' failed: {ex.Message}");
                }
                lock (_batchLock) _batchDone++;
            }
            lock (_batchLock) _batchProgress = "";
        }

        void ReapBatchResult()
        {
            if (_batchTask == null || !_batchTask.IsCompleted) return;

            int done, total, ok;
            lock (_batchLock)
            {
                done = _batchDone;
                total = _batchTotal;
                ok = _batchSucceeded;
            }

            _batchTask = null;
            _status   = $"Global batch decode: {ok}/{total} succeeded.";
            _statusOk = ok > 0;
            _lastScan = DateTime.MinValue; // refresh zone list
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
            ImGui.Text("Spawn state markers (vertical lines above spawns):");

            var s = _controller.Settings;
            bool sel = s.ShowSelectedMarker;
            bool dirty = s.ShowDirtyMarkers;
            bool ph = s.ShowPlaceholderMarkers;
            bool grids = s.ShowPathGrids;

            bool changed = false;
            if (ImGui.Checkbox($"Selected spawn (cyan)###{Id}sSel", ref sel))
            { s.ShowSelectedMarker = sel; changed = true; }
            if (ImGui.Checkbox($"Dirty spawns (orange)###{Id}sDirty", ref dirty))
            { s.ShowDirtyMarkers = dirty; changed = true; }
            if (ImGui.Checkbox($"Placeholder spawns (yellow)###{Id}sPh", ref ph))
            { s.ShowPlaceholderMarkers = ph; changed = true; }
            if (ImGui.Checkbox($"Path grid for selected spawn (amber)###{Id}sGrid", ref grids))
            { s.ShowPathGrids = grids; changed = true; }
            if (changed) SettingsManager.Save(s);

            ImGui.Separator();
            _dbForm.RenderInline();
        }

        void BeginLoad(string zoneName)
        {
            _status = "";
            _loadingZone = zoneName;
            _loadPhase = LoadPhase.PaintMessage;
            _loadProgress = 0f;
            _loadLabel = "Preparing…";
            _recoveryChecked = false;
            _recoveryModalActive = false;
            _recoveryBuffer = null;
        }

        void RenderLoadingDialog(Gui gui)
        {
            // Centered on the OS window; fixed-size, non-movable, non-resizable modal. The
            // dialog grows taller when the recovery buttons are showing.
            const float dlgW = 480f;
            var dlgH = _recoveryModalActive ? 240f : 150f;
            var pos = new Vector2((gui.Dimensions.X - dlgW) / 2, (gui.Dimensions.Y - dlgH) / 2);

            ImGui.SetNextWindowPos(pos, Condition.Always, Vector2.Zero);
            ImGui.SetNextWindowSize(new Vector2(dlgW, dlgH), Condition.Always);

            const WindowFlags flags = WindowFlags.NoTitleBar | WindowFlags.NoMove
                                    | WindowFlags.NoResize   | WindowFlags.NoCollapse
                                    | WindowFlags.NoSavedSettings | WindowFlags.NoBringToFrontOnFocus;

            ImGui.BeginWindow($"###{Id}Loading", flags);

            if (_recoveryModalActive)
                RenderRecoveryModal();
            else
            {
                ImGui.Text($"Loading zone '{_loadingZone}'");
                ImGui.Text(_loadLabel);
                ImGui.Separator();
                ImGui.ProgressBar(_loadProgress, new Vector2(0, 28), $"{(_loadProgress * 100):F0}%");
            }

            ImGui.EndWindow();
        }

        void RenderRecoveryModal()
        {
            var b = _recoveryBuffer;
            ImGui.Text($"Unsaved changes for zone '{_loadingZone}'");
            ImGui.Text("found from a previous session.");
            ImGui.Separator();
            ImGui.Text($"  {b.Spawns.Count} pending spawn moves");
            if (b.GridEntries.Count > 0)
                ImGui.Text($"  {b.GridEntries.Count} pending grid edits");
            ImGui.Text($"  Last edit: {b.LastModifiedAt.ToLocalTime():yyyy-MM-dd HH:mm}");
            ImGui.Separator();

            var sz = new Vector2(130, 28);
            if (ImGui.Button($"Restore###{Id}rvR", sz))
                FinishRecoveryWith(applyRestore: true, discard: false, cancel: false);
            ImGui.SameLine();
            if (ImGui.Button($"Discard###{Id}rvD", sz))
                FinishRecoveryWith(applyRestore: false, discard: true, cancel: false);
            ImGui.SameLine();
            if (ImGui.Button($"Cancel###{Id}rvC", sz))
                FinishRecoveryWith(applyRestore: false, discard: false, cancel: true);
        }

        void FinishRecoveryWith(bool applyRestore, bool discard, bool cancel)
        {
            try
            {
                if (applyRestore)
                    _controller.ApplyPendingBuffer(_recoveryBuffer);
                else if (discard)
                    EditBufferManager.DeleteForZone(_recoveryBuffer.Zone);
                else if (cancel)
                {
                    _controller.ClearCurrentZone();
                    _loadPhase = LoadPhase.None;
                    _loadingZone = null;
                    _loadProgress = 0f;
                    _loadLabel = "";
                    _recoveryChecked = false;
                    _recoveryModalActive = false;
                    _recoveryBuffer = null;
                    return;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MainMenu] Recovery '{_loadingZone}' failed: {ex}");
            }

            _recoveryModalActive = false;
            _recoveryBuffer = null;
            _loadProgress = 1f;
            _loadPhase = LoadPhase.Done;
            _loadLabel = "Ready.";
        }

        // Runs one phase of the load per frame. The bar's % shown BEFORE this call is what
        // the user sees during the phase's blocking work (so the label describes what's
        // happening while the bar sits at that value).
        void AdvanceLoadPhase()
        {
            try
            {
                switch (_loadPhase)
                {
                    case LoadPhase.PaintMessage:
                        _loadPhase = LoadPhase.ClearScene;
                        _loadLabel = "Clearing previous scene…";
                        break;

                    case LoadPhase.ClearScene:
                        _controller.ClearCurrentZone();
                        _loadProgress = 0.10f;
                        _loadPhase = LoadPhase.LoadGeometry;
                        _loadLabel = "Loading zone geometry…";
                        break;

                    case LoadPhase.LoadGeometry:
                        _controller.LoadZone(_loadingZone);
                        _loadProgress = 0.55f;
                        _loadPhase = LoadPhase.LoadDefault;
                        _loadLabel = "Loading fallback character…";
                        break;

                    case LoadPhase.LoadDefault:
                        _controller.LoadDefaultCharacterForZone(_loadingZone);
                        // Restore the last-known camera pose for this zone if we've been
                        // here before this session; otherwise drop to the (0,0,1000)
                        // default so first-visit still looks down from above the map.
                        if (!_controller.TryRestoreCameraForZone(_loadingZone))
                            VisualEQ.Engine.Globals.Camera.Position = new Vector3(0, 0, 1000);
                        _loadProgress = 0.65f;
                        _loadPhase = LoadPhase.FetchSpawns;
                        _loadLabel = "Fetching spawns from database…";
                        break;

                    case LoadPhase.FetchSpawns:
                        if (_controller.BeginSpawnLoad(_loadingZone))
                        {
                            _loadProgress = 0.68f;
                            _loadPhase = LoadPhase.SpawnLoadChunk;
                            _loadLabel = _controller.SpawnLoadTotal > 0
                                ? $"Placing spawn 0 of {_controller.SpawnLoadTotal}…"
                                : "No spawns for this zone.";
                        }
                        else
                        {
                            // No DB or already loaded — skip straight to done.
                            _loadProgress = 1.0f;
                            _loadPhase = LoadPhase.Done;
                            _loadLabel = "Ready.";
                        }
                        break;

                    case LoadPhase.SpawnLoadChunk:
                        _controller.ContinueSpawnLoad(SpawnLoadChunkSize);
                        if (_controller.SpawnLoadDone)
                        {
                            _loadProgress = 0.98f;
                            _loadPhase = LoadPhase.FinishSpawns;
                            _loadLabel = "Finalizing…";
                        }
                        else
                        {
                            // Progress sweeps 68% → 98% across the chunk phase.
                            float pct = _controller.SpawnLoadTotal == 0
                                ? 1f
                                : (float)_controller.SpawnLoadProcessed / _controller.SpawnLoadTotal;
                            _loadProgress = 0.68f + pct * 0.30f;
                            _loadLabel = $"Placing spawn {_controller.SpawnLoadProcessed} of {_controller.SpawnLoadTotal}…";
                        }
                        break;

                    case LoadPhase.FinishSpawns:
                        _controller.FinishSpawnLoad();
                        _loadProgress = 0.99f;
                        _loadPhase = LoadPhase.CheckRecovery;
                        _loadLabel = "Checking for unsaved changes…";
                        break;

                    case LoadPhase.CheckRecovery:
                        // First entry: query disk. Subsequent frames (while modal is up)
                        // do nothing here — the button handlers advance the state.
                        if (_recoveryModalActive) break;
                        if (!_recoveryChecked)
                        {
                            _recoveryChecked = true;
                            _recoveryBuffer = EditBufferManager.LoadForZone(_loadingZone);
                            if (_recoveryBuffer != null && !_recoveryBuffer.IsEmpty)
                            {
                                _recoveryModalActive = true;
                                _loadLabel = $"{_recoveryBuffer.TotalPending} pending edits from last session";
                                break;
                            }
                            _recoveryBuffer = null;
                        }
                        _loadProgress = 1f;
                        _loadPhase = LoadPhase.Done;
                        _loadLabel = "Ready.";
                        break;

                    case LoadPhase.Done:
                        _loadPhase = LoadPhase.None;
                        _loadingZone = null;
                        _loadProgress = 0f;
                        _loadLabel = "";
                        break;
                }
            }
            catch (Exception ex)
            {
                _status   = $"Load failed: {ex.Message}";
                _statusOk = false;
                Console.WriteLine($"[MainMenu] Load '{_loadingZone}' failed at {_loadPhase}: {ex}");
                _loadPhase = LoadPhase.None;
                _loadingZone = null;
                _loadProgress = 0f;
                _loadLabel = "";
            }
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

            _zones = _zones.OrderBy(z => z.Name, StringComparer.OrdinalIgnoreCase).ToList();
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
