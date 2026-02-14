namespace XNOEdit.Logging.Formatters
{
    interface ILogFormatter
    {
        string Format(LogEventArgs args);
    }
}
