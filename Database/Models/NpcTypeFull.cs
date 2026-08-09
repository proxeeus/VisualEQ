namespace VisualEQ.Database.Models
{
    // Full-fidelity mirror of the npc_types row. Distinct from NpcType (the light-weight
    // model used by the spawn-loading batch path) — this one carries every column the
    // NPC editor reads and (in later slices) writes back. Property names use PascalCase
    // and match the SqlQueries.GetNpcTypeById alias list column-for-column.
    //
    // Nullable ints are used only for columns marked NULL in the schema; the rest default
    // to 0 / null strings for absent rows.
    public class NpcTypeFull
    {
        // Identity
        public int Id { get; set; }
        public string Name { get; set; }
        public string LastName { get; set; }
        public int Level { get; set; }
        public int Race { get; set; }
        public int Class { get; set; }
        public int BodyType { get; set; }
        public int Gender { get; set; }
        public float Size { get; set; }

        // Combat vitals
        public long Hp { get; set; }
        public long Mana { get; set; }
        public int Ac { get; set; }
        public int MinDmg { get; set; }
        public int MaxDmg { get; set; }
        public int Atk { get; set; }
        public int Accuracy { get; set; }
        public int Avoidance { get; set; }
        public int SlowMitigation { get; set; }
        public float AttackSpeed { get; set; }
        public int AttackDelay { get; set; }
        public int AttackCount { get; set; }
        public int HeroicStrikethrough { get; set; }

        // Regen
        public long HpRegenRate { get; set; }
        public long HpRegenPerSecond { get; set; }
        public long ManaRegenRate { get; set; }

        // Stats
        public int Str { get; set; }
        public int Sta { get; set; }
        public int Dex { get; set; }
        public int Agi { get; set; }
        public int Int_ { get; set; }
        public int Wis { get; set; }
        public int Cha { get; set; }

        // Resistances
        public int MR { get; set; }
        public int CR { get; set; }
        public int DR { get; set; }
        public int FR { get; set; }
        public int PR { get; set; }
        public int Corrup { get; set; }
        public int PhR { get; set; }

        // Visual — textures / model
        public int Texture { get; set; }
        public int HelmTexture { get; set; }
        public int Face { get; set; }
        public int HerosForgeModel { get; set; }
        public int ArmTexture { get; set; }
        public int BracerTexture { get; set; }
        public int HandTexture { get; set; }
        public int LegTexture { get; set; }
        public int FeetTexture { get; set; }
        public int Light { get; set; }
        public int Model { get; set; }
        public int DMeleeTexture1 { get; set; }
        public int DMeleeTexture2 { get; set; }
        public string AmmoIdfile { get; set; }
        public int PrimMeleeType { get; set; }
        public int SecMeleeType { get; set; }
        public int RangedType { get; set; }

        // Visual — Luclin / Drakkin cosmetics (unused by Trilogy but preserved so writes
        // round-trip cleanly against non-classic schemas)
        public int LuclinHairstyle { get; set; }
        public int LuclinHaircolor { get; set; }
        public int LuclinEyecolor { get; set; }
        public int LuclinEyecolor2 { get; set; }
        public int LuclinBeardcolor { get; set; }
        public int LuclinBeard { get; set; }
        public int DrakkinHeritage { get; set; }
        public int DrakkinTattoo { get; set; }
        public int DrakkinDetails { get; set; }

        // Visual — armor tint
        public int ArmortintId { get; set; }
        public int ArmortintRed { get; set; }
        public int ArmortintGreen { get; set; }
        public int ArmortintBlue { get; set; }

        // AI / behavior
        public int AggroRadius { get; set; }
        public int AssistRadius { get; set; }
        public float Runspeed { get; set; }
        public int Walkspeed { get; set; }
        public int SeeInvis { get; set; }
        public int SeeInvisUndead { get; set; }
        public int SeeHide { get; set; }
        public int SeeImprovedHide { get; set; }
        public int NpcAggro { get; set; }
        public int AlwaysAggro { get; set; }
        public int Findable { get; set; }
        public int Trackable { get; set; }
        public int RaidTarget { get; set; }
        public int NoTargetHotkey { get; set; }
        public int Untargetable { get; set; }
        public int ShowName { get; set; }
        public int PrivateCorpse { get; set; }
        public int UniqueSpawnByName { get; set; }
        public int Unique { get; set; }
        public int Fixed { get; set; }
        public int IgnoreDespawn { get; set; }
        public int StuckBehavior { get; set; }
        public int Flymode { get; set; }
        public int? RareSpawn { get; set; }
        public int Exclude { get; set; }
        public int IsBot { get; set; }
        public int IsQuest { get; set; }
        public int Qglobal { get; set; }
        public int EmoteId { get; set; }
        public int Underwater { get; set; }
        public int SpawnLimit { get; set; }

        // References (foreign-key style ints — resolved to names in later slices)
        public int LoottableId { get; set; }
        public int MerchantId { get; set; }
        public int Greed { get; set; }
        public int AltCurrencyId { get; set; }
        public int NpcSpellsId { get; set; }
        public int NpcSpellsEffectsId { get; set; }
        public int NpcFactionId { get; set; }
        public int AdventureTemplateId { get; set; }
        public int? TrapTemplate { get; set; }
        public int FactionAmount { get; set; }
        public int KeepsSoldItems { get; set; }
        public int IsParcelMerchant { get; set; }
        public int MultiquestEnabled { get; set; }
        public int? SkipGlobalLoot { get; set; }

        // Scaling
        public int Scalerate { get; set; }
        public float Spellscale { get; set; }
        public float Healscale { get; set; }
        public int ExpMod { get; set; }
        public int Maxlevel { get; set; }

        // Charm overrides (all nullable — the schema allows NULL)
        public int? CharmAc { get; set; }
        public int? CharmMinDmg { get; set; }
        public int? CharmMaxDmg { get; set; }
        public int? CharmAttackDelay { get; set; }
        public int? CharmAccuracyRating { get; set; }
        public int? CharmAvoidanceRating { get; set; }
        public int? CharmAtk { get; set; }

        // Special abilities — raw strings, Slice 4 gives them a friendly editor.
        public string NpcSpecialAttks { get; set; }
        public string SpecialAbilities { get; set; }

        // Version / provenance
        public int Version { get; set; }
        public int PeqId { get; set; }
    }
}
