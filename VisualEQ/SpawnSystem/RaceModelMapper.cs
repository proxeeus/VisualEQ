using System.Collections.Generic;

namespace VisualEQ.SpawnSystem
{
    // Maps EQ race IDs to candidate 3-letter model codes.
    //
    // EQ character models live in files named after the client-side 3-letter code:
    //   - Playable races encode gender in the 3rd character (BAM/BAF, DWM/DWF, HUM/HUF, ...).
    //   - Very old EQ builds also used `HUM_M`/`HUM_F` style with a `_M`/`_F` suffix.
    //   - Monsters generally have no gender variant (WOL, BAT, ORC).
    // A given decoded chr zip may follow any of these conventions, so ResolveCandidates
    // yields multiple names per (race, gender) — the caller picks the first that exists
    // in the availableModels dictionary.
    public static class RaceModelMapper
    {
        struct PlayableRace
        {
            public string Male;    // 3-letter modern male code (e.g. BAM)
            public string Female;  // 3-letter modern female code (e.g. BAF)
            public string Legacy;  // Legacy 3-letter code used with `_M`/`_F` suffixes (e.g. BAR → BAR_M)
            public PlayableRace(string male, string female, string legacy) { Male = male; Female = female; Legacy = legacy; }
        }

        // Playable races and common humanoid NPC races.
        static readonly Dictionary<int, PlayableRace> _playable = new Dictionary<int, PlayableRace>
        {
            {  1, new PlayableRace("HUM", "HUF", "HUM") },  // Human
            {  2, new PlayableRace("BAM", "BAF", "BAR") },  // Barbarian
            {  3, new PlayableRace("ERM", "ERF", "ERU") },  // Erudite
            {  4, new PlayableRace("ELM", "ELF", "ELF") },  // Wood Elf
            {  5, new PlayableRace("HIM", "HIF", "HIE") },  // High Elf
            {  6, new PlayableRace("DAM", "DAF", "DEF") },  // Dark Elf
            {  7, new PlayableRace("HOM", "HOF", "HEF") },  // Half Elf
            {  8, new PlayableRace("DWM", "DWF", "DWF") },  // Dwarf
            {  9, new PlayableRace("TRM", "TRF", "TRL") },  // Troll
            { 10, new PlayableRace("OGM", "OGF", "OGR") },  // Ogre
            { 11, new PlayableRace("HAM", "HAF", "HFL") },  // Halfling
            { 12, new PlayableRace("GNM", "GNF", "GNM") },  // Gnome
            { 14, new PlayableRace("IKM", "IKF", "IKS") },  // Iksar
            { 15, new PlayableRace("KEM", "KEF", "VAH") },  // Vah Shir / Kerran
            { 16, new PlayableRace("FRO", "FRO", "FRG") },  // Froglok (playable) — actual chr files may use pcfroglok
            { 17, new PlayableRace("DRK", "DRK", "DRK") },  // Drakkin
        };

        // Non-humanoid creatures — usually no gender variant. Base code only.
        static readonly Dictionary<int, string> _creatures = new Dictionary<int, string>
        {
            { 13, "ORC" },  // Orc
            { 21, "WOL" },  // Wolf
            { 22, "BEA" },  // Bear (best guess — verify with actual chr enumeration)
            { 23, "GOR" },  // Gorilla
            { 24, "SNA" },  // Snake
            { 26, "GIA" },  // Giant
            { 27, "MAD" },  // Madman? (uncertain)
            { 28, "KOB" },  // Kobold
            { 29, "KER" },  // Kerra
            { 30, "PIR" },  // Fish / piranha (uncertain)
            { 33, "BOA" },  // Boat (rarely spawned as NPC)
            { 34, "BAT" },  // Bat variant — confirmed by 'a_bat' spawns in Freeport
            { 36, "RAT" },  // Large rat variant — confirmed by 'a_large_rat' spawns
            { 38, "AVI" },  // Aviak
            { 39, "RAT" },  // Rat
            { 40, "SNK" },  // (uncertain — sometimes snake)
            { 42, "ELE" },  // Air Elemental
            { 43, "EAR" },  // Earth Elemental
            // Race 44: user's Freeport DB uses this for named human NPCs ('Lunce_Nasin' etc.),
            // not fire elementals. Best-effort HUM until we find a fork that treats it otherwise.
            { 44, "HUM" },
            { 45, "WAT" },  // Water Elemental
            { 46, "SKE" },  // Skeleton
            { 48, "BEA" },  // Bear (variant)
            { 50, "ALG" },  // Alligator (uncertain code)
            { 51, "SPX" },  // Sphinx (uncertain)
            { 52, "GRI" },  // Griffin (uncertain)
            { 54, "ORC" },  // Orc variant — confirmed by 'orc_centurion' spawns
            { 55, "BAS" },  // Basilisk (uncertain)
            { 60, "BAT" },  // Bat
            { 66, "FAI" },  // Fairy (uncertain — may be FAY)
            { 67, "FUN" },  // Fungus Man (uncertain)
            { 69, "WRA" },  // Wraith (uncertain)
            { 71, "WOL" },  // Wolf (variant)
            { 76, "SPE" },  // Spectre (uncertain)
            { 82, "ZOM" },  // Zombie
            { 88, "GOB" },  // Goblin
            { 91, "GHO" },  // Ghost (uncertain)
            { 96, "RHI" },  // Rhino (uncertain)
            { 98, "GRI" },  // Griffawn (uncertain)
            { 105, "CYC" }, // Cyclops
            { 108, "NAG" }, // Naga (uncertain)
            { 116, "LIO" }, // Lion (uncertain)
            { 118, "WIL" }, // Willowisp
            { 122, "WIS" }, // Wisp (uncertain — may collide with WIL)
            { 128, "WER" }, // Werewolf (uncertain)
            { 130, "SKE" }, // Reanimated Skeleton (uses skeleton model)
            { 156, "GOL" }, // Golem
        };

        // Backwards compat with any callers that still use the old single-code API.
        public static string Resolve(int raceId)
        {
            if (_playable.TryGetValue(raceId, out var pr)) return pr.Legacy;
            return _creatures.TryGetValue(raceId, out var c) ? c : null;
        }

        // Backwards compat — first candidate.
        public static string ResolveWithGender(int raceId, int gender)
        {
            foreach (var c in ResolveCandidates(raceId, gender)) return c;
            return null;
        }

        // Yields candidate model codes in priority order. Caller tries each against the
        // availableModels dict and stops at the first hit.
        public static IEnumerable<string> ResolveCandidates(int raceId, int gender)
        {
            if (_playable.TryGetValue(raceId, out var pr))
            {
                // 1. Modern per-gender 3-letter code (e.g. HUM / HUF).
                if (gender == 0 && pr.Male != null) yield return pr.Male;
                else if (gender == 1 && pr.Female != null) yield return pr.Female;
                else
                {
                    // Neuter or unknown: try both.
                    if (pr.Male != null) yield return pr.Male;
                    if (pr.Female != null && pr.Female != pr.Male) yield return pr.Female;
                }

                // 2. Legacy `{base}_M` / `{base}_F` naming.
                if (pr.Legacy != null)
                {
                    if (gender == 0) yield return pr.Legacy + "_M";
                    else if (gender == 1) yield return pr.Legacy + "_F";
                    // 3. Bare legacy base code (gender-neutral chr zip).
                    if (pr.Legacy != pr.Male && pr.Legacy != pr.Female)
                        yield return pr.Legacy;
                }
                yield break;
            }

            if (_creatures.TryGetValue(raceId, out var code))
            {
                yield return code;
                // Some creature files still ship gender variants (BRF/BRM etc.).
                // Try 3-letter-with-sex-letter as a fallback: swap the 3rd char.
                if (code.Length == 3)
                {
                    if (gender == 0) yield return code.Substring(0, 2) + "M";
                    else if (gender == 1) yield return code.Substring(0, 2) + "F";
                }
            }
        }
    }
}
