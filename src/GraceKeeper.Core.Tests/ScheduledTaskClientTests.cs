using System.Threading.Tasks;
using GraceKeeper.Core;
using Moq;
using Xunit;

namespace GraceKeeper.Core.Tests;

public class ScheduledTaskClientTests
{
    [Fact]
    public async Task ChangeIntervalAsync_ShellsOutToSchtasksWithCorrectArgs()
    {
        var processRunner = new Mock<IProcessRunner>();
        processRunner
            .Setup(p => p.RunAsync("schtasks.exe", It.IsAny<string>()))
            .ReturnsAsync(new ProcessResult(0, "", ""));

        var client = new ScheduledTaskClient("GraceKeeper - Cleanup RNL", processRunner.Object);
        await client.ChangeIntervalAsync(360);

        processRunner.Verify(p => p.RunAsync(
            "schtasks.exe",
            It.Is<string>(args => args.Contains("/Change") && args.Contains("/TN \"GraceKeeper - Cleanup RNL\"") && args.Contains("/RI 360"))),
            Times.Once);
    }

    [Fact]
    public async Task ChangeStartTimeAsync_FormatsArgs()
    {
        var processRunner = new Mock<IProcessRunner>();
        processRunner
            .Setup(p => p.RunAsync("schtasks.exe", It.IsAny<string>()))
            .ReturnsAsync(new ProcessResult(0, "", ""));

        var client = new ScheduledTaskClient("GraceKeeper - Cleanup RNL", processRunner.Object);
        await client.ChangeStartTimeAsync("06:30");

        processRunner.Verify(p => p.RunAsync(
            "schtasks.exe",
            It.Is<string>(args => args.Contains("/ST 06:30"))),
            Times.Once);
    }

    [Fact]
    public async Task ExistsAsync_ReturnsTrueWhenQuerySucceeds()
    {
        var processRunner = new Mock<IProcessRunner>();
        processRunner
            .Setup(p => p.RunAsync("schtasks.exe", It.IsAny<string>()))
            .ReturnsAsync(new ProcessResult(0, "TaskName: GraceKeeper - Cleanup RNL\nNext Run Time: ...", ""));

        var client = new ScheduledTaskClient("GraceKeeper - Cleanup RNL", processRunner.Object);
        Assert.True(await client.ExistsAsync());
    }

    [Fact]
    public async Task ExistsAsync_ReturnsFalseWhenQueryFails()
    {
        var processRunner = new Mock<IProcessRunner>();
        processRunner
            .Setup(p => p.RunAsync("schtasks.exe", It.IsAny<string>()))
            .ReturnsAsync(new ProcessResult(1, "", "ERROR: The system cannot find the file specified."));

        var client = new ScheduledTaskClient("GraceKeeper - Cleanup RNL", processRunner.Object);
        Assert.False(await client.ExistsAsync());
    }
}
