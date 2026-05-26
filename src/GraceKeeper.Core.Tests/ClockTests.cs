using System;
using GraceKeeper.Core;
using Xunit;

namespace GraceKeeper.Core.Tests;

public class ClockTests
{
    [Fact]
    public void SystemClock_UtcNow_IsCloseToDateTimeUtcNow()
    {
        IClock clock = new SystemClock();
        var delta = (clock.UtcNow - DateTime.UtcNow).Duration();
        Assert.True(delta < TimeSpan.FromSeconds(1));
    }
}
