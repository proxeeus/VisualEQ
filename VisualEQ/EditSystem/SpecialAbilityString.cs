using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace VisualEQ.EditSystem
{
    // Parse/serialize the `npc_types.special_abilities` string. Format (from
    // server-side Mob::ProcessSpecialAbilities in /c/eqemu/source/Server/zone/mob.cpp):
    //
    //   ability_id,value[,param0[,param1[,...paramN]]]^ability_id,value...
    //
    // - '^' separates ability entries
    // - ',' separates fields within one entry
    // - First field: ability_id (int)
    // - Second field: value (int) — required. Often 0/1 for "enabled" but
    //   Flurry/Rampage/etc. use it for chance %.
    // - Trailing fields: up to SpecialAbilityCatalog.MaxParams int params.
    // - The server drops entries with fewer than 2 fields, non-numeric ids, or
    //   non-numeric values. We do the same on parse and never emit malformed
    //   entries on serialize.
    public static class SpecialAbilityString
    {
        // One parsed ability entry — value + up to MaxParams params. Slot 0 of
        // Params corresponds to server-side GetSpecialAbilityParam(ability, 0),
        // which is field index 2 in the wire format (fields 0/1 are id/value).
        public sealed class Entry
        {
            public int AbilityId;
            public int Value;
            public int[] Params = new int[SpecialAbilityCatalog.MaxParams];
        }

        // Parse a special_abilities string into a dict keyed by ability id.
        // Duplicate ids collapse — the last-seen entry wins (matches
        // ProcessSpecialAbilities's iteration order: later SetSpecialAbility
        // overrides earlier). Malformed sub-entries are silently dropped.
        // Null / empty input returns an empty dict.
        public static Dictionary<int, Entry> Parse(string raw)
        {
            var result = new Dictionary<int, Entry>();
            if (string.IsNullOrWhiteSpace(raw)) return result;

            foreach (var part in raw.Split('^'))
            {
                if (string.IsNullOrEmpty(part)) continue;
                var toks = part.Split(',');
                if (toks.Length < 2) continue;
                if (!int.TryParse(toks[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var id))    continue;
                if (!int.TryParse(toks[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)) continue;

                var entry = new Entry { AbilityId = id, Value = value };
                for (int i = 2, p = 0; i < toks.Length && p < SpecialAbilityCatalog.MaxParams; i++, p++)
                {
                    if (int.TryParse(toks[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out var pv))
                        entry.Params[p] = pv;
                }
                result[id] = entry;
            }
            return result;
        }

        // Serialize back to the wire format. Trailing-zero params are trimmed
        // (matches the human-written convention — e.g. "5,20" not "5,20,0,0,0")
        // so a round-trip through a UI that only touched Value keeps the string
        // as compact as it was. Entries emitted in ascending ability-id order for
        // deterministic diffs across saves.
        public static string Serialize(IReadOnlyDictionary<int, Entry> entries)
        {
            if (entries == null || entries.Count == 0) return "";
            var parts = new List<string>(entries.Count);
            foreach (var kv in entries.OrderBy(e => e.Key))
            {
                var e = kv.Value;
                var fields = new List<string>(2 + SpecialAbilityCatalog.MaxParams)
                {
                    e.AbilityId.ToString(CultureInfo.InvariantCulture),
                    e.Value.ToString(CultureInfo.InvariantCulture),
                };
                int lastNonZero = -1;
                for (int p = 0; p < SpecialAbilityCatalog.MaxParams; p++)
                    if (e.Params[p] != 0) lastNonZero = p;
                for (int p = 0; p <= lastNonZero; p++)
                    fields.Add(e.Params[p].ToString(CultureInfo.InvariantCulture));
                parts.Add(string.Join(",", fields));
            }
            return string.Join("^", parts);
        }
    }
}
