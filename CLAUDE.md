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
- `OnRenderFrame` runs deferred pass (if enabled, key `L`) then forward pass. Back-face culling is **enabled** for the deferred/opaque draw and disabled for the forward (transparent) draw — see §7. F9 toggles culling globally for regression testing.
- Mouse input: right-drag rotates camera (hides cursor); left-click without GUI hits `ModelSelector.TrySelect` → drag with plane-projection + surface sticking via `CollisionHelper`; mouse-wheel while dragging adjusts drag-plane depth.

## 4. Build & run

**Physical environment: Apple M2 Mac → Parallels → Windows 11 ARM64.** Two dotnet installs are expected on the Windows side:
- `C:\Program Files\dotnet\dotnet.exe` — ARM64 SDK + runtime. Used for **building** and for the converter/lister CLIs.
- `C:\Program Files\dotnet\x64\dotnet.exe` — x64 **runtime only** (no SDK). Used for **running** `VisualEQ.dll` because `cimgui.dll` is x64-native and won't load into an ARM64 process.

Do **not** try to switch to `dotnet run` for the main app; it will pick the ARM64 runtime and crash on `cimgui.dll`. Always launch the built DLL through the x64 runtime with `dotnet exec`. The scripts in `dev/` already do this — extend them if the launch pattern changes.

Build everything: `dotnet build` at the repo root.

Run: `.\dev\visualeq.bat` (opens the in-app main menu — zone browser, in-app decoder, settings). Legacy CLI: `.\dev\load_zone.bat` or `.\dev\load_zone.bat "C:\Program Files (x86)\EverQuest" gfaydark` skips the menu and loads a zone directly. Both invoke `"%DOTNET_X64%" exec "bin\Debug\net8.0\VisualEQ.dll" [<zone>]` inside `VisualEQ/`. Each dev script starts with `cd /d "%~dp0.."` so their internal relative paths keep resolving.

From the main menu you can list available zones, decode a new zone (in-process — no external bat needed), and edit settings (EQ install path, DB). Once a zone is loaded, **F10** clears the scene and returns to the menu so you can swap zones without restarting.

List models in a chr zip: `.\dev\list_models.bat` (interactive) or `.\dev\list_models.bat <zone> <MODEL>` to dump a single model's animations. `VisualEQ.exe --list-models` also works from a packaged build.

**Zone/chr zip location:** the app reads and writes converted assets to the path in `AppSettings.ConvertedAssetsPath` (default `%APPDATA%\VisualEQ\zones\`). The value is exposed on the controller as `Controller.ConvertedAssetsDir` — every runtime lookup uses that (see [Controller.cs](VisualEQ/Controller.cs), [App.cs](VisualEQ/App.cs), [MainMenuView.cs](VisualEQ/Views/MainMenuView.cs), [SpawnManager.cs](VisualEQ/SpawnSystem/SpawnManager.cs)). `SpawnManager.BuildAvailableModels(zoneName, dir)` takes the directory as a parameter — pass `Controller.ConvertedAssetsDir` at call sites. The legacy `dev\load_zone.bat` and `dev\list_models.bat` still reference the pre-2026-07 `ConverterApp/` layout and won't find zips in the new location without a manual copy.

`eq_config.txt` stored the EQ install path for the pre-menu bat workflow. The in-app menu writes to `AppSettings.EqInstallPath` (`%APPDATA%\VisualEQ\settings.json`) instead — `eq_config.txt` still exists for `dev\load_zone.bat` compatibility.

**Building a release zip:** `./publish-release.ps1 -Version <version>` at repo root produces `release/VisualEQ-<version>-win-x64.zip` — a self-contained portable Windows build (no .NET install needed on the user side). CI runs the same script on `v*` tag push via [.github/workflows/release.yml](.github/workflows/release.yml) and uploads the zip to a GitHub Release. `cimgui.dll` is force-copied next to the exe on Publish via the `CopyNativeLibsOnPublish` MSBuild target ([VisualEQ.csproj](VisualEQ/VisualEQ.csproj)) — do not remove.

Database credentials: **not** committed. `database.config.template.json` is a template — the app actually reads/writes `%APPDATA%\VisualEQ\settings.json` via `SettingsManager`. Users configure DB through the `DatabaseConnectionView` window.

## 5. Rendering engine (Engine/)

- OpenGL **4.1 forward-compatible core** (see `EngineCore` ctor). On Parallels this reports "4.1 Metal - 89.4" / "Parallels using Apple M2 Pro (Compat)".
- Dual pathway: `DeferredPathway.cs` for the G-buffer + light pass, forward pass runs unconditionally after (for transparent + always). Toggle deferred with `L`.
- `Model` — static geometry, may or may not be collidable/fixed. `AniModel` — animated character mesh; `AniModelInstance` wraps one instance with position/rotation/**scale**/animation name.
- `AnimatedMesh` = `Material` + reference to a shared `MeshGeometry`. `MeshGeometry` (index buffer + per-animation VAOs + vertex buffers) is cached in `Loader._meshGeometryCache` keyed by `(chr-zip path, model code, mesh index, singleFrame)` and shared across every AniModel variant that differs only in material (texture / helm / face). Before this split, each `(code, texture, helm, face)` combo allocated its own 13 VAOs; freporte-with-humans stopped scaling around 24 combos.
- **Animation resolution**: loader picks an idle from `SpawnAnimationCandidates` (P01 > P02 > P03 > L01 > L02 > L03 > O01 > STA > POS) and falls through to the bind pose (`""`). Missing anims previously crashed; loader now filters to what the model actually has.
- **LANTERN animation sources**: races whose own tracks are bind-pose-only inherit anim frames from a "donor" model per `Converter.AnimationSources` (mirrors `LanternExtractor/ClientData/animationsources.txt`). HAM inherits ELM, GNM inherits DWM, TRM inherits OGF, etc. Baked at convert time — the runtime doesn't need the map.
- Materials in `Engine/Materials/` map to `OESEffect.Name`: `default`, `animated`, `diffuse+normal`, `fire`. Alpha-masked and transparent variants live in `ForwardDiffuseMasked` / `DeferredDiffuseMasked`.
- `Globals` static: `Camera` (`FpsCamera`), `Collider`, `PhysicsEnabled`, `FrameTime`, `ProjectionMat`, plus `vec2`/`vec3` helpers.

## 6. Spawn system (VisualEQ/SpawnSystem/)

Design: `SpawnPoint` **wraps** `AniModelInstance` — the engine renders raw `AniModelInstance`s, `SpawnManager` owns the DB↔scene mapping. `Controller._modelCache` is keyed by `"code"` for `npc.Texture=npc.HelmTexture=npc.Face=0` and `"code#texture#helm#face"` otherwise; each unique combo gets its own AniModel but they share `MeshGeometry` VAOs (§5).

- `SpawnManager.LoadFromRecords(records, engine, modelCache, availableModels, fallback)`:
  - Picks the highest-`Chance` `SpawnEntryWithNpc` per record as the "primary" NPC.
  - **Model resolution**: `RaceModelMapper.ResolveCandidates(race, gender)` → picks the first candidate present in `availableModels`. Cross-verified against LANTERN's `ClientData/animationsources.txt`. Table covers ~230 races. If nothing resolves, falls back to `LastModelLoaded` (`fallback`) with `IsPlaceholder = true`.
  - **Positions** instances at `Vector3(record.Spawn.Y, record.Spawn.X, record.Spawn.Z)` — **note the X/Y swap**. See §8.
  - **Scale**: `Scale = npc.Size / MeshAuthoredHeightForRace(race)`. Default divisor is 6 (canonical humanoid); dwarves + halflings + Rivervale/Kaladim/Coldain citizens use 4 (short-mesh stock). Placeholder / template spawns (size=1) end up tiny; giants (size=15+) tower correctly.
  - **Texture / helm / face** flow through `Loader.LoadCharacter`'s `textureIndex` / `helmTextureIndex` / `faceIndex` params (see §12 for the resolution pipeline).
- `SpawnManager.BuildAvailableModels(zoneName, dir)`:
  - Zone chr wins for anything it declares (freporte's `PRE` mesh doesn't get overridden by overthere's higher-anim `PRE` from the Bloated Belly skeleton family).
  - For models the zone chr doesn't declare, richness-wins (`anim_count × 1000 + mesh_count`) across the remaining chr zips — picks the animated version over an empty stub.
  - Uses `Loader.GetCharacterModelRichness` which shallow-scans chunk headers only. Result is cached to `%APPDATA%\VisualEQ\richness-cache.txt` (mtime-invalidated) so subsequent zone loads skip the walk.
- `Controller.LoadZoneSpawnsAsync`:
  - No-ops without a DB. Guards against double-loading.
  - After building spawn points it adds each `sp.Model` to `CharacterModels` so `ModelSelector` can pick it.
- Selection: `ModelSelector.OnSelectionChanged` → `SpawnManager.Select(instance)` → resolves back to a `SpawnPoint` and fires `SpawnSelected`.

## 7. Known gotchas & invariants

- **Back-face culling is ENABLED** for the deferred/opaque draw and disabled for the forward/transparent draw. Both mesh sources (converter + character path) emit CCW-front triangles. F9 toggles culling globally as a regression escape hatch. Character-mesh path (`GenerateAnimatedMeshes` / `GenerateStaticMesh`) explicitly swaps winding to CCW; zone-mesh path preserves Fragment36's original CCW. Do not change either winding-swap without also flipping this back off.
- **cimgui.dll must sit next to `VisualEQ.dll`.** The `CopyNativeLibs` MSBuild target handles this. If it stops copying, verify `runtimes/win-x64/native/cimgui.dll` exists under the build output.
- **ImGui.NET is pinned at 0.4.6.** Its `InputText` signature is byte-buffer-based (see `DatabaseConnectionView`), there is no `TextColored` overload we use, and the `Password` input flag breaks input on this version — do not add it.
- **OpenTK 1.x** — this is not modern OpenTK 4.x. Types like `GameWindow`, `Key`, `MouseButton` come from the legacy namespaces, and the update/render loop is virtual overrides, not `IApplicationLifecycle`.
- **OpenGL calls must run on the OpenTK thread** (`OnUpdateFrame`/`OnRenderFrame` and their callees). Do not touch GL from a `Task` continuation. This includes `Mesh` and `AnimatedMesh` construction — their ctors call `new Vao()` / `new Buffer<>(...)` which hit `GL.GenBuffer`/`GL.BufferData` immediately (see [Engine/Buffer.cs](Engine/Buffer.cs)). So any code path that reaches `Loader.LoadZoneFile` or `Loader.LoadCharacter` must run on the GL thread. `LoadZoneSpawnsAsync` is only safe when awaited *before* `Engine.Start()` (main-thread boot). For the in-app zone loader (after `Run()`), `Controller.LoadZoneFromMenu` and `LoadZoneSpawnsSync` are the correct entry points — they block the GL thread rather than awaiting, so the DB continuation never touches GL from ThreadPool.
- **ImGui text input requires KeyMap + KeyPress wiring.** `EngineCore` maps the 19 `GuiKey` slots to OpenTK `Key` ints and forwards `KeyPress` characters. When `Gui.KeyboardWanted` is true, `EngineCore.OnKeyDown` and `OnUpdateFrame` skip camera/game input.
- **StyleCop is loaded via `Directory.Build.props`.** Warnings appear but do not fail the build.
- **Zip lookups are case-sensitive on some paths.** OES chunk types match by 4-char code (trimmed). Character model name comparisons use `OrdinalIgnoreCase` in the availability map — keep it that way.
- **`spawn2` schema variance.** This codebase targets a schema that has **no `enabled` column**; `SpawnViewModel.IsEnabled` is hardcoded `true`. `npc_types` has cosmetic columns (`hair*`/`beard*`/`eye*`) that vary across EQEmu forks — `GetNpcTypesBatch` intentionally selects only the columns needed for model/level/gender lookup.
- **Batch queries avoid N+1.** `GetZoneSpawnsFullAsync` runs 5 queries total regardless of spawn count: spawns, zoneid, spawnentries, npc_types, grid_entries.
- **LANTERN is the reference for client-side quirks.** When a race, mesh, or texture doesn't behave right at runtime and EQEmu server just forwards data unchanged, read LANTERN's `LanternExtractor/EQ/Wld/` — especially `Helpers/CharacterFixer.cs`, `Fragments/MaterialList.cs:ParseCharacterSkin` (chr material names are exactly 9 chars: race(3) + region(2) + variant(2) + subpart(2)), and `WldFileCharacters.FindAdditionalAnimationsAndMeshes`. Local copy at `~/Downloads/LANTERN/`.
- **Chr material names are 9 chars exactly.** Chars 5-6 = skin variant (npc.Texture); char 7 = face-tens digit (npc.Face); chars 3-4 + 7-8 = body-part slot. Do not parse "last 4 digits = variant" — that conflates sub-parts (LG01 vs LG02) with skin variants (LG01 vs LG0101).
- **Alt-skin materials are unreferenced by the mesh's Fragment31.** Higher armor variants (`FPMCH0101`, `HUMHE0011` etc.) live as orphan Fragment30s in the WLD. `Converter.GenerateAnimatedMeshes` sweeps the WLD after the base pass and emits them into the OES skin so `Loader.FromSkin.PickVariantFilename` can swap PNGs at load. Requires re-decoding chr zips after any converter change.
- **Helmet variant Fragment36s are unreferenced by `Fragment10.Meshes`.** LANTERN's `WldFileCharacters.FindAdditionalAnimationsAndMeshes` sweeps orphan Fragment36s named `{race}HE##_DMSPRITEDEF` (for ## > 00) and adds them as skeleton secondaries. `Converter.GenerateAnimatedMeshes` does the same and tags each helmet variant with a helmet group in the `OESMeshGroups` chunk (§12).
- **OES root parse is expensive.** A 60 MB `global_chr` OES takes a couple of seconds to fully deserialize. `Loader._oesRootCache` holds one parsed tree per chr-zip path for the session; `OESFile.ShallowScanCharacters` is a header-only walk for the richness-scan path and never touches OESAnimationBuffer data.
- **GL texture uploads are session-cached in `Loader._textureCache`** keyed by `(zipPath, entryName)`. Every FromSkin call goes through `GetOrUploadTexture`; two Materials referencing the same PNG share the same Texture, and zone-swap → re-visit doesn't re-upload. Never call `new Texture(...)` outside this path for character/zone skins — the `Texture` finalizer runs on the finalizer thread with no current GL context, so bypassing the cache leaks the GL handle on-GPU per zone visit and per (texture, helm, face) combo. `FireMaterial` is the one exception (single hardcoded flame.png, guarded by its own `static Texture Texture`).

## 8. Coordinate systems

- EQ DB stores `spawn2.x = east/west`, `spawn2.y = north/south`, `spawn2.z = up`. The scene's `AniModelInstance.Position` expects the axes swapped: instances are created at `Vector3(Spawn.Y, Spawn.X, Spawn.Z)` (see `SpawnManager.LoadFromRecords`). When writing back (`UpdateSpawnLocationAsync`) the mapping must be reversed — anything reading `sp.Model.Position` and passing it straight to `UpdateSpawnLocationAsync` will produce swapped coords on save. Add the swap at the save boundary when implementing Phase 5.
- Camera is Z-up: `Camera.Position.Z = 1000` on `Home` key. `Camera.OnGround` gates jumping. `FpsCamera.CameraHeight` is added to eye position for ray-casting.

## 9. Database layer (Database/)

- `Dapper 2.0.35` + `MySqlConnector 2.3.7`. **No `MySql.Data`** — an older writeup may mention it; the current stack is MySqlConnector.
- `IDbConnectionFactory` → `MySqlConnectionFactory` (built from `DatabaseSettings`). `TestConnectionAsync()` returns `(bool, string)`. Repositories accept `IDbConnectionFactory` via `RepositoryBase`.
- `ISpawnRepository`: `GetZoneSpawnsAsync` (simple), `GetZoneSpawnsFullAsync` (full compound records), `GetSpawnByIdAsync`, `UpdateSpawnLocationAsync`.
- `SqlQueries` — every column that Dapper needs is aliased with `AS PascalCase` (e.g. `spawngroupID AS SpawnGroupId`) to make mapping deterministic across drivers.
- Grid keying is `(zoneid, id)`. `GetZoneId` fetches `zone.zoneidnumber` by `short_name` — always filter `grid_entries` by both.
- Always check the database schema in the database configured in eqemu_config.json at c:\eqemu

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

## 11. NPC visual pipeline (npc_types → rendered mesh)

Foundation for the eventual NPC editor. Every `npc_types` field that affects the rendered mesh flows through this pipeline; changing a field at runtime will need to re-invoke it (a full `Loader.LoadCharacter` with a new cache key is the current mechanism).

### Fields consumed

| npc_types column | Effect | Where it lands |
|---|---|---|
| `race` + `gender` | Model code (HUM / FPM / BAT / ...) | `RaceModelMapper.ResolveCandidates` → picks first candidate present in `availableModels` |
| `size` | Uniform mesh scale | `AniModelInstance.Scale = size / MeshAuthoredHeightForRace(race)`; §6 |
| `texture` | Armor tier (chars 5-6 of material filename) | `Loader.LoadCharacter`'s `textureIndex` param → `FromSkin.PickVariantFilename` picks `FPMCH0001` → `FPMCH0101` for texture=1 |
| `helmtexture` | Helmet mesh (`{race}HE##` Fragment36) | `Loader.LoadCharacter`'s `helmTextureIndex` param → `ShouldRender` filter on `OESMeshGroups` chunk |
| `face` | Face variant (char 7 of material filename) | `Loader.LoadCharacter`'s `faceIndex` param → `FromSkin.PickVariantFilename` picks `HUMHE0001` → `HUMHE0011` for face=1 |
| `class` | Sidebar label only (not visual yet) | `SpawnInfoLookups.ClassName` |
| `hairstyle`, `haircolor`, `beardstyle`, `beardcolor`, `eyecolor1`, `eyecolor2` | Not consumed (Trilogy client has no hair variants; see §7) | — |

### Converter side (`ConverterCore/Converter.cs`)

`GenerateAnimatedMeshes` produces the OES that carries everything the runtime needs:

1. **Base meshes** — `f10.Meshes` (skeleton's primary meshes: base body + `{race}HE00` base head) go through the animation-frame loop first.
2. **Orphan helmet meshes** — WLD scan for Fragment36s named `{race}HE##_DMSPRITEDEF` (## > 00) that aren't in `f10.Meshes`. LANTERN's `FindAdditionalAnimationsAndMeshes` does the same. Baked into the same combined `meshesToProcess` list as the primaries.
3. **Alt-skin materials** — post-pass over all Fragment30s in the WLD. Any orphan Fragment30 whose name matches a base slot's character+region prefix (any variant, any face) gets added to the OES skin as a name-lookup entry. `FromSkin.PickVariantFilename` walks these at load time.
4. **Helmet groups** — `OESMeshGroups` chunk stores a `uint[]` parallel to the mesh list:
   - 0 = always render (base body, unrecognised sub-meshes like HUM01)
   - 1 = base head + hair (render when `helmTexture == 0`)
   - 2, 3, 4, ... = numbered helmet variants (render when `helmTexture == group - 1`)
5. **Character material flags** — mask bits `(tf & (2|8|16))` set `AlphaMask`; `Transparent` is deliberately NOT set on characters (would route via `ForwardDiffuseMasked` and produce semi-transparent NPCs). Sails etc. still work because `DeferredDiffuseMasked` does alpha-discard on the BMP chroma-key PNG.
6. **Animation baking** — animations restricted to `BakedAnimationPrefixes` (P01/P02/P03/L01/L02/L03/O01/STA/POS); combat / social / damage anims are dropped at bake time. Trims `global_chr_oes.zip` from ~260 MB to ~60 MB.

### Loader side (`VisualEQ/Loader.cs`)

`LoadCharacter(path, name, animationWhitelist, singleFrame, textureIndex, helmTextureIndex, faceIndex)`:

1. `GetOrLoadRoot(path)` — session-shared parsed OESRoot.
2. `FromSkin(skin, zipPath, zip, textureIndex, faceIndex, materialsToBuild)` — builds `Material[]` for the mesh's own materials only. Extra alt-skin OES materials are scanned for name lookup but skip GL upload. `PickVariantFilename` computes the combined skin+face transformation:
   - Combined `race + region + textureIndex.ToString("00") + faceIndex.ToString()[0] + subpartOnes`
   - Falls back progressively: combined → skin-only → face-only → original
3. `ShouldRender(meshIdx)` — reads `OESMeshGroups.Groups[meshIdx]`, applies helmet-group filter per `helmTextureIndex`. Base head only hides when a matching secondary is present.
4. `MeshGeometry` cache `(path, name, meshIdx, singleFrame)` — VBOs / VAOs / index buffer shared across every variant. See §5.
5. `_textureCache` `(zipPath, entryName) → Texture` — session-lifetime. Every PNG in any skin (chr or zone) uploads to GL exactly once, then is shared across every Material that references it. Critical for zone-swap stability: without it, each F10 → new-zone orphaned every character texture from the outgoing zone (their `Texture` finalizers can't call `GL.DeleteTexture` on the finalizer thread without a current context — the GL handle just leaks on-GPU). Cleared only in `Loader.ClearAllCaches` at Shutdown. Materials now take `Texture[]` (not `Image[]`) in their constructors so callers can't accidentally bypass the cache.

### Cache key layout

`Controller._modelCache: Dictionary<string, AniModel>`:
- Key = `"{code}"` when texture=helm=face=0 (share the default across the herd of NPCs).
- Key = `"{code}#{texture}#{helm}#{face}"` otherwise.
- Zone unload clears the cache but the `Loader._meshGeometryCache` (session-lifetime) keeps the VAOs alive so re-loading the same zone doesn't rebuild them.

## 12. Style & conventions

- 4-space indentation, `using` outside namespace, newline at EOF (StyleCop-enforced).
- Public views inherit `BaseView` and are added via `Controller.AddView` — `Setup` runs once with the `Gui`, `Update` runs per frame.
- The default UI pattern is `NsimGui.Widgets` declarative (`new Window("...") { new Text(...) }`) — reach for a raw `BaseWidget + ImGui.*` calls only when you need mutable byte-buffer input, async feedback, or conditional layout (as `DatabaseConnectionView` does).
- Avoid new dependencies without a strong reason — the whole stack (OpenTK 1.x, ImGui.NET 0.4.6, Dapper, MySqlConnector, StyleCop, LangVersion 7.3) is stable and any bump is a rabbit hole.
- Prefer editing existing files; keep new files inside the appropriate subsystem folder (`SpawnSystem/`, `Views/`, `Database/*`, `Engine/`).
- Always assume a "pull request-first" approach.

## 12. Misc

- Always keep the F1 Cheat Sheet info up to date when relevant fixes/features are added.