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

    // Banner written at the top of every cleaner run. Captures what we found
    // before any deletes — invaluable when the user reports "it didn't clean
    // anything" and we can't see their machine. exists=False or files-found=0
    // tells us the path resolution / FT install layout is the problem, not
    // a permissions / locking issue.
    public void WriteStarted(string mode, string targetDir, bool targetExists, int filesFound, bool runningAsSystem) =>
        AppendAndRotate($"{Stamp()} | started | mode={mode} | target=\"{targetDir}\" | exists={targetExists} | files-found={filesFound} | as-system={runningAsSystem}");

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
