using System.Text.Json;
using System.Text.Json.Serialization;

namespace XNOEdit
{
    public class ConfigurationData
    {
        public string ShaderArcPath { get; set; }
    }

    [JsonSourceGenerationOptions(WriteIndented = true)]
    [JsonSerializable(typeof(ConfigurationData))]
    [JsonSerializable(typeof(bool))]
    [JsonSerializable(typeof(int))]
    internal partial class SourceGenerationContext : JsonSerializerContext;

    public class Configuration
    {
        private static readonly Lazy<Configuration> Lazy = new(() => new Configuration());
        private static Configuration Instance => Lazy.Value;

        public static string ShaderArcPath
        {
            get => Instance._data.ShaderArcPath;
            set
            {
                Instance._data.ShaderArcPath = value;
                Instance.Save();
            }
        }

        private ConfigurationData _data;
        private const string DefaultBaseDir = "XNOEdit";
        private const string DefaultConfigFile = "config.json";
        private readonly string _configPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), DefaultBaseDir);

        public Configuration()
        {
            Load();
        }

        private void Load()
        {
            if (!Directory.Exists(_configPath))
            {
                try
                {
                    Directory.CreateDirectory(_configPath);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                    throw;
                }
            }

            var configFile = Path.Combine(_configPath, DefaultConfigFile);

            if (!File.Exists(configFile))
            {
                _data = new ConfigurationData();
            }
            else
            {
                try
                {
                    var json = File.ReadAllText(configFile);
                    _data = JsonSerializer.Deserialize(json, SourceGenerationContext.Default.ConfigurationData);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }
            }
        }

        private void Save()
        {
            if (!Directory.Exists(_configPath))
            {
                try
                {
                    Directory.CreateDirectory(_configPath);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                    throw;
                }
            }

            var configFile = Path.Combine(_configPath, DefaultConfigFile);

            try
            {
                var json = JsonSerializer.Serialize(_data, SourceGenerationContext.Default.ConfigurationData);
                File.WriteAllText(configFile, json);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }
    }
}
