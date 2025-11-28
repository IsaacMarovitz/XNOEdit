using System.Collections.ObjectModel;

namespace XNOEdit
{
    public static class StagesMap
    {
        public static ReadOnlyDictionary<string, string> Stages => _stages.AsReadOnly();
        public static ReadOnlyDictionary<string, string> Bosses => _bosses.AsReadOnly();
        public static ReadOnlyDictionary<string, string> Events => _events.AsReadOnly();

        private static readonly Dictionary<string, string> _stages = new()
        {
            { "Aquatic Base A", "stage_aqa_a" },
            { "Aquatic Base B", "stage_aqa_b" },
            { "Crisis City A", "stage_csc_a" },
            { "Crisis City B", "stage_csc_b" },
            { "Crisis City C", "stage_csc_c" },
            { "Crisis City E", "stage_csc_e" },
            { "Crisis City F", "stage_csc_F" },
            { "Dusty Desert A", "stage_dtd_a" },
            { "Dusty Desert B", "stage_dtd_b" },
            { "Flame Core A", "stage_flc_a" },
            { "Flame Core B", "stage_flc_b" },
            { "Kingdom Valley A", "stage_kdv_a" },
            { "Kingdom Valley B", "stage_kdv_b" },
            { "Kingdom Valley C", "stage_kdv_c" },
            { "Kingdom Valley D", "stage_kdv_d" },
            { "Radical Train A", "stage_rct_a" },
            { "Radical Train B", "stage_rct_b" },
            { "Tropical Jungle A", "stage_tpj_a" },
            { "Tropical Jungle B", "stage_tpj_b" },
            { "Tropical Jungle C", "stage_tpj_c" },
            { "White Acropolis A", "stage_wap_a" },
            { "White Acropolis B", "stage_wap_b" },
            { "Wave Ocean A", "stage_wvo_a" },
            { "Wave Ocean B", "stage_wvo_b" },
            { "Castle Town", "stage_twn_a" },
            { "New City", "stage_twn_b" },
            { "Soleanna Forest", "stage_twn_c" },
            { "Soleanna Circuit", "stage_twn_d" },
        };

        private static readonly Dictionary<string, string> _bosses = new()
        {
            { "Egg-Cerberus Dusty Desert", "stage_boss_dr1_dtd" },
            { "Egg-Cerberus White Acropolis", "stage_boss_dr1_wap" },
            { "Egg-Genesis", "stage_boss_dr2" },
            { "Egg-Wyvern", "stage_boss_dr3" },
            { "Iblis 1", "stage_csc_iblis01" },
            { "Iblis 2", "stage_boss_iblis02" },
            { "Iblis 3", "stage_boss_iblis03" },
            { "Mephiles 1", "stage_boss_mefi01" },
            { "Mephiles 2", "stage_boss_mefi02" },
            { "v.s. Shadow", "stage_boss_rct" },
            { "Solaris", "stage_boss_solaris" },
        };

        private static readonly Dictionary<string, string> _events = new()
        {
            { "Dusty Desert Prison - E0003", "stage_e0003" },
            { "Time Machine Present - E0009", "stage_e0009" },
            { "Time Machine Future - E0010", "stage_e0010" },
            { "Sonic Computer Room - E0012", "stage_e0012" },
            { "Castle Town Eggman's Threat - E0021", "stage_e0021" },
            { "Captive in Egg Carrier - E0022", "stage_e0022" },
            { "Silver Helps Sonic - E0023", "stage_e0023" },
            { "Egg Carrier Crash - E0026", "stage_e0026" },
            { "Egg Carrier Launch Bay - E0028", "stage_e0028" },
            { "Elise Captured at Festival - E0031", "stage_e0031" },
            { "Mephiles Revived - E0104", "stage_e0104" },
            { "Shadow Crisis City Building - E0105", "stage_e0105" },
            { "Shadow Computer Room - E0106", "stage_e0106" },
            { "Eggman's Train - E0120", "stage_e0120" },
            { "Kingdom Valley Castle Past - E0125", "stage_e0125" },
            { "Silver Meets Amy - E0206", "stage_e0206" },
            { "Amy in Eggman's Base - E0214", "stage_e0214" },
            { "Elise Captured at Castle - E0216", "stage_e0216" },
            { "Iblis Sealed in Elise - E0221", "stage_e0221" },
            { "EotW Soleanna - E0304", "stage_e0304" },
        };
    }
}
