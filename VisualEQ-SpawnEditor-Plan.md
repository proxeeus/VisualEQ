# VisualEQ Spawn Editor — Implementation Plan

**Last updated:** 2026-05-10  
**Status:** Living document — phases are sequential, tasks within each phase are roughly ordered by dependency.

---

## 1. Current State

Zone rendering works. Static geometry loads from `.oes.zip` files. `AniModelInstance` objects can be placed at arbitrary positions, selected via ray-cast, and dragged (with surface sticking via the collision octree). The database layer (`Database/`) is scaffolded with Dapper + MySql.Data, models for `spawn2`, `spawngroup`, `spawnentry`, `npc_types`, `grid`, `grid_entries`, and `SpawnRepository` — **but none of it is wired into zone loading yet**.

---

## 2. Architecture Overview

### New Components

```
VisualEQ/
  SpawnSystem/
    SpawnPoint.cs         — Runtime spawn: AniModelInstance + DB data + dirty flag
    SpawnManager.cs       — Owns all SpawnPoints for the current zone, load/save/undo
    SpawnUndoStack.cs     — Command stack (MoveSpawnCommand, etc.)
    RaceModelMapper.cs    — Race ID → 3-letter EQ model code lookup
    SpawnLoader.cs        — Orchestrates DB query → model resolution → ScenePoint creation
  Views/
    DatabaseConnectionView.cs   — DB settings UI panel
    SpawnListView.cs            — Scrollable sidebar list of all zone spawns
    SpawnInfoView.cs            — Detail panel for the selected spawn (replaces/extends ModelEditorView)
    PathGridView.cs             — Toggle + controls for path waypoint overlay
  Settings/
    AppSettings.cs        — POCO persisted to settings.json (DB connection, UI preferences)
    SettingsManager.cs    — Load/save settings.json to %APPDATA%/VisualEQ/
```

### Integration Points

| Existing component | Change |
|---|---|
| `App.cs` | Load `AppSettings` at startup; wire `SpawnManager` into zone-load flow |
| `Controller.cs` | Add `SpawnManager` field; forward selection events to `SpawnInfoView` |
| `EngineCore.cs` | Add `SpawnPoints` list alongside `AniModels`; render path grid lines |
| `ModelSelector.cs` | Extend to fire `SpawnSelected` / `SpawnMoved` events (not just generic `OnSelectionChanged`) |
| `ModelEditorView.cs` | Can be retired or kept as a debug fallback once `SpawnInfoView` is stable |

### Key Design Decisions

- **`SpawnPoint` wraps, not inherits, `AniModelInstance`** — avoids coupling the engine to DB concepts. `SpawnManager` holds `List<SpawnPoint>`, engine holds `List<AniModelInstance>`. They reference the same `AniModelInstance` object.
- **Fallback model for unknown races:** A hardcoded placeholder `AniModelInstance` using a known working model (e.g., the default ORC), tinted or scaled distinctly, so every spawn always has a visual representation.
- **Undo is session-only.** No cross-session persistence for undo history.
- **Writes go directly to the live DB** — no staging table, no diff file. A confirmation dialog precedes multi-spawn saves.
- **Path grid lines rendered in the forward pass** using GL `GL_LINES` drawn per-frame when enabled, no persistent VBO needed for the prototype. A dedicated `PathGridRenderer` can be added later for performance.

---

## 3. Implementation Phases

---

### Phase 1 — DB Connection & Settings

**Goal:** User can enter connection details, test them, and have them persist across launches.

#### 1.1 — `AppSettings` + `SettingsManager`

- **File:** `VisualEQ/Settings/AppSettings.cs`
  ```csharp
  public class AppSettings {
      public DatabaseSettings Database { get; set; } = new();
      public bool ShowPathGrids { get; set; } = true;
      public bool ShowSpawnList { get; set; } = true;
  }
  ```
- `SettingsManager` reads/writes `%APPDATA%/VisualEQ/settings.json` using `System.Text.Json`. Creates the directory and file on first launch.
- Wire into `App.cs`: load settings before `Controller` creation; pass `DatabaseSettings` into `MySqlConnectionFactory`.

#### 1.2 — `DatabaseConnectionView`

- **File:** `VisualEQ/Views/DatabaseConnectionView.cs`
- ImGui window (280×320), shown on startup if no DB settings saved, or via menu.
- Fields: Host, Port (default 3306), Database, Username, Password (masked), Connection Timeout.
- Buttons: **Test Connection** (calls `TestConnectionAsync()`, shows inline result), **Save & Connect**.
- On successful connect: hide the panel, trigger zone spawn load if a zone is already loaded.
- On failure: red status text with the exception message.

#### 1.3 — Verify `SpawnRepository` queries against live schema

- Open `Database/Constants/SqlQueries.cs` and audit all four queries against `proxeeus_db_schema.sql`:
  - `GetZoneSpawns`: JOIN `spawn2` → `spawngroup` → `spawnentry` → `npc_types`. Confirm column names match (e.g., `spawngroupID` not `spawngroupid`).
  - `UpdateSpawnLocation`: confirm `x`, `y`, `z`, `heading` column names.
- Add `GetZoneSpawnsFull` query that returns, for each `spawn2` row, all `spawnentry` rows (npcID + chance) and a join to the primary NPC type (highest-chance entry).

---

### Phase 2 — Spawn Data Loading

**Goal:** After a zone loads, all its spawns are fetched from DB and available in memory.

#### 2.1 — Extended DB query

- `ISpawnRepository.GetZoneSpawnsFullAsync(string zoneName)` returns `IEnumerable<SpawnRecord>` where `SpawnRecord` contains:
  ```csharp
  public class SpawnRecord {
      public Spawn2 Spawn { get; set; }
      public SpawnGroup Group { get; set; }
      public List<(SpawnEntry Entry, NpcType Npc)> Entries { get; set; }  // all possible NPCs
      public Grid? Grid { get; set; }
      public List<GridEntry> Waypoints { get; set; }
  }
  ```
- Implementation: one query for `spawn2` + `spawngroup` JOIN, then batch-load `spawnentry` + `npc_types` for all retrieved `spawngroupID`s, then batch-load `grid_entries` for all non-zero `pathgrid` values. Avoid N+1.

#### 2.2 — `RaceModelMapper`

- **File:** `VisualEQ/SpawnSystem/RaceModelMapper.cs`
- Static dictionary `Dictionary<int, string>` mapping EQ race IDs to 3-letter model codes:

  | Race ID | Code | Race |
  |---|---|---|
  | 1 | HUM | Human |
  | 2 | BAR | Barbarian |
  | 3 | ERU | Erudite |
  | 4 | ELF | Wood Elf |
  | 5 | HIE | High Elf |
  | 6 | DEF | Dark Elf |
  | 7 | HEF | Half Elf |
  | 8 | DWF | Dwarf |
  | 9 | TRL | Troll |
  | 10 | OGR | Ogre |
  | 11 | HFL | Halfling |
  | 12 | GNM | Gnome |
  | 13 | ORC | Orc |
  | 14 | IKS | Iksar |
  | 15 | VAH | Vah Shir (Kerran) |
  | 16 | FRG | Froglok |
  | 17 | DRK | Drakkin |
  | 21 | WOL | Wolf |
  | 42 | ELE | Air Elemental |
  | 43 | EAR | Earth Elemental |
  | 44 | FIR | Fire Elemental |
  | 45 | WAT | Water Elemental |
  | 46 | SKL | Skeleton |
  | 54 | DRG | Dragon |
  | ... | ... | (extend as encountered) |

- `string? Resolve(int raceId, int gender)` — returns model code (e.g. `"ORC"`) or `null` if unmapped.
- Gender suffix rules (appended to model name when looking up in chr file): 0=`_M`, 1=`_F`, 2=no suffix. The `LoadCharacter` lookup should try gender-specific first, then base code.

#### 2.3 — Model availability check at zone load

- When `Loader.LoadZoneFile` opens the chr zip, enumerate available model names and expose them: `HashSet<string> AvailableModels`.
- `SpawnLoader` checks `RaceModelMapper.Resolve()` then `AvailableModels.Contains()` before calling `LoadCharacter`. If unavailable, uses the fallback placeholder.
- This check is O(1) per spawn — no file I/O per spawn.

#### 2.4 — `SpawnPoint` + `SpawnManager`

- **`SpawnPoint`** (`VisualEQ/SpawnSystem/SpawnPoint.cs`):
  ```csharp
  public class SpawnPoint {
      public SpawnRecord Record { get; }        // DB data
      public AniModelInstance Model { get; }    // Scene object
      public bool IsDirty { get; private set; }
      public Vector3 OriginalPosition { get; }  // from DB on load
      public float OriginalHeading { get; }
      public void MarkMoved(Vector3 newPos, float heading) { IsDirty = true; ... }
      public void Revert() { Model.Position = OriginalPosition; IsDirty = false; ... }
  }
  ```

- **`SpawnManager`** (`VisualEQ/SpawnSystem/SpawnManager.cs`):
  ```csharp
  public class SpawnManager {
      public List<SpawnPoint> SpawnPoints { get; }
      public SpawnPoint? Selected { get; private set; }
      public SpawnUndoStack UndoStack { get; }
      public event Action<SpawnPoint>? SpawnSelected;
      public event Action<SpawnPoint>? SpawnMoved;

      public async Task LoadZoneSpawnsAsync(string zoneName, ISpawnRepository repo, Loader loader) { ... }
      public async Task SaveDirtySpawnsAsync(ISpawnRepository repo) { ... }
      public void Select(AniModelInstance model) { ... }
      public void HandleDragEnd(AniModelInstance model, Vector3 newPos) { ... }
  }
  ```

#### 2.5 — Wire into `App.cs` / `Controller.cs`

- After `LoadZone()` succeeds: `await spawnManager.LoadZoneSpawnsAsync(zoneName, repo, loader)`.
- Each `SpawnPoint.Model` (the `AniModelInstance`) is added to `EngineCore.AniModels` for rendering.
- `ModelSelector` events are forwarded through `SpawnManager.Select()` / `HandleDragEnd()`.

---

### Phase 3 — Spawn Visualization

**Goal:** Every spawn point has a visual 3D model at the correct world position.

#### 3.1 — Coordinate mapping

EQ DB coordinates (`spawn2.x`, `y`, `z`) map directly to world space. Verify with a known spawn in a familiar zone (e.g., find a named ORC in `gfaydark` whose rough location is known in-game). No transform should be needed beyond what the existing `AniModelInstance.Position` setter applies.

#### 3.2 — Fallback placeholder model

- Define a constant `FallbackModelName = "ORC"` (or whichever is always available).
- `SpawnLoader` uses it when `RaceModelMapper` returns `null` or the model is absent from the chr zip.
- Tag the `SpawnPoint` with `bool IsPlaceholder = true` so the UI can show a warning icon.

#### 3.3 — Visual differentiation

Using the existing material/rendering system:
- **Normal spawn:** standard render.
- **Placeholder spawn:** add a tint (modify the AniModel's uniform color if supported) or render a simple colored marker quad above the model.
- **Selected spawn:** draw a colored wireframe ring or bounding circle below the model (forward pass, no depth write).
- **Dirty (moved) spawn:** show a small colored dot or halo (yellow/orange) to indicate unsaved state.

These markers can be drawn in `EngineCore.OnRenderFrame` after the main AniModel pass using simple immediate-mode GL line geometry (no VBO needed at prototype stage).

---

### Phase 4 — Spawn Info Panel

**Goal:** Clicking a spawn shows a full-detail panel with all relevant DB data, editable fields, and action buttons.

#### 4.1 — `SpawnInfoView`

- **File:** `VisualEQ/Views/SpawnInfoView.cs`
- ImGui window (320×520), docked to the right side of the screen.
- Subscribes to `SpawnManager.SpawnSelected`.

**Content when a spawn is selected:**

```
[ Spawn #1234 ]  [✓ Enabled] [!Dirty]

--- Position ---
X: [-153.00]  Y: [149.00]  Z: [80.00]
Heading: [0.00]
[Revert Position]

--- Spawn Group: "orc_warrior_group" (ID: 456) ---
Respawn Time: 300s  Variance: ±60s
Spawn Limit: 1   Dist: 0

--- Possible Spawns ---
 85%  Orc Warrior      (ID: 789)   Lvl 12  Race: Orc  Class: Warrior
 15%  Orc Scout        (ID: 790)   Lvl 10  Race: Orc  Class: Rogue

--- Path Grid ---
Grid ID: 12  (7 waypoints)  [Show/Hide Path]

[Save This Spawn]   [Save All Dirty (3)]
```

- **Editable position fields**: `ImGui.InputFloat` for X, Y, Z, Heading. Editing a field marks the spawn dirty and moves the model.
- **Revert Position**: calls `SpawnPoint.Revert()`, pushes nothing to undo stack (revert is its own undo).
- **Save This Spawn**: saves only the selected spawn.
- **Save All Dirty**: saves all dirty spawns with a count indicator.

#### 4.2 — Keyboard shortcuts (handled in `EngineCore.OnKeyDown`)

| Shortcut | Action |
|---|---|
| `Ctrl+Z` | Undo last move |
| `Ctrl+Y` / `Ctrl+Shift+Z` | Redo |
| `Ctrl+S` | Save all dirty spawns |
| `Escape` | Deselect current spawn |
| `Delete` | (Phase 6) Mark spawn disabled |

#### 4.3 — Status bar additions

Extend `StatusView` to show:
- `[DB: Connected]` / `[DB: Disconnected]` indicator.
- `[N spawns loaded]` count.
- `[N unsaved changes]` when dirty spawns exist.

---

### Phase 5 — Move, Undo, and Save

**Goal:** Dragging a spawn marks it dirty, Ctrl+Z restores, Ctrl+S writes to DB.

#### 5.1 — `SpawnUndoStack`

- **File:** `VisualEQ/SpawnSystem/SpawnUndoStack.cs`
- Interface: `IUndoCommand { void Execute(); void Undo(); }`
- `MoveSpawnCommand`: stores `SpawnPoint`, `oldPosition`, `newPosition`, `oldHeading`, `newHeading`.
- Stack cap: 50 commands. Beyond that, oldest are dropped.
- `SpawnManager.HandleDragEnd()` creates and pushes a `MoveSpawnCommand`.
- Undo: pop stack, call `command.Undo()`, push to redo stack.

#### 5.2 — Drag end detection

`ModelSelector` already fires `OnPositionChanged`. Extend it (or add a new event `OnDragEnded`) that fires once when the left mouse button is released after a drag. This is the point at which `SpawnManager.HandleDragEnd()` creates the undo command.

**Important:** During drag, do NOT create undo commands — only at drag release. Otherwise Ctrl+Z would undo micro-movements.

#### 5.3 — Save flow

```csharp
// SpawnManager.SaveDirtySpawnsAsync
foreach (var sp in SpawnPoints.Where(s => s.IsDirty)) {
    await repo.UpdateSpawnLocationAsync(sp.Record.Spawn.Id, sp.Model.Position, sp.CurrentHeading);
    sp.ClearDirty();
}
```

- Wrap in a try/catch. On failure: surface error in `SpawnInfoView` status area, do NOT clear dirty flag.
- On success: update `sp.OriginalPosition` to the new position (so Revert now goes back to the saved position, not the original load-time position).

#### 5.4 — Heading update on drag

The existing drag moves X/Y/Z. Add heading calculation: when dragging, compute the yaw angle from the drag delta and update `AniModelInstance.Rotation` accordingly (optional — heading can also be set manually in the info panel).

---

### Phase 6 — Path Grid Visualization

**Goal:** Spawns with a `pathgrid` set show their waypoints as a connected line in the 3D view.

#### 6.1 — Data

`SpawnRecord.Waypoints` is already populated by Phase 2's batch load. Each `GridEntry` has X, Y, Z, Heading, Pause (seconds).

#### 6.2 — Rendering

- In `EngineCore`, add `List<PathGridOverlay> PathGrids` (or let `SpawnManager` own them and expose a draw method).
- `PathGridOverlay.Draw(Matrix4x4 projView)`:
  - Use an unlit forward shader (or a minimal hardcoded GLSL program).
  - Draw waypoint positions as `GL_POINTS` (size 8).
  - Draw edges between consecutive waypoints as `GL_LINES`.
  - Draw a line from the last waypoint back to the first (looping grid).
  - Color: light blue by default; yellow when the owning spawn is selected.
- Use a VAO/VBO that is rebuilt only when waypoints change (not every frame).

#### 6.3 — UI controls

In `SpawnInfoView`, the **[Show/Hide Path]** button toggles `PathGridOverlay.Visible`.

In `AppSettings`: `ShowPathGrids` bool controls default visibility; toggled via a checkbox in `StatusView` or a menu.

In `PathGridView` (optional separate panel, Phase 6+):
- List all grids in the zone.
- Show/hide individual grids.
- Click a grid row to camera-teleport to its centroid.

---

### Phase 7 — Spawn List Sidebar

**Goal:** A scrollable list of all zone spawns for navigation and filtering without relying on 3D picking.

#### 7.1 — `SpawnListView`

- **File:** `VisualEQ/Views/SpawnListView.cs`
- ImGui window pinned to the left side (220×800 or resizable).
- Subscribes to `SpawnManager` — refreshes when spawns load.

**Content:**
```
[Search: ____________]  [Filter ▾]

 # 1234  Orc Warrior       [!]
 # 1235  Orc Scout
 # 1236  Orc Shaman        [D]
 # 1237  ??? (placeholder) [P]
```

- `[!]` = dirty (unsaved move), `[D]` = disabled, `[P]` = placeholder model.
- Clicking a row: selects the spawn, teleports camera to its position.
- **Search**: filters by NPC name (substring, case-insensitive).
- **Filter dropdown**: All / Dirty only / Enabled / Disabled / Has Path Grid / Placeholder.
- Double-click: centers camera on spawn and zooms in.

#### 7.2 — Camera teleport to spawn

Add `Controller.TeleportToSpawn(SpawnPoint sp)`: sets `Camera.Position = sp.Model.Position + vec3(0, 150, 0)` and points camera downward at the spawn. Reuse the existing camera rotation mechanism.

---

### Phase 8 — Advanced Editing (Future)

These are deferred until the core phases are stable.

#### 8.1 — Multi-select

- `Ctrl+click` in 3D view or spawn list adds to selection set.
- `SpawnManager.SelectedSet` replaces single `Selected`.
- Batch move: all selected spawns move by the same delta.
- Batch save: "Save All Selected".

#### 8.2 — Clone spawn

- Right-click context menu on selected spawn → **Clone**.
- Creates a new `spawn2` row in DB (via `INSERT`) at the same position + small offset.
- New `SpawnPoint` immediately added to scene.

#### 8.3 — Enable / Disable spawn

- Right-click → **Toggle Enabled**.
- Updates `spawn2` `enabled` column (not in schema but common EQEmu extension) or uses `_condition`/`cond_value` pattern.
- Disabled spawns rendered semi-transparent.

#### 8.4 — Waypoint editing

- Click a waypoint sphere in the 3D view to select it.
- Drag to move (updates `grid_entries` on save).
- Right-click waypoint → **Add After**, **Delete**.
- Edit `pause` time in a small popup.

#### 8.5 — Search & filter across zones

- Add a "Zone Picker" dropdown to load spawns from any zone without reloading the 3D zone geometry. Useful for cross-referencing data.

#### 8.6 — Export

- **Export to SQL**: generates `UPDATE spawn2 SET x=..., y=..., z=... WHERE id=...;` statements for all dirty spawns. Useful as a backup before writing to DB.
- **Export to CSV**: all spawns in zone with full npc_types data.

---

## 4. Technical Reference

### DB Schema Notes

- `spawn2.spawngroupID` (note the capital G) — match case in Dapper queries.
- `spawnentry` primary key is `(spawngroupID, npcID)` — no auto-increment ID.
- `grid` primary key is `(zoneid, id)` — need zone's numeric ID to look up grids. Fetch via `SELECT zoneidnumber FROM zone WHERE short_name = @ZoneName`.
- `npc_types.name` is `TEXT`, not `VARCHAR` — Dapper handles this fine.
- All float columns are `float(14,6)` — maps to C# `float` without precision issues at EQ coordinate scales.

### EQ → Scene Coordinate Mapping

EQ world coordinates from the DB should map directly to `AniModelInstance.Position`. Verify by loading a zone and comparing a known spawn's DB X/Y/Z to where you'd expect the model to appear. No axis flip is expected based on the existing ORC test position (`-153, 149, 80`).

### Model Name Resolution Algorithm

```
1. Get NpcType.Race, NpcType.Gender
2. code = RaceModelMapper.Resolve(race, gender) → e.g., "ORC_M"
3. if code != null && AvailableModels.Contains(code) → LoadCharacter(chrPath, code)
4. else if code != null && AvailableModels.Contains(baseCode) → LoadCharacter(chrPath, baseCode)
5. else → use FallbackModelInstance (pre-loaded shared instance, mark IsPlaceholder=true)
```

AniModel instances can be **shared** across multiple SpawnPoints with the same model code. Only `AniModelInstance` needs to be unique per spawn (it holds position/rotation state). `AniModel` (the mesh data) is the expensive shared resource.

### Render Performance Notes

- Zones with 500+ spawns: at prototype stage, one `AniModelInstance` per spawn is acceptable. If frame rate degrades, instanced rendering (GL instancing) is the next step — but that requires refactoring `AniModel.Draw()`.
- Path grid lines: immediate-mode GL LINE drawing (no persistent VBO) is fine for hundreds of waypoints total across a zone.
- LOD (level of detail) and culling are deferred to a performance optimization phase.

---

## 5. Success Milestones

| Milestone | Definition of Done |
|---|---|
| **M1** | DB connection UI works; settings persist; can connect to local EQEmu DB |
| **M2** | Loading a zone also loads and prints all `spawn2` rows to console |
| **M3** | Every spawn renders a 3D model (or placeholder) at the correct world position |
| **M4** | Clicking a spawn shows its full data in SpawnInfoView |
| **M5** | Dragging a spawn, releasing, and pressing Ctrl+Z correctly reverts the position |
| **M6** | Ctrl+S saves all dirty spawns to DB; server-side positions match new values |
| **M7** | Path grids render for spawns with a grid ID |
| **M8** | SpawnListView with search/filter is functional |

---

## 6. File Creation Order (Recommended)

1. `VisualEQ/Settings/AppSettings.cs` + `SettingsManager.cs`
2. `Database/Constants/SqlQueries.cs` — audit & extend existing queries
3. `Database/Models/SpawnRecord.cs` — compound result type
4. `Database/Repositories/SpawnRepository.cs` — add `GetZoneSpawnsFullAsync`
5. `VisualEQ/Views/DatabaseConnectionView.cs`
6. `VisualEQ/SpawnSystem/RaceModelMapper.cs`
7. `VisualEQ/SpawnSystem/SpawnPoint.cs`
8. `VisualEQ/SpawnSystem/SpawnUndoStack.cs`
9. `VisualEQ/SpawnSystem/SpawnManager.cs`
10. `VisualEQ/SpawnSystem/SpawnLoader.cs`
11. Wire `App.cs` + `Controller.cs` + `EngineCore.cs`
12. `VisualEQ/Views/SpawnInfoView.cs`
13. `VisualEQ/Views/SpawnListView.cs`
14. Path grid rendering in `EngineCore.cs`
