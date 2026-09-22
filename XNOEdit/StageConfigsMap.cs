using System.Numerics;
using Lua;
using Lua.Standard;
using Marathon.Formats.Script.Lua;
using XNOEdit.Logging;

namespace XNOEdit
{
    public readonly record struct SceneLight(Vector4 Color, Vector3 Position, Vector3 Target);
    public readonly record struct SceneOls(Vector4 SunColor, Vector4 BRay, Vector4 BMie, float G);

    // Defaults taken from scene_wvo_a.lub
    public readonly struct SceneConfig()
    {
        public Vector4 Ambient { get; } = new(0.48f, 0.49f, 0.5f, 1.1f);

        public SceneLight Main { get; } = new()
        {
            Color = new Vector4(0.92f, 0.92f, 0.92f, 0.85f),
            Position = new Vector3(0.316847f, 0.127879f, 0.939816f),
            Target = Vector3.Zero
        };

        public SceneLight Sub { get; } = new()
        {
            Color = new Vector4(0.18f, 0.28f, 0.35f, 1.0f),
            Position = new Vector3(0.470259f, -0.813585f, -0.341958f),
            Target = Vector3.Zero
        };

        public SceneOls Ols { get; } = new()
        {
            SunColor = new Vector4(1.0f, 1.0f, 0.92f, 13.0f),
            BRay = new Vector4(0.07f, 0.09f, 0.14f, 1.0E-4f),
            BMie = new Vector4(0.06f, 0.06f, 0.05f, 1.0E-4f),
            G = 0.99f
        };

        public string EnvMap { get; } = "stage/wvo/a/wvo_envmap.dds";

        public SceneConfig(Vector4 ambient,  SceneLight main, SceneLight sub, SceneOls ols,  string envMap) : this()
        {
            Ambient = ambient;
            Main = main;
            Sub = sub;
            Ols = ols;
            EnvMap = envMap;
        }
    }

    public static class StageConfigsMap
    {
        public const string ConfigDir = "xenon/scripts/stage/";

        public static async Task<SceneConfig?> GetSceneConfig(string sceneName)
        {
            if (_stages.TryGetValue(sceneName, out var sceneConfig)
                ||  _bosses.TryGetValue(sceneName, out sceneConfig)
                || _events.TryGetValue(sceneName, out sceneConfig))
            {
                var configPath = ConfigDir + sceneConfig;
                var configFile = ArcFiles.ScriptsArc.GetFile(configPath);

                var binary = new LuaBinary(configFile.Decompress().Open());
                var luaSource = binary.Decompile();

                var state = LuaState.Create();

                state.OpenBasicLibrary();
                await state.DoStringAsync("script = setmetatable({}, { __index = function() return function() end end })",
                    "scene_stub");
                await state.DoStringAsync(luaSource);

                if (!state.Environment["Light"].TryRead<LuaTable>(out var lights))
                    return null;

                state.Environment["OLS"].TryRead<LuaTable>(out var ols);
                state.Environment["EnvMap"].TryRead<LuaTable>(out var envMap);

                return new SceneConfig(
                    ToVector4(Field(lights, "Ambient", "Color")),
                    ReadLight(Field(lights, "Main")),
                    ReadLight(Field(lights, "Sub")),
                    new SceneOls(
                        ToVector4(Field(ols, "SunColor")),
                        ToVector4(Field(ols, "BRay"), 0.0f),
                        ToVector4(Field(ols, "BMie"), 0.0f),
                        Number(ols, "G")),
                    Text(envMap, "FileName"));
            }

            Logger.Warning?.PrintMsg(LogClass.Application, $"Unknown scene name {sceneName}");
            return null;
        }

        private static SceneLight ReadLight(LuaTable? light)
        {
            var direction = Field(light, "Direction_3dsmax");

            return new SceneLight(
                ToVector4(Field(light, "Color")),
                ToVector3(Field(direction, "Position")),
                ToVector3(Field(direction, "Target")));
        }

        private static LuaTable? Field(LuaTable? table, string key) =>
            table != null && table[key].TryRead<LuaTable>(out var value) ? value : null;

        private static LuaTable? Field(LuaTable? table, string key, string nested) =>
            Field(Field(table, key), nested);

        private static string Text(LuaTable? table, string key) =>
            table != null && table[key].TryRead<string>(out var value) ? value : string.Empty;

        private static float Number(LuaTable? table, string key) =>
            table != null && table[key].TryRead<double>(out var value) ? (float)value : 0.0f;

        /// <summary>Lua arrays are one-based.</summary>
        private static float Item(LuaTable? table, int index, float fallback = 0.0f) =>
            table != null && table[index].TryRead<double>(out var value) ? (float)value : fallback;

        private static Vector3 ToVector3(LuaTable? table) =>
            new(Item(table, 1), Item(table, 2), Item(table, 3));

        private static Vector4 ToVector4(LuaTable? table, float w = 1.0f) =>
            new(Item(table, 1), Item(table, 2), Item(table, 3), Item(table, 4, w));

        private static readonly Dictionary<string, string> _stages = new()
        {
            { "stage_aqa_a", "aqa/scene_aqa_a.lub" },
            { "stage_aqa_b", "aqa/scene_aqa_b.lub" },
            { "stage_csc_a",  "csc/scene_csc_a.lub" },
            { "stage_csc_b",  "csc/scene_csc_b.lub" },
            { "stage_csc_c",  "csc/scene_csc_c.lub" },
            { "stage_csc_e",  "csc/scene_csc_e.lub" },
            { "stage_csc_F",  "csc/scene_csc_f.lub" },
            { "stage_dtd_a",  "dtd/scene_dtd_a_sonic.lub" },
            { "stage_dtd_b",  "dtd/scene_dtd_b_sonic.lub" },
            // Scene config file exists, stage does not
            { "stage_dtd_c",  "dtd/scene_dtd_c_shadow.lub" },
            { "stage_flc_a",  "flc/scene_flc_a.lub" },
            { "stage_flc_b",  "flc/scene_flc_b.lub" },
            { "stage_kdv_a",  "kdv/scene_kdv_a.lub" },
            { "stage_kdv_b",  "kdv/scene_kdv_b.lub" },
            { "stage_kdv_c",  "kdv/scene_kdv_c.lub" },
            { "stage_kdv_d",  "kdv/scene_kdv_d.lub" },
            { "stage_rct_a",  "rct/scene_rct_a.lub" },
            { "stage_rct_b",  "rct/scene_rct_b.lub" },
            { "stage_tpj_a",  "tpj/scene_tpj_a.lub" },
            { "stage_tpj_b",  "tpj/scene_tpj_b.lub" },
            { "stage_tpj_c",  "tpj/scene_tpj_c.lub" },
            { "stage_wap_a",  "wap/scene_wap_a.lub" },
            { "stage_wap_b",  "wap/scene_wap_b.lub" },
            { "stage_wvo_a",  "wvo/scene_wvo_a.lub" },
            { "stage_wvo_b",  "wvo/scene_wvo_b.lub" },
            { "stage_twn_a",  "twn/scene_twn_a.lub" },
            { "stage_twn_b",  "twn/scene_twn_b.lub" },
            { "stage_twn_c",  "twn/scene_twn_c.lub" },
            { "stage_twn_d",  "twn/scene_twn_d.lub" },
        };

        private static readonly Dictionary<string, string> _bosses = new()
        {
            { "stage_boss_dr1_dtd", "boss/scene_eCerberus_sonic.lub" },
            { "stage_boss_dr1_wap", "boss/scene_eCerberus_shadow.lub" },
            // Silver has his own, but it's identical to eCerberus_shadow
            { "stage_boss_dr2", "boss/scene_eGenesis_sonic.lub" },
            { "stage_boss_dr3", "boss/scene_eWyvern_sonic.lub" },
            { "stage_csc_iblis01", "boss/scene_firstiblis.lub" },
            { "stage_boss_iblis02", "boss/scene_secondiblis.lub" },
            { "stage_boss_iblis03", "boss/scene_thirdiblis.lub" },
            { "stage_boss_mefi01", "boss/scene_firstmefiress.lub" },
            { "stage_boss_mefi02", "boss/scene_secondmefiress.lub" },
            { "stage_boss_rct", "boss/scene_shadow_vs_silver.lub" },
            { "stage_boss_solaris", "boss/scene_solaris_super3.lub" },
        };

        private static readonly Dictionary<string, string> _events = new()
        {
            // Events without their own stages also have their own
            // scene config files. They are omitted here.
            { "stage_e0003", "event/scene_e0003.lub" },
            { "stage_e0009", "event/scene_e0009.lub" },
            { "stage_e0010", "event/scene_e0010.lub" },
            { "stage_e0012", "event/scene_e0012.lub" },
            { "stage_e0021", "event/scene_e0021.lub" },
            { "stage_e0022", "event/scene_e0022.lub" },
            { "stage_e0023", "event/scene_e0023.lub" },
            { "stage_e0026", "event/scene_e0026.lub" },
            { "stage_e0028", "event/scene_e0028.lub" },
            { "stage_e0031", "event/scene_e0031.lub" },
            { "stage_e0104", "event/scene_e0104.lub" },
            { "stage_e0105", "event/scene_e0105.lub" },
            { "stage_e0106", "event/scene_e0106.lub" },
            { "stage_e0120", "event/scene_e0120.lub" },
            { "stage_e0125", "event/scene_e0125.lub" },
            // TODO: Check if this is right
            { "stage_e0206", "event/scene_e0031.lub" },
            { "stage_e0214", "event/scene_e0214.lub" },
            { "stage_e0216", "event/scene_e0216.lub" },
            { "stage_e0221", "event/scene_e0221.lub" },
            { "stage_e0304", "event/scene_e0304.lub" },
        };
    }
}
