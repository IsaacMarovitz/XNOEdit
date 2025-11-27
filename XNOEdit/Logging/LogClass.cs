using System.Text.Json.Serialization;

namespace XNOEdit.Logging
{
    [JsonConverter(typeof(LogClass))]
    public enum LogClass
    {
        Application,
    }
}
