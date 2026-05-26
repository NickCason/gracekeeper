using System.Collections.Generic;
using GraceKeeper.Core;
using Moq;
using Xunit;

namespace GraceKeeper.Core.Tests;

public class EchoControllerProbeTests
{
    private const int EmulateServicePid = 9020;

    private static IEchoControllerProbe Build(params ProcessInfo[] procs)
    {
        var reader = new Mock<IProcessTreeReader>();
        reader.Setup(r => r.GetProcesses()).Returns(procs);
        return new EchoControllerProbe(reader.Object, emulateServiceProcessName: "EmulateService.exe");
    }

    [Fact]
    public void NoEmulateService_ReturnsCountZero()
    {
        var probe = Build(
            new ProcessInfo(4, 0, "System"),
            new ProcessInfo(100, 4, "explorer.exe"));
        var a = probe.GetActivity();
        Assert.Equal(0, a.Count);
        Assert.Empty(a.FamilyNames);
    }

    [Fact]
    public void EmulateServiceWithNoChildren_ReturnsCountZero()
    {
        var probe = Build(
            new ProcessInfo(EmulateServicePid, 4, "EmulateService.exe"));
        var a = probe.GetActivity();
        Assert.Equal(0, a.Count);
    }

    [Fact]
    public void EmulateServiceWithOneEmulateChild_ReturnsCountOneAndFamilyName()
    {
        var probe = Build(
            new ProcessInfo(EmulateServicePid, 4, "EmulateService.exe"),
            new ProcessInfo(14580, EmulateServicePid, "EmulateCompactLogix5380.exe"));
        var a = probe.GetActivity();
        Assert.Equal(1, a.Count);
        Assert.Single(a.FamilyNames);
        Assert.Equal("CompactLogix5380", a.FamilyNames[0]);
    }

    [Fact]
    public void NonEmulateChildrenAreIgnored()
    {
        var probe = Build(
            new ProcessInfo(EmulateServicePid, 4, "EmulateService.exe"),
            new ProcessInfo(99999, EmulateServicePid, "conhost.exe"),
            new ProcessInfo(14580, EmulateServicePid, "EmulateControlLogix5580.exe"));
        var a = probe.GetActivity();
        Assert.Equal(1, a.Count);
        Assert.Equal("ControlLogix5580", a.FamilyNames[0]);
    }

    [Fact]
    public void TwoControllers_CountsBoth_FamiliesAreUnique()
    {
        var probe = Build(
            new ProcessInfo(EmulateServicePid, 4, "EmulateService.exe"),
            new ProcessInfo(14580, EmulateServicePid, "EmulateCompactLogix5380.exe"),
            new ProcessInfo(14581, EmulateServicePid, "EmulateControlLogix5580.exe"));
        var a = probe.GetActivity();
        Assert.Equal(2, a.Count);
        Assert.Equal(2, a.FamilyNames.Count);
    }
}
