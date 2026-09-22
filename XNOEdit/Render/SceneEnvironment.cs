using Solaris;
using XNOEdit.Services;

namespace XNOEdit.Render
{
    public sealed class SceneEnvironment(SlDevice device) : IDisposable
    {
        private readonly record struct EnvironmentMap(string Name, SlTexture Texture, SlTextureIndex Index);

        private SceneConfig _defaultConfig = new();
        private SceneConfig? _sceneConfig;
        private EnvironmentMap? _defaultMap;
        private EnvironmentMap? _sceneMap;

        public SceneConfig Config { get; set; } = new();

        public string EnvMapName => (_sceneMap ?? _defaultMap)?.Name ?? "None";
        public SlTextureIndex EnvMap => (_sceneMap ?? _defaultMap)?.Index ?? SlTextureIndex.NullTextureCube;
        public bool IsOverridden => _sceneConfig != null || _sceneMap != null;

        public void Reset() => Config = _sceneConfig ?? _defaultConfig;

        public void SetDefault(SceneConfig config, LoadedTexture? envMap)
        {
            _defaultConfig = config;
            Replace(ref _defaultMap, envMap);

            if (_sceneConfig == null)
                Config = config;
        }

        public void Override(SceneConfig? config, LoadedTexture? envMap)
        {
            _sceneConfig = config;
            Reset();
            Replace(ref _sceneMap, envMap);
        }

        public void ClearOverride() => Override(null, null);

        private void Replace(ref EnvironmentMap? slot, LoadedTexture? texture)
        {
            if (slot is { } previous)
            {
                device.Tables.Release(previous.Index);
                device.Retire(previous.Texture);
            }

            slot = texture is { } loaded
                ? new EnvironmentMap(loaded.Name, loaded.Texture, device.Tables.Register(loaded.Texture))
                : null;
        }

        public void Dispose()
        {
            Replace(ref _defaultMap, null);
        }
    }
}
