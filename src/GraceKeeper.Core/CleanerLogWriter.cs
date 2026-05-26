using System;
using System.Collections.Generic;
using System.IO;

namespace GraceKeeper.Core;

public sealed class CleanerLogWriter
{
    private readonly string _path;
    private readonly IClock _clock;
    private readonly int _maxLines;

    public CleanerLogWriter(string path, IClock clock, int maxLines = 500)
    {
        _path = path;
        _clock = clock;
        _maxLines = maxLines;
    }

    public void WriteResult(CleanupResult r)
    {
        var deferredPart = r.DeferredReason == null
            ? $"deferred={r.DeferredCount}"
            : $"deferred={r.DeferredCount} ({r.DeferredReason})";
        var line = $"{Stamp()} | refreshed={r.RefreshedCount} | freed-by-bounce={r.FreedByBounceCount} | {deferredPart} | still-locked={r.StillLockedCount} | duration={(int)r.Duration.TotalMilliseconds}ms";
        AppendAndRotate(line);
    }

    public void WriteSkipped(string reason) =>
        AppendAndRotate($"{Stamp()} | skipped ({reason})");

    public void WriteFailed(string exceptionTypeName, string message) =>
        AppendAndRotate($"{Stamp()} | failed: {exceptionTypeName}: {message}");

    private string Stamp() => _clock.UtcNow.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");

    private void AppendAndRotate(string line)
    {
        var dir = Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);
        File.AppendAllText(_path, line + Environment.NewLine);

        var lines = File.ReadAllLines(_path);
        if (lines.Length > _maxLines)
        {
            var kept = new List<string>(_maxLines);
            for (int i = lines.Length - _maxLines; i < lines.Length; i++)
                kept.Add(lines[i]);
            File.WriteAllLines(_path, kept);
        }
    }
}
