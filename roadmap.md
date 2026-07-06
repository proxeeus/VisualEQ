# VisualEQ Roadmap

Living document. Sprint work at top; QoL ideas grouped below. Update as items ship or new ones come up. Cross-reference the original phased plan in [VisualEQ-SpawnEditor-Plan.md](VisualEQ-SpawnEditor-Plan.md) when a task descends from it — the code is authoritative when the two disagree (see CLAUDE.md §10).

---

## Current sprint — remaining

### Phase 5 — Undo + save
The `SpawnPoint.IsDirty` flag exists and the orange marker overlay already renders on it; nothing sets it yet, and nothing writes changes back to the DB.

- Hook `SpawnPoint.MarkMoved(newPos, heading)` into the drag path in `ModelSelector.UpdateDrag` / `StopDrag` so moves fire.
- Wire `ISpawnRepository.UpdateSpawnLocationAsync` from the UI — need to **un-swap** X/Y (scene → EQ DB) and reverse `HeadingToRotation` back to a heading value. See `SpawnManager.LoadFromRecords` for the load-side swap (`Vector3(Y, X, Z)`).
- Undo stack per zone (or global). Ctrl+Z / Ctrl+Y for undo/redo — need to add those key handlers, gated on `!Gui.KeyboardWanted`.
- Bulk save button (sidebar? menu?) plus per-spawn `Revert()` action (already implemented on `SpawnPoint`).
- Persist across zone unloads? Consider warning on F10 with unsaved changes.

### T-pose fix for zero-animation classic models
`ConverterCore.Converter.GenerateAnimatedMeshes` prefix-reference logic doesn't match for many classic Trilogy models (see [[project_spawn_rendering.md]] in memory). Affected models: DAM, DAF, HUF, HAF, ERM, HAM, HOF, BAF — they all end up with `0 of 0` animation sets and render in the mesh's authored bind pose. Requires diving into the WLD parser and comparing `Fragment13.Name` derivation vs how the classic WLD actually names them. Cosmetic payoff; large investigation.

### Reserve 3D viewport region for sidebar
Currently `GL.Viewport` covers the full OS window even though the sidebar overlays the left 380 px (or wherever the user dragged it). Result: the 3D projection center sits at 50% window width instead of the middle of the *visible* viewport, and the left 30-ish% of the render is wasted GPU work.

- Read sidebar width from the render loop.
- Adjust `GL.Viewport(sidebarWidth, 0, Width - sidebarWidth, Height)` before drawing meshes.
- Rebuild the perspective matrix with the visible aspect ratio.
- Also update `ScreenToWorldRay` (in `ModelSelector`) so clicks in the 3D area still ray-cast correctly — its `engine.Width` reference needs to become `visibleWidth` and mouse-X needs offsetting.

### Smoother geometry-load phase
Progress bar sits at 10% for 1–3 s during `LoadZone` because `Loader.LoadZoneFile` is one monolithic call. Break it into stages:
1. Parse OES + read chunks (fast, CPU-only)
2. Build static-mesh Vao/Buffers N-at-a-time (each frame does a batch, bar sweeps)
3. Build object instances similarly
4. Add lights
5. Rebuild collision octree

Requires refactoring `Loader.LoadZoneFile` into a state-carrying iterator similar to what `SpawnManager` now does for spawns. Same pattern as `BeginSpawnLoad`/`ContinueSpawnLoad`/`FinishSpawnLoad`.

---

## UX / QoL upgrades

Grouped roughly by area. Rough sizing hints: **S** = under an hour, **M** = 2–4 h, **L** = day-plus. Ordered by impact-per-effort within each group, not by absolute priority.

### Selection & editing
- **S** — Programmatic selection should update `ModelSelector` state, not just `SpawnManager`. Currently list-click sets `SpawnManager.Selected` but leaves `ModelSelector.SelectedModel` untouched, so an immediate drag after list-click may misbehave. `ModelSelector` needs a public `SelectExternally(instance)` method.
- **S** — Keyboard nudge for selected spawn. Arrow keys move ±1 unit, Shift+arrow moves ±10. Wire into `EngineCore.OnKeyDown` gated on `SpawnManager.Selected != null`.
- **M** — Rotation control in Spawn Info section. A slider (0–511) or arrow buttons to change heading, `SpawnPoint.MarkMoved` reused (currently only handles position + heading together).
- **M** — Bulk selection. Ctrl+click to add-to-selection, drag operates on all selected. Requires refactoring `SpawnManager.Selected` from single-item to a `HashSet<SpawnPoint>`.
- **M** — Right-click context menu on spawn: "Fly to", "Copy NPC name", "Copy Spawn ID", "Reset to original position" (uses existing `Revert()`), "Delete" (Phase 5+).
- **S** — Halo/outline shader around the selected model. More "connected" than the vertical marker line. Would use stencil buffer or a re-drawn wireframe pass.
- **S** — Cursor state feedback. Hover a draggable spawn → grab cursor; during drag → grabbing cursor. Uses OpenTK cursor API.

### Camera & navigation
- **S** — "Frame selection" hotkey (F key, like Blender/Godot). Reuse `FlyToAndSelect`'s wall-aware camera positioning for the currently-selected spawn.
- **M** — Per-zone camera-position persistence. Save last camera pos/orientation to `AppSettings` keyed by zone name, restore on load.
- **S** — Toggle sidebar visibility (Ctrl+B). Users focused on the 3D view might want it hidden.
- **S** — Escape currently exits the app — treat as a mis-designed default. Change to "cancel selection / close modal", add a separate quit hotkey or menu action.
- **S** — Fullscreen toggle (F11). `EngineCore.WindowState = Fullscreen`.
- **S** — Recent zones list. Persist last 5 loaded zones to `AppSettings`, promote to top of the zone browser (with a divider from the main list).

### Sidebar
- **S** — Zone-list filter in main menu. As the number of decoded zones grows, scrolling the list becomes annoying. Add a text filter matching the decoder's approach.
- **S** — Spawn List: also filter by race + placeholder state. Currently only name substring. Row: NPC name [L{level}]; add a checkbox "hide placeholders" and a race dropdown.
- **S** — Placeholder badge on Spawn List filter. "N of M spawns (P placeholders)" so the ratio is glanceable.
- **M** — Path grid section in the sidebar. When a spawn with a grid is selected, show waypoint list with X/Y/Z and Pause per waypoint. Click a waypoint → camera flies to it.
- **S** — Ctrl+F to focus Spawn List filter. Common editor pattern.
- **S** — "Show all grids" toggle. Currently only the selected spawn's grid renders — deferred earlier as scope. A second checkbox in Settings makes it optional.

### Data / diagnostics
- **M** — Waypoint numbers rendered at waypoints. Needs a small billboard text renderer (texture atlas of digits, camera-facing quad). Deferred earlier as too much scope.
- **M** — Error surfacing in the UI. Currently `Console.WriteLine` for DB failures, decode failures, model-load failures. A collapsible "Log" section in the sidebar (with severity coloring) makes them visible without a console window.
- **S** — Session log to disk. Write console output to `%APPDATA%\VisualEQ\session.log` so we can inspect past runs without keeping the console open.
- **S** — Zone info readout. `zone` DB row (safe coords, min/max Z, sky, etc.) rendered in Spawn Info or a new "Zone" section.
- **S** — DB write history log. Once Phase 5 lands, append every UPDATE to a log with timestamp + spawn ID.
- **M** — Zone thumbnails cached on first render. Screenshot the 3D scene after load, save to `%APPDATA%\VisualEQ\thumbnails\<zone>.png`, display in the zone browser as small previews.

### Rendering polish
- **S** — Toggle physics (P key) and deferred rendering (L key) exposed in the Settings section. Both work today via hotkey but aren't discoverable.
- **M** — Face culling fix. Currently `GL.Disable(EnableCap.CullFace)` in `OnRenderFrame` because winding order is inconsistent. Re-enabling requires fixing winding at one or more mesh sources — see CLAUDE.md §7.
- **S** — Camera slow-modifier. `Shift = fast` exists; add `Ctrl = slow` for precise navigation.
- **M** — Improved placeholder appearance. Instead of "unknown race → ORC", pick a per-race-family fallback (humanoid → HUM, quadruped → WOL, etc.) OR tint the fallback distinctively so users know at a glance it's a placeholder without needing the yellow marker line.

### Converter / decoder
- **M** — Undo the empty-zip cleanup: currently `MainMenuWidget.RenderDeleteGlobalsControls` deletes near-empty (`< 1024 bytes`) chr zips silently on button click. Should log which files were deleted so the user has an audit trail.
- **M** — Progress bar for individual decodes too. `MainMenuWidget.RenderDecodeSection` shows "Decoding {zone}…" but no % — the batch has per-file progress but a single decode is a black box. Would need `ConverterCore.Converter.Convert` to accept a progress callback.
- **S** — "Cancel batch decode" button. Currently the batch runs to completion once started; if it takes 5 min and the user changes their mind, they're stuck.

### Persistence & recovery
- **S** — Save recent DB connections. Currently `AppSettings.Database` holds one connection; if the user swaps between EQEmu forks they retype every time.
- **M** — Session recovery. On startup, if the last known zone had unsaved spawn moves, offer to reload state (dirty positions persisted in a temp file).

### Misc
- **S** — Copy-to-clipboard actions in Spawn Info (name, NPC ID, spawn ID). Handy for cross-referencing with EQEmu tooling.
- **S** — Generate INSERT/UPDATE SQL for the selected spawn as a text blob users can copy. Debugging aid for DB work.
- **M** — Keybinding customization. Settings-side editor for hotkeys (F10, F, P, L, Escape). Currently all hardcoded.

---

## Deferred / low-priority
- Custom shaders / effects on placeholder models (tinting requires shader mods across the material system).
- Zone geometry editing (moving world meshes). Out of scope for a spawn editor.
- Multi-select drag with proportional falloff. Nice for path-grid style edits, but no obvious use case for spawn editing.
