# VisualEQ Roadmap

Living document. Sprint work at top; QoL ideas grouped below. Update as items ship or new ones come up. Cross-reference the original phased plan in [VisualEQ-SpawnEditor-Plan.md](VisualEQ-SpawnEditor-Plan.md) when a task descends from it — the code is authoritative when the two disagree (see CLAUDE.md §10).

---

## Current sprint — "make it feel like an editor"

Phase 5 (undo/save) is complete end-to-end: commit was validated against a live server. The tooling now needs to feel like an actual editor, not a viewer that lets you tweak. Delivered in three iterations, each independently shippable.

### Iteration 1 — "Feels sane" (~1 day)

The bare minimum to stop pulling hair. Grouped so any subset can ship.

#### Drag mechanics

- **Surface-stick regardless of `PhysicsEnabled`.** [`ModelSelector.FindSurfaceHeight`](Engine/ModelSelector.cs) currently returns early if `!PhysicsEnabled`, but `Collider` is set on zone load regardless. Cast the downward ray unconditionally — no gravity involvement, just "what's directly below this model right now". Same treatment for `WaypointEditor`. Applies to both spawn drag and waypoint drag. **This is bug #1.**
- **Ground-plane drag by default.** Mouse drag translates the model in world XY at the model's Z; mouse wheel adjusts Z. Currently the drag plane is perpendicular to camera direction, so looking down means dragging horizontally shifts the model in Z — actively counter-intuitive. Alt as a modifier can opt back into camera-perpendicular for close-quarters work.
- **Size-adjusted selection radius.** Currently 10 units flat for all models. Derive a rough radius from the model's mesh bounds (cache per-`AniModel`). Floor at 8. Same for waypoint hit-test.
- **Visual "grabbed" state.**
  - Model gets a bright cyan outline / tint during drag (either via a wireframe overlay pass or a shader `uTint`).
  - Cursor changes to a grab/grabbing icon via OpenTK's `Cursor` API.
  - Sidebar Status section adds a live `Dragging: X=… Y=… Z=…` line.
- **Waypoint drag visibility.** Selected + being-dragged waypoint: crosshair arms grow to 20 units, brighter green, plus a vertical line from waypoint straight down to ground so it never disappears against textured floors.

#### Navigation

- **Escape stops exiting the app.** Change to "cancel drag / clear selection / close top-most modal". Add explicit quit via main menu (F10 → menu → Quit).
- **F = frame selection.** Fly camera to the selected spawn's face-to-face vantage (reuse `SidebarView.FlyToAndSelect`'s wall-aware logic).
- **Coordinate readout HUD.** Small top-right always-visible overlay (via `ImGui.GetOverlayDrawList`): camera XYZ + yaw, selected spawn's current + original position, delta during drag.

### Iteration 2 — "Feels like an editor" (~half day + a couple hours)

Standard 3D-editor conventions everyone expects.

- **Q/E for vertical fly** — up/down independent of look direction. Reserves Space for jump (physics mode).
- **Mouse wheel = fly-speed adjustment** (when not dragging a model). Persist last-used speed to `AppSettings`. Range 25–500, log-scale steps. Shift-run multiplier stays 3×.
- **Middle-mouse orbit around selection.** When a spawn is selected, middle-drag orbits the camera around it. Blender/Unreal convention. Fallback: `Alt+left-drag` if middle-mouse is awkward.
- **Axis-lock modifiers during drag.** Hold `X`/`Y`/`Z` to constrain drag to that world axis. Draw a colored line along the locked axis through the model. Blender convention.
- **"Show all grids dimly"** toggle. Selected spawn's grid stays bright amber; others render at ~15% alpha so you can plan pathing across the zone at a glance.

### Iteration 3 — "Precision work" (~half day)

- **Grid snap** with steps 1 / 5 / 10 / 50 units, hotkey `G` toggles. Ghost preview at the snap target during drag.
- **Numeric input** for XYZ under the heading slider in Spawn Info. Enter → records a `SpawnMoveAction` to those DB-space coords. Same for the selected waypoint.
- **Waypoint number labels.** Small camera-facing billboard with the waypoint number next to each crosshair. Needs a tiny text-billboard renderer (texture atlas of digits + camera-facing quad). Also unblocks any future "hover shows extra info" style features.

---

## Following sprint items (from earlier roadmap, still open)

### T-pose fix for zero-animation classic models
`ConverterCore.Converter.GenerateAnimatedMeshes` prefix-reference logic doesn't match for many classic Trilogy models (see [[project_spawn_rendering.md]] in memory). Affected models: DAM, DAF, HUF, HAF, ERM, HAM, HOF, BAF — they all end up with `0 of 0` animation sets and render in the mesh's authored bind pose. Requires diving into the WLD parser and comparing `Fragment13.Name` derivation vs how the classic WLD actually names them. Cosmetic payoff; large investigation.

### Reserve 3D viewport region for sidebar
Currently `GL.Viewport` covers the full OS window even though the sidebar overlays the left ~380 px. Result: the 3D projection center sits at 50% window width instead of the middle of the *visible* viewport, and the left 30-ish% of the render is wasted GPU work.

- Read sidebar width from the render loop.
- Adjust `GL.Viewport(sidebarWidth, 0, Width - sidebarWidth, Height)` before drawing meshes.
- Rebuild the perspective matrix with the visible aspect ratio.
- Also update `ScreenToWorldRay` (in `ModelSelector` and `WaypointEditor`) so clicks in the 3D area still ray-cast correctly — `engine.Width` needs to become visible-width and mouse-X needs offsetting.

### Smoother geometry-load phase
Progress bar sits at ~10% for 1–3 s during `LoadZone` because `Loader.LoadZoneFile` is one monolithic call. Break it into stages:
1. Parse OES + read chunks (fast, CPU-only)
2. Build static-mesh Vao/Buffers N-at-a-time (each frame does a batch, bar sweeps)
3. Build object instances similarly
4. Add lights
5. Rebuild collision octree

Requires refactoring `Loader.LoadZoneFile` into a state-carrying iterator, similar to what `SpawnManager` now does for spawns (`BeginSpawnLoad` / `ContinueSpawnLoad` / `FinishSpawnLoad`).

---

## Backlog — UX / QoL upgrades (not yet scoped into a sprint)

Grouped by area. Sizing hints: **S** = under an hour, **M** = 2–4 h, **L** = day-plus. Items that get promoted into a sprint get moved out of this section.

### Selection & editing

- **S** — Programmatic selection should update `ModelSelector` state, not just `SpawnManager`. Currently list-click sets `SpawnManager.Selected` but leaves `ModelSelector.SelectedModel` untouched, so an immediate drag after list-click may misbehave. Add a public `SelectExternally(instance)` on ModelSelector.
- **S** — Keyboard nudge for selected spawn. Arrow keys (in edit mode) move ±1 unit, Shift+arrow moves ±10. Records a `SpawnMoveAction` per press (or coalesces into one action if held).
- **M** — Bulk selection. Ctrl+click adds-to-selection, drag operates on all selected. Requires refactoring `SpawnManager.Selected` from single-item to a `HashSet<SpawnPoint>`.
- **M** — Right-click context menu on spawn: "Fly to", "Copy NPC name", "Copy Spawn ID", "Reset to original position" (uses existing `Revert()`).
- **S** — Halo/outline shader around the selected model. More "connected" than the vertical marker line. Stencil buffer or a re-drawn wireframe pass.

### Camera & navigation (leftovers not in Iteration 1–2)

- **M** — Per-zone camera-position persistence. Save last camera pos/orientation to `AppSettings` keyed by zone name, restore on load.
- **S** — Toggle sidebar visibility (Ctrl+B). Users focused on the 3D view might want it hidden.
- **S** — Fullscreen toggle (F11). `EngineCore.WindowState = Fullscreen`.
- **S** — Recent zones list. Persist last 5 loaded zones to `AppSettings`, promote to top of the zone browser (with a divider from the main list).
- **M** — Movement smoothing / inertia. Camera velocity ramp-up / deceleration for less "twitchy" fly-through. Configurable amount.
- **S** — Camera slow-modifier. `Shift = fast` exists; add `Ctrl = slow` for precise navigation.

### Sidebar

- **S** — Zone-list filter in main menu. As the number of decoded zones grows, scrolling the list becomes annoying. Add a text filter matching the decoder's approach.
- **S** — Spawn List: filter by race + placeholder state, not just name substring. Add a checkbox "hide placeholders" and a race dropdown.
- **S** — Placeholder badge on Spawn List filter. "N of M spawns (P placeholders)".
- **M** — Path grid section: when a spawn with a grid is selected, show a waypoint list with X/Y/Z and Pause per waypoint. Click a waypoint → camera flies to it.
- **S** — Ctrl+F to focus Spawn List filter.

### Data / diagnostics

- **M** — Error surfacing in the UI. Currently `Console.WriteLine` for DB failures, decode failures, model-load failures. A collapsible "Log" section in the sidebar (with severity coloring) makes them visible without a console window.
- **S** — Session log to disk. Write console output to `%APPDATA%\VisualEQ\session.log`.
- **S** — Zone info readout. `zone` DB row (safe coords, min/max Z, sky, etc.) rendered in Spawn Info or a new "Zone" section.
- **S** — DB write history log. Append every commit's UPDATEs to a log with timestamp + affected IDs.
- **M** — Zone thumbnails cached on first render. Screenshot the 3D scene after load, save to `%APPDATA%\VisualEQ\thumbnails\<zone>.png`, display in the zone browser.

### Rendering polish

- **S** — Toggle physics (P key) and deferred rendering (L key) exposed in the Settings section. Both work today via hotkey but aren't discoverable.
- **M** — Face culling fix. Currently `GL.Disable(EnableCap.CullFace)` in `OnRenderFrame` because winding order is inconsistent across converted meshes. Re-enabling requires fixing winding at one or more mesh sources — see CLAUDE.md §7.
- **M** — Improved placeholder appearance. Instead of "unknown race → ORC", pick a per-race-family fallback (humanoid → HUM, quadruped → WOL) OR tint the fallback distinctively so users know at a glance without needing the yellow marker line.

### Converter / decoder

- **M** — Log deleted files during "Delete decoded globals". Currently `MainMenuWidget.RenderDeleteGlobalsControls` deletes near-empty (`< 1024 bytes`) chr zips silently — audit trail wanted.
- **M** — Progress bar for individual decodes. Batch decodes show per-file progress; a single decode is a black box. Would need `ConverterCore.Converter.Convert` to accept a progress callback.
- **S** — "Cancel batch decode" button.

### Persistence & recovery

- **S** — Save multiple DB connections. Currently `AppSettings.Database` holds one. Users swapping between EQEmu forks retype every time.

### Misc

- **S** — Copy-to-clipboard actions in Spawn Info (name, NPC ID, spawn ID). Handy for cross-referencing with EQEmu tooling.
- **S** — Generate INSERT/UPDATE SQL for the selected spawn as a text blob. Debugging aid for DB work.
- **M** — Keybinding customization. Settings-side editor for hotkeys. Currently all hardcoded.

---

## Deferred / low-priority

- Custom shaders / effects on placeholder models (tinting requires shader mods across the material system).
- Zone geometry editing (moving world meshes). Out of scope for a spawn editor.
- Multi-select drag with proportional falloff. Nice for path-grid style edits, but no obvious use case for spawn editing.

---

## Recently shipped

- **Phase 5 — Undo + save** (9 sub-phases). Read-only-by-default edit mode with orange viewport border. Per-zone JSON edit buffer with debounced auto-save, session recovery on load, F10 unsaved-changes warning. Undo/redo (500 deep, Ctrl+Z/Ctrl+Y with AZERTY awareness). Pending Changes sidebar section with per-item revert and Discard confirm. Waypoint selection + drag with shared-grid sync + green highlight. Commit dialog with atomic DB transaction (validated against live EQEmu server). Rotation UI with heading slider + `SpawnRotateAction`.
- **Phase 6 (visualization)** — colored spawn-state markers with settings toggles, path grid rendering (Phase 6 from original plan), `PathGridRenderer` for the selected spawn's grid.
- **Phase 7 (spawn list sidebar)** — searchable/filterable spawn list, click-to-fly-and-select with wall-aware camera placement.
- **Menu overhaul** — in-app main menu, F10 zone-swap, in-process decoder with batch globals, resizable + reorderable sidebar sections with persistence, centered loading dialog with chunked spawn phase for a smooth progress bar.
- **Perf** — animation whitelist + single-frame truncation for spawn models (~600× fewer GL objects); WLD decoder null-guard fix for classic `global_chr.s3d`.
- **Model correctness** — multi-zip character search, expanded `RaceModelMapper` with fork-specific overrides, heading applied to spawn models on load with un-swap on commit.
