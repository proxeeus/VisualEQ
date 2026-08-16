namespace VisualEQ.Database.Constants
{
    public static class SqlQueries
    {
        // Explicit aliases ensure Dapper maps correctly regardless of DB driver case behaviour.
        // spawn2 columns: id, spawngroupID, zone, version, x, y, z, heading,
        //                 respawntime, variance, pathgrid, animation
        // No 'enabled' column in this schema version.
        public const string GetZoneSpawns = @"
            SELECT
                s.id,
                s.spawngroupID  AS SpawnGroupId,
                s.zone,
                s.version,
                s.x,
                s.y,
                s.z,
                s.heading,
                s.respawntime   AS RespawnTime,
                s.variance,
                s.pathgrid      AS PathGrid,
                s.animation,
                sg.name         AS SpawnGroupName
            FROM spawn2 s
            JOIN spawngroup sg ON s.spawngroupID = sg.id
            WHERE s.zone = @ZoneName";

        public const string GetSpawnById = @"
            SELECT
                s.id,
                s.spawngroupID  AS SpawnGroupId,
                s.zone,
                s.version,
                s.x,
                s.y,
                s.z,
                s.heading,
                s.respawntime   AS RespawnTime,
                s.variance,
                s.pathgrid      AS PathGrid,
                s.animation,
                sg.name         AS SpawnGroupName
            FROM spawn2 s
            JOIN spawngroup sg ON s.spawngroupID = sg.id
            WHERE s.id = @SpawnId";

        public const string UpdateSpawnLocation = @"
            UPDATE spawn2
            SET x = @X, y = @Y, z = @Z, heading = @Heading
            WHERE id = @SpawnId";

        // Removes a spawn2 row by id. Does not touch spawngroup / spawnentry — those may
        // be referenced by other spawn2 rows in this or other zones; a "cleanup orphan
        // spawngroups" tool is a separate concern.
        public const string DeleteSpawn2 = @"
            DELETE FROM spawn2 WHERE id = @Id";

        // Duplicate-spawn commit chain (used by EditCommitter for buffer.SpawnInserts).
        // Every non-required column relies on its DB default (spawn_limit=0, delay,
        // mindelay, min_expansion=-1, etc.). LAST_INSERT_ID() is session-scoped so it
        // stays coherent inside a Dapper transaction — must be read AT ONCE via
        // ExecuteScalar<int> before the next INSERT overwrites it.
        //
        // spawngroup.name is UNIQUE — callers must pre-generate a unique name (see
        // Controller.DuplicateSelectedSpawn — source name + 4-hex Guid suffix, truncated
        // to varchar(30)). A collision throws inside the transaction → whole commit
        // rolls back → user retries (< 1-in-65k on random inserts, so acceptable).
        public const string InsertSpawnGroup = @"
            INSERT INTO spawngroup (name) VALUES (@Name);
            SELECT LAST_INSERT_ID();";

        // Minimal spawnentry insert. Composite PK is (spawngroupID, npcID); duplicates
        // within the same call are impossible because SpawnInsertAction dedupes on the
        // client side. Other cols (condition_value_filter, min_time/max_time,
        // min/max_expansion, content_flags*) all default in-DB.
        public const string InsertSpawnEntry = @"
            INSERT INTO spawnentry (spawngroupID, npcID, chance)
            VALUES (@SpawnGroupId, @NpcId, @Chance)";

        // spawn2 row insert. Columns that VisualEQ tracks in the SpawnInsert record are
        // named explicitly; DB-defaulted columns (path_when_zone_idle, _condition,
        // cond_value, min/max_expansion, content_flags*) inherit their schema defaults.
        public const string InsertSpawn2 = @"
            INSERT INTO spawn2
                (spawngroupID, zone, version, x, y, z, heading,
                 respawntime, variance, pathgrid, animation)
            VALUES
                (@SpawnGroupId, @Zone, @Version, @X, @Y, @Z, @Heading,
                 @RespawnTime, @Variance, @PathGrid, @Animation);
            SELECT LAST_INSERT_ID();";

        // Used by the Phase 5 commit path to write waypoint drags back to the DB. Key is
        // (gridid, number, zoneid) — grid_entries has no primary key beyond that composite.
        public const string UpdateGridEntry = @"
            UPDATE grid_entries
            SET x = @X, y = @Y, z = @Z, heading = @Heading, pause = @Pause, centerpoint = @Centerpoint
            WHERE gridid = @GridId AND number = @Number AND zoneid = @ZoneId";

        // Waypoint INSERT/DELETE for the add/delete affordance. Composite PK is
        // (gridid, number, zoneid) so the WHERE is deterministic.
        public const string InsertGridEntry = @"
            INSERT INTO grid_entries (gridid, zoneid, number, x, y, z, heading, pause, centerpoint)
            VALUES (@GridId, @ZoneId, @Number, @X, @Y, @Z, @Heading, @Pause, @Centerpoint)";

        public const string DeleteGridEntry = @"
            DELETE FROM grid_entries
            WHERE gridid = @GridId AND number = @Number AND zoneid = @ZoneId";

        // Grid-level metadata (wander / pause behavior). PK is (id, zoneid).
        public const string GetGridsBatch = @"
            SELECT id AS Id, zoneid AS ZoneId, type AS Type, type2 AS Type2
            FROM grid
            WHERE id IN @GridIds AND zoneid = @ZoneId";

        public const string UpdateGrid = @"
            UPDATE grid
            SET type = @Type, type2 = @Type2
            WHERE id = @Id AND zoneid = @ZoneId";

        // Look up the numeric zone ID — required for filtering grid/grid_entries by zone.
        public const string GetZoneId = @"
            SELECT zoneidnumber FROM zone WHERE short_name = @ZoneName";

        // Batch-load all spawnentry rows for a set of spawn groups (avoids N+1).
        public const string GetSpawnEntriesBatch = @"
            SELECT spawngroupID AS SpawnGroupId, npcID AS NpcId, chance AS Chance
            FROM spawnentry
            WHERE spawngroupID IN @GroupIds";

        // Batch-load npc_types for a set of NPC IDs.
        // Only selects the columns needed for model resolution in Phase 2.
        // Cosmetic columns (hair/beard/eye) are omitted — they vary by EQEmu version.
        public const string GetNpcTypesBatch = @"
            SELECT id, name, lastname AS LastName, level, race,
                   `class` AS Class, bodytype AS BodyType, size, gender,
                   texture, helmtexture AS HelmTexture, face
            FROM npc_types
            WHERE id IN @NpcIds";

        // Full npc_types read — every column the NPC editor consumes, aliased so Dapper's
        // strict-case matching populates NpcTypeFull deterministically. Kept as one query
        // rather than several so a spawn selection incurs a single round-trip.
        //
        // Reserved words / special names:
        //   class       → PickPascalCase can't hold `class`
        //   _INT        → SQL column literally starts with an underscore; aliased to Int_
        //                 (matches the C# property name we picked to sidestep the C# keyword)
        //   STR/STA/... → all-caps DB names aliased to Pascal so callers don't need to shout
        public const string GetNpcTypeById = @"
            SELECT
                id                     AS Id,
                name                   AS Name,
                lastname               AS LastName,
                level                  AS Level,
                race                   AS Race,
                `class`                AS Class,
                bodytype               AS BodyType,
                gender                 AS Gender,
                size                   AS Size,

                hp                     AS Hp,
                mana                   AS Mana,
                AC                     AS Ac,
                mindmg                 AS MinDmg,
                maxdmg                 AS MaxDmg,
                ATK                    AS Atk,
                Accuracy               AS Accuracy,
                Avoidance              AS Avoidance,
                slow_mitigation        AS SlowMitigation,
                attack_speed           AS AttackSpeed,
                attack_delay           AS AttackDelay,
                attack_count           AS AttackCount,
                heroic_strikethrough   AS HeroicStrikethrough,

                hp_regen_rate          AS HpRegenRate,
                hp_regen_per_second    AS HpRegenPerSecond,
                mana_regen_rate        AS ManaRegenRate,

                STR                    AS Str,
                STA                    AS Sta,
                DEX                    AS Dex,
                AGI                    AS Agi,
                _INT                   AS Int_,
                WIS                    AS Wis,
                CHA                    AS Cha,

                MR                     AS MR,
                CR                     AS CR,
                DR                     AS DR,
                FR                     AS FR,
                PR                     AS PR,
                Corrup                 AS Corrup,
                PhR                    AS PhR,

                texture                AS Texture,
                helmtexture            AS HelmTexture,
                face                   AS Face,
                herosforgemodel        AS HerosForgeModel,
                armtexture             AS ArmTexture,
                bracertexture          AS BracerTexture,
                handtexture            AS HandTexture,
                legtexture             AS LegTexture,
                feettexture            AS FeetTexture,
                light                  AS Light,
                model                  AS Model,
                d_melee_texture1       AS DMeleeTexture1,
                d_melee_texture2       AS DMeleeTexture2,
                ammo_idfile            AS AmmoIdfile,
                prim_melee_type        AS PrimMeleeType,
                sec_melee_type         AS SecMeleeType,
                ranged_type            AS RangedType,

                luclin_hairstyle       AS LuclinHairstyle,
                luclin_haircolor       AS LuclinHaircolor,
                luclin_eyecolor        AS LuclinEyecolor,
                luclin_eyecolor2       AS LuclinEyecolor2,
                luclin_beardcolor      AS LuclinBeardcolor,
                luclin_beard           AS LuclinBeard,
                drakkin_heritage       AS DrakkinHeritage,
                drakkin_tattoo         AS DrakkinTattoo,
                drakkin_details        AS DrakkinDetails,

                armortint_id           AS ArmortintId,
                armortint_red          AS ArmortintRed,
                armortint_green        AS ArmortintGreen,
                armortint_blue         AS ArmortintBlue,

                aggroradius            AS AggroRadius,
                assistradius           AS AssistRadius,
                runspeed               AS Runspeed,
                walkspeed              AS Walkspeed,
                see_invis              AS SeeInvis,
                see_invis_undead       AS SeeInvisUndead,
                see_hide               AS SeeHide,
                see_improved_hide      AS SeeImprovedHide,
                npc_aggro              AS NpcAggro,
                always_aggro           AS AlwaysAggro,
                findable               AS Findable,
                trackable              AS Trackable,
                raid_target            AS RaidTarget,
                no_target_hotkey       AS NoTargetHotkey,
                untargetable           AS Untargetable,
                show_name              AS ShowName,
                private_corpse         AS PrivateCorpse,
                unique_spawn_by_name   AS UniqueSpawnByName,
                `unique_`              AS `Unique`,
                `fixed`                AS `Fixed`,
                ignore_despawn         AS IgnoreDespawn,
                stuck_behavior         AS StuckBehavior,
                flymode                AS Flymode,
                rare_spawn             AS RareSpawn,
                exclude                AS Exclude,
                isbot                  AS IsBot,
                isquest                AS IsQuest,
                qglobal                AS Qglobal,
                emoteid                AS EmoteId,
                underwater             AS Underwater,
                spawn_limit            AS SpawnLimit,

                loottable_id           AS LoottableId,
                merchant_id            AS MerchantId,
                greed                  AS Greed,
                alt_currency_id        AS AltCurrencyId,
                npc_spells_id          AS NpcSpellsId,
                npc_spells_effects_id  AS NpcSpellsEffectsId,
                npc_faction_id         AS NpcFactionId,
                adventure_template_id  AS AdventureTemplateId,
                trap_template          AS TrapTemplate,
                faction_amount         AS FactionAmount,
                keeps_sold_items       AS KeepsSoldItems,
                is_parcel_merchant     AS IsParcelMerchant,
                multiquest_enabled     AS MultiquestEnabled,
                skip_global_loot       AS SkipGlobalLoot,

                scalerate              AS Scalerate,
                spellscale             AS Spellscale,
                healscale              AS Healscale,
                exp_mod                AS ExpMod,
                maxlevel               AS Maxlevel,

                charm_ac               AS CharmAc,
                charm_min_dmg          AS CharmMinDmg,
                charm_max_dmg          AS CharmMaxDmg,
                charm_attack_delay     AS CharmAttackDelay,
                charm_accuracy_rating  AS CharmAccuracyRating,
                charm_avoidance_rating AS CharmAvoidanceRating,
                charm_atk              AS CharmAtk,

                npcspecialattks        AS NpcSpecialAttks,
                special_abilities      AS SpecialAbilities,

                version                AS Version,
                peqid                  AS PeqId
            FROM npc_types
            WHERE id = @NpcId";

        // Reference-data queries for the NPC editor's typeahead pickers. Each returns
        // {id, name} rows so ReferenceDataCache can hold one dict per table and any FK
        // int column in the sidebar can resolve to a human label without a second query.
        public const string GetAllLootTables = @"
            SELECT id AS Id, name AS Name
            FROM loottable
            ORDER BY name";

        public const string GetAllNpcFactions = @"
            SELECT id AS Id, name AS Name
            FROM npc_faction
            ORDER BY name";

        public const string GetAllNpcSpellSets = @"
            SELECT id AS Id, name AS Name
            FROM npc_spells
            ORDER BY name";

        public const string GetAllNpcSpellEffectSets = @"
            SELECT id AS Id, name AS Name
            FROM npc_spells_effects
            ORDER BY name";

        // Merchantlist has no name column — one row per (merchantid, slot). Aggregate by
        // merchantid so each distinct merchant surfaces exactly once, with an item-count
        // synthesized as the label so the picker isn't just naked ids.
        public const string GetAllMerchants = @"
            SELECT merchantid AS Id, CONCAT('merchant ', merchantid, ' (', COUNT(*), ' items)') AS Name
            FROM merchantlist
            GROUP BY merchantid
            ORDER BY merchantid";

        // faction_list — master list of factions used by the per-entry picker inside
        // a faction set (npc_faction_entries.faction_id references this). ~2k rows,
        // fine for in-memory typeahead.
        public const string GetAllFactionList = @"
            SELECT id AS Id, name AS Name
            FROM faction_list
            ORDER BY name";

        // One npc_faction (set) row. Holds set-wide metadata: display name, the
        // primary faction id (fk to faction_list) this NPC belongs to, and whether
        // the primary is excluded from assist aggro.
        public const string GetNpcFactionById = @"
            SELECT
                nf.id                     AS Id,
                nf.name                   AS Name,
                nf.primaryfaction         AS PrimaryFaction,
                nf.ignore_primary_assist  AS IgnorePrimaryAssist,
                fl.name                   AS PrimaryFactionName
            FROM npc_faction nf
            LEFT JOIN faction_list fl ON fl.id = nf.primaryfaction
            WHERE nf.id = @Id";

        // Entries in a specific npc_faction set. Composite PK is
        // (npc_faction_id, faction_id) — one row per faction that dying to this NPC
        // affects. `value` = client-visible hit amount, `npc_value` = NPC-side hit
        // amount used for aggro calc, `temp` = temporary faction flag.
        public const string GetNpcFactionEntries = @"
            SELECT
                nfe.npc_faction_id  AS NpcFactionId,
                nfe.faction_id      AS FactionId,
                nfe.value           AS Value,
                nfe.npc_value       AS NpcValue,
                nfe.temp            AS Temp,
                fl.name             AS FactionName
            FROM npc_faction_entries nfe
            LEFT JOIN faction_list fl ON fl.id = nfe.faction_id
            WHERE nfe.npc_faction_id = @NpcFactionId
            ORDER BY fl.name, nfe.faction_id";

        // Commit-path DML for per-entry edits. Composite PK on
        // (npc_faction_id, faction_id) — the (id, faction) pair must be unique.
        public const string InsertNpcFactionEntry = @"
            INSERT INTO npc_faction_entries
                (npc_faction_id, faction_id, value, npc_value, temp)
            VALUES
                (@NpcFactionId, @FactionId, @Value, @NpcValue, @Temp)";

        public const string UpdateNpcFactionEntry = @"
            UPDATE npc_faction_entries
            SET value = @Value, npc_value = @NpcValue, temp = @Temp
            WHERE npc_faction_id = @NpcFactionId AND faction_id = @FactionId";

        public const string DeleteNpcFactionEntry = @"
            DELETE FROM npc_faction_entries
            WHERE npc_faction_id = @NpcFactionId AND faction_id = @FactionId";

        // ── Slice 6 (loot editor) ─────────────────────────────────────────

        // One loottable row by id. Header block of the loot editor: cash range,
        // expansion / content flags, avg-coin. Fetched independently of the
        // entries so a broken loottable_entries row can't hide the header.
        public const string GetLootTableById = @"
            SELECT
                id                      AS Id,
                name                    AS Name,
                mincash                 AS MinCash,
                maxcash                 AS MaxCash,
                avgcoin                 AS AvgCoin,
                min_expansion           AS MinExpansion,
                max_expansion           AS MaxExpansion,
                content_flags           AS ContentFlags,
                content_flags_disabled  AS ContentFlagsDisabled
            FROM loottable
            WHERE id = @Id";

        // Every lootdrop attached to a loottable. LEFT JOINs lootdrop.name so
        // the widget can render "guard_common (#45)" without a per-row lookup.
        // Ordered by lootdrop name for scan-friendliness.
        public const string GetLootTableEntries = @"
            SELECT
                lte.loottable_id  AS LoottableId,
                lte.lootdrop_id   AS LootdropId,
                lte.multiplier    AS Multiplier,
                lte.droplimit     AS DropLimit,
                lte.mindrop       AS MinDrop,
                lte.probability   AS Probability,
                ld.name           AS LootdropName
            FROM loottable_entries lte
            LEFT JOIN lootdrop ld ON ld.id = lte.lootdrop_id
            WHERE lte.loottable_id = @LoottableId
            ORDER BY ld.name, lte.lootdrop_id";

        // Every item entry under one lootdrop. LEFT JOINs items.Name (note the
        // capital N — the items table is a legacy schema). Ordered by item name
        // so the drop list reads alphabetically. Fetched per-lootdrop; a batch
        // form (IN @Ids) is exposed alongside so the widget can pull all its
        // lootdrops' items in one round-trip.
        public const string GetLootDropEntries = @"
            SELECT
                lde.lootdrop_id       AS LootdropId,
                lde.item_id           AS ItemId,
                lde.item_charges      AS ItemCharges,
                lde.equip_item        AS EquipItem,
                lde.chance            AS Chance,
                lde.disabled_chance   AS DisabledChance,
                lde.trivial_min_level AS TrivialMinLevel,
                lde.trivial_max_level AS TrivialMaxLevel,
                lde.multiplier        AS Multiplier,
                lde.npc_min_level     AS NpcMinLevel,
                lde.npc_max_level     AS NpcMaxLevel,
                i.Name                AS ItemName
            FROM lootdrop_entries lde
            LEFT JOIN items i ON i.id = lde.item_id
            WHERE lde.lootdrop_id = @LootdropId
            ORDER BY i.Name, lde.item_id";

        // Batched form of GetLootDropEntries — one query for many lootdrops so
        // the widget avoids N+1 when a loottable has many entries. Dapper
        // expands @Ids automatically for IEnumerable<int>.
        public const string GetLootDropEntriesBatch = @"
            SELECT
                lde.lootdrop_id       AS LootdropId,
                lde.item_id           AS ItemId,
                lde.item_charges      AS ItemCharges,
                lde.equip_item        AS EquipItem,
                lde.chance            AS Chance,
                lde.disabled_chance   AS DisabledChance,
                lde.trivial_min_level AS TrivialMinLevel,
                lde.trivial_max_level AS TrivialMaxLevel,
                lde.multiplier        AS Multiplier,
                lde.npc_min_level     AS NpcMinLevel,
                lde.npc_max_level     AS NpcMaxLevel,
                i.Name                AS ItemName
            FROM lootdrop_entries lde
            LEFT JOIN items i ON i.id = lde.item_id
            WHERE lde.lootdrop_id IN @Ids
            ORDER BY lde.lootdrop_id, i.Name, lde.item_id";

        // Count of NPCs pointing at a loottable — feeds the "Uses: N NPCs"
        // header hint. Slice 6c will use this for the copy-on-edit prompt.
        public const string GetLootTableUsageCount = @"
            SELECT COUNT(*) FROM npc_types WHERE loottable_id = @LoottableId";

        // Slice 6c — per-lootdrop usage count (how many loottables reference
        // each lootdrop). Batched via IN so one query covers every lootdrop in
        // the current view. The sidebar surfaces N > 1 as "shared → clone
        // before editing" so a well-meaning edit doesn't quietly change loot
        // for every NPC that inherits from the same lootdrop.
        public const string GetLootDropUsageCountBatch = @"
            SELECT lootdrop_id AS LootdropId, COUNT(*) AS Count
            FROM loottable_entries
            WHERE lootdrop_id IN @Ids
            GROUP BY lootdrop_id";

        // Slice 6c — clone / create writes. All immediate (own transaction on
        // the connection); they don't route through the pending buffer because
        // AUTO_INCREMENT id remapping across buffer ops would be intrusive to
        // add for a rarely-undone action. Documented in the sidebar with a
        // confirm modal before firing.

        // Copies a loottable row (with " (clone)" name suffix) so the clone is
        // instantly identifiable in searches. Selects every editable column so
        // future schema additions don't silently drop.
        public const string CloneLootTableRow = @"
            INSERT INTO loottable
                (name, mincash, maxcash, avgcoin, done, min_expansion,
                 max_expansion, content_flags, content_flags_disabled)
            SELECT
                CONCAT(name, ' (clone)'), mincash, maxcash, avgcoin, done,
                min_expansion, max_expansion, content_flags, content_flags_disabled
            FROM loottable
            WHERE id = @SourceId";

        // Bulk-copy every loottable_entries row from source to new loottable.
        // Preserves multiplier / droplimit / mindrop / probability.
        public const string CloneLootTableEntries = @"
            INSERT INTO loottable_entries
                (loottable_id, lootdrop_id, multiplier, droplimit, mindrop, probability)
            SELECT
                @NewId, lootdrop_id, multiplier, droplimit, mindrop, probability
            FROM loottable_entries
            WHERE loottable_id = @SourceId";

        public const string CloneLootDropRow = @"
            INSERT INTO lootdrop
                (name, min_expansion, max_expansion, content_flags, content_flags_disabled)
            SELECT
                CONCAT(name, ' (clone)'), min_expansion, max_expansion,
                content_flags, content_flags_disabled
            FROM lootdrop
            WHERE id = @SourceId";

        public const string CloneLootDropEntries = @"
            INSERT INTO lootdrop_entries
                (lootdrop_id, item_id, item_charges, equip_item, chance,
                 disabled_chance, trivial_min_level, trivial_max_level,
                 multiplier, npc_min_level, npc_max_level,
                 min_expansion, max_expansion, content_flags, content_flags_disabled)
            SELECT
                @NewId, item_id, item_charges, equip_item, chance,
                disabled_chance, trivial_min_level, trivial_max_level,
                multiplier, npc_min_level, npc_max_level,
                min_expansion, max_expansion, content_flags, content_flags_disabled
            FROM lootdrop_entries
            WHERE lootdrop_id = @SourceId";

        // Swaps a loottable_entries row's lootdrop_id (used post-clone: the
        // owning loottable now points at the fresh copy instead of the shared
        // original). Composite PK preserved by the WHERE clause.
        public const string RepointLootTableEntryLootdrop = @"
            UPDATE loottable_entries
            SET lootdrop_id = @NewLootdropId
            WHERE loottable_id = @LoottableId AND lootdrop_id = @OldLootdropId";

        // Fresh loottable — user supplies name + cash range in the create
        // modal (Slice 6c follow-up). The row is committed immediately; the
        // sidebar then routes the NPC's npc_types.loottable_id through the
        // normal buffered edit path so discarding the session doesn't leave
        // a rogue assignment.
        public const string CreateEmptyLootTable = @"
            INSERT INTO loottable (name, mincash, maxcash, avgcoin)
            VALUES (@Name, @MinCash, @MaxCash, @AvgCoin)";

        // Fresh empty lootdrop — used by the ""+ New empty lootdrop"" path in
        // the add-lootdrop search picker. Caller wires the returned id into a
        // pending LootTableEntryEditAction so the link between the owning
        // loottable and the new lootdrop lands with the normal commit.
        public const string CreateEmptyLootDrop = @"
            INSERT INTO lootdrop (name)
            VALUES (@Name)";

        // Slice 6b — SEARCH pickers. lootdrop (24k rows) and items (80k rows) are
        // both too large for the preload path (ReferenceDataCache). Callers pre-
        // wrap @Filter with '%' wildcards (empty filter is fine — LIMIT caps
        // the load).
        public const string SearchLootdrops = @"
            SELECT id AS Id, name AS Name
            FROM lootdrop
            WHERE name LIKE @Filter
            ORDER BY name
            LIMIT @Limit";

        public const string SearchItems = @"
            SELECT id AS Id, Name AS Name
            FROM items
            WHERE Name LIKE @Filter
            ORDER BY Name
            LIMIT @Limit";

        // Slice 6b — CRUD DML for the two per-entry tables. Composite PKs on both
        // (loottable_id, lootdrop_id) / (lootdrop_id, item_id) so the WHERE clause
        // on UPDATE / DELETE always keys on both columns.
        public const string InsertLootTableEntry = @"
            INSERT INTO loottable_entries
                (loottable_id, lootdrop_id, multiplier, droplimit, mindrop, probability)
            VALUES
                (@LoottableId, @LootdropId, @Multiplier, @DropLimit, @MinDrop, @Probability)";

        public const string UpdateLootTableEntry = @"
            UPDATE loottable_entries
            SET multiplier = @Multiplier, droplimit = @DropLimit,
                mindrop = @MinDrop, probability = @Probability
            WHERE loottable_id = @LoottableId AND lootdrop_id = @LootdropId";

        public const string DeleteLootTableEntry = @"
            DELETE FROM loottable_entries
            WHERE loottable_id = @LoottableId AND lootdrop_id = @LootdropId";

        public const string InsertLootDropEntry = @"
            INSERT INTO lootdrop_entries
                (lootdrop_id, item_id, item_charges, equip_item, chance,
                 disabled_chance, trivial_min_level, trivial_max_level,
                 multiplier, npc_min_level, npc_max_level)
            VALUES
                (@LootdropId, @ItemId, @ItemCharges, @EquipItem, @Chance,
                 @DisabledChance, @TrivialMinLevel, @TrivialMaxLevel,
                 @Multiplier, @NpcMinLevel, @NpcMaxLevel)";

        public const string UpdateLootDropEntry = @"
            UPDATE lootdrop_entries
            SET item_charges = @ItemCharges, equip_item = @EquipItem,
                chance = @Chance, disabled_chance = @DisabledChance,
                trivial_min_level = @TrivialMinLevel, trivial_max_level = @TrivialMaxLevel,
                multiplier = @Multiplier,
                npc_min_level = @NpcMinLevel, npc_max_level = @NpcMaxLevel
            WHERE lootdrop_id = @LootdropId AND item_id = @ItemId";

        public const string DeleteLootDropEntry = @"
            DELETE FROM lootdrop_entries
            WHERE lootdrop_id = @LootdropId AND item_id = @ItemId";

        // NPC picker search — substring match on npc_types.name. Caller passes @Filter
        // pre-wrapped with '%' wildcards (empty @Filter still returns rows, LIMIT
        // caps the load). Ordered by name so the UI list stays stable across
        // keystrokes. Same column set as GetNpcTypesBatch so the returned rows
        // plug straight into the model-resolution pipeline.
        public const string SearchNpcTypes = @"
            SELECT id, name, lastname AS LastName, level, race,
                   `class` AS Class, bodytype AS BodyType, size, gender,
                   texture, helmtexture AS HelmTexture, face
            FROM npc_types
            WHERE name LIKE @Filter
            ORDER BY name
            LIMIT @Limit";

        // Batch-load grid_entries for multiple grids in one zone.
        public const string GetGridEntriesBatch = @"
            SELECT gridid AS GridId, number, x, y, z, heading, pause
            FROM grid_entries
            WHERE gridid IN @GridIds AND zoneid = @ZoneId
            ORDER BY gridid, number";

        // Full sweep of every grid in a zone — used by the Grid List sidebar section so
        // orphan grids (no spawn2 references them; quest scripts spawn NPCs onto them at
        // runtime) become visible/editable. GetGridsBatch above is spawn-driven and would
        // miss these entirely.
        public const string GetAllZoneGrids = @"
            SELECT id AS Id, zoneid AS ZoneId, type AS Type, type2 AS Type2
            FROM grid
            WHERE zoneid = @ZoneId
            ORDER BY id";

        public const string GetAllZoneGridEntries = @"
            SELECT gridid AS GridId, number, x, y, z, heading, pause, centerpoint
            FROM grid_entries
            WHERE zoneid = @ZoneId
            ORDER BY gridid, number";

        // grid.id isn't AUTO_INCREMENT — it's a user-assigned composite PK with zoneid.
        // On commit we compute the next id inside the same transaction: FOR UPDATE locks
        // the peer rows for this zone so two concurrent commits get sequential ids
        // instead of colliding on the PK.
        public const string NextGridIdForZone = @"
            SELECT COALESCE(MAX(id), 0) + 1
            FROM grid
            WHERE zoneid = @ZoneId
            FOR UPDATE";

        public const string InsertGrid = @"
            INSERT INTO grid (id, zoneid, type, type2)
            VALUES (@Id, @ZoneId, @Type, @Type2)";

        // Trilogy client's server-side zone-crossing triggers. Columns aliased so Dapper
        // maps deterministically regardless of MySQL's platform-dependent case handling.
        public const string GetTrilogyZonePoints = @"
            SELECT
                id,
                zone,
                x, y, z, heading,
                target_zone     AS TargetZone,
                target_x        AS TargetX,
                target_y        AS TargetY,
                target_z        AS TargetZ,
                Zrange,
                maxZDiff        AS MaxZDiff,
                UseNewZoning,
                MinVert, MaxVert, CenterPoint,
                keepX           AS KeepX,
                keepY           AS KeepY,
                keepZ           AS KeepZ,
                ToZoneID        AS ToZoneId
            FROM trilogy_zone_points
            WHERE zone = @ZoneName";

        public const string UpdateTrilogyZonePoint = @"
            UPDATE trilogy_zone_points
            SET x = @X, y = @Y, z = @Z, heading = @Heading,
                target_zone = @TargetZone,
                target_x = @TargetX, target_y = @TargetY, target_z = @TargetZ,
                Zrange = @Zrange, maxZDiff = @MaxZDiff, UseNewZoning = @UseNewZoning,
                MinVert = @MinVert, MaxVert = @MaxVert, CenterPoint = @CenterPoint,
                keepX = @KeepX, keepY = @KeepY, keepZ = @KeepZ,
                ToZoneID = @ToZoneId
            WHERE id = @Id";

        // INSERT returns the AUTO_INCREMENT id via LAST_INSERT_ID() (session-scoped, safe
        // inside a transaction). Committer runs this then remaps the in-memory temp id
        // to the returned real id.
        public const string InsertTrilogyZonePoint = @"
            INSERT INTO trilogy_zone_points
                (zone, x, y, z, heading,
                 target_zone, target_x, target_y, target_z,
                 Zrange, maxZDiff, UseNewZoning,
                 MinVert, MaxVert, CenterPoint,
                 keepX, keepY, keepZ, ToZoneID)
            VALUES
                (@Zone, @X, @Y, @Z, @Heading,
                 @TargetZone, @TargetX, @TargetY, @TargetZ,
                 @Zrange, @MaxZDiff, @UseNewZoning,
                 @MinVert, @MaxVert, @CenterPoint,
                 @KeepX, @KeepY, @KeepZ, @ToZoneId);
            SELECT LAST_INSERT_ID();";

        public const string DeleteTrilogyZonePoint = @"
            DELETE FROM trilogy_zone_points WHERE id = @Id";

        // Populates the target-zone dropdown in the inspector. Sorted alphabetically so
        // the Combo is easy to scan; ORDER BY short_name is the natural human order for
        // dozens of Trilogy-era zones.
        public const string GetAllZoneShortNames = @"
            SELECT short_name
            FROM zone
            ORDER BY short_name";

        // Rows in other zones that land inside the currently-viewed zone — used to render
        // "incoming" arrows at the target_x/y/z coord with a heading indicator. The row's
        // `zone` field stays as the SOURCE (foreign) zone; edits still UPDATE by id so
        // the cross-zone commit path is identical to the normal case.
        public const string GetIncomingZonePoints = @"
            SELECT
                id,
                zone,
                x, y, z, heading,
                target_zone     AS TargetZone,
                target_x        AS TargetX,
                target_y        AS TargetY,
                target_z        AS TargetZ,
                Zrange,
                maxZDiff        AS MaxZDiff,
                UseNewZoning,
                MinVert, MaxVert, CenterPoint,
                keepX           AS KeepX,
                keepY           AS KeepY,
                keepZ           AS KeepZ,
                ToZoneID        AS ToZoneId
            FROM trilogy_zone_points
            WHERE target_zone = @ZoneName AND zone <> @ZoneName";

        // Peer-zone rows for the sandwich detector: for each destination zone reached from
        // the current zone's owned rows, load every row IN that destination zone so we
        // can check whether an outgoing landing coord falls inside one of that zone's
        // fire regions. Kept as a single IN-list query so N destinations = 1 round trip.
        public const string GetZonePointsForZones = @"
            SELECT
                id,
                zone,
                x, y, z, heading,
                target_zone     AS TargetZone,
                target_x        AS TargetX,
                target_y        AS TargetY,
                target_z        AS TargetZ,
                Zrange,
                maxZDiff        AS MaxZDiff,
                UseNewZoning,
                MinVert, MaxVert, CenterPoint,
                keepX           AS KeepX,
                keepY           AS KeepY,
                keepZ           AS KeepZ,
                ToZoneID        AS ToZoneId
            FROM trilogy_zone_points
            WHERE zone IN @ZoneNames";
    }
}