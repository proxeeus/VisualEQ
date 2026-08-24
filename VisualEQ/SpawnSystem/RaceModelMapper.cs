using System.Collections.Generic;

namespace VisualEQ.SpawnSystem
{
    // EQ race id → 3-letter (or 4-letter, e.g. BOAT / SHIP) character-mesh code.
    //
    // Codes are the OESCharacter.Name entries inside `*_chr_oes.zip` archives —
    // see Loader.GetAvailableCharacterModels. Table derived from EQEmu's
    // canonical race constants (common/races.h) and cross-referenced against
    // live npc_types samples. Where a resolved code is not present in any
    // loaded chr zip, SpawnManager falls back to the shared placeholder and
    // logs the miss — decode the missing zone or add the model to a global
    // chr zip to resolve it.
    //
    // Gender: playable races encode gender in the 3rd char (HUM/HUF, DAM/DAF).
    // Most creature races are gender-neutral. A few humanoid NPC races (44,
    // 55, 67, 71, 77, 78, 81, 90, 92, 93, 94, 106, 112, 128, 139, 202, 217,
    // 331–342, 351, 352) render a base humanoid mesh; the fork-specific armor
    // variant lives in npc_types.texture and is deferred to a follow-up slice.
    public static class RaceModelMapper
    {
        readonly struct Entry
        {
            public readonly string Male;
            public readonly string Female;
            public readonly string Neuter;

            public Entry(string code)
                : this(code, code, code) { }

            public Entry(string male, string female)
                : this(male, female, male) { }

            public Entry(string male, string female, string neuter)
            {
                Male = male;
                Female = female;
                Neuter = neuter;
            }
        }

        static readonly Dictionary<int, Entry> _byRace = new Dictionary<int, Entry>
        {
            // Playable + core humanoid races. Codes cross-verified against LANTERN's
            // ClientData/animationsources.txt — races 7 and 11 were previously
            // swapped (Half Elf used HOM = short-mesh code, Halfling used HAM =
            // tall-mesh code), producing the "halflings look big hairy-footed"
            // regression noted during Slice #5 size scaling.
            {   1, new Entry("HUM", "HUF") },      // Human
            {   2, new Entry("BAM", "BAF") },      // Barbarian
            {   3, new Entry("ERM", "ERF") },      // Erudite
            {   4, new Entry("ELM", "ELF") },      // Wood Elf
            {   5, new Entry("HIM", "HIF") },      // High Elf
            {   6, new Entry("DAM", "DAF") },      // Dark Elf
            {   7, new Entry("HAM", "HAF") },      // Half Elf (tall mesh, ELM/ELF anim source)
            {   8, new Entry("DWM", "DWF") },      // Dwarf
            {   9, new Entry("TRM", "TRF") },      // Troll
            {  10, new Entry("OGM", "OGF") },      // Ogre
            {  11, new Entry("HOM", "HOF") },      // Halfling (short mesh, DWM/DWF anim source)
            {  12, new Entry("GNM", "GNF") },      // Gnome
            {  14, new Entry("WER") },             // Werewolf — LANTERN RaceData.csv row 14 (was wrongly IKM/IKF/IKS — race 14 in EQEmu/LANTERN is Werewolf, not Iksar; Iksar PC is race 128)
            {  15, new Entry("BRM", "BRF") },      // Brownie (LANTERN: BRM,ELM)
            {  16, new Entry("CEN") },             // Centaur — LANTERN RaceData.csv (was wrongly FRO / "Froglok PC"; race 16 is Centaur)
            {  17, new Entry("GOM", "GOM", "GOL") }, // Golem (LANTERN: GOM,GIA)

            // Race 13-17 in this DB are populated with different creature types
            // due to fork/data reuse — DB overrides below.

            // --- Creatures & NPCs (verified against live npc_types samples) ---
            {  13, new Entry("AVI") },             // Aviak
            //   ↑ race 13 shows as 'aviak_darter' in DB, but this fork also
            //   places 'Romiak_Jusathorn' (human name) here — AVI is the
            //   canonical model; oddball humanoids fall through to placeholder.
            {  18, new Entry("GIA") },             // Giant (hill / forest)
            {  19, new Entry("TRK") },             // Trakanon
            {  20, new Entry("VST") },             // Doppleganger — LANTERN RaceData.csv (was wrongly VNS/"Venril Sathir")
            {  21, new Entry("BEH") },             // Evil Eye — LANTERN RaceData.csv (uses Beholder mesh; was wrongly EYE which is Eye of Zomm at race 108)
            {  22, new Entry("BET") },             // Beetle (LANTERN: BET,SPI — was wrongly "BEE")
            {  23, new Entry("CPM", "CPF") },      // Kerra (LANTERN: CPF,CPM)
            {  24, new Entry("FIS") },             // Fish (koalindl, sludge guppy)
            {  25, new Entry("FAI") },             // Faerie
            {  26, new Entry("FRO") },             // Froglok (NPC)
            {  27, new Entry("FRG") },             // Froglok Ghoul
            {  28, new Entry("SPO") },             // Sporali
            {  29, new Entry("GAM", "GAM", "GAR") }, // Gargoyle (LANTERN: GAM,GAR — GAM primary, GAR anim source)
            {  30, new Entry("BEH") },             // Gasbag — LANTERN RaceData.csv (shares Beholder mesh with Evil Eye)
            {  31, new Entry("CUB") },             // Gelatinous Cube — LANTERN RaceData.csv (was wrongly SNA/"snake")
            {  32, new Entry("DRA") },             // Dragon (Kunark)
            {  33, new Entry("GHU") },             // Ghoul — confirmed by 'a_ghoul_yeoman'
            {  34, new Entry("BAT") },             // Bat
            {  36, new Entry("RAT") },             // Rodent / large rat
            {  37, new Entry("SNA") },             // Snake
            {  38, new Entry("SPI") },             // Spider — was wrongly 'AVI'
            {  39, new Entry("GNL") },             // Gnoll — was wrongly 'RAT'
            {  40, new Entry("GOB") },             // Goblin (Pickclaw)
            {  41, new Entry("GOR") },             // Gorilla
            {  42, new Entry("WOL", "WOF") },      // Wolf — classic WOL; Kunark scaled wolf / chokidai mesh is WOF (fieldofbone/nurga/warslikswood). Zone-local resolution in SpawnManager picks the right one per zone.
            {  43, new Entry("BEA") },             // Bear — was wrongly 'EAR'
            {  44, new Entry("FPM") },             // Freeport Guard / knight (falls back on HUM elsewhere)
            {  45, new Entry("DML") },             // Demi Lich
            {  46, new Entry("IMP") },             // Imp / Familiar
            {  47, new Entry("GRI") },             // Griffin / Griffawn
            {  48, new Entry("KOB") },             // Kobold — was wrongly 'BEA'
            {  49, new Entry("DRA") },             // Elder Dragon (Nagafen / Vox)
            {  50, new Entry("LIM", "LIF") },      // Lion (LANTERN: LIF,LIM — was wrongly "LIO")
            {  51, new Entry("LIZ") },             // Lizardman
            {  52, new Entry("MIM") },             // Mimic (a_chest, a_mimic)
            {  53, new Entry("MIN") },             // Minotaur
            {  54, new Entry("ORC") },             // Orc
            {  55, new Entry("BGM", null, "BGM") }, // Human Beggar — confirmed BGM in freporte
            {  56, new Entry("PIF") },             // Pixie
            {  57, new Entry("DRM", "DRF") },      // Drachnid — LANTERN RaceData.csv: DRM/DRF (was wrongly "DRA" which is Dragon)
            {  58, new Entry("SOL") },             // Solusek Ro / puppet
            {  59, new Entry("BGG") },             // Bloodgill Goblin (LANTERN: BGG,KGO)
            {  60, new Entry("SKE") },             // Skeleton — was wrongly 'BAT'
            {  61, new Entry("SHA") },             // Shark
            {  62, new Entry("TUN") },             // Tunare
            {  63, new Entry("TIG") },             // Tiger (LANTERN: TIG,LIM)
            {  64, new Entry("TRE") },             // Treant
            {  65, new Entry("DVM") },             // Vampire — LANTERN RaceData.csv (uses Dark-elf-vampire mesh; was wrongly VAM which resolves nowhere)
            {  66, new Entry("RAL") },             // Rallos Zek statue
            {  67, new Entry("HHM") },             // Highpass Citizen (LANTERN: HHM,ELM)
            {  68, new Entry("TEN") },             // Tentacle Terror
            {  69, new Entry("WIL") },             // Willowisp
            {  70, new Entry("ZOM", "ZOF") },      // Zombie (LANTERN: ZOM,ELM / ZOF,ELF)
            {  71, new Entry("QCM", "QCF") },      // Qeynos Citizen (LANTERN: QCM,ELM / QCF,ELF)
            // Race 72 (SHIP) — the trilogy client picks a different mesh per
            // gender for this race. Sourced from LANTERN's WldFileCharacters
            // fixer (LanternExtractor/EQ/Wld/Helpers/CharacterFixer.cs):
            //   gender 0 → SHIP mesh (viking-style longship — the base actor)
            //   gender 2 → PRE mesh  (PRE_HS_DEF skeleton = Sea King, Golden
            //                          Maiden, StormBreaker, SirensBane — the
            //                          Freeport / Erudin / Iceclad ferry model
            //                          that players actually board)
            //   Same-zone variants swap PRE→OGS (Bloated Belly in Iceclad) or
            //   SHIP→GNS (Icebreaker) / SHIP→ELS (Maidens Voyage) but the base
            //   mesh code is what the client picks by race+gender; the variant
            //   rename is a LANTERN-only convenience for asset export and does
            //   not change what the classic client loads.
            {  72, new Entry("SHIP", "SHIP", "PRE") },
            {  73, new Entry("LAUNCH") },          // Launch (Captain's Skiff)
            {  74, new Entry("PIR") },             // Piranha
            {  75, new Entry("ELE") },             // Elemental (air / fire / water / earth)
            {  76, new Entry("PUM") },             // Puma (LANTERN: PUM,LIM)
            {  77, new Entry("NGM") },             // Neriak Citizen (LANTERN: NGM,ELM)
            {  78, new Entry("EGM") },             // Erudin Citizen (LANTERN: EGM,ELM)
            {  79, new Entry("BIX") },             // Bixie
            {  80, new Entry("REA") },             // Reanimated Hand (was wrongly "HAN")
            {  81, new Entry("RIM", "RIF") },      // Rivervale Citizen (LANTERN: RIM,DWM / RIF,DWF)
            {  82, new Entry("SCR") },             // Scarecrow
            {  83, new Entry("SKU") },             // Skunk
            {  85, new Entry("SKE") },             // Skeleton (pet variant)
            {  86, new Entry("SPH") },             // Sphinx (LANTERN: SPH,DRK — was wrongly "SPX")
            {  87, new Entry("ARM") },             // Armadillo (LANTERN: ARM,RAT)
            {  88, new Entry("CLM", "CLF") },      // Clockwork Gnome (LANTERN: CLM,DWM / CLF,DWF)
            {  89, new Entry("DRK") },             // Drake / ash-drakeling
            {  90, new Entry("HLM", "HLF") },      // Halas Citizen (LANTERN: HLM,ELM / HLF,ELF)
            {  91, new Entry("ALL") },             // Alligator — LANTERN RaceData.csv (was wrongly BAS/"basilisk")
            {  92, new Entry("GRM") },             // Grobb Citizen (LANTERN: GRM,OGF / GRF,OGF; ogres use OGF for both)
            {  93, new Entry("OKM", "OKF") },      // Oggok Citizen (LANTERN: OKM,OGF / OKF,OGF)
            {  94, new Entry("KAM", "KAF") },      // Kaladim Citizen (LANTERN: KAM,DWM / KAF,DWF — was wrongly QCM/QCF)
            {  95, new Entry("CAZ") },             // Cazic-Thule (avatar)
            {  96, new Entry("COC") },             // Cockatrice (was wrongly "COK")
            {  98, new Entry("VSM", "VSF", "VST") }, // Elf Vampire — LANTERN RaceData.csv (was wrongly DHM/DHF best-guess "Dark Assassin"; VST is the vampire-thrall Karnor variant)
            {  99, new Entry("DEN") },             // Denizen — LANTERN RaceData.csv (was wrongly AMY/"Amygdalan")
            { 100, new Entry("DER") },             // Rock Dervish (freporte has DER)
            { 101, new Entry("EFR") },             // Efreeti
            { 102, new Entry("FRO") },             // Froglok Tadpole (uses base Froglok)
            { 103, new Entry("PHI") },             // Phinigel Autropos
            { 104, new Entry("LEE") },             // Leech
            { 105, new Entry("SWO") },             // Swordfish (freporte has SWO)
            { 106, new Entry("FEM", "FEF") },      // Felguard (LANTERN: FEM,ELM / FEF,ELF)
            { 107, new Entry("MAM") },             // Mammoth (was wrongly "ELP" — LANTERN: MAM is the mammoth code)
            { 108, new Entry("EYE") },             // Eye of Zomm (uses Evil Eye model)
            { 109, new Entry("WAS") },             // Wasp / Yellowjacket
            { 110, new Entry("MER") },             // Mermaid
            { 111, new Entry("HAR") },             // Harpy
            { 112, new Entry("GFM", "GFF") },      // Fayguard (LANTERN: GFM,ELM / GFF,ELF)
            { 113, new Entry("DRI") },             // Drixie — LANTERN RaceData.csv (was wrongly DRK/"Fae Drake")
            { 114, new Entry("GHO") },             // Ghost
            { 116, new Entry("SEA") },             // Seahorse
            { 117, new Entry("GDM") },             // Ghost Dwarf — LANTERN RaceData.csv (was wrongly HUM/HUF/"Torklar Battlemaster")
            { 118, new Entry("GEM", "GEF") },      // Erudite Ghost — LANTERN RaceData.csv (was wrongly SPE/"city spirit"; SPE is Spectre at race 85)
            { 119, new Entry("STC") },             // Sabertooth Cat
            { 120, new Entry("WOE") },             // Wolf Elemental — LANTERN RaceData.csv (was wrongly WOL/"Spirit Wolf")
            { 121, new Entry("GRG") },             // Gorgalosk
            { 122, new Entry("HOR") },             // Horror construct
            { 123, new Entry("INN") },             // Innoruuk
            { 124, new Entry("UNI") },             // Unicorn (was wrongly "NIG"!)
            { 125, new Entry("PEG") },             // Pegasus (was wrongly "QUI")
            { 126, new Entry("DJI") },             // Djinn (was wrongly "STM")
            { 127, new Entry("HUM", "HUF") },      // Invisible-model / summoned-item — HUM base
            { 128, new Entry("IKM", "IKF") },      // Iksar Broodling / Kotiz — Iksar variant
            { 129, new Entry("SCR") },             // Scorpion — LANTERN RaceData.csv: SCR (was wrongly "SCO" which resolves nowhere → beetle fallback)
            { 130, new Entry("HUM", "HUF") },      // Taruun Guardian (human variant)
            { 131, new Entry("SRW") },             // Sarnak — LANTERN RaceData.csv (was wrongly SAR which resolves nowhere; SRW is the actual mesh)
            { 133, new Entry("LYC") },             // Lycanthrope (was wrongly "DVA")
            { 134, new Entry("MOS") },             // Mosquito / Bloodneedle
            { 135, new Entry("RHI") },             // Rhino (Kunark)
            { 136, new Entry("XAL") },             // Xalgoz
            { 137, new Entry("KGO") },             // Kunark Goblin (Droga/Nurga/Kurn/Kaesora) — distinct from race 40 Pickclaw GOB
            { 138, new Entry("YET") },             // Yeti (was wrongly "BRU")
            { 139, new Entry("ICM", "ICF", "ICN") }, // Iksar Citizen (LANTERN: ICM/ICF/ICN,IKM)
            { 140, new Entry("FGI") },             // Forest Giant
            { 141, new Entry("BOAT") },            // Boat (a_boat, a_row_boat) — confirmed BOAT in global
            { 144, new Entry("BRN") },             // Burynai — LANTERN RaceData.csv: BRN (was wrongly "BUR" which resolves nowhere)
            { 145, new Entry("OOZ") },             // Ooze / slime
            { 146, new Entry("SSN") },             // Spectral Sarnak — LANTERN RaceData.csv (was wrongly SPE/"Spectral Guardian")
            { 147, new Entry("SIM") },             // Spectral Iksar — LANTERN RaceData.csv (was wrongly SPE/"Guardian of Xalgoz")
            { 148, new Entry("BAC") },             // Kunark Fish — LANTERN RaceData.csv (was wrongly BAR/"Barracuda")
            { 149, new Entry("ISC") },             // Iksar Scorpion — LANTERN RaceData.csv (was wrongly SPX/"Scorpikis")
            { 150, new Entry("EROL") },            // Erollisi Marr — best-guess
            { 151, new Entry("TRB") },             // Tribunal
            { 153, new Entry("BRI") },             // Bristlebane
            { 154, new Entry("FDR") },             // Fay Drake (LANTERN: FDF,FDR — was wrongly "CHR")
            { 155, new Entry("SSK") },             // Sarnak Skeleton (LANTERN: SSK,SRW — was wrongly "SKE")
            { 156, new Entry("VRM", "VRF") },      // Ratman (LANTERN: VRF,VRM — was wrongly "RAT")
            { 157, new Entry("WYV") },             // Wyvern
            { 158, new Entry("WUR") },             // Wurm
            { 159, new Entry("DEV") },             // Devourer — LANTERN RaceData.csv (was wrongly GNA/"Insatiable Gnawer")
            { 160, new Entry("IKG") },             // Iksar Golem — LANTERN RaceData.csv (was wrongly SEB/"Sebilite golem")
            { 161, new Entry("IKS") },             // Iksar Skeleton (Scalebone) — LANTERN RaceData.csv: IKS (was wrongly "SKE" which resolves to the human skeleton)
            { 162, new Entry("MEP") },             // Man-Eating Plant — LANTERN RaceData.csv (was wrongly PLA which resolves nowhere)
            { 163, new Entry("RAP") },             // Raptor
            { 164, new Entry("SGO") },             // Sathir Construct
            { 165, new Entry("SED") },             // Water Dragon (Faydedar / Sirens Grotto) — LANTERN RaceData.csv (was wrongly PRA/"Praklion")
            { 166, new Entry("IKH") },             // Iksar Hand (LANTERN: IKH,REA — was wrongly "HAN")
            { 167, new Entry("SUC") },             // Succulent plant
            { 168, new Entry("FMO") },             // Flying Monkey (LANTERN: FMO,DRK — was wrongly "HOL")
            { 169, new Entry("BTM") },             // Brontotherium (LANTERN: BTM,RHI — was wrongly "BRO")
            { 170, new Entry("SDE") },             // Snow Dervish (LANTERN: SDE,DML — was wrongly "SHG")
            { 171, new Entry("WOL") },             // Direwolf — WOL base
            { 172, new Entry("MTC") },             // Manticore — LANTERN RaceData.csv (was wrongly MAN which resolves nowhere)
            { 173, new Entry("ENT") },             // Entoling
            { 174, new Entry("ISH") },             // Ice shade
            { 175, new Entry("ENA") },             // Enchanted Armor — LANTERN RaceData.csv (was wrongly ARS/"Armored Shadow")
            { 176, new Entry("SBU") },             // Snow Bunny — LANTERN RaceData.csv (was wrongly RAB/"Rabbit")
            { 177, new Entry("WAL") },             // Walrus
            { 178, new Entry("GEO") },             // Geonid
            { 181, new Entry("YAK") },             // Yak Man (LANTERN: YAK,GNN — was wrongly "TIZ")
            { 183, new Entry("COM", "COF", "COK") }, // Coldain (LANTERN: COM,DWM / COF,DWF / COK,DWM)
            { 184, new Entry("DR2") },             // Velious Dragons (LANTERN: DR2,TRK — was wrongly "VDR")
            { 185, new Entry("HAG") },             // Hag (LANTERN: HAG,ELF — was wrongly "KOB")
            { 187, new Entry("SIR") },             // Siren
            { 188, new Entry("FSG") },             // Frost Giant (Kael) — was wrongly "FRG" which is Froglok Ghoul (race 27)
            { 189, new Entry("STG") },             // Storm Giant
            { 190, new Entry("SHL") },             // Shellfish collector
            { 191, new Entry("WLM") },             // Walrus Man — LANTERN RaceData.csv (was wrongly PAN/"Panda")
            { 193, new Entry("TSE") },             // Tserrina Syl'Tor
            { 194, new Entry("STU") },             // Sea Turtle (was wrongly "TOR")
            { 195, new Entry("BWD") },             // Black & White Dragons (LANTERN: BWD,TRK)
            { 196, new Entry("GDR") },             // Ghost Dragon (LANTERN: GDR,DRA — was wrongly "WRA")
            { 198, new Entry("PRI") },             // Prismatic Dragon (LANTERN: PRI,TRK — was wrongly "KER")
            { 199, new Entry("SHK") },             // Shik'Nar
            { 200, new Entry("HOP") },             // Rockhopper
            { 201, new Entry("UNB") },             // Underbulk
            { 202, new Entry("GRM") },             // Grimling
            { 203, new Entry("XAK") },             // Xakra
            { 205, new Entry("SHA") },             // Shadel Bandit (best-guess)
            { 206, new Entry("OWL") },             // Owlbear
            { 207, new Entry("BEE") },             // Rhinobeetle
            { 208, new Entry("COT") },             // Coterie / vampyre — best-guess
            { 209, new Entry("ELE") },             // Elemental warrior
            { 210, new Entry("ELE") },             // Steam elemental
            { 211, new Entry("ELE") },             // Essence of Water / Air
            { 212, new Entry("ELE") },             // Fire construct
            { 213, new Entry("FIS") },             // Wormbait minnow / wetfang
            { 214, new Entry("LCH") },             // Thought Leech
            { 215, new Entry("BOG") },             // Bogling
            { 217, new Entry("SHI") },             // Shissar
            { 218, new Entry("FUN") },             // Fungal Fiend
            { 219, new Entry("DWM", "DWF") },      // Coldain variant
            { 220, new Entry("STG") },             // Stonegrabber
            { 221, new Entry("CHE") },             // Scarlet Cheetah
            { 222, new Entry("ZEL") },             // Zelniak
            { 223, new Entry("LGC") },             // Lightcrawler
            { 224, new Entry("XAK") },             // Xakra variant / Ward
            { 225, new Entry("SUN") },             // Sunflower plant
            { 226, new Entry("KHA") },             // Khati Sha
            { 227, new Entry("SAP") },             // Saprophyte
            { 228, new Entry("CAL") },             // Caller
            { 229, new Entry("NEB") },             // Netherbian
            { 230, new Entry("ATN") },             // Aten Ha Ra
            { 231, new Entry("GRV") },             // Grieg Veneficus
            { 232, new Entry("WOL") },             // Sonic Wolf — WOL base
            { 233, new Entry("ELE") },             // Elemental (wind/flame/sand)
            { 234, new Entry("SKE") },             // Skeletal minion
            { 235, new Entry("MUT") },             // Savage Mutant
            { 236, new Entry("SER") },             // Lord Inquisitor Seru
            { 237, new Entry("HUM", "HUF") },      // Wandering fanatic / madman
            { 238, new Entry("KER") },             // King Raja Kerrath (Vah Shir)
            { 239, new Entry("HUM", "HUF") },      // Spiritual Arcanist
            { 240, new Entry("HUM", "HUF") },      // Narandi the Wretched
            { 241, new Entry("NST") },             // Nightstalker
            { 242, new Entry("POT") },             // Potameid
            { 243, new Entry("DRY") },             // Dryad
            { 244, new Entry("TRE") },             // Treant (variant)
            { 245, new Entry("TSE") },             // Tsetsian fly
            { 246, new Entry("COI") },             // Coirnav (avatar of water)
            { 248, new Entry("CLM") },             // Clockwork Golem
            { 249, new Entry("MAN") },             // Manaetic prototype
            { 250, new Entry("BAN") },             // Banshee
            { 253, new Entry("GRU") },             // Grummus
            { 259, new Entry("TAR") },             // Tar'Dak hunter
            { 260, new Entry("BAT") },             // Bat variant
            { 261, new Entry("HRA") },             // Hraquis / slarghilug
            { 263, new Entry("TIN") },             // Tin soldier
            { 264, new Entry("WRA") },             // Wraith variant
            { 265, new Entry("MAL") },             // Malarian
            { 266, new Entry("HUM", "HUF") },      // Knight of Pestilence
            { 267, new Entry("HUM", "HUF") },      // Guardian of Pestilence
            { 268, new Entry("HUM", "HUF") },      // Rallius Rattican
            { 269, new Entry("BUB") },             // Bubonian
            { 270, new Entry("PUS") },             // Wretched Pusling
            { 271, new Entry("TRL") },             // Triloun
            { 272, new Entry("STO") },             // Stormrider
            { 274, new Entry("CLM") },             // Erratic/corroded model (clockwork variant)
            { 275, new Entry("MAB") },             // Manaetic Behemoth
            { 276, new Entry("CLM") },             // Clockwork device
            { 277, new Entry("HOB") },             // Hobgoblin
            { 278, new Entry("KAR") },             // Karana / Agnarr
            { 279, new Entry("RVN") },             // Blood raven
            { 280, new Entry("GAR") },             // Gargoyle (Velious variant)
            { 286, new Entry("MUJ") },             // Mujaki the Devourer
            { 287, new Entry("NIG") },             // Shadow steed / gloom nightmare
            { 294, new Entry("MEP") },             // Mephit
            { 297, new Entry("VHA") },             // Vhaksiz the Shade
            { 302, new Entry("REG") },             // Regrua
            { 305, new Entry("GRZ") },             // Grezlan
            { 306, new Entry("HUM", "HUF") },      // Jord militis (viking-styled human)
            { 307, new Entry("HUM", "HUF") },      // Vann Stav (Vind/Brann variant)
            { 308, new Entry("HUM", "HUF") },      // Brann militis
            { 309, new Entry("HUM", "HUF") },      // Vind militis
            { 310, new Entry("HUM", "HUF") },      // Relv the Mysterious
            { 311, new Entry("HUM", "HUF") },      // Evynd Firestorm
            { 312, new Entry("HUM", "HUF") },      // Emmerik Skyfury
            { 315, new Entry("KRA") },             // Deepwater Kraken
            { 316, new Entry("LOR") },             // Hurricane Lorok
            { 321, new Entry("BOA") },             // Broken Skull Boar
            { 324, new Entry("HUM", "HUF") },      // Foreboding guardian
            { 326, new Entry("SPI") },             // Virulent arachnid
            { 329, new Entry("POR") },             // Storm portal
            { 330, new Entry("HAM", "HAF") },      // Halfling variant (Gubbly Gippledo)
            { 331, new Entry("TRM", "TRF") },      // Broken Skull troll
            { 332, new Entry("TRM", "TRF") },      // Broken Skull troll plunderer
            { 335, new Entry("TRM", "TRF") },      // Overlord Ngrub (troll)
            { 336, new Entry("TRM", "TRF") },      // Spiritseeker Nadox (troll)
            { 337, new Entry("TRM", "TRF") },      // Broken Skull Blackhand
            { 338, new Entry("GNM", "GNF") },      // Gnome plunderer
            { 339, new Entry("DAM", "DAF") },      // Dark Elf plunderer
            { 340, new Entry("OGM", "OGF") },      // Ogre plunderer
            { 341, new Entry("HUM", "HUF") },      // Human plunderer
            { 342, new Entry("ERM", "ERF") },      // Erudite plunderer
            { 344, new Entry("ZOM") },             // Pestilent zombie
            { 345, new Entry("HUM", "HUF") },      // Emissary of Hate
            { 346, new Entry("HUM", "HUF") },      // Blackblooded Assassin
            { 347, new Entry("LUG") },             // Luggald
            { 348, new Entry("DRG") },             // Drogmor
            { 351, new Entry("HUM", "HUF") },      // Templar of Hate
            { 352, new Entry("HUM", "HUF") },      // Warlock of Hate
            { 353, new Entry("HUM", "HUF") },      // Initiate / Master
            { 354, new Entry("HUM", "HUF") },      // Vessel / animist
            { 355, new Entry("HUM", "HUF") },      // Highborn commander
            { 357, new Entry("WOL") },             // Decaying scaled wolf
            { 371, new Entry("HUM", "HUF") },      // Curse-Ruined Shinta Knight
            { 376, new Entry("KOB") },             // Kobold siege supplies (reuses KOB)
            { 377, new Entry("KOB") },             // Kobold barrel
            { 378, new Entry("MIM") },             // Chest / footlocker (mimic-style)
            { 383, new Entry("ZOM") },             // Pile of digested remains
            { 392, new Entry("UKN") },             // Ukun
            { 394, new Entry("IKV") },             // Ikaav
            { 395, new Entry("ANU") },             // Aneuk
            { 396, new Entry("KYV") },             // Kyv
            { 397, new Entry("NOC") },             // Noc
            { 400, new Entry("HUV") },             // Huvul
            { 404, new Entry("BOAT") },            // Test Boat
            { 405, new Entry("STO") },             // Stoneworker
            { 406, new Entry("MMU") },             // Overlord Mata Muram
            { 407, new Entry("LWR") },             // Lightning Warrior
            { 409, new Entry("BAZ") },             // Bazu
            { 410, new Entry("FER") },             // Feran
            { 411, new Entry("DGN") },             // Dragorn
            { 412, new Entry("CHI") },             // Chimera
            { 413, new Entry("DGN") },             // Dragorn
            { 414, new Entry("MGL") },             // Murkglider
            { 415, new Entry("RAT") },             // Swamp rat / diseased rat
            { 416, new Entry("BAT") },             // Vampyre bat
            { 417, new Entry("HUM", "HUF") },      // Herrian Warfrost / Tarn Icewind
            { 418, new Entry("DIS") },             // Discordling
            { 419, new Entry("GIR") },             // Girplan
            { 420, new Entry("HUM", "HUF") },      // Arch Magus Vangl
            { 422, new Entry("ORB") },             // Orb of Discordant Energy
            { 426, new Entry("POR") },             // Crystalline portal
            { 428, new Entry("VER") },             // Vermin nest
            { 429, new Entry("EGG") },             // Dormant egg
            { 432, new Entry("FLU") },             // Flutterwing
            { 433, new Entry("GOB") },             // Exhausted goblin / slave
            { 440, new Entry("SPI") },             // Gloom spider
            { 441, new Entry("SPI") },             // Queen Gloomfang
            { 449, new Entry("EGG") },             // Spider cocoon cluster
            { 455, new Entry("GOB") },             // Gloomingdeep warrior (goblin variant)
            { 510, new Entry("TRP") },             // Trap (invisible)
            { 513, new Entry("TRP") },             // Trap
            { 514, new Entry("TRP") },             // Trap
        };

        // Backwards-compatible single-code API.
        public static string Resolve(int raceId)
        {
            return _byRace.TryGetValue(raceId, out var e) ? e.Neuter : null;
        }

        // Backwards-compatible: first candidate for a given (race, gender).
        public static string ResolveWithGender(int raceId, int gender)
        {
            foreach (var c in ResolveCandidates(raceId, gender)) return c;
            return null;
        }

        // Yields candidate model codes in priority order. Caller tries each
        // against the availableModels dict and stops at the first hit.
        public static IEnumerable<string> ResolveCandidates(int raceId, int gender)
        {
            if (!_byRace.TryGetValue(raceId, out var e)) yield break;

            var primary = gender == 0 ? e.Male
                        : gender == 1 ? e.Female
                        : e.Neuter;

            if (primary != null) yield return primary;

            // Fallbacks — try the other gender codes so a chr zip missing e.g.
            // 'HUF' still resolves via 'HUM'.
            if (e.Male != null && e.Male != primary) yield return e.Male;
            if (e.Female != null && e.Female != primary && e.Female != e.Male) yield return e.Female;
            if (e.Neuter != null && e.Neuter != primary && e.Neuter != e.Male && e.Neuter != e.Female)
                yield return e.Neuter;
        }
    }
}
