using System;
using System.Collections.Generic;
using GraceKeeper.Core;
using Xunit;

namespace GraceKeeper.Core.Tests;

public class CleanupResultTests
{
    [Fact]
    public void Empty_ReturnsZeroCountsAndNoDeferredFiles()
    {
        var result = CleanupResult.Empty(TimeSpan.FromMilliseconds(5));
        Assert.Equal(0, result.RefreshedCount);
        Assert.Equal(0, result.FreedByBounceCount);
        Assert.Equal(0, result.DeferredCount);
        Assert.Equal(0, result.StillLockedCount);
        Assert.Empty(result.DeferredFiles);
        Assert.Null(result.DeferredReason);
        Assert.Equal(TimeSpan.FromMilliseconds(5), result.Duration);
    }

    [Fact]
    public void With_DeferredFiles_CountMatchesList()
    {
        var result = new CleanupResult(
            RefreshedCount: 2,
            FreedByBounceCount: 0,
            DeferredCount: 1,
            DeferredFiles: new List<string> { "abc.rnl" },
            DeferredReason: "echo-busy: CompactLogix5380",
            StillLockedCount: 0,
            Duration: TimeSpan.FromMilliseconds(120));
        Assert.Equal(1, result.DeferredCount);
        Assert.Single(result.DeferredFiles);
    }

    [Theory]
    [InlineData(CleanupMode.Boot)]
    [InlineData(CleanupMode.Runtime)]
    [InlineData(CleanupMode.SafetyNet)]
    [InlineData(CleanupMode.ManualForce)]
    public void CleanupMode_HasFourMembers(CleanupMode mode)
    {
        Assert.True(Enum.IsDefined(typeof(CleanupMode), mode));
    }
}
