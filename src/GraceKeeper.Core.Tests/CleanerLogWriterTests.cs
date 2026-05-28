using System;
using System.IO;
using System.Linq;
using GraceKeeper.Core;
using Moq;
using Xunit;

namespace GraceKeeper.Core.Tests;

public class CleanerLogWriterTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "gk-tests-" + Guid.NewGuid().ToString("N"));
    private readonly string _logFile;

    public CleanerLogWriterTests()
    {
        Directory.CreateDirectory(_dir);
        _logFile = Path.Combine(_dir, "cleaner.log");
    }

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    // Use Local kind so ToLocalTime() is a no-op (avoids CI timezone sensitivity).
    private static IClock FixedClock(DateTime local)
    {
        var mock = new Mock<IClock>();
        // UtcNow must be Utc; caller passes a local time — convert back to UTC for the mock,
        // then ToLocalTime() in Stamp() will round-trip back to the same local value.
        mock.SetupGet(c => c.UtcNow).Returns(local.ToUniversalTime());
        return mock.Object;
    }

    private static DateTime LocalTime(int y, int mo, int d, int h, int mi, int s) =>
        new DateTime(y, mo, d, h, mi, s, DateTimeKind.Local);

    [Fact]
    public void WriteResult_AppendsExpectedLineFormat_NoDeferred()
    {
        var clock = FixedClock(LocalTime(2026, 5, 26, 14, 5, 7));
        var w = new CleanerLogWriter(_logFile, clock, maxLines: 500);

        var result = new CleanupResult(
            RefreshedCount: 3, FreedByBounceCount: 0, DeferredCount: 0,
            DeferredFiles: Array.Empty<string>(), DeferredReason: null,
            StillLockedCount: 0, Duration: TimeSpan.FromMilliseconds(42));

        w.WriteResult(result);

        var lines = File.ReadAllLines(_logFile);
        Assert.Single(lines);
        Assert.StartsWith("2026-05-26 14:05:07 | refreshed=3 | freed-by-bounce=0 | deferred=0 | still-locked=0 | duration=42ms", lines[0]);
    }

    [Fact]
    public void WriteResult_IncludesDeferredReason_WhenPresent()
    {
        var clock = FixedClock(LocalTime(2026, 5, 26, 14, 5, 7));
        var w = new CleanerLogWriter(_logFile, clock, maxLines: 500);

        var result = new CleanupResult(
            RefreshedCount: 2, FreedByBounceCount: 0, DeferredCount: 1,
            DeferredFiles: new[] { "echo.rnl" }, DeferredReason: "echo-busy: CompactLogix5380",
            StillLockedCount: 0, Duration: TimeSpan.FromMilliseconds(120));

        w.WriteResult(result);

        var line = File.ReadAllLines(_logFile)[0];
        Assert.Contains("deferred=1 (echo-busy: CompactLogix5380)", line);
    }

    [Fact]
    public void WriteSkipped_WritesDistinctLine()
    {
        var clock = FixedClock(LocalTime(2026, 5, 26, 14, 5, 7));
        var w = new CleanerLogWriter(_logFile, clock, maxLines: 500);
        w.WriteSkipped("disabled");
        var line = File.ReadAllLines(_logFile)[0];
        Assert.Equal("2026-05-26 14:05:07 | skipped (disabled)", line);
    }

    [Fact]
    public void WriteStarted_WritesBannerLine()
    {
        var clock = FixedClock(LocalTime(2026, 5, 26, 14, 5, 7));
        var w = new CleanerLogWriter(_logFile, clock, maxLines: 500);
        w.WriteStarted("Manual", @"C:\ProgramData\Rockwell Automation\FactoryTalk Activation", true, 3, true);
        var line = File.ReadAllLines(_logFile)[0];
        Assert.Equal(
            "2026-05-26 14:05:07 | started | mode=Manual | target=\"C:\\ProgramData\\Rockwell Automation\\FactoryTalk Activation\" | exists=True | files-found=3 | as-system=True",
            line);
    }

    [Fact]
    public void Rotation_KeepsLastN_LinesOnly()
    {
        var clock = FixedClock(LocalTime(2026, 5, 26, 14, 5, 7));
        var w = new CleanerLogWriter(_logFile, clock, maxLines: 3);

        for (int i = 0; i < 10; i++)
            w.WriteResult(CleanupResult.Empty(TimeSpan.FromMilliseconds(i)));

        var lines = File.ReadAllLines(_logFile);
        Assert.Equal(3, lines.Length);
    }
}
