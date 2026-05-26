using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GraceKeeper.Core;
using Moq;
using Xunit;

namespace GraceKeeper.Core.Tests;

public class ServiceBouncerTests
{
    private static readonly string[] OrderedServices = new[]
    {
        "FactoryTalk Logix Echo Message Broker",
        "FactoryTalk Logix Echo Service",
        "FactoryTalk Activation Service",
        "FTActivationBoost"
    };

    private static (ServiceBouncer bouncer, Mock<IServiceController> svc, List<string> ops) Build()
    {
        var svc = new Mock<IServiceController>();
        svc.Setup(s => s.Exists(It.IsAny<string>())).Returns(true);
        var ops = new List<string>();
        svc.Setup(s => s.StopAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
           .Callback<string, TimeSpan, CancellationToken>((n, _, _) => ops.Add("stop:" + n))
           .ReturnsAsync(true);
        svc.Setup(s => s.StartAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
           .Callback<string, TimeSpan, CancellationToken>((n, _, _) => ops.Add("start:" + n))
           .ReturnsAsync(true);
        var b = new ServiceBouncer(svc.Object, OrderedServices, TimeSpan.FromSeconds(10));
        return (b, svc, ops);
    }

    [Fact]
    public async Task BounceAndRetry_StopsServicesInOrder()
    {
        var (b, _, ops) = Build();
        var retried = false;
        await b.BounceAndRetryAsync(async () => { retried = true; return new RetryOutcome(FreedCount: 2, StillLocked: System.Array.Empty<string>()); }, default);
        var stops = ops.Where(o => o.StartsWith("stop:")).ToList();
        Assert.Equal(new[]
        {
            "stop:FactoryTalk Logix Echo Message Broker",
            "stop:FactoryTalk Logix Echo Service",
            "stop:FactoryTalk Activation Service",
            "stop:FTActivationBoost"
        }, stops);
        Assert.True(retried);
    }

    [Fact]
    public async Task BounceAndRetry_StartsServicesInReverseOrder()
    {
        var (b, _, ops) = Build();
        await b.BounceAndRetryAsync(async () => new RetryOutcome(1, System.Array.Empty<string>()), default);
        var starts = ops.Where(o => o.StartsWith("start:")).ToList();
        Assert.Equal(new[]
        {
            "start:FTActivationBoost",
            "start:FactoryTalk Activation Service",
            "start:FactoryTalk Logix Echo Service",
            "start:FactoryTalk Logix Echo Message Broker"
        }, starts);
    }

    [Fact]
    public async Task BounceAndRetry_SkipsMissingServices_WithoutFailure()
    {
        var svc = new Mock<IServiceController>();
        svc.Setup(s => s.Exists(It.IsAny<string>())).Returns<string>(n =>
            n != "FactoryTalk Logix Echo Message Broker");
        svc.Setup(s => s.StopAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        svc.Setup(s => s.StartAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var b = new ServiceBouncer(svc.Object, OrderedServices, TimeSpan.FromSeconds(10));
        var result = await b.BounceAndRetryAsync(async () => new RetryOutcome(0, System.Array.Empty<string>()), default);
        Assert.NotNull(result);
        svc.Verify(s => s.StopAsync("FactoryTalk Logix Echo Message Broker", It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task BounceAndRetry_RetryAfterStopsReturnsFreedAndStillLocked()
    {
        var (b, _, _) = Build();
        var result = await b.BounceAndRetryAsync(async () => new RetryOutcome(3, new[] { "stuck.rnl" }), default);
        Assert.Equal(3, result.FreedByBounceCount);
        Assert.Single(result.StillLocked);
        Assert.Equal("stuck.rnl", result.StillLocked[0]);
    }
}
