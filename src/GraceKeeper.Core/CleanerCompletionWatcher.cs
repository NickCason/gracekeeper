using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace GraceKeeper.Core;

public enum CleanerCompletionResult
{
    Completed,
    TimedOut,
}

public sealed class CleanerCompletionWatcher
{
    private readonly string _logPath;
    private readonly TimeSpan _pollInterval;

    public CleanerCompletionWatcher(string logPath, TimeSpan? pollInterval = null)
    {
        _logPath = logPath;
        _pollInterval = pollInterval ?? TimeSpan.FromMilliseconds(500);
    }

    public async Task<CleanerCompletionResult> WaitAsync(long startOffset, TimeSpan timeout, CancellationToken ct)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            ct.ThrowIfCancellationRequested();
            if (HasNewEntryPast(startOffset)) return CleanerCompletionResult.Completed;
            try { await Task.Delay(_pollInterval, ct); }
            catch (OperationCanceledException) { throw; }
        }
        return HasNewEntryPast(startOffset) ? CleanerCompletionResult.Completed : CleanerCompletionResult.TimedOut;
    }

    private bool HasNewEntryPast(long startOffset)
    {
        if (!File.Exists(_logPath)) return false;
        var len = new FileInfo(_logPath).Length;
        return len > startOffset;
    }
}
