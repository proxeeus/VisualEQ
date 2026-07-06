# CLAUDE.md — VisualEQ

Living reference for future Claude sessions in this repo. Update when architecture, invariants, or build/run commands change.

## 1. What this project is

VisualEQ is a **3D editor for EQEmu server data**. It renders a converted EverQuest zone in real time and overlays the server's `spawn2` data as manipulable NPC models. The near-term goal is a full spawn editor (move / edit / save NPCs, path grids). The long-term goal is a broader server-ops toolkit (loot tables, quests, etc.). Successor in spirit to Daeken's original VisualEQ viewer.

## 2. Solution layout

Root `VisualEQ.sln` — 12 projects, all `net8.0`, `Any CPU`.

| Project | Role |
|---|---|
| `VisualEQ/` | App entry point (`App.cs`), `Controller`, `Loader`, `Settings/`, `SpawnSystem/`, `Views/` |
| `Engine/` | OpenGL rendering (`EngineCore` : `GameWindow`), forward + deferred pathways, camera, models, animation, mouse-picking (`ModelSelector`) |
| `Database/` (assembly `VisualEQ.Database`) | Dapper + MySqlConnector layer: `Configuration/`, `Models/`, `Repositories/`, `Constants/SqlQueries.cs`, `ViewModels/`, `Exceptions/` |
| `Common/` | `OESFile` chunked binary format (root/mat/fx/tex/zone/char/obj/inst/lit/skin/mesh/aset/amsh/abuf) + Extensions |
| `NsimGui/` | Declarative wrapper over ImGui.NET 0.4.6 (`Window`, `Text`, `HBox`, `Size`, ...); backing renderer in `Engine/GuiRenderer.cs` |
| `CollisionManager/` | Octree + triangle collision used for camera physics and drag-to-surface sticking |
| `ConverterCore/` + `ConverterApp/` | Convert raw EQ client files → `.oes.zip`. `ConverterApp/` is where converted zone/character zips live at runtime (`gfaydark_oes.zip`, `gfaydark_chr_oes.zip`, ...) |
| `LegacyFileReader/` | Raw EQ format readers (S3D, WLD, Zon, TerMod) used by the converter |
| `ImageLib/` | PNG/BMP/DDS decoders used when reading OES textures |
| `ModelLister/` | CLI: dump character models + animations from a `_chr_oes.zip` |
| `OesDumper/` | CLI: dump OES chunk tree for debugging |
| `Zlib.Portable/` | Vendored zlib |

Global settings:
- `Directory.Build.props` pins `LangVersion=7.3` and the StyleCop ruleset (`VisualEQ.ruleset`, `stylecop.json`).
- `VisualEQ/VisualEQ.csproj` has a `CopyNativeLibs` target that copies `runtimes/win-x64/native/cimgui.dll` next to `VisualEQ.dll` after build. **Do not remove this** — see §4.

## 3. Runtime data flow

```
App.Main(zoneName, [modelName|--list-models])
  └─ SettingsManager.Load()                          → %APPDATA%\VisualEQ\settings.json → AppSettings
  └─ new Controller(settings)                        → creates EngineCore, MySqlConnectionFactory (if DB configured)
  └─ Controller.LoadZone(zoneName)                   → Loader.LoadZoneFile("../ConverterApp/<zone>_oes.zip")
                                                       parses main.oes as OESZone, adds static geometry + object instances + lights to Engine
  └─ LoadCharacters(...)                             → picks a default AniModel (usually ORC), Controller.LoadCharacter → LastModelLoaded
  └─ Controller.LoadZoneSpawnsAsync(zoneName)        → SpawnRepository.GetZoneSpawnsFullAsync → SpawnManager.LoadFromRecords
                                                       → per-record: RaceModelMapper → cache/load AniModel → AniModelInstance → engine.Add
  └─ Views (StatusView, ModelEditorView, DatabaseConnectionView, TeleportView) added to controller
  └─ Controller.Start() → EngineCore.Start()         → builds collision octree from IsFixed+IsCollidable meshes, then GameWindow.Run()
```

Runtime loop (per frame):
- `OnUpdateFrame` reads keyboard/mouse for camera movement — **suppressed when `Gui.KeyboardWanted` is true** so typing in text fields doesn't drive the camera.
- `OnRenderFrame` runs deferred pass (if enabled, key `L`) then forward pass; face culling is currently `GL.Disable(EnableCap.CullFace)` — see §7.
- Mouse input: right-drag rotates camera (hides cursor); left-click without GUI hits `ModelSelector.TrySelect` → drag with plane-projection + surface sticking via `CollisionHelper`; mouse-wheel while dragging adjusts drag-plane depth.

## 4. Build & run

**Physical environment: Apple M2 Mac → Parallels → Windows 11 ARM64.** Two dotnet installs are expected on the Windows side:
- `C:\Program Files\dotnet\dotnet.exe` — ARM64 SDK + runtime. Used for **building** and for the converter/lister CLIs.
- `C:\Program Files\dotnet\x64\dotnet.exe` — x64 **runtime only** (no SDK). Used for **running** `VisualEQ.dll` because `cimgui.dll` is x64-native and won't load into an ARM64 process.

Do **not** try to switch to `dotnet run` for the main app; it will pick the ARM64 runtime and crash on `cimgui.dll`. Always launch the built DLL through the x64 runtime with `dotnet exec`. `load_zone.bat` and `list_models.bat` already do this — extend them if the launch pattern changes.

Build everything: `dotnet build` at the repo root.

Run: `.\visualeq.bat` (opens the in-app main menu — zone browser, in-app decoder, settings). Legacy CLI: `.\load_zone.bat` or `.\load_zone.bat "C:\Program Files (x86)\EverQuest" gfaydark` skips the menu and loads a zone directly. Both invoke `"%DOTNET_X64%" exec "bin\Debug\net8.0\VisualEQ.dll" [<zone>]` inside `VisualEQ/`.

From the main menu you can list available zones, decode a new zone (in-process — no external bat needed), and edit settings (EQ install path, DB). Once a zone is loaded, **F10** clears the scene and returns to the menu so you can swap zones without restarting.

List models in a chr zip: `.\list_models.bat` (interactive) or `.\list_models.bat <zone> <MODEL>` to dump a single model's animations.

Working directory when running: `VisualEQ/`. All zone/chr zip lookups are `../ConverterApp/<name>_oes.zip`. If you change that relative path, update `Loader.LoadZoneFile`, `Loader.LoadCharacter`, `App.ListAvailableModels`, `App.LoadCharacters`, `SpawnManager.BuildAvailableModels`, and `MainMenuWidget.ConvertedZoneDir` together.

`eq_config.txt` stored the EQ install path for the pre-menu bat workflow. The in-app menu writes to `AppSettings.EqInstallPath` (`%APPDATA%\VisualEQ\settings.json`) instead — `eq_config.txt` still exists for `load_zone.bat` compatibility.

Database credentials: **not** committed. `database.config.template.json` is a template — the app actually reads/writes `%APPDATA%\VisualEQ\settings.json` via `SettingsManager`. Users configure DB through the `DatabaseConnectionView` window.

## 5. Rendering engine (Engine/)

- OpenGL **4.1 forward-compatible core** (see `EngineCore` ctor). On Parallels this reports "4.1 Metal - 89.4" / "Parallels using Apple M2 Pro (Compat)".
- Dual pathway: `DeferredPathway.cs` for the G-buffer + light pass, forward pass runs unconditionally after (for transparent + always). Toggle deferred with `L`.
- `Model` — static geometry, may or may not be collidable/fixed. `AniModel` — animated character mesh; `AniModelInstance` wraps one instance with position/rotation/animation name.
- **Animation quirks**: default character animation set on load is `"C05"` (see `Controller.LoadCharacter` and `SpawnManager.LoadFromRecords`). Not every model has every animation — missing keys previously crashed; loader now only loads animations that exist on the mesh.
- Materials in `Engine/Materials/` map to `OESEffect.Name`: `default`, `animated`, `diffuse+normal`, `fire`. Alpha-masked and transparent variants live in `ForwardDiffuseMasked` / `DeferredDiffuseMasked`.
- `Globals` static: `Camera` (`FpsCamera`), `Collider`, `PhysicsEnabled`, `FrameTime`, `ProjectionMat`, plus `vec2`/`vec3` helpers.

## 6. Spawn system (VisualEQ/SpawnSystem/)

Design: `SpawnPoint` **wraps** `AniModelInstance` — the engine renders raw `AniModelInstance`s, `SpawnManager` owns the DB↔scene mapping. Same `AniModel` mesh is shared across many spawns via `Controller._modelCache`.

- `SpawnManager.LoadFromRecords(records, engine, modelCache, availableModels, fallback)`:
  - Picks the highest-`Chance` `SpawnEntryWithNpc` per record as the "primary" NPC.
  - Model resolution: `RaceModelMapper.ResolveWithGender(race, gender)` → `_M`/`_F` suffix → falls back to base 3-letter code → falls back to the shared `LastModelLoaded` (`fallback`) with `IsPlaceholder = true`. If no fallback exists, spawn is skipped.
  - Positions instances at `Vector3(record.Spawn.Y, record.Spawn.X, record.Spawn.Z)` — **note the X/Y swap**. See §8.
- `Controller.LoadZoneSpawnsAsync`:
  - No-ops without a DB. Guards against double-loading.
  - After building spawn points it adds each `sp.Model` to `CharacterModels` so `ModelSelector` can pick it.
- Selection: `ModelSelector.OnSelectionChanged` → `SpawnManager.Select(instance)` → resolves back to a `SpawnPoint` and fires `SpawnSelected`.

## 7. Known gotchas & invariants

- **Face culling is disabled** in the forward pass (`EngineCore.OnRenderFrame`, `GL.Disable(EnableCap.CullFace)`). Re-enabling requires fixing winding-order in one or more mesh sources — do not flip this without checking.
- **cimgui.dll must sit next to `VisualEQ.dll`.** The `CopyNativeLibs` MSBuild target handles this. If it stops copying, verify `runtimes/win-x64/native/cimgui.dll` exists under the build output.
- **ImGui.NET is pinned at 0.4.6.** Its `InputText` signature is byte-buffer-based (see `DatabaseConnectionView`), there is no `TextColored` overload we use, and the `Password` input flag breaks input on this version — do not add it.
- **OpenTK 1.x** — this is not modern OpenTK 4.x. Types like `GameWindow`, `Key`, `MouseButton` come from the legacy namespaces, and the update/render loop is virtual overrides, not `IApplicationLifecycle`.
- **OpenGL calls must run on the OpenTK thread** (`OnUpdateFrame`/`OnRenderFrame` and their callees). Do not touch GL from a `Task` continuation. This includes `Mesh` and `AnimatedMesh` construction — their ctors call `new Vao()` / `new Buffer<>(...)` which hit `GL.GenBuffer`/`GL.BufferData` immediately (see [Engine/Buffer.cs](Engine/Buffer.cs)). So any code path that reaches `Loader.LoadZoneFile` or `Loader.LoadCharacter` must run on the GL thread. `LoadZoneSpawnsAsync` is only safe when awaited *before* `Engine.Start()` (main-thread boot). For the in-app zone loader (after `Run()`), `Controller.LoadZoneFromMenu` and `LoadZoneSpawnsSync` are the correct entry points — they block the GL thread rather than awaiting, so the DB continuation never touches GL from ThreadPool.
- **ImGui text input requires KeyMap + KeyPress wiring.** `EngineCore` maps the 19 `GuiKey` slots to OpenTK `Key` ints and forwards `KeyPress` characters. When `Gui.KeyboardWanted` is true, `EngineCore.OnKeyDown` and `OnUpdateFrame` skip camera/game input.
- **StyleCop is loaded via `Directory.Build.props`.** Warnings appear but do not fail the build.
- **Zip lookups are case-sensitive on some paths.** OES chunk types match by 4-char code (trimmed). Character model name comparisons use `OrdinalIgnoreCase` in the availability map — keep it that way.
- **`spawn2` schema variance.** This codebase targets a schema that has **no `enabled` column**; `SpawnViewModel.IsEnabled` is hardcoded `true`. `npc_types` has cosmetic columns (`hair*`/`beard*`/`eye*`) that vary across EQEmu forks — `GetNpcTypesBatch` intentionally selects only the columns needed for model/level/gender lookup.
- **Batch queries avoid N+1.** `GetZoneSpawnsFullAsync` runs 5 queries total regardless of spawn count: spawns, zoneid, spawnentries, npc_types, grid_entries.

## 8. Coordinate systems

- EQ DB stores `spawn2.x = east/west`, `spawn2.y = north/south`, `spawn2.z = up`. The scene's `AniModelInstance.Position` expects the axes swapped: instances are created at `Vector3(Spawn.Y, Spawn.X, Spawn.Z)` (see `SpawnManager.LoadFromRecords`). When writing back (`UpdateSpawnLocationAsync`) the mapping must be reversed — anything reading `sp.Model.Position` and passing it straight to `UpdateSpawnLocationAsync` will produce swapped coords on save. Add the swap at the save boundary when implementing Phase 5.
- Camera is Z-up: `Camera.Position.Z = 1000` on `Home` key. `Camera.OnGround` gates jumping. `FpsCamera.CameraHeight` is added to eye position for ray-casting.

## 9. Database layer (Database/)

- `Dapper 2.0.35` + `MySqlConnector 2.3.7`. **No `MySql.Data`** — an older writeup may mention it; the current stack is MySqlConnector.
- `IDbConnectionFactory` → `MySqlConnectionFactory` (built from `DatabaseSettings`). `TestConnectionAsync()` returns `(bool, string)`. Repositories accept `IDbConnectionFactory` via `RepositoryBase`.
- `ISpawnRepository`: `GetZoneSpawnsAsync` (simple), `GetZoneSpawnsFullAsync` (full compound records), `GetSpawnByIdAsync`, `UpdateSpawnLocationAsync`.
- `SqlQueries` — every column that Dapper needs is aliased with `AS PascalCase` (e.g. `spawngroupID AS SpawnGroupId`) to make mapping deterministic across drivers.
- Grid keying is `(zoneid, id)`. `GetZoneId` fetches `zone.zoneidnumber` by `short_name` — always filter `grid_entries` by both.
- `proxeeus_db_schema.sql` at repo root is the reference schema for the currently supported EQEmu fork.

## 10. Roadmap status (from `VisualEQ-SpawnEditor-Plan.md`)

Track this instead of re-inventing the plan:

- **Phase 1 (DB + settings)** — done. `AppSettings`, `SettingsManager`, `DatabaseConnectionView`, `MySqlConnectionFactory`, batched repository methods all landed.
- **Phase 2 (spawn load)** — done. `SpawnRecord`, `RaceModelMapper`, availability check via `SpawnManager.BuildAvailableModels`, `SpawnPoint`, `SpawnManager.LoadFromRecords`, wiring in `App`/`Controller` all present.
- **Phase 3 (visualization)** — partial. Positions work; visual differentiation for placeholder / selected / dirty is **not** implemented.
- **Phase 4 (`SpawnInfoView`)** — not yet started. `ModelEditorView` is the current placeholder.
- **Phase 5 (undo + save)** — not yet started. No `SpawnUndoStack.cs` exists; `UpdateSpawnLocationAsync` is unused from the UI.
- **Phase 6 (path grids)** — not yet started. Waypoints are fetched into `SpawnRecord.Waypoints` but not rendered.
- **Phase 7 (spawn list sidebar)** — not yet started.
- **Phase 8+** — deferred.

If the plan and the code disagree, **the code is authoritative** — update the plan and this file rather than reworking already-shipped code to match a stale doc.

## 11. Style & conventions

- 4-space indentation, `using` outside namespace, newline at EOF (StyleCop-enforced).
- Public views inherit `BaseView` and are added via `Controller.AddView` — `Setup` runs once with the `Gui`, `Update` runs per frame.
- The default UI pattern is `NsimGui.Widgets` declarative (`new Window("...") { new Text(...) }`) — reach for a raw `BaseWidget + ImGui.*` calls only when you need mutable byte-buffer input, async feedback, or conditional layout (as `DatabaseConnectionView` does).
- Avoid new dependencies without a strong reason — the whole stack (OpenTK 1.x, ImGui.NET 0.4.6, Dapper, MySqlConnector, StyleCop, LangVersion 7.3) is stable and any bump is a rabbit hole.
- Prefer editing existing files; keep new files inside the appropriate subsystem folder (`SpawnSystem/`, `Views/`, `Database/*`, `Engine/`).
