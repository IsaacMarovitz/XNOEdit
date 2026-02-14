namespace XNOEdit.Logging
{
    public interface ILogTarget : IDisposable
    {
        void Log(object sender, LogEventArgs args);
        string Name { get; }
    }
}
