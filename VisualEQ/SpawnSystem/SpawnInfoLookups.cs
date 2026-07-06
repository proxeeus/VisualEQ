using System.Collections.Generic;

namespace VisualEQ.SpawnSystem
{
    // Human-readable names for the numeric IDs the DB stores for race, class, body type,
    // and gender. Separate from RaceModelMapper (which is about model resolution) — this
    // is display-only. Unknown IDs fall back to a generic label so unusual values still
    // show something.
    public static class SpawnInfoLookups
    {
        public static string RaceName(int race) =>
            Races.TryGetValue(race, out var n) ? n : $"Race {race}";

        public static string ClassName(int cls) =>
            Classes.TryGetValue(cls, out var n) ? n : $"Class {cls}";

        public static string BodyTypeName(int bt) =>
            BodyTypes.TryGetValue(bt, out var n) ? n : $"Body {bt}";

        public static string GenderName(int gender)
        {
            switch (gender)
            {
                case 0:  return "Male";
                case 1:  return "Female";
                case 2:  return "Neuter";
                default: return $"Gender {gender}";
            }
        }

        static readonly Dictionary<int, string> Races = new Dictionary<int, string>
        {
            {  1, "Human" }, {  2, "Barbarian" }, {  3, "Erudite" }, {  4, "Wood Elf" },
            {  5, "High Elf" }, {  6, "Dark Elf" }, {  7, "Half Elf" }, {  8, "Dwarf" },
            {  9, "Troll" }, { 10, "Ogre" }, { 11, "Halfling" }, { 12, "Gnome" },
            { 13, "Orc" }, { 14, "Iksar" }, { 15, "Vah Shir" }, { 16, "Froglok (PC)" },
            { 17, "Drakkin" },
            { 21, "Wolf" }, { 22, "Bear" }, { 23, "Gorilla" }, { 24, "Snake" },
            { 25, "Cobalt Scar Dragon" }, { 26, "Giant" }, { 27, "Trakanon" },
            { 28, "Kobold" }, { 29, "Kerra" }, { 30, "Piranha" }, { 33, "Boat" },
            { 34, "Bat" }, { 36, "Large Rat" }, { 38, "Aviak" }, { 39, "Rat" },
            { 42, "Air Elemental" }, { 43, "Earth Elemental" }, { 44, "Human (variant)" },
            { 45, "Water Elemental" }, { 46, "Skeleton" }, { 48, "Bear (variant)" },
            { 50, "Alligator" }, { 51, "Sphinx" }, { 52, "Griffin" },
            { 54, "Orc (variant)" }, { 55, "Basilisk" },
            { 60, "Bat (large)" }, { 66, "Fairy" }, { 67, "Fungus Man" },
            { 69, "Wraith" }, { 71, "Wolf (dire)" }, { 76, "Spectre" },
            { 82, "Zombie" }, { 88, "Goblin" }, { 91, "Ghost" }, { 96, "Rhino" },
            { 98, "Griffawn" }, { 105, "Cyclops" }, { 108, "Naga" }, { 116, "Lion" },
            { 118, "Willowisp" }, { 122, "Wisp" }, { 128, "Werewolf" },
            { 130, "Reanimated Skeleton" }, { 156, "Golem" },
        };

        static readonly Dictionary<int, string> Classes = new Dictionary<int, string>
        {
            {  0, "None" },
            {  1, "Warrior" }, {  2, "Cleric" }, {  3, "Paladin" }, {  4, "Ranger" },
            {  5, "Shadow Knight" }, {  6, "Druid" }, {  7, "Monk" }, {  8, "Bard" },
            {  9, "Rogue" }, { 10, "Shaman" }, { 11, "Necromancer" }, { 12, "Wizard" },
            { 13, "Magician" }, { 14, "Enchanter" }, { 15, "Beastlord" }, { 16, "Berserker" },
            { 20, "Warrior GM" }, { 32, "Bard GM" },
            { 40, "Merchant" }, { 41, "Adventure Recruiter" }, { 42, "Adventure Merchant" },
            { 60, "LDoN Recruiter" }, { 61, "LDoN Merchant" }, { 62, "Guild Bank" },
            { 63, "Rewards Merchant" }, { 65, "Discord Merchant" },
        };

        static readonly Dictionary<int, string> BodyTypes = new Dictionary<int, string>
        {
            {  0, "Undefined" }, {  1, "Humanoid" }, {  2, "Lycanthrope" }, {  3, "Undead" },
            {  4, "Giant" }, {  5, "Construct" }, {  6, "Extraplanar" }, {  7, "Magical" },
            {  8, "Summoned Undead" }, {  9, "Raid Giant" }, { 10, "Untargetable" },
            { 11, "Vampyre" }, { 12, "Atenha Ra" }, { 13, "Greater Akheva" },
            { 14, "Khati Sha" }, { 15, "Seru" }, { 16, "Draz Nurakk" }, { 17, "Zek" },
            { 18, "Luggald" }, { 19, "Animal" }, { 20, "Insect" }, { 21, "Reptile" },
            { 22, "Elemental / Planar" }, { 23, "Plant" }, { 24, "Dragon" },
            { 25, "Summoned2" }, { 27, "Special" }, { 33, "Muramite" },
        };
    }
}
