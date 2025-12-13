using System.Collections.ObjectModel;

namespace XNOEdit
{
    public static class MissionsMap
    {
        public static ReadOnlyCollection<string> TwnAMissions => _twnAMissions.AsReadOnly();
        public static ReadOnlyCollection<string> TwnBMissions => _twnBMissions.AsReadOnly();
        public static ReadOnlyCollection<string> TwnCMissions => _twnCMissions.AsReadOnly();
        public static ReadOnlyCollection<string> TwnDMissions => _twnDMissions.AsReadOnly();
        public static ReadOnlyCollection<string> EndAMissions => _endAMissions.AsReadOnly();
        public static ReadOnlyCollection<string> WvoAMissions => _wvoAMissions.AsReadOnly();
        public static ReadOnlyCollection<string> WvoBMissions => _wvoBMissions.AsReadOnly();
        public static ReadOnlyCollection<string> DtdAMissions => _dtdAMissions.AsReadOnly();
        public static ReadOnlyCollection<string> DtdBMissions => _dtdBMissions.AsReadOnly();
        public static ReadOnlyCollection<string> CscAMissions => _cscAMissions.AsReadOnly();
        public static ReadOnlyCollection<string> CscBMissions => _cscBMissions.AsReadOnly();
        public static ReadOnlyCollection<string> CscCMissions => _cscCMissions.AsReadOnly();
        public static ReadOnlyCollection<string> CscEMissions => _cscEMissions.AsReadOnly();
        public static ReadOnlyCollection<string> CscFMissions => _cscFMissions.AsReadOnly();
        public static ReadOnlyCollection<string> FlcAMissions => _flcAMissions.AsReadOnly();
        public static ReadOnlyCollection<string> FlcBMissions => _flcBMissions.AsReadOnly();
        public static ReadOnlyCollection<string> FlcCMissions => _flcCMissions.AsReadOnly();
        public static ReadOnlyCollection<string> TpjAMissions => _tpjAMissions.AsReadOnly();
        public static ReadOnlyCollection<string> TpjBMissions => _tpjBMissions.AsReadOnly();
        public static ReadOnlyCollection<string> TpjCMissions => _tpjCMissions.AsReadOnly();
        public static ReadOnlyCollection<string> RctAMissions => _rctAMissions.AsReadOnly();
        public static ReadOnlyCollection<string> RctBMissions => _rctBMissions.AsReadOnly();
        public static ReadOnlyCollection<string> AqaAMissions => _aqaAMissions.AsReadOnly();
        public static ReadOnlyCollection<string> AqaBMissions => _aqaBMissions.AsReadOnly();
        public static ReadOnlyCollection<string> KdvAMissions => _kdvAMissions.AsReadOnly();
        public static ReadOnlyCollection<string> KdvBMissions => _kdvBMissions.AsReadOnly();
        public static ReadOnlyCollection<string> KdvCMissions => _kdvCMissions.AsReadOnly();
        public static ReadOnlyCollection<string> KdvDMissions => _kdvDMissions.AsReadOnly();
        public static ReadOnlyCollection<string> WapAMissions => _wapAMissions.AsReadOnly();
        public static ReadOnlyCollection<string> WapBMissions => _wapBMissions.AsReadOnly();
        public static ReadOnlyCollection<string> Dr1DtdMissions => _dr1DtdMissions.AsReadOnly();
        public static ReadOnlyCollection<string> Dr1WapMissions => _dr1WapMissions.AsReadOnly();
        public static ReadOnlyCollection<string> Dr2Missions => _dr2Missions.AsReadOnly();
        public static ReadOnlyCollection<string> Dr3Missions => _dr3Missions.AsReadOnly();
        public static ReadOnlyCollection<string> ShadowVsSilverMissions => _shadowVsSilverMissions.AsReadOnly();
        public static ReadOnlyCollection<string> FirstIblisMissions => _firstIblisMissions.AsReadOnly();
        public static ReadOnlyCollection<string> SecondIblisMissions => _secondIblisMissions.AsReadOnly();
        public static ReadOnlyCollection<string> ThirdIblisMissions => _thirdIblisMissions.AsReadOnly();
        public static ReadOnlyCollection<string> FirstMefMissions => _firstMefMissions.AsReadOnly();
        public static ReadOnlyCollection<string> SecondMefMissions => _secondMefMissions.AsReadOnly();
        public static ReadOnlyCollection<string> SolarisMissions => _solarisMissions.AsReadOnly();

        private static readonly string[] _twnAMissions =
        [
            "set_mission_0001",
            "set_mission_0004",
            "set_mission_0003a",
            "set_mission_0003a_01",
            "set_mission_0004a_01",
            "set_mission_0004a_02",
            "set_mission_0010a_01",
            "set_mission_0012a",
            "set_mission_0014a",
            "set_mission_0015a",
            "set_mission_0105a",
            "set_mission_0107a",
            "set_mission_0108a01",
            "set_mission_0109a",
            "set_mission_0110a",
            "set_mission_0202a",
            "set_mission_0202a_02",
            "set_mission_0203a",
            "set_mission_0208a",
            "set_mission_0211a_01",
            "set_mission_0211a_02",
            "set_mission_0212a",
            "set_mission_1001_00",
            "set_mission_1001_01",
            "set_mission_1003",
            "set_mission_1003_00",
            "set_mission_1004_00",
            "set_mission_1004_01",
            "set_mission_1005_00",
            "set_mission_1005_01",
            "set_mission_1006_00",
            "set_mission_1006_01",
            "set_mission_1006_02",
            "set_mission_1007_00",
            "set_mission_1007_01",
            "set_mission_1008_00",
            "set_mission_1008_01",
            "set_mission_1010_00",
            "set_mission_1010_01",
            "set_mission_1011_00",
            "set_mission_1011_01",
            "set_mission_1014_00",
            "set_mission_1014_q1",
            "set_mission_1014_q2",
            "set_mission_1014_q3",
            "set_mission_1014_q4",
            "set_mission_1014_q5",
            "set_mission_1014_a1",
            "set_mission_1014_a2",
            "set_mission_1014_a3",
            "set_mission_1014_a4",
            "set_mission_1014_a5",
            "set_mission_1017",
            "set_mission_1033_03",
            "set_mission_1035_00",
            "set_mission_1035_01",
            "set_mission_1106_00",
            "set_mission_1106_01",
            "set_mission_1114_00",
            "set_mission_1114_01",
            "set_mission_1114_02",
            "set_mission_1114_03",
            "set_mission_1115_00",
            "set_mission_1115_01",
            "set_mission_1116_00",
            "set_mission_1116_01",
            "set_mission_1117_00",
            "set_mission_1117_01",
            "set_mission_1119_00",
            "set_mission_1119_01",
            "set_mission_1120_00",
            "set_mission_1120_01",
            "set_mission_1121_00",
            "set_mission_1121_01",
            "set_mission_1131_00",
            "set_mission_1131_01",
            "set_mission_1132_00",
            "set_mission_1132_01",
            "set_mission_1220",
            "set_mission_1204_00",
            "set_mission_1204_01",
            "set_mission_1208_00",
            "set_mission_1208_01",
            "set_mission_1208_02",
            "set_mission_1209",
            "set_mission_1210_00",
            "set_mission_1210_01",
            "set_mission_1211_00",
            "set_mission_1211_01",
            "set_mission_1212_00",
            "set_mission_1212_01",
            "set_mission_1214_00",
            "set_mission_1214_01",
            "set_mission_1215_00",
            "set_mission_1215_01",
            "set_mission_1215_02",
            "set_mission_1216_00",
            "set_mission_1216_01",
            "set_mission_1217_00",
            "set_mission_1217_01",
            "set_mission_1218_00",
            "set_mission_1218_01",
            "set_mission_1220",
            "set_mission_1230",
            "set_mission_1231_00",
            "set_mission_1231_01",
            "set_mission_1236_00",
            "set_mission_1236_01",
            "set_mission_1301",
            "set_mission_1302",
            "set_sonic_vs_silver",
            "set_silver_vs_sonic",
        ];

        private static readonly string[] _twnBMissions =
        [
            "set_twn_b_060228",
            "set_mission_0004b",
            "set_mission_0004b_01",
            "set_mission_0004b_02",
            "set_mission_0004b_03",
            "set_mission_0007b",
            "set_mission_0010b",
            "set_mission_0012b",
            "set_mission_0014b",
            "set_mission_0015b",
            "set_mission_0101b",
            "set_mission_0102b",
            "set_mission_0105b",
            "set_mission_0105b_02",
            "set_mission_0108b01",
            "set_mission_0109b",
            "set_mission_0110b",
            "set_mission_0203b01",
            "set_mission_0204b_01",
            "set_mission_0210b",
            "set_mission_0210b_01",
            "set_mission_0212b",
            "set_mission_1002",
            "set_mission_1012_00",
            "set_mission_1013_00",
            "set_mission_1013_01",
            "set_mission_1013_02",
            "set_mission_1015_00",
            "set_mission_1015_01",
            "set_mission_1016_00",
            "set_mission_1016_01",
            "set_mission_1018_00",
            "set_mission_1018_01",
            "set_mission_1019_00",
            "set_mission_1019_01",
            "set_mission_1020_00",
            "set_mission_1020_01",
            "set_mission_1021_00",
            "set_mission_1021_01",
            "set_mission_1024",
            "set_mission_1024_00",
            "set_mission_1025",
            "set_mission_1026_00",
            "set_mission_1026_01",
            "set_mission_1026_02",
            "set_mission_1027",
            "set_mission_1033_00",
            "set_mission_1033_01",
            "set_mission_1101_00",
            "set_mission_1101_01",
            "set_mission_1101_00_missionman",
            "set_mission_1102",
            "set_mission_1104",
            "set_mission_1104_00",
            "set_mission_1104_01",
            "set_mission_1107_00",
            "set_mission_1107_01",
            "set_mission_1107_02",
            "set_mission_1107_03",
            "set_mission_1107_04",
            "set_mission_1107_05",
            "set_mission_1108_00",
            "set_mission_1108_02",
            "set_mission_1109_00",
            "set_mission_1110_00",
            "set_mission_1110_01",
            "set_mission_1112_00",
            "set_mission_1112_01",
            "set_mission_1112_02",
            "set_mission_1112_03",
            "set_mission_1113_00",
            "set_mission_1113_01",
            "set_mission_1118_00",
            "set_mission_1118_01",
            "set_mission_1118_02",
            "set_mission_1118_03",
            "set_mission_1118_04",
            "set_mission_1118_05",
            "set_mission_1118_06",
            "set_mission_1122_00",
            "set_mission_1122_01",
            "set_mission_1123_00",
            "set_mission_1123_01",
            "set_mission_1129",
            "set_mission_1129_00",
            "set_mission_1130_00",
            "set_mission_1134_00",
            "set_mission_1134_01",
            "set_mission_1135_00",
            "set_mission_1135_01",
            "set_mission_1202_00",
            "set_mission_1202_01",
            "set_mission_1202_02",
            "set_mission_1213_00",
            "set_mission_1213_01",
            "set_mission_1219_00",
            "set_mission_1219_01",
            "set_mission_1223_00",
            "set_mission_1223_01",
            "set_mission_1224_00",
            "set_mission_1224_01",
            "set_mission_1225_00",
            "set_mission_1225_01",
            "set_mission_1226_00",
            "set_mission_1226_01",
            "set_mission_1229_00",
            "set_mission_1229_01",
            "set_mission_1233_00",
            "set_mission_1233_01",
            "set_mission_1239_00",
            "set_mission_1239_01",
            "set_mission_1240",
            "set_mission_1240_00"
        ];

        private static readonly string[] _twnCMissions =
        [
            "set_twn_c",
            "set_mission_0008c_01",
            "set_mission_0010c",
            "set_mission_0012c",
            "set_mission_0012c_01",
            "set_mission_0012c_02",
            "set_mission_0014c",
            "set_mission_0015c",
            "set_mission_0101c",
            "set_mission_0110c",
            "set_mission_0201c",
            "set_mission_0202c",
            "set_mission_0211c",
            "set_mission_0211c_01",
            "set_mission_0211c_02",
            "set_mission_0212c",
            "set_mission_1022_00",
            "set_mission_1022_01",
            "set_mission_1023_00",
            "set_mission_1023_01",
            "set_mission_1029",
            "set_mission_1029_00",
            "set_mission_1029_01",
            "set_mission_1030_00",
            "set_mission_1030_01",
            "set_mission_1031_00",
            "set_mission_1031_01",
            "set_mission_1032_00",
            "set_mission_1032_01",
            "set_mission_1033_02",
            "set_mission_1034_kn",
            "set_mission_1103_00",
            "set_mission_1103_01",
            "set_mission_1124",
            "set_mission_1124_01",
            "set_mission_1124_02",
            "set_mission_1125_00",
            "set_mission_1125_01",
            "set_mission_1126_00",
            "set_mission_1126_01",
            "set_mission_1127_00",
            "set_mission_1127_01",
            "set_mission_1128_00",
            "set_mission_1128_01",
            "set_mission_1201_00",
            "set_mission_1201_01",
            "set_mission_1203_00",
            "set_mission_1203_01",
            "set_mission_1205_00",
            "set_mission_1205_01",
            "set_mission_1206_01",
            "set_mission_1207_00",
            "set_mission_1207_01",
            "set_mission_1221",
            "set_mission_1221_00",
            "set_mission_1227_00",
            "set_mission_1227_01",
            "set_mission_1228_00",
            "set_mission_1228_01",
            "set_mission_1232_00",
            "set_mission_1232_01",
            "set_mission_1232_02",
            "set_mission_1232_03",
            "set_mission_1234_00",
            "set_mission_1234_01",
            "set_mission_1235_00",
            "set_mission_1235_01",
            "set_mission_1237_00",
            "set_mission_1237_01",
            "set_mission_1238_00",
            "set_mission_1238_01"
        ];

        private static readonly string[] _twnDMissions =
        [
            "set_mission_1012_01",
            "set_mission_1108_01",
            "set_mission_1109_01",
            "set_mission_1130_01"
        ];

        private static readonly string[] _endAMissions =
        [
            "set_end_a_sonic",
        ];

        private static readonly string[] _wvoAMissions =
        [
            "set_wvoA_sonic",
            "set_wvoA_sonic_h",
            "set_wvoA_tails",
            "set_wvoA_tails_h",
            "set_wvoA_shadow",
            "set_wvoA_shadow_h",
            "set_wvoA_silver",
            "set_wvoA_silver_h",
            "set_end_e_sonic",
            "set_wvo_a_tag"
        ];

        private static readonly string[] _wvoBMissions =
        [
            "set_wvoB_sonic",
            "set_wvoB_sonic_h",
            "set_wvoB_shadow",
            "set_wvoB_shadow_h",
        ];

        private static readonly string[] _dtdAMissions =
        [
            "set_dtd_a_sonic",
            "set_dtd_a_sonic_h",
            "set_dtd_a_shadow",
            "set_dtd_a_shadow_h",
            "set_end_d_sonic"
        ];

        private static readonly string[] _dtdBMissions =
        [
            "set_dtd_b_shadow",
            "set_dtd_b_shadow_h",
            "set_dtd_b_silver",
            "set_dtd_a_silver_h",
            "set_dtd_b_tag"
        ];

        private static readonly string[] _cscAMissions =
        [
            "set_cscA_sonic",
            "set_cscA_sonic_h",
            "set_cscA_shadow",
            "set_cscA_shadow_h"
        ];

        private static readonly string[] _cscBMissions =
        [
            "set_cscB_sonic",
            "set_cscB_sonic_h",
            "set_cscB_shadow",
            "set_cscB_shadow_h",
            "set_cscB_silver",
            "set_cscB_silver_h",
            "set_csc_b_tag"
        ];

        private static readonly string[] _cscCMissions =
        [
            "set_cscC_sonic",
            "set_cscC_sonic_h",
            "set_cscC_shadow",
            "set_cscC_shadow_h"
        ];

        private static readonly string[] _cscEMissions =
        [
            "set_cscE_sonic",
            "set_cscE_sonic_h"
        ];

        private static readonly string[] _cscFMissions =
        [
            "set_cscF_shadow",
            "set_cscF_shadow_h",
            "set_cscF1_silver",
            "set_cscF1_silver_h",
            "set_cscF2_silver",
            "set_cscF2_silver_h",
            "set_end_a_sonic"
        ];

        private static readonly string[] _flcAMissions =
        [
            "set_flc_a_sonic",
            "set_flc_a_sonic_h",
            "set_flc_a_shadow",
            "set_flc_a_shadow_h",
            "set_flc_a_silver",
            "set_flc_a_silver_h",
            "set_end_b_sonic",
            "set_flc_a_tag"
        ];

        private static readonly string[] _flcBMissions =
        [
            "set_flc_b_sonic",
            "set_flc_b_sonic_h",
            "set_flc_b_shadow",
            "set_flc_b_shadow_h",
            "set_flc_b_silver",
            "set_flc_b_silver_h"
        ];

        private static readonly string[] _flcCMissions =
        [
            "set_flc_b_silver",
        ];

        private static readonly string[] _tpjAMissions =
        [
            "set_tpjA_sonic",
            "set_tpjA_sonic_h"
        ];

        private static readonly string[] _tpjBMissions =
        [
            "set_tpjB_sonic",
            "set_tpjB_sonic_h"
        ];

        private static readonly string[] _tpjCMissions =
        [
            "set_tpjC_rouge",
            "set_tpjC_rouge_h",
            "set_tpjC_silver",
            "set_tpjC_silver_h",
            "set_end_c_sonic"
        ];

        private static readonly string[] _rctAMissions =
        [
            "set_rctA_sonic",
            "set_rctA_sonic_h",
            "set_rctA_shadow",
            "set_rctA_shadow_h",
            "set_rctA_silver",
            "set_rctA_silver_h"
        ];

        private static readonly string[] _rctBMissions =
        [
            "set_rctB_sonic",
            "set_rctB_sonic_h",
            "set_rctB_shadow",
            "set_rctB_shadow_h"
        ];

        private static readonly string[] _aqaAMissions =
        [
            "set_aqaA_sonic",
            "set_aqaA_sonic_h",
            "set_aqaA_shadow",
            "set_aqaA_shadow_h",
            "set_aqaA_silver",
            "set_aqaA_silver_h",
            "set_aqa_a_tag"
        ];

        private static readonly string[] _aqaBMissions =
        [
            "set_aqaB_sonic",
            "set_aqaB_sonic_h",
            "set_aqaB_shadow",
            "set_aqaB_shadow_h",
            "set_aqaB_silver",
            "set_aqaB_silver_h"
        ];

        private static readonly string[] _kdvAMissions =
        [
            "set_kdv_a_sonic",
            "set_kdv_a_sonic_h",
            "set_kdv_a_shadow",
            "set_kdv_a_shadow_h",
            "set_end_g_sonic",
            "set_kdv_a_tag"
        ];

        private static readonly string[] _kdvBMissions =
        [
            "set_kdv_b_sonic",
            "set_kdv_b_sonic_h",
            "set_kdv_b_shadow",
            "set_kdv_b_shadow_h",
            "set_kdv_b_silver",
            "set_kdv_b_silver_h"
        ];

        private static readonly string[] _kdvCMissions =
        [
            "set_kdv_c_sonic",
            "set_kdv_c_sonic_h"
        ];

        private static readonly string[] _kdvDMissions =
        [
            "set_kdv_d_sonic",
            "set_kdv_d_sonic_h",
            "set_kdv_d_shadow",
            "set_kdv_d_shadow_h",
            "set_kdv_d_silver",
            "set_kdv_d_silver_h"
        ];

        private static readonly string[] _wapAMissions =
        [
            "set_wap_a_sonic",
            "set_wap_a_sonic_h",
            "set_wap_a_shadow",
            "set_wap_a_shadow_h",
            "set_wap_a_silver",
            "set_wap_a_silver_h",
        ];

        private static readonly string[] _wapBMissions =
        [
            "set_wap_b_sonic",
            "set_wap_b_sonic_h",
            "set_wap_b_shadow",
            "set_wap_b_shadow_h",
            "set_wap_b_silver",
            "set_wap_b_silver_h",
            "set_end_f_sonic",
            "set_wap_b_tag"
        ];

        private static readonly string[] _dr1DtdMissions =
        [
            "set_eCerberus_sonic",
        ];

        private static readonly string[] _dr1WapMissions =
        [
            "set_eCerberus_shadow",
            "set_eGenesis_silver"
        ];

        private static readonly string[] _dr2Missions =
        [
            "set_eGenesis_sonic"
        ];

        private static readonly string[] _dr3Missions =
        [
            "set_ewyvern_sonic"
        ];

        private static readonly string[] _shadowVsSilverMissions =
        [
            "set_shadow_vs_silver",
            "set_silver_vs_shadow"
        ];

        private static readonly string[] _firstIblisMissions =
        [
            "set_iblis01_silver",
        ];

        private static readonly string[] _secondIblisMissions =
        [
            "set_secondiblis_sonic",
            "set_secondiblis_shadow"
        ];

        private static readonly string[] _thirdIblisMissions =
        [
            "set_thirdiblis_silver",
        ];

        private static readonly string[] _firstMefMissions =
        [
            "set_firstmefiress_shadow",
            "set_firstmefiress_omega"
        ];

        private static readonly string[] _secondMefMissions =
        [
            "set_secondmefiress_shadow",
        ];

        private static readonly string[] _solarisMissions =
        [
            "set_solaris01_super3",
            "set_solaris02_super3"
        ];
    }
}
