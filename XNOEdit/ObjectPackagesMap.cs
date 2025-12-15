using System.Collections.ObjectModel;

namespace XNOEdit
{
    public static class ObjectPackagesMap
    {
        public static PackageGroup CmnObjectPackageGroup => new(CmnObjectPackages, "cmn");
        public static PackageGroup AqaObjectPackageGroup => new(AqaObjectPackages, "aqa");
        public static PackageGroup CscObjectPackageGroup => new(CscObjectPackages, "csc");
        public static PackageGroup DtdObjectPackageGroup => new(DtdObjectPackages, "dtd");
        public static PackageGroup EndObjectPackageGroup => new(EndObjectPackages, "end");
        public static PackageGroup FlcObjectPackageGroup => new(FlcObjectPackages, "flc");
        public static PackageGroup KdvObjectPackageGroup => new(KdvObjectPackages, "kdv");
        public static PackageGroup RctObjectPackageGroup => new(RctObjectPackages, "rct");
        public static PackageGroup TpjObjectPackageGroup => new(TpjObjectPackages, "tpj");
        public static PackageGroup TwnObjectPackageGroup => new(TwnObjectPackages, "twn");
        public static PackageGroup WvoObjectPackageGroup => new(WvoObjectPackages, "wvo");
        public static PackageGroup WapObjectPackageGroup => new(WapObjectPackages, "wap");
        public static ReadOnlyCollection<string> GizmoTypes => _gizmoTypes.AsReadOnly();

        public static readonly PackageGroup[] All;

        static ObjectPackagesMap()
        {
            All =
            [
                CmnObjectPackageGroup,
                AqaObjectPackageGroup,
                CscObjectPackageGroup,
                DtdObjectPackageGroup,
                EndObjectPackageGroup,
                FlcObjectPackageGroup,
                KdvObjectPackageGroup,
                RctObjectPackageGroup,
                TpjObjectPackageGroup,
                TwnObjectPackageGroup,
                WvoObjectPackageGroup,
                WapObjectPackageGroup
            ];
        }

        private static readonly Dictionary<string, string> CmnObjectPackages = new()
        {
            { "common_cage", "cage" },
            { "common_chaosemerald", "chaosemerald"},
            { "dashpanel", "dashpanel" },
            { "common_dashring", "dashring" },
            { "goalring", "goalring" },
            { "common_guillotine", "guillotine" },
            { "common_hint", "hint" },
            { "itemboxa", "itembox" },
            { "itemboxg", "itembox" },
            { "itembox_next", "itembox" },
            { "common_jumpboard", "jumpboard" },
            { "jumppanel", "jumppanel" },
            { "common_key", "key" },
            { "common_laser", "laser" },
            { "common_lensflare", "lensflare" },
            { "pole", "pole" },
            { "common_rainbowring", "rainbowring" },
            { "ring", "ring" },
            { "savepoint", "savepoint" },
            { "spring", "spring" },
            { "spring_twn", "spring" },
            { "common_switch", "switch" },
            { "common_thorn", "thorn" },
            { "updownreel", "updownreel" },
            { "widespring", "widespring" }
        };

        private static readonly Dictionary<string, string> AqaObjectPackages = new()
        {
            { "aqa_door", "aqa_door" },
            { "aqa_glass_blue", "aqa_glass_blue" },
            { "aqa_glass_red", "aqa_glass_red" },
            { "aqa_lamp", "aqa_lamp" },
            { "aqa_launcher", "aqa_launcher" },
            { "aqa_magnet", "aqa_magnet" }
        };

        private static readonly Dictionary<string, string> CscObjectPackages = new()
        {
            { "ironspring", "ironspring" }
        };

        private static readonly Dictionary<string, string> DtdObjectPackages = new()
        {
            { "dtd_billiard", "billiard" },
            { "dtd_door", "dtddoor" },
            { "dtd_movingfloor", "movingfloor" },
            { "dtd_pillar", "pillar" },
            { "dtd_pillar_eagle", "pillar_eagle" },
            { "dtd_sandwave", "sandwave" }
        };

        private static readonly Dictionary<string, string> EndObjectPackages = new()
        {
            { "end_inputwarp", "inputwarp" },
            { "end_outputwarp", "outputwarp" },
            { "end_soleannaswitch", "soleannaswitch" }
        };

        private static readonly Dictionary<string, string> FlcObjectPackages = new()
        {
            { "crater", "crater" },
            { "flc_volcanicbomb", "flamecore_volcanicbomb" },
            { "flc_flamecore", "flamecore" },
            { "flamesequence", "flamesequence" },
            { "flamesingle", "flamesingle" },
            { "flc_door", "flc_door" },
            { "freezedmantle", "freezedmantle" },
            { "inclinedstonebridge", "inclinedstonebridge" }
        };

        private static readonly Dictionary<string, string> KdvObjectPackages = new()
        {
            // TODO: Double check these two
            { "brokenstairs_right", "brokenstairs" },
            { "brokenstairs_left", "brokenstairs2" },
            { "brokentower", "brokentower" },
            { "kdv_decalog", "decalog" },
            { "eagle", "eagle" },
            // TODO: Double check these two
            { "espstairs_right", "espstairs1" },
            { "espstairs_left", "espstairs2" },
            { "gate", "gate" },
            { "inclinedbridge", "inclinedbridge" },
            { "kdv_door", "kdv_door" },
            { "pendulum", "pendulum" },
            { "kdv_rainbow", "rainbow" },
            { "robustdoor", "robustdoor" },
            { "rope", "rope" },
            { "scaffold", "scaffold" },
            { "windroad", "windroad" },
            { "windswitch", "windswitch" }
        };

        private static readonly Dictionary<string, string> RctObjectPackages = new()
        {
            { "eggman_train", "eggman_train" },
            { "freight_train", "freight_train" },
            { "normal_train", "normal_train" },
            { "rct_belt", "rct_belt" },
            { "rct_door", "rct_door" },
            { "rct_seesaw", "rct_seesaw" },
            { "rct_train", "rct_train" }
        };

        private static readonly Dictionary<string, string> TpjObjectPackages = new()
        {
            { "bungee", "bungee" },
            { "espswing", "esp_swing" },
            { "fruit", "fruit" },
            { "hangingrock", "hangingrock" },
            { "lotus", "lotus" },
            { "tarzan",  "tarzan" },
            { "turtle",  "turtle" }
        };

        private static readonly Dictionary<string, string> TwnObjectPackages = new()
        {
            { "twn_door", "twn_door" },
            { "gondola", "twn_gondola" },
            { "bell", "twn_obj_bell" },
            { "medal_of_royal_bronze", "twn_obj_bronzemdl" },
            { "medal_of_royal_silver", "twn_obj_silvermdl" },
            { "candlestick", "twn_obj_candlestick" },
            { "kingdomcrest", "twn_obj_crest" },
            { "darkness", "twn_obj_darkness" },
            { "disk", "twn_obj_disk" },
            { "gliderope", "twn_obj_gliderope" },
            { "glidewire", "twn_obj_glidewire" },
            { "passring", "twn_obj_passring" },
            { "present", "twn_obj_present" },
            { "shopTV", "twn_obj_shopTV" },
            { "trial_post", "twn_obj_trialpillar" },
            { "venthole", "twn_obj_venthole" },
            { "warpgate", "twn_warpgate" }
        };

        private static readonly Dictionary<string, string> WvoObjectPackages = new()
        {
            { "wvo_battleship", "battleship" },
            { "wvo_jumpsplinter", "jumpsplinter" },
            { "wvo_orca", "orca" },
            { "wvo_revolvingnet", "revolvingnet" },
            { "wvo_doorA", "wvodoorA" },
            { "wvo_doorB", "wvodoorB" }
        };

        private static readonly Dictionary<string, string> WapObjectPackages = new()
        {
            { "wap_brokensnowball", "brokensnowball" },
            { "wap_conifer", "conifer" },
            { "wap_pathsnowball", "pathsnowball" },
            { "wap_searchlight", "searchlight" },
            { "wap_snow", "snow" },
            { "wap_door", "wapdoor" }
        };

        // These do not have a visual representation
        // They should be shown as gizmos in a later revision
        private static readonly string[] _gizmoTypes =
        [
            // TODO: MAB Editing
            "particle",
            "wvo_waterslider",
            "chainjump",
            "cameraeventbox",
            "cameraeventcylinder",
            "eventbox",
            "player_start2",
            "player_goal",
            "amigo_collision",
            "pointsample",
            "positionSample",
            "common_stopplayercollision",
            "common_water_collision",
            "common_hint_collision",
            "common_windcollision_box",
            "impulsesphere",
            "snowboardjump"
        ];
    }

    public struct PackageGroup
    {
        public readonly ReadOnlyDictionary<string, string> ObjectPackages;
        public readonly string Folder;

        public PackageGroup(Dictionary<string, string> objectPackages, string folder)
        {
            ObjectPackages = objectPackages.AsReadOnly();
            Folder = folder;
        }
    }
}
