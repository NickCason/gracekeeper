using System;
using System.IO;

namespace GraceKeeper.Core;

public interface ISupervisorLogger
{
    void Log(string message);
}

public sealed class SupervisorLogger : ISupervisorLogger
{
    private readonly string _path;
    private readonly object _gate = new();

    public SupervisorLogger(string path) { _path = path; }

    public void Log(string message)
    {
        try
        {
            var dir = Path.GetDirectoryName(_path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} | {message}{Environment.NewLine}";
            lock (_gate) { File.AppendAllText(_path, line); }
        }
        catch { /* swallow — supervisor must never crash the dashboard */ }
    }
}
