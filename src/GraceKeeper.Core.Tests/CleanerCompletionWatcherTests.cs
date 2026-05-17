using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using GraceKeeper.Core;
using Xunit;

namespace GraceKeeper.Core.Tests;

public class CleanerCompletionWatcherTests : IDisposable
{
    private readonly string _tmpDir;
    private readonly string _logPath;

    public CleanerCompletionWatcherTests()
    {
        _tmpDir = Path.Combine(Path.GetTempPath(), "gk-cleanwatch-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tmpDir);
        _logPath = Path.Combine(_tmpDir, "cleaner.log");
        File.WriteAllText(_logPath, "");
    }

    public void Dispose()
    {
        try { Directory.Delete(_tmpDir, true); } catch { /* best effort */ }
    }

    [Fact]
    public async Task ReturnsCompleted_WhenNewCleanEntryAppears()
    {
        var watcher = new CleanerCompletionWatcher(_logPath, pollInterval: TimeSpan.FromMilliseconds(50));
        var task = watcher.WaitAsync(startOffset: 0, timeout: TimeSpan.FromSeconds(5), CancellationToken.None);

        await Task.Delay(100);
        File.AppendAllText(_logPath, "2026-05-17T12:00:00Z clean deleted=3\n");

        var result = await task;
        Assert.Equal(CleanerCompletionResult.Completed, result);
    }

    [Fact]
    public async Task ReturnsTimedOut_WhenNoNewEntryWithinTimeout()
    {
        var watcher = new CleanerCompletionWatcher(_logPath, pollInterval: TimeSpan.FromMilliseconds(50));
        var result = await watcher.WaitAsync(startOffset: 0, timeout: TimeSpan.FromMilliseconds(200), CancellationToken.None);
        Assert.Equal(CleanerCompletionResult.TimedOut, result);
    }

    [Fact]
    public async Task IgnoresEntriesBeforeStartOffset()
    {
        File.WriteAllText(_logPath, "2026-05-17T11:00:00Z clean deleted=1\n");
        var startOffset = new FileInfo(_logPath).Length;

        var watcher = new CleanerCompletionWatcher(_logPath, pollInterval: TimeSpan.FromMilliseconds(50));
        var task = watcher.WaitAsync(startOffset, timeout: TimeSpan.FromMilliseconds(400), CancellationToken.None);

        var result = await task;
        Assert.Equal(CleanerCompletionResult.TimedOut, result);
    }
}
