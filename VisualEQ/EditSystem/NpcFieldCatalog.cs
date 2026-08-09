using System;
using System.Collections.Generic;
using System.Globalization;

namespace VisualEQ.EditSystem
{
    // Central registry of every editable npc_types column. Drives:
    //   1. NpcEdit's string-typed sparse storage (parse on commit per Kind)
    //   2. NpcFieldEditAction's type-preserving Convert.ToXxx calls
    //   3. EditCommitter's dynamic UPDATE SQL builder
    //
    // NOT included:
    //   - cosmetic Luclin/Drakkin/armor-tint fields — RaceModelMapper doesn't consult
    //     them on the Trilogy client this editor targets; can be turned on later
    //     when a fork wires them into resolution
    //   - npcspecialattks — legacy per-letter flag field superseded by
    //     special_abilities (see database_update_manifest migration that
    //     translates letters like S/E/R into 1,1^2,1^3,1^). Read-only in the UI.
    //   - version / peqid — provenance metadata, not intended to be user-editable
    //
    // Slice 3 additions: texture / helmtexture / face — live preview via
    // Controller.RefreshNpcVisualForNpc.
    // Slice 4 additions: special_abilities — friendly checkbox editor over
    // SpecialAbilityCatalog serializes to the id,value,params^ wire format.
    //
    // Column names are the exact npc_types SQL identifiers (lowercase, some with underscores
    // or leading _), so EditCommitter can splice them straight into SET clauses.
    public static class NpcFieldCatalog
    {
        public enum FieldKind
        {
            Int,           // 32-bit int, non-null
            Long,          // 64-bit bigint, non-null
            Float,         // float, non-null
            String,        // varchar/tinytext/text, non-null (empty string allowed)
            NullableInt,   // int, may be NULL
            NullableFloat, // float, may be NULL
        }

        public sealed class FieldDef
        {
            public string SqlColumn { get; }
            public FieldKind Kind { get; }
            public bool NeedsBackticks { get; }

            public FieldDef(string sqlColumn, FieldKind kind, bool needsBackticks = false)
            {
                SqlColumn = sqlColumn;
                Kind = kind;
                NeedsBackticks = needsBackticks;
            }

            public string ForSet => NeedsBackticks ? $"`{SqlColumn}`" : SqlColumn;
        }

        // Field name → definition. The field name is the identifier the UI and NpcEdit
        // dictionaries use; it matches SqlColumn 1:1 to keep debugging simple.
        public static readonly Dictionary<string, FieldDef> Fields = new Dictionary<string, FieldDef>(StringComparer.OrdinalIgnoreCase)
        {
            // Identity
            { "name",                   new FieldDef("name",                   FieldKind.String) },
            { "lastname",               new FieldDef("lastname",               FieldKind.String) },
            { "level",                  new FieldDef("level",                  FieldKind.Int) },
            { "race",                   new FieldDef("race",                   FieldKind.Int) },
            { "class",                  new FieldDef("class",                  FieldKind.Int, needsBackticks: true) },
            { "bodytype",               new FieldDef("bodytype",               FieldKind.Int) },
            { "gender",                 new FieldDef("gender",                 FieldKind.Int) },
            { "size",                   new FieldDef("size",                   FieldKind.Float) },

            // Visual (Slice 3 — live preview via Controller.RefreshNpcVisualForNpc)
            { "texture",                new FieldDef("texture",                FieldKind.Int) },
            { "helmtexture",            new FieldDef("helmtexture",            FieldKind.Int) },
            { "face",                   new FieldDef("face",                   FieldKind.Int) },

            // Combat vitals
            { "hp",                     new FieldDef("hp",                     FieldKind.Long) },
            { "mana",                   new FieldDef("mana",                   FieldKind.Long) },
            { "AC",                     new FieldDef("AC",                     FieldKind.Int) },
            { "mindmg",                 new FieldDef("mindmg",                 FieldKind.Int) },
            { "maxdmg",                 new FieldDef("maxdmg",                 FieldKind.Int) },
            { "ATK",                    new FieldDef("ATK",                    FieldKind.Int) },
            { "Accuracy",               new FieldDef("Accuracy",               FieldKind.Int) },
            { "Avoidance",              new FieldDef("Avoidance",              FieldKind.Int) },
            { "slow_mitigation",        new FieldDef("slow_mitigation",        FieldKind.Int) },
            { "attack_speed",           new FieldDef("attack_speed",           FieldKind.Float) },
            { "attack_delay",           new FieldDef("attack_delay",           FieldKind.Int) },
            { "attack_count",           new FieldDef("attack_count",           FieldKind.Int) },
            { "heroic_strikethrough",   new FieldDef("heroic_strikethrough",   FieldKind.Int) },

            // Regen
            { "hp_regen_rate",          new FieldDef("hp_regen_rate",          FieldKind.Long) },
            { "hp_regen_per_second",    new FieldDef("hp_regen_per_second",    FieldKind.Long) },
            { "mana_regen_rate",        new FieldDef("mana_regen_rate",        FieldKind.Long) },

            // Stats — DB columns are all-caps (or leading underscore for INT). Preserve.
            { "STR",                    new FieldDef("STR",                    FieldKind.Int) },
            { "STA",                    new FieldDef("STA",                    FieldKind.Int) },
            { "DEX",                    new FieldDef("DEX",                    FieldKind.Int) },
            { "AGI",                    new FieldDef("AGI",                    FieldKind.Int) },
            { "_INT",                   new FieldDef("_INT",                   FieldKind.Int) },
            { "WIS",                    new FieldDef("WIS",                    FieldKind.Int) },
            { "CHA",                    new FieldDef("CHA",                    FieldKind.Int) },

            // Resistances
            { "MR",                     new FieldDef("MR",                     FieldKind.Int) },
            { "CR",                     new FieldDef("CR",                     FieldKind.Int) },
            { "DR",                     new FieldDef("DR",                     FieldKind.Int) },
            { "FR",                     new FieldDef("FR",                     FieldKind.Int) },
            { "PR",                     new FieldDef("PR",                     FieldKind.Int) },
            { "Corrup",                 new FieldDef("Corrup",                 FieldKind.Int) },
            { "PhR",                    new FieldDef("PhR",                    FieldKind.Int) },

            // AI / behavior
            { "aggroradius",            new FieldDef("aggroradius",            FieldKind.Int) },
            { "assistradius",           new FieldDef("assistradius",           FieldKind.Int) },
            { "runspeed",               new FieldDef("runspeed",               FieldKind.Float) },
            { "walkspeed",              new FieldDef("walkspeed",              FieldKind.Int) },
            { "see_invis",              new FieldDef("see_invis",              FieldKind.Int) },
            { "see_invis_undead",       new FieldDef("see_invis_undead",       FieldKind.Int) },
            { "see_hide",               new FieldDef("see_hide",               FieldKind.Int) },
            { "see_improved_hide",      new FieldDef("see_improved_hide",      FieldKind.Int) },
            { "npc_aggro",              new FieldDef("npc_aggro",              FieldKind.Int) },
            { "always_aggro",           new FieldDef("always_aggro",           FieldKind.Int) },
            { "findable",               new FieldDef("findable",               FieldKind.Int) },
            { "trackable",              new FieldDef("trackable",              FieldKind.Int) },
            { "raid_target",            new FieldDef("raid_target",            FieldKind.Int) },
            { "no_target_hotkey",       new FieldDef("no_target_hotkey",       FieldKind.Int) },
            { "untargetable",           new FieldDef("untargetable",           FieldKind.Int) },
            { "show_name",              new FieldDef("show_name",              FieldKind.Int) },
            { "private_corpse",         new FieldDef("private_corpse",         FieldKind.Int) },
            { "unique_spawn_by_name",   new FieldDef("unique_spawn_by_name",   FieldKind.Int) },
            { "unique_",                new FieldDef("unique_",                FieldKind.Int, needsBackticks: true) },
            { "fixed",                  new FieldDef("fixed",                  FieldKind.Int, needsBackticks: true) },
            { "ignore_despawn",         new FieldDef("ignore_despawn",         FieldKind.Int) },
            { "stuck_behavior",         new FieldDef("stuck_behavior",         FieldKind.Int) },
            { "flymode",                new FieldDef("flymode",                FieldKind.Int) },
            { "rare_spawn",             new FieldDef("rare_spawn",             FieldKind.NullableInt) },
            { "exclude",                new FieldDef("exclude",                FieldKind.Int) },
            { "isbot",                  new FieldDef("isbot",                  FieldKind.Int) },
            { "isquest",                new FieldDef("isquest",                FieldKind.Int) },
            { "qglobal",                new FieldDef("qglobal",                FieldKind.Int) },
            { "emoteid",                new FieldDef("emoteid",                FieldKind.Int) },
            { "underwater",             new FieldDef("underwater",             FieldKind.Int) },
            { "spawn_limit",            new FieldDef("spawn_limit",            FieldKind.Int) },

            // References — raw int for the FK id. Typeahead pickers translate name → id
            // in the UI layer, but storage is the id itself.
            { "loottable_id",           new FieldDef("loottable_id",           FieldKind.Int) },
            { "merchant_id",            new FieldDef("merchant_id",            FieldKind.Int) },
            { "greed",                  new FieldDef("greed",                  FieldKind.Int) },
            { "alt_currency_id",        new FieldDef("alt_currency_id",        FieldKind.Int) },
            { "npc_spells_id",          new FieldDef("npc_spells_id",          FieldKind.Int) },
            { "npc_spells_effects_id",  new FieldDef("npc_spells_effects_id",  FieldKind.Int) },
            { "npc_faction_id",         new FieldDef("npc_faction_id",         FieldKind.Int) },
            { "adventure_template_id",  new FieldDef("adventure_template_id",  FieldKind.Int) },
            { "trap_template",          new FieldDef("trap_template",          FieldKind.NullableInt) },
            { "faction_amount",         new FieldDef("faction_amount",         FieldKind.Int) },
            { "keeps_sold_items",       new FieldDef("keeps_sold_items",       FieldKind.Int) },
            { "is_parcel_merchant",     new FieldDef("is_parcel_merchant",     FieldKind.Int) },
            { "multiquest_enabled",     new FieldDef("multiquest_enabled",     FieldKind.Int) },
            { "skip_global_loot",       new FieldDef("skip_global_loot",       FieldKind.NullableInt) },

            // Scaling
            { "scalerate",              new FieldDef("scalerate",              FieldKind.Int) },
            { "spellscale",             new FieldDef("spellscale",             FieldKind.Float) },
            { "healscale",              new FieldDef("healscale",              FieldKind.Float) },
            { "exp_mod",                new FieldDef("exp_mod",                FieldKind.Int) },
            { "maxlevel",               new FieldDef("maxlevel",               FieldKind.Int) },

            // Charm overrides (all nullable per schema)
            { "charm_ac",               new FieldDef("charm_ac",               FieldKind.NullableInt) },
            { "charm_min_dmg",          new FieldDef("charm_min_dmg",          FieldKind.NullableInt) },
            { "charm_max_dmg",          new FieldDef("charm_max_dmg",          FieldKind.NullableInt) },
            { "charm_attack_delay",     new FieldDef("charm_attack_delay",     FieldKind.NullableInt) },
            { "charm_accuracy_rating",  new FieldDef("charm_accuracy_rating",  FieldKind.NullableInt) },
            { "charm_avoidance_rating", new FieldDef("charm_avoidance_rating", FieldKind.NullableInt) },
            { "charm_atk",              new FieldDef("charm_atk",              FieldKind.NullableInt) },

            // Special abilities (Slice 4). Stored as a delimited string; edited
            // via the friendly checkbox editor over SpecialAbilityCatalog. Raw
            // string still goes through the same UPDATE npc_types path.
            { "special_abilities",      new FieldDef("special_abilities",      FieldKind.String) },
        };

        public static FieldDef Get(string fieldName) =>
            Fields.TryGetValue(fieldName, out var def) ? def : null;

        // Stringify a value for storage in NpcEdit.OriginalValues / CurrentValues. Uses
        // InvariantCulture so a French user's decimal separator doesn't break Parse on
        // an English user's next load.
        public static string StringifyValue(object value)
        {
            if (value == null) return null;
            switch (value)
            {
                case string s: return s;
                case bool b:   return b ? "1" : "0";
                case float f:  return f.ToString("R", CultureInfo.InvariantCulture);
                case double d: return d.ToString("R", CultureInfo.InvariantCulture);
                case int i:    return i.ToString(CultureInfo.InvariantCulture);
                case long l:   return l.ToString(CultureInfo.InvariantCulture);
                case byte by:  return by.ToString(CultureInfo.InvariantCulture);
                case short sh: return sh.ToString(CultureInfo.InvariantCulture);
                default:       return Convert.ToString(value, CultureInfo.InvariantCulture);
            }
        }

        // Parse a stored string back to the target column's runtime type. Returns the boxed
        // primitive so Dapper's parameter binder can hand it to MySqlConnector as-is.
        // A null string round-trips to null for nullable columns; for non-null columns
        // it means "no edit was recorded", which callers should filter out before calling.
        public static object ParseValue(string s, FieldKind kind)
        {
            if (s == null)
            {
                if (kind == FieldKind.NullableInt || kind == FieldKind.NullableFloat)
                    return null;
                throw new InvalidOperationException($"Null value for non-nullable field kind {kind}");
            }
            switch (kind)
            {
                case FieldKind.Int:           return int.Parse(s, CultureInfo.InvariantCulture);
                case FieldKind.Long:          return long.Parse(s, CultureInfo.InvariantCulture);
                case FieldKind.Float:         return float.Parse(s, CultureInfo.InvariantCulture);
                case FieldKind.String:        return s;
                case FieldKind.NullableInt:   return int.Parse(s, CultureInfo.InvariantCulture);
                case FieldKind.NullableFloat: return float.Parse(s, CultureInfo.InvariantCulture);
                default: throw new ArgumentOutOfRangeException(nameof(kind));
            }
        }

        // Equality helper for the drop-when-back-to-original logic. Compares two stored
        // string values under the field's parsing rules (so "3" == "3.0" for a float, and
        // whitespace differences in stringified floats don't linger as pending edits).
        public static bool ValuesEqual(string a, string b, FieldKind kind)
        {
            if (a == null && b == null) return true;
            if (a == null || b == null) return false;
            try
            {
                switch (kind)
                {
                    case FieldKind.Float:
                    case FieldKind.NullableFloat:
                        return Math.Abs(float.Parse(a, CultureInfo.InvariantCulture)
                                      - float.Parse(b, CultureInfo.InvariantCulture)) < 0.0001f;
                    case FieldKind.Int:
                    case FieldKind.NullableInt:
                        return int.Parse(a, CultureInfo.InvariantCulture)
                             == int.Parse(b, CultureInfo.InvariantCulture);
                    case FieldKind.Long:
                        return long.Parse(a, CultureInfo.InvariantCulture)
                             == long.Parse(b, CultureInfo.InvariantCulture);
                    default:
                        return string.Equals(a, b, StringComparison.Ordinal);
                }
            }
            catch
            {
                return string.Equals(a, b, StringComparison.Ordinal);
            }
        }
    }
}
