using System;
using System.IO;
using GraceKeeper.Core;
using Moq;
using Xunit;

namespace GraceKeeper.Core.Tests;

public class SafetyNetGateTests : IDisposable
{
    private readonly string _logFile = Path.Combine(Path.GetTempPath(), "gk-snt-" + Guid.NewGuid().ToString("N") + ".log");
    public void Dispose() { if (File.Exists(_logFile)) File.Delete(_logFile); }

    private static IClock Clk(DateTime utc) { var m = new Mock<IClock>(); m.SetupGet(c => c.UtcNow).Returns(utc); return m.Object; }

    [Fact]
    public void ShouldSkip_ReturnsFalse_WhenLogMissing()
    {
        var gate = new SafetyNetGate(_logFile, Clk(DateTime.UtcNow), TimeSpan.FromHours(11));
        Assert.False(gate.ShouldSkip());
    }

    [Fact]
    public void ShouldSkip_ReturnsTrue_WhenRecentRefreshedEntryExists()
    {
        File.WriteAllLines(_logFile, new[]
        {
            "2026-05-26 09:00:00 | refreshed=3 | freed-by-bounce=0 | deferred=0 | still-locked=0 | duration=20ms"
        });
        var now = new DateTime(2026, 5, 26, 15, 0, 0, DateTimeKind.Local).ToUniversalTime();
        var gate = new SafetyNetGate(_logFile, Clk(now), TimeSpan.FromHours(11));
        Assert.True(gate.ShouldSkip());
    }

    [Fact]
    public void ShouldSkip_ReturnsFalse_WhenLastEntryIsOlderThanWindow()
    {
        File.WriteAllLines(_logFile, new[]
        {
            "2026-05-25 09:00:00 | refreshed=3 | freed-by-bounce=0 | deferred=0 | still-locked=0 | duration=20ms"
        });
        var now = new DateTime(2026, 5, 26, 15, 0, 0, DateTimeKind.Local).ToUniversalTime();
        var gate = new SafetyNetGate(_logFile, Clk(now), TimeSpan.FromHours(11));
        Assert.False(gate.ShouldSkip());
    }

    [Fact]
    public void ShouldSkip_IgnoresSkippedAndFailedLines_AndUsesPriorSuccess()
    {
        File.WriteAllLines(_logFile, new[]
        {
            "2026-05-26 09:00:00 | refreshed=3 | freed-by-bounce=0 | deferred=0 | still-locked=0 | duration=20ms",
            "2026-05-26 12:00:00 | skipped (disabled)",
            "2026-05-26 13:00:00 | failed: InvalidOperationException: x"
        });
        var now = new DateTime(2026, 5, 26, 14, 0, 0, DateTimeKind.Local).ToUniversalTime();
        var gate = new SafetyNetGate(_logFile, Clk(now), TimeSpan.FromHours(11));
        Assert.True(gate.ShouldSkip());
    }

    [Fact]
    public void ShouldSkip_AcceptsLegacyDeletedFormat()
    {
        File.WriteAllLines(_logFile, new[]
        {
            "2026-05-26 09:00:00 | deleted=5 | locked=0 | duration=22ms"
        });
        var now = new DateTime(2026, 5, 26, 15, 0, 0, DateTimeKind.Local).ToUniversalTime();
        var gate = new SafetyNetGate(_logFile, Clk(now), TimeSpan.FromHours(11));
        Assert.True(gate.ShouldSkip());
    }
}
