using System.Collections.Generic;

namespace VisualEQ.SpawnSystem
{
    public static class RaceModelMapper
    {
        private static readonly Dictionary<int, string> _map = new Dictionary<int, string>
        {
            {  1, "HUM" },  // Human
            {  2, "BAR" },  // Barbarian
            {  3, "ERU" },  // Erudite
            {  4, "ELF" },  // Wood Elf
            {  5, "HIE" },  // High Elf
            {  6, "DEF" },  // Dark Elf
            {  7, "HEF" },  // Half Elf
            {  8, "DWF" },  // Dwarf
            {  9, "TRL" },  // Troll
            { 10, "OGR" },  // Ogre
            { 11, "HFL" },  // Halfling
            { 12, "GNM" },  // Gnome
            { 13, "ORC" },  // Orc
            { 14, "IKS" },  // Iksar
            { 15, "VAH" },  // Vah Shir / Kerran
            { 16, "FRG" },  // Froglok
            { 17, "DRK" },  // Drakkin
            { 21, "WOL" },  // Wolf
            { 42, "ELE" },  // Air Elemental
            { 43, "EAR" },  // Earth Elemental
            { 44, "FIR" },  // Fire Elemental
            { 45, "WAT" },  // Water Elemental
            { 46, "SKL" },  // Skeleton
            { 54, "DRG" },  // Dragon
        };

        // Returns the base 3-letter model code or null if unmapped.
        public static string Resolve(int raceId) =>
            _map.TryGetValue(raceId, out var code) ? code : null;

        // Returns the gender-suffixed name: 0=_M, 1=_F, anything else=base code.
        public static string ResolveWithGender(int raceId, int gender)
        {
            var code = Resolve(raceId);
            if (code == null) return null;
            return gender == 0 ? code + "_M"
                 : gender == 1 ? code + "_F"
                 : code;
        }
    }
}
