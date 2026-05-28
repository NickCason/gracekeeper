using System;
using System.IO;
using GraceKeeper.Core;
using Xunit;

namespace GraceKeeper.Core.Tests;

public class LogTailerTests : IDisposable
{
    private readonly string _tmpFile;

    public LogTailerTests()
    {
        _tmpFile = Path.Combine(Path.GetTempPath(), $"gk-log-{Guid.NewGuid()}.log");
    }

    public void Dispose()
    {
        if (File.Exists(_tmpFile)) File.Delete(_tmpFile);
    }

    [Fact]
    public void ReadNew_FromStart_ParsesDismisserLine()
    {
        File.WriteAllText(_tmpFile, "2026-05-16 16:32:14 | dismissed pid=12345 (LogixDesigner.Exe) | restored focus to \"X\"\n");
        var tailer = new LogTailer(_tmpFile);
        var events = tailer.ReadNew(0, out var newOffset);

        Assert.Single(events);
        Assert.Equal(LogEventKind.Dismiss, events[0].Kind);
        Assert.Equal(new DateTime(2026, 5, 16, 16, 32, 14), events[0].Timestamp);
        Assert.Contains("LogixDesigner.Exe", events[0].Description);
        Assert.True(newOffset > 0);
    }

    [Fact]
    public void ReadNew_ParsesLegacyCleanerLine_DeletedFormat()
    {
        File.WriteAllText(_tmpFile, "2026-05-16 14:30:00 | deleted=6 | locked=0 | duration=30ms\n");
        var tailer = new LogTailer(_tmpFile);
        var events = tailer.ReadNew(0, out _);

        Assert.Single(events);
        Assert.Equal(LogEventKind.Clean, events[0].Kind);
        Assert.Contains("deleted=6", events[0].Description);
    }

    [Fact]
    public void ReadNew_ParsesV0_4_CleanerLine_RefreshedFormat()
    {
        // v0.4.0 changed the format. Without this classification, dashboard
        // counters silently stopped advancing after upgrade.
        File.WriteAllText(_tmpFile, "2026-05-28 08:58:41 | refreshed=3 | freed-by-bounce=0 | deferred=0 | still-locked=0 | duration=2ms\n");
        var tailer = new LogTailer(_tmpFile);
        var events = tailer.ReadNew(0, out _);

        Assert.Single(events);
        Assert.Equal(LogEventKind.Clean, events[0].Kind);
        Assert.Contains("refreshed=3", events[0].Description);
    }

    [Fact]
    public void ReadNew_BannerLine_IsClassifiedAsOther()
    {
        // The v0.4.1 "started" banner must not increment the counter — only
        // the matching "refreshed=" result line counts as a Clean event.
        File.WriteAllText(_tmpFile,
            "2026-05-28 08:58:41 | started | mode=Manual | target=\"X\" | exists=True | files-found=3 | as-system=True\n");
        var tailer = new LogTailer(_tmpFile);
        var events = tailer.ReadNew(0, out _);

        Assert.Single(events);
        Assert.Equal(LogEventKind.Other, events[0].Kind);
    }

    [Fact]
    public void ReadNew_ResumesFromOffset()
    {
        File.WriteAllText(_tmpFile, "2026-05-16 10:00:00 | dismissed pid=1 (app1.exe)\n");
        var tailer = new LogTailer(_tmpFile);
        var first = tailer.ReadNew(0, out var offsetAfterFirst);
        Assert.Single(first);

        File.AppendAllText(_tmpFile, "2026-05-16 10:05:00 | dismissed pid=2 (app2.exe)\n");
        var second = tailer.ReadNew(offsetAfterFirst, out _);

        Assert.Single(second);
        Assert.Contains("app2", second[0].Description);
    }

    [Fact]
    public void ReadNew_NoFile_ReturnsEmpty()
    {
        var tailer = new LogTailer(Path.Combine(Path.GetTempPath(), $"gk-does-not-exist-{Guid.NewGuid()}.log"));
        var events = tailer.ReadNew(0, out var offset);

        Assert.Empty(events);
        Assert.Equal(0, offset);
    }
}
