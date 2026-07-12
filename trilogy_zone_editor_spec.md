# Feature: Visual editor for Trilogy `zone_points` (EQEmu server data)

## Domain background

EQEmu is an EverQuest server emulator. This tool adds visual editing for a table called `trilogy_zone_points` — a per-zone list of "zoneline triggers" used by the Trilogy (v29c) client's server-side zone-crossing detection. Each row defines an in-world trigger volume and a destination the player is teleported to when they enter it.

Because Trilogy's client has no client-side zone-edge detection, the server checks player position every tick and fires the first matching trigger. Getting the geometry right is critical — misplaced triggers cause silent teleport failures, teleport-to-void, or "sandwich" loops between adjacent zones.

## Data model — the columns

Table `trilogy_zone_points`:

| Column | Type | Meaning |
|---|---|---|
| `id` | int PK | |
| `zone` | varchar | Source zone shortname (`skyshrine`, `commons`, etc.) |
| `x, y, z` | float | Trigger center in EQ world coords |
| `heading` | float | 0-255 (EQClassic scale; server doubles to 0-512 EQEmu scale) |
| `target_zone` | varchar | Destination zone shortname |
| `target_x, target_y, target_z` | float | Landing coord in the destination zone |
| `Zrange` | int | Half-side of the trigger box in XY (5 = 10×10 box) |
| `maxZDiff` | int | Z tolerance for box mode. `0` treated as effectively unbounded (50000) |
| `UseNewZoning` | tinyint | `0`=box mode, `1`=X-plane crossing, `2`=Y-plane crossing |
| `MinVert, MaxVert` | float | Perpendicular-axis bounds for plane crossings (`0/0` = unbounded) |
| `CenterPoint` | float | Reference for centerpoint remap when `keepX/Y != 1` on plane crossings |
| `keepX, keepY, keepZ` | int | If 1, preserve that axis of player position when teleporting (outdoor seamless transitions) |
| `ToZoneID` | int | Destination zone numeric id (redundant with `target_zone` for the server; UI can ignore) |

**Constraint:** `Zrange` is a single scalar used for BOTH X and Y half-widths. Box mode is always **square** in XY. For elongated triggers, use plane crossings (mode 1 or 2), not stretched boxes.

## The three trigger modes to visualize

### Box (`UseNewZoning=0`) — most common
- 3D axis-aligned box centered at `(x, y, z)`.
- XY extent: `2*Zrange` per side (always square).
- Z extent: `2*maxZDiff` (or infinite when `maxZDiff=0` — render as a tall column with a cap indicator).
- Corner/face handles resize `Zrange` and `maxZDiff`.
- Center handle moves `(x, y, z)`.

### X-plane crossing (`UseNewZoning=1`)
- Vertical plane at `X = zln.x`, perpendicular to the world X axis.
- Extends from `MinVert` to `MaxVert` on the Y axis (both `0` = unbounded, treat as world Y range).
- Fires when player crosses that X. Direction inferred from sign of `x`:
  - `x >= 0` → fires on player `X >= x` (heading east)
  - `x <= 0` → fires on player `X <= x` (heading west)
- Draw an arrow indicating fire direction.
- End-cap handles set `MinVert` / `MaxVert`. Center handle sets `x`.
- No Z gating in current engine — the plane is effectively infinite vertically. Render as a tall wall.

### Y-plane crossing (`UseNewZoning=2`)
- Same as X-plane but roles of X and Y swapped.
- Plane at `Y = zln.y`, extends between `MinVert/MaxVert` on X.
- Direction inferred from sign of `y`:
  - `y >= 0` → fires on player `Y >= y` (heading north)
  - `y <= 0` → fires on player `Y <= y` (heading south)

## Wildcard semantics

The value `999999` (or `-999999`, sentinel threshold `|coord| >= 999998`) is a **wildcard**:

- **In source `x` or `y`**: "this axis is not gated" — trigger fires regardless of player's X (or Y). Used for fall-through triggers (Plane of Sky at `z=-2000`) and outdoor edge patterns.
- **In target `target_x/y/z`**: "preserve player's axis across the crossing" — treated equivalently to `keepX/Y/Z=1`. Used for seamless outdoor transitions between adjacent zones sharing a coord system.

UI should visually distinguish wildcards from real values (grayed placeholder text, wildcard icon, or explicit "wildcard" toggle on each axis input).

## Editing gestures required

1. **Corner-drag** on box handles → resize `Zrange` (and `maxZDiff` for Z-axis handles). Because `Zrange` is a single scalar, dragging any XY corner uniformly changes both axes — the visual should reflect a square constraint.
2. **Center-drag** to move trigger position (`x/y/z`).
3. **Drag rectangle in the ground plane** → creates a new box `zone_point` with center = rectangle center, `Zrange` = half of max(width, height). If the rectangle is markedly non-square, the UI should surface that the box will be square-fit (largest of width/height) and offer to switch to plane crossing mode instead.
4. **Drag a line across the zone edge** → creates a plane crossing. Line endpoints become `MinVert`/`MaxVert`, midpoint's X or Y becomes the trigger coord. Sign inferred from which side of the line the destination sits (needs a "destination direction" indicator).
5. **Shift-drag** any existing trigger → duplicates it (for the "multi-box long edge" pattern).
6. **Ctrl-drag** corner handles → symmetric resize (both sides at once).

## Visualization requirements

### Destination anchor
Show a floating marker at `target_x/y/z` if the target is in the current zone, or a directional arrow labeled `→ <target_zone>` pointing off-view if cross-zone. Makes "where does this send you" answerable at a glance.

### Color-coding by row health
- **Green**: source is real (`x != 0 || y != 0 || z != 0`), target is real.
- **Yellow**: source real but target has any `999999` wildcard.
- **Red**: source is `(0, 0, 0)` — the row cannot fire in-game until fixed.
- **Purple**: source uses legitimate wildcards (`|x| >= 999998` or `|y| >= 999998`) — special-case trigger like a fall-through, don't confuse with red.

### Sandwich detector overlay
For each cross-zone row where the destination is another loaded zone we have data for:

1. Look up the reverse row (`WHERE zone = <destination> AND target_zone = <current>`).
2. Project this row's `target_x/y/z` (the landing point in the destination) against the reverse row's trigger region.
3. If the landing point is INSIDE the reverse row's fire region:
   - Draw the current trigger box outlined in bright red with a "SANDWICH" badge.
   - Show a preview line from the landing point back to where the reverse row would fire.
4. Fix suggestion: shift `target_x/y/z` 50+ units *away* from the reverse trigger.

For box mode: sandwich fires if the landing coord falls inside the reverse trigger's XY box.

For plane crossings: sandwich fires if the target coord falls on the fire side of the reverse trigger (accounting for `MinVert/MaxVert` bounds and trigger direction).

### Trigger volume fill
Semi-transparent volumetric fill inside boxes/planes, not just wireframe. Makes the trigger region readable at any camera angle. Alpha ~0.15, hue matches the row-health color.

### Zone geometry overlay
If the editor already loads zone geometry (`.s3d` collision meshes or converted representations) for placing spawns, layer the trigger volumes on top of that same mesh. Lets the user align triggers to walls, floors, actual doorways.

## Data-model constraints for edits

- No schema changes needed. Editor reads/writes existing columns.
- Coordinate space matches the server's — EQ world coords in floats, no transformation needed.
- Sentinel values: use `999999.0` when setting a wildcard. Don't use `-999999` for target-axis wildcards (the engine accepts both but the convention is positive for targets).
- After any DB write, emit a `#reload static` command to the running zone process (via existing server-command interface) so live gameplay reflects edits without a zone restart.

## Reference: what "correct" data looks like

- **Same-zone teleporter pad** (Skyshrine style): box mode, `Zrange=5`, source at the pad's exact XY, `target_x/y/z` = where the player should land, all `keep*` = 0.
- **Outdoor zone edge**: plane crossing (`UseNewZoning=1` or `2`), single row per edge, `MinVert/MaxVert` = perpendicular-axis extent of the edge on the map, `target_x` or `target_y` = the specific arrival coord in the destination, `keepY` or `keepX` = 1 to preserve the perpendicular axis. **Landing coord must be 50+ units past the destination's own reverse trigger to avoid sandwich loops.**
- **Dungeon-entrance door**: box mode, tight `Zrange=5`, real `x/y/z` at the door threshold, real `target_x/y/z` at the dungeon spawn point.
- **Fall-through trigger** (Plane of Sky style): box mode, `x=999999, y=999999`, real `z` (e.g. `-2000`), `maxZDiff=100` (tight enough for the fall-through band, loose enough that fast-falling characters catch it).

## Out of scope for this iteration

- Direct in-game position recording ("drop trigger at my character's current pos" mode) — deferred.
- Live playtesting inside the editor.
- Migration/import from modern EQEmu's `zone_points` table.
- Schema extension for rectangular box mode (per-axis `XRange`/`YRange` columns). Editor should treat elongated drags as plane-crossing intent instead.
