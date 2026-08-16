# VisualEQ NPC Editor — Design & Delivery Plan

Companion to `VisualEQ-SpawnEditor-Plan.md`. This is the plan for the NPC content editor built on top of the shipped spawn viewer.

## 1. Scope

**In:**
- Full edit surface for `npc_types` (combat stats, visuals, flags, AI, faction assignment, special abilities).
- Faction assignment editor on the NPC (`npc_faction` / `npc_faction_entries`), plus a lightweight faction browser.
- Loot editor: assign a `loottable` to an NPC, edit its `loottable_entries` (which `lootdrop`s + drop counts), edit `lootdrop_entries` (items + chances). **What the NPC drops / doesn't drop only — no item-table editing.**
- Live visual preview for every field that changes what's rendered.
- Undo everywhere, transactional save.
- Read-only mode toggle to safely browse a prod DB.

**Deferred (future milestones):**
- Item editor (`items` table). Dedicated milestone later.
- Faction hit editing on quests / kills.
- Merchant / trader tables.

## 2. UX doctrine ("minimal menu diving")

1. **One panel, three tabs.** All editing in the right sidebar. Tabs: **Stats / Visual / Combat & AI**, plus collapsible **Factions** and **Loot** sections below.
2. **Live preview by default; dirty-until-committed.** Visual fields update the model in-scene as you scrub. Save commits to DB; Escape/Ctrl-Z reverts.
3. **Camera follows intent.** Editing "face" flies to head-height with look-at lock. "Helm" frames upper body. "Texture" frames full body. "Size" pulls camera back.
4. **Inline pickers, not dropdowns of IDs.** Race/class/faction/loottable are searchable typeaheads.
5. **Adjacent-value scrubbers.** Any small-N enum (texture 0–3, face 0–7, helm 0–N, gender 0–2) gets `◀ [current] ▶` arrows + numeric field. Arrow keys work when focused.

## 3. UI layout

```
┌───────────────────── Scene ─────────────────────┬─ Editor ─┐
│                                                  │ Guard    │
│                                                  │ Kalen    │
│                                                  │ HUM · M  │
│                                                  │ Uses: 3  │  ← shared-npc warning
│              [selected NPC framed]               │ 🔒 R/O   │  ← read-only toggle
│                                                  │ ─────────│
│                                                  │ [Stats][Vis][Combat]
│                                                  │          │
│                                                  │ Face: ◀ 3 ▶
│                                                  │ Helm: ◀ 0 ▶
│                                                  │ Texture: ◀ 1 ▶
│                                                  │ Size: [ 6.0 ]
│                                                  │          │
│                                                  │ ▼ Factions (3)
│                                                  │ ▼ Loot
│                                                  │ ─────────│
│                                                  │ [Save] [Revert]
└──────────────────────────────────────────────────┴─────────┘
```

- Sidebar ~340 px wide, resizable, right side, always mounted (shows "Select an NPC" empty state).
- Ctrl+S saves, Ctrl+Z undoes, Escape reverts un-saved field.
- **Ctrl+D always clones**: creates a new `npc_types` row (copy of current) and re-points this spawn's `spawnentry.npcID` to it, so subsequent edits only affect this spawn. "Uses: N" indicator near NPC name shows when editing a shared record.

## 4. Real-time preview mechanics

Loader today bakes `(textureIndex, helmTextureIndex, faceIndex)` into the AniModel cache key ([Loader.cs](VisualEQ/Loader.cs)). No in-place buffer swap.

**Approach — leverage the existing cache:**
- On any visual-field change, call `Loader.LoadCharacter(..., tex, helm, face)` — shared `_meshGeometryCache` and `_textureCache` per CLAUDE.md §5/§11 — and swap the `AniModelInstance.Model` pointer.
- First scrub uploads one PNG; warm scrubs are free.
- When the Face/Helm/Texture tab opens, background-prime the cache by loading each variant's Materials (GL calls dispatched on the render thread).
- **Preview vs commit:** `SpawnPoint` grows a `PendingNpcEdits` bag. Live-swapped instance uses pending values; DB and `SpawnPoint.Npc` snapshot untouched until Save. Revert restores the original AniModel pointer.

**Camera framing** — extend `FpsCamera` ([Engine/FpsCamera.cs](Engine/FpsCamera.cs)):
- `FrameSubject(instance, FramingHint)` — computes target position + orientation from instance bounds; reuses existing 0.25 s tween.
- `LockLookAt(worldPoint)` — while active, rotation driven by look-at each frame; any keyboard/mouse-drag releases.
- Head/torso points from spine cylinder in `ModelSelector`: head ≈ `Position + (0,0,6*Scale)`, torso ≈ `Position + (0,0,4*Scale)`.

Wiring: focus on Face → `FrameSubject(Face)` + `LockLookAt(HeadPoint)`. Focus lost → release lock, camera stays put (don't yank back).

## 5. Faction editor

Rows in `npc_faction_entries` where `npc_faction_id = npc_types.npc_faction_id`. Two levels:

- **Inline** (sidebar Faction section): current `npc_faction`'s entries — `faction_list.name` + hit value + temp flag. Add/remove/tweak inline. Global typeahead over `faction_list`.
- **Pop-out browser** — "Manage Faction Sets" dockable window for creating/reusing `npc_faction` rows across NPCs. Deferred to polish slice.

Out of scope for v1: editing `faction_list` itself, quest hits, `faction_association`.

## 6. Loot editor

Three-level hierarchy, all inline:

1. **NPC → loottable** — dropdown: "None", "New (empty)", or typeahead over existing loottables.
2. **Loottable → lootdrops** — rows in `loottable_entries` (multiplier, droplimit, mindrop, probability).
3. **Lootdrop → items** — rows in `lootdrop_entries` (item + chance + multiplier + min/max). Item picker is typeahead over `items.name`.

**Preview UX:** loottable rows show "% chance to actually drop"; item rows show `chance / sum(chance) × multiplier` estimate.

**Copy-on-edit safety:** editing a shared loottable/lootdrop prompts "Shared with N other NPCs. Edit anyway / Clone for this NPC only." Prevents footgun.

## 7. Special abilities editor

`npc_types.special_abilities` is a delimited string (`^`-separated abilities, each `id,param1,param2,…`). Raw editing is a footgun; friendly editor lives on Combat & AI tab:

```
▼ Special Abilities
  ☑ Summon         Range: [ 75 ]                    [?]
  ☐ Enrage
  ☑ Rampage        Chance: [ 20 ]%  Targets: [ 4 ]  [?]
  ☑ Flurry         Chance: [ 30 ]%                  [?]
  ...
  ─────────────────────────────────
  ▶ Advanced (raw string)
    [ 1,1^7,20,4^11,30 ]
```

- One row per known ability; checkbox toggles inclusion; enabled rows expose only that ability's parameter fields (labeled, typed).
- `[?]` tooltip shows the server-side description from the `SpecialAbility` enum.
- **Advanced expander** shows raw serialized string, editable fallback. Unknown entries surface as read-only "Custom (id=99)" rows so we never silently drop data.
- Live parse/serialize between checkboxes and raw. Save-time validation rejects unparseable strings, warns on out-of-range params.

**Ability catalog** generated once from EQEmu server source (`/c/eqemu/source/Server` per user memory) — `SpecialAbility` enum + `ProcessSpecialAbilities` parser. Produces a static `SpecialAbilityCatalog`; no runtime dependency on the server tree.

## 8. Data layer additions

Following [Database/](Database/) conventions (SqlQueries constants + Dapper repos + record models).

**New models** ([Database/Models/](Database/Models/))
- `NpcTypeFull` (all editable columns).
- `FactionListRecord`, `NpcFactionRecord`, `NpcFactionEntryRecord`.
- `LootTableRecord`, `LootTableEntryRecord`, `LootDropRecord`, `LootDropEntryRecord`, `ItemBriefRecord` (picker only).

**New repositories** ([Database/Repositories/](Database/Repositories/))
- `INpcRepository` — `GetNpcByIdAsync`, `UpdateNpcAsync`, `DuplicateNpcAsync(int npcId) → int newId`, `RepointSpawnEntryAsync(int spawnEntryId, int newNpcId)`.
- `IFactionRepository` — `GetAllFactionsAsync()` (cached), `GetNpcFactionAsync`, `UpsertNpcFactionAsync`, `DeleteNpcFactionEntryAsync`.
- `ILootRepository` — read + write for loottable / entry / drop / drop-entry hierarchy. Shared-table detection helper.
- `IItemRepository` — `SearchItemsAsync(prefix, limit)` picker only.

All new SQL in [Constants/SqlQueries.cs](Database/Constants/SqlQueries.cs), `AS PascalCase` alias convention. Every write parameterized. Batch reads for list UIs.

**Schema variance guardrail** (per CLAUDE.md §7): before wiring any UPDATE, run `SHOW COLUMNS FROM <table>` and select only columns that actually exist. Fail closed with clear "Your schema is missing column X, editing disabled for this field" toast, don't crash on write.

## 9. Save & undo architecture

**Dirty tracking** — `EditSession` per-selected-NPC in-memory delta. Sidebar shows unsaved-changes indicator on tab headers.

**Undo stack** — `EditCommand` interface, one command per user action (`ChangeFieldCommand`, `AddFactionEntryCommand`, `RemoveLootRowCommand`, …). Do/Undo apply to `EditSession` and to live scene state (undoing a face change flips the AniModel back). Bounded to N=50.

**Save transactionality**
- NPC-only edits: single `UPDATE npc_types` in a transaction.
- NPC + factions: transaction covers `npc_types` + `npc_faction_entries` upsert/delete.
- Loot: transaction covers all three loot tables affected.
- On failure: rollback DB, keep dirty state in editor, toast error. Do NOT auto-revert scene visuals — user chose those values, might want to retry.

**No autosave.** Explicit Save button. Ctrl+S shortcut. Closing panel with unsaved edits prompts.

## 10. Read-only mode

`AppSettings.NpcEditorReadOnly` (persisted). Toggle in sidebar header (🔒 icon). When on:
- All edit widgets render disabled (grayed).
- Save button hidden.
- Ctrl+D disabled.
- Live-preview scrubbers still work (visual-only, don't touch DB).

Intent: safe browsing of prod DB.

## 11. Phased delivery

Small, individually-usable slices. PR each. Pause between for real-world testing.

| Slice | Delivers | Why this order |
|---|---|---|
| **1. NPC read-side + selection wiring** | ✅ Shipped in PR #54. Sidebar shows full npc_types read-only when a spawn is picked. `NpcTypeFull` model + `GetNpcByIdAsync`. | Proves selection→panel wiring and model coverage. |
| **2. All-fields editable + FK pickers + save** | ✅ Shipped. Every non-visual, non-special-abilities npc_types column is editable in edit mode (identity, combat, regen, stats, resistances, AI/behavior, references, scaling, charm overrides). FK id fields (loottable, faction, merchant, spells, effects) render resolved names via `ReferenceDataCache` and open a modal typeahead picker on edit. Enum fields (gender, stuck_behavior, flymode, melee types) get labeled combos. Nullable ints (charm_*, rare_spawn, trap_template, skip_global_loot) get set/unset checkboxes. Save flows through the existing PendingBuffer → EditCommitter → single-transaction UPDATE. Edit-mode toggle (already shipped) doubles as the read-only guard. | Big slice — expanded scope from "just stats" per user feedback that everything should be editable and every FK id should be human-readable. |
| **3. Live-preview + camera framing** | ✅ Shipped. `texture` / `helmtexture` / `face` fields are now editable and trigger `Controller.RefreshNpcVisualForNpc` (rebuilds the AniModel via the shared cache and swaps the on-scene instance). Race, gender, and size trigger the same refresh — race/gender re-resolve the chr code via `RaceModelMapper` so a Human → Ogre change actually replaces the model. Camera auto-frames on visual-field focus (`FpsCamera.FlyToLookAt` + `LockLookAt`); face zooms into the head, helm frames upper body, texture/size/race/gender pull back to full silhouette. Any mouse-look drag releases the lock. Multi-spawn NPCs (many spawn2 rows sharing one npc_types) update every visible instance in one edit. | The QoL slice: high demo value. |
| **4. Special Abilities editor** | ✅ Shipped. `SpecialAbilityCatalog` covers all 57 abilities scraped from `emu_constants.h`; `SpecialAbilityString` parses/serializes the `id,value,p0,p1,...^` wire format (matching the server's `Mob::ProcessSpecialAbilities`). Widget renders one checkbox+value row per ability; unknown ids from raw string surface at the bottom as read-only "Custom (id=N)" rows and are preserved on save. Params 0–8 shown read-only inline for enabled rows (edit surface deferred — rare enough case). `npcspecialattks` stays read-only (legacy per-letter field the server auto-migrated into `special_abilities`; mainstream path is the new column). | Slice 3 kept `special_abilities` read-only because raw string editing was a footgun. |
| **5. Faction editor** | ✅ Shipped. Inline editor for `npc_faction_entries` rows belonging to the NPC's assigned `npc_faction` set. Each row: faction name (resolved via `ReferenceDataCache.Table.FactionList`) + `value` + `npc_value` + `temp` checkbox + Remove. Add button opens the FK picker over `faction_list` (~2k rows) with a callback path — reuses the same picker modal that handles other FK fields via a new `onPicked` delegate. `NpcFactionEntryEditAction` encodes insert/update/delete via `(Original, Current)` pair; `EditCommitter` runs deletes → inserts → updates within the same transaction as spawn/grid/NPC edits. Buffer bumps SchemaVersion 9 → 10. Pop-out "Manage Faction Sets" browser + `npc_faction` row edits (PrimaryFaction / IgnorePrimaryAssist) deferred to Slice 7. | Standalone value — independent of loot. |
| **6. Loot editor** | ✅ Shipped in three sub-slices. **6a** (PR #60): read-only 3-level view (loottable header + per-lootdrop cards + item table with names via LEFT JOIN on `items.Name` — no dependency on any lazy cache). **6b** (PR #61): full CRUD on `loottable_entries` + `lootdrop_entries` via new IEditActions on the buffered path, plus a SEARCH-based picker (`BeginSearchPicker` / `RenderSearchPickerDialog`) for the too-big-to-preload lootdrop (24k) and items (80k) tables. **6c** (PR #62): shared-table detection (`GetLootDropUsageCountBatch`), clone loottable / clone lootdrop / create-new-empty flows (immediate DB writes with `CONCAT(name, ' (clone)')` suffix + transaction-wrapped `INSERT + LAST_INSERT_ID`); confirm modals for clones; inline-editable mincash / maxcash / avgcoin on the loottable header via a new `LootTableFieldEditAction` (SchemaVersion 10 → 12). | Biggest slice; sub-sliced to keep review cycles short. |
| **7. Duplicate NPC + faction pop-out browser** | ✅ Shipped in two sub-slices. **7a** (PR #63): shared-record indicator ("Used by N spawn entries", orange when > 1) + "Duplicate NPC (for this spawn)" button — `INpcRepository.DuplicateAsync` clones the `npc_types` row (columns discovered dynamically from `INFORMATION_SCHEMA` so fork columns clone too) + `RepointSpawnEntryAsync` swaps the current spawn's `spawnentry.npcID`. Ctrl+D stays bound to spawn2 duplication in EngineCore — distinct operation. **7b** (PR #64): `npc_faction` row edits (primaryfaction picker + ignore_primary_assist checkbox in the Slice 5 editor header) via new `NpcFactionFieldEditAction` (SchemaVersion 12 → 13) + Manage Faction Sets pop-out browser (filterable list of all 20k sets from `ReferenceDataCache` + "Assign to current NPC" + inline "+ New faction set" sub-form with nested primaryfaction picker). Name-field edit deferred — needs `NpcText`-style deferred-write plumbing. | Roadmap closer. |

Each slice is a PR against master, mergeable and demo-able on its own. Pause after each for real usage before starting the next.
