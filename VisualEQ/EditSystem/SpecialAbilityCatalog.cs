using System.Collections.Generic;

namespace VisualEQ.EditSystem
{
    // Registry of every SpecialAbility the server recognizes, sourced from
    // /c/eqemu/source/Server/common/emu_constants.h (SpecialAbility namespace +
    // special_ability_names map). Kept in server-id order so the widget's row
    // layout matches the numeric ids in `special_abilities` strings and any
    // out-of-band tooling that references them by number.
    //
    // Params: each ability carries a `value` (position 1 in the id,value,p0,p1,...
    // format) plus up to SpecialAbility::MaxParameters (9) trailing int params.
    // Semantics of value/params vary per ability — the value is often 0/1 for
    // "enabled" but for chance-based ones (Flurry, Rampage) it's a % or count.
    // Rather than baking per-ability param names + docs (that vary across forks
    // and could go stale), the editor exposes them as "value" + "param 0..8" and
    // relies on the OP's fork familiarity. A future revision can layer named
    // params over the well-known abilities.
    public static class SpecialAbilityCatalog
    {
        public sealed class Entry
        {
            public int    Id { get; }
            public string Name { get; }
            // True for abilities where the "value" field is a pure on/off flag
            // (checkbox is sufficient — enabled ↔ value=1). False for abilities
            // where the value carries meaningful magnitude that the OP needs to
            // dial in (chance %, HP threshold, distance, etc.).
            //
            // Conservative: only the abilities where server code clearly reads
            // the value as magnitude are flagged non-boolean. If a fork uses
            // value for a currently-boolean ability, params 0..8 still ride
            // along in the wire format — a future revision can add a per-ability
            // "advanced" expander to expose value + params.
            public bool   IsBoolean { get; }
            public Entry(int id, string name, bool isBoolean = true)
            {
                Id = id; Name = name; IsBoolean = isBoolean;
            }
        }

        // Max ability id (SpecialAbility::Max in server = 58, one past the last).
        public const int MaxAbilityId = 57;

        // Max trailing params (SpecialAbility::MaxParameters). Position 1 is the
        // "value" bit, positions 2..MaxParameters+1 are params 0..MaxParameters-1.
        public const int MaxParams = 9;

        public static readonly IReadOnlyList<Entry> All = new List<Entry>
        {
            new Entry( 1, "Summon",           isBoolean: false), // value = HP threshold %
            new Entry( 2, "Enrage"),
            new Entry( 3, "Rampage",          isBoolean: false), // value = chance %
            new Entry( 4, "Area Rampage",     isBoolean: false), // value = chance %
            new Entry( 5, "Flurry",           isBoolean: false), // value = chance %
            new Entry( 6, "Triple Attack",    isBoolean: false), // value = chance %
            new Entry( 7, "Quadruple Attack", isBoolean: false), // value = chance %
            new Entry( 8, "Dual Wield",       isBoolean: false), // value = chance %
            new Entry( 9, "Bane Attack"),
            new Entry(10, "Magical Attack"),
            new Entry(11, "Ranged Attack"),
            new Entry(12, "Immune to Slow"),
            new Entry(13, "Immune to Mesmerize"),
            new Entry(14, "Immune to Charm"),
            new Entry(15, "Immune to Stun"),
            new Entry(16, "Immune to Snare"),
            new Entry(17, "Immune to Fear"),
            new Entry(18, "Immune to Dispell"),
            new Entry(19, "Immune to Melee"),
            new Entry(20, "Immune to Magic"),
            new Entry(21, "Immune to Fleeing"),
            new Entry(22, "Immune to Melee except Bane"),
            new Entry(23, "Immune to Non-Magical Melee"),
            new Entry(24, "Immune to Aggro"),
            new Entry(25, "Immune to Being Aggro"),
            new Entry(26, "Immune to Ranged Spells"),
            new Entry(27, "Immune to Feign Death"),
            new Entry(28, "Immune to Taunt"),
            new Entry(29, "Tunnel Vision"),
            new Entry(30, "Does Not Heal or Buff Allies"),
            new Entry(31, "Immune to Pacify"),
            new Entry(32, "Leashed"),
            new Entry(33, "Tethered"),
            new Entry(34, "Destructible Object"),
            new Entry(35, "Immune to Harm from Client"),
            new Entry(36, "Always Flees"),
            new Entry(37, "Flee Percentage",           isBoolean: false), // value = HP % to flee at
            new Entry(38, "Allows Beneficial Spells"),
            new Entry(39, "Melee is Disabled"),
            new Entry(40, "Chase Distance",            isBoolean: false), // params carry min/max
            new Entry(41, "Allowed to Tank"),
            new Entry(42, "Ignores Root Aggro"),
            new Entry(43, "Casting Resist Difficulty", isBoolean: false), // value = difficulty
            new Entry(44, "Counter Damage Avoidance",  isBoolean: false), // params 0-4 counters
            new Entry(45, "Proximity Aggro",           isBoolean: false), // value = distance
            new Entry(46, "Immune to Ranged Attacks"),
            new Entry(47, "Immune to Client Damage"),
            new Entry(48, "Immune to NPC Damage"),
            new Entry(49, "Immune to Client Aggro"),
            new Entry(50, "Immune to NPC Aggro"),
            new Entry(51, "Modify Damage Avoidance",   isBoolean: false), // params 0-4 modifiers
            new Entry(52, "Immune to Memory Fades"),
            new Entry(53, "Immune to Open"),
            new Entry(54, "Immune to Assassinate"),
            new Entry(55, "Immune to Headshot"),
            new Entry(56, "Immune to Bot Aggro"),
            new Entry(57, "Immune to Bot Damage"),
        };

        static readonly Dictionary<int, Entry> ById = BuildIndex();
        static Dictionary<int, Entry> BuildIndex()
        {
            var d = new Dictionary<int, Entry>(All.Count);
            foreach (var e in All) d[e.Id] = e;
            return d;
        }

        public static Entry Get(int id) => ById.TryGetValue(id, out var e) ? e : null;
        public static bool IsKnown(int id) => ById.ContainsKey(id);
    }
}
