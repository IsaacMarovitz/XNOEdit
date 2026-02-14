using System.Text.Json.Serialization;

namespace XNOEdit.Logging
{
    [JsonConverter(typeof(LogLevel))]
    public enum LogLevel
    {
        Debug,
        Info,
        Warning,
        Error,
    }
}
