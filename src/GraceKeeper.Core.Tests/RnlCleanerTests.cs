using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GraceKeeper.Core;
using Moq;
using Xunit;

namespace GraceKeeper.Core.Tests;

public class RnlCleanerTests : IDisposable
{
    private readonly string _targetDir = Path.Combine(Path.GetTempPath(), "gk-rnl-" + Guid.NewGuid().ToString("N"));
    public RnlCleanerTests() => Directory.CreateDirectory(_targetDir);
    public void Dispose() { if (Directory.Exists(_targetDir)) Directory.Delete(_targetDir, true); }

    private string WriteRnl(string name)
    {
        var p = Path.Combine(_targetDir, name);
        File.WriteAllBytes(p, new byte[] { 1, 2, 3 });
        return p;
    }

    private static IEchoControllerProbe Probe(int count, params string[] families)
    {
        var m = new Mock<IEchoControllerProbe>();
        m.Setup(p => p.GetActivity()).Returns(new EchoActivity(count, families));
        return m.Object;
    }

    private static IServiceBouncer SuccessfulBouncer(int freed)
    {
        var m = new Mock<IServiceBouncer>();
        m.Setup(b => b.BounceAndRetryAsync(It.IsAny<Func<Task<RetryOutcome>>>(), It.IsAny<CancellationToken>()))
            .Returns<Func<Task<RetryOutcome>>, CancellationToken>(async (fn, _) =>
            {
                var outcome = await fn();
                return new BounceResult(freed, outcome.StillLocked);
            });
        return m.Object;
    }

    [Fact]
    public async Task EmptyDir_ReturnsEmptyResult()
    {
        var cleaner = new RnlCleaner(_targetDir, Probe(0), SuccessfulBouncer(0), retryDelay: TimeSpan.Zero);
        var r = await cleaner.RunAsync(CleanupMode.Runtime, default);
        Assert.Equal(0, r.RefreshedCount);
        Assert.Equal(0, r.DeferredCount);
    }

    [Fact]
    public async Task AllFilesUnlocked_AreRefreshed_BouncerNeverCalled()
    {
        WriteRnl("a.rnl"); WriteRnl("b.rnl"); WriteRnl("c.rnl");
        var bouncer = new Mock<IServiceBouncer>();
        var cleaner = new RnlCleaner(_targetDir, Probe(0), bouncer.Object, retryDelay: TimeSpan.Zero);
        var r = await cleaner.RunAsync(CleanupMode.Runtime, default);
        Assert.Equal(3, r.RefreshedCount);
        Assert.Equal(0, r.DeferredCount);
        bouncer.Verify(b => b.BounceAndRetryAsync(It.IsAny<Func<Task<RetryOutcome>>>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.Empty(Directory.GetFiles(_targetDir));
    }

    [Fact]
    public async Task RuntimeMode_WithEchoBusy_DefersLockedFiles_AndDoesNotBounce()
    {
        WriteRnl("a.rnl");
        var locked = WriteRnl("b.rnl");
        using var hold = new FileStream(locked, FileMode.Open, FileAccess.Read, FileShare.Read);  // keeps file open
        var bouncer = new Mock<IServiceBouncer>();
        var cleaner = new RnlCleaner(_targetDir, Probe(1, "CompactLogix5380"), bouncer.Object, retryDelay: TimeSpan.Zero);
        var r = await cleaner.RunAsync(CleanupMode.Runtime, default);
        Assert.Equal(1, r.RefreshedCount);
        Assert.Equal(1, r.DeferredCount);
        Assert.Contains("CompactLogix5380", r.DeferredReason);
        bouncer.Verify(b => b.BounceAndRetryAsync(It.IsAny<Func<Task<RetryOutcome>>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task BootMode_WithEchoBusy_StillBouncesUnconditionally()
    {
        WriteRnl("a.rnl");
        var locked = WriteRnl("b.rnl");
        using var hold = new FileStream(locked, FileMode.Open, FileAccess.Read, FileShare.Read);
        var bouncer = new Mock<IServiceBouncer>();
        bouncer.Setup(b => b.BounceAndRetryAsync(It.IsAny<Func<Task<RetryOutcome>>>(), It.IsAny<CancellationToken>()))
               .ReturnsAsync(new BounceResult(0, new[] { "b.rnl" }));
        var cleaner = new RnlCleaner(_targetDir, Probe(1, "CompactLogix5380"), bouncer.Object, retryDelay: TimeSpan.Zero);
        var r = await cleaner.RunAsync(CleanupMode.Boot, default);
        bouncer.Verify(b => b.BounceAndRetryAsync(It.IsAny<Func<Task<RetryOutcome>>>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ManualMode_BypassesEchoGuard_LikeBoot()
    {
        WriteRnl("a.rnl");
        var locked = WriteRnl("b.rnl");
        using var hold = new FileStream(locked, FileMode.Open, FileAccess.Read, FileShare.Read);
        var bouncer = new Mock<IServiceBouncer>();
        bouncer.Setup(b => b.BounceAndRetryAsync(It.IsAny<Func<Task<RetryOutcome>>>(), It.IsAny<CancellationToken>()))
               .ReturnsAsync(new BounceResult(0, new[] { "b.rnl" }));
        var cleaner = new RnlCleaner(_targetDir, Probe(1, "CompactLogix5380"), bouncer.Object, retryDelay: TimeSpan.Zero);
        await cleaner.RunAsync(CleanupMode.Manual, default);
        bouncer.Verify(b => b.BounceAndRetryAsync(It.IsAny<Func<Task<RetryOutcome>>>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ManualForceMode_BypassesEchoGuard()
    {
        WriteRnl("a.rnl");
        var locked = WriteRnl("b.rnl");
        using var hold = new FileStream(locked, FileMode.Open, FileAccess.Read, FileShare.Read);
        var bouncer = new Mock<IServiceBouncer>();
        bouncer.Setup(b => b.BounceAndRetryAsync(It.IsAny<Func<Task<RetryOutcome>>>(), It.IsAny<CancellationToken>()))
               .ReturnsAsync(new BounceResult(0, new[] { "b.rnl" }));
        var cleaner = new RnlCleaner(_targetDir, Probe(2, "CompactLogix5380", "ControlLogix5580"), bouncer.Object, retryDelay: TimeSpan.Zero);
        await cleaner.RunAsync(CleanupMode.ManualForce, default);
        bouncer.Verify(b => b.BounceAndRetryAsync(It.IsAny<Func<Task<RetryOutcome>>>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RuntimeMode_EchoIdle_BouncerCalledForLockedFiles()
    {
        WriteRnl("a.rnl");
        var locked = WriteRnl("b.rnl");
        using var hold = new FileStream(locked, FileMode.Open, FileAccess.Read, FileShare.Read);
        var bouncer = new Mock<IServiceBouncer>();
        bouncer.Setup(b => b.BounceAndRetryAsync(It.IsAny<Func<Task<RetryOutcome>>>(), It.IsAny<CancellationToken>()))
               .ReturnsAsync(new BounceResult(1, System.Array.Empty<string>()));
        var cleaner = new RnlCleaner(_targetDir, Probe(0), bouncer.Object, retryDelay: TimeSpan.Zero);
        var r = await cleaner.RunAsync(CleanupMode.Runtime, default);
        bouncer.Verify(b => b.BounceAndRetryAsync(It.IsAny<Func<Task<RetryOutcome>>>(), It.IsAny<CancellationToken>()), Times.Once);
        Assert.Equal(1, r.FreedByBounceCount);
    }
}
