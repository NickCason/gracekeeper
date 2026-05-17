using System;
using System.IO;
using GraceKeeper.Core;
using Xunit;

namespace GraceKeeper.Core.Tests;

public class DisabledSentinelTests : IDisposable
{
    private readonly string _tmpFile;

    public DisabledSentinelTests()
    {
        _tmpFile = Path.Combine(Path.GetTempPath(), $"gk-disabled-{Guid.NewGuid()}");
    }

    public void Dispose()
    {
        if (File.Exists(_tmpFile)) File.Delete(_tmpFile);
    }

    [Fact]
    public void IsDisabled_NoFile_ReturnsFalse()
    {
        var s = new DisabledSentinel(_tmpFile);
        Assert.False(s.IsDisabled);
    }

    [Fact]
    public void Disable_CreatesFile()
    {
        var s = new DisabledSentinel(_tmpFile);
        s.Disable();
        Assert.True(File.Exists(_tmpFile));
        Assert.True(s.IsDisabled);
    }

    [Fact]
    public void Enable_RemovesFile()
    {
        File.WriteAllText(_tmpFile, "");
        var s = new DisabledSentinel(_tmpFile);
        s.Enable();
        Assert.False(File.Exists(_tmpFile));
        Assert.False(s.IsDisabled);
    }

    [Fact]
    public void Enable_NoFile_IsNoOp()
    {
        var s = new DisabledSentinel(_tmpFile);
        s.Enable();
        Assert.False(File.Exists(_tmpFile));
    }
}
