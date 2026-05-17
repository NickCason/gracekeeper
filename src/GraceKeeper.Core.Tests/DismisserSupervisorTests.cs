using GraceKeeper.Core;
using Moq;
using Xunit;

namespace GraceKeeper.Core.Tests;

public class DismisserSupervisorTests
{
    [Fact]
    public void CheckOnce_ProcessAlive_DoesNothing()
    {
        var probe = new Mock<IProcessProbe>();
        probe.Setup(p => p.IsRunning("popup-dismisser.exe")).Returns(true);
        var launcher = new Mock<IProcessLauncher>();

        var sup = new DismisserSupervisor("popup-dismisser.exe", "C:\\Program Files\\GraceKeeper", probe.Object, launcher.Object);
        sup.CheckOnce();

        launcher.Verify(l => l.Launch(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public void CheckOnce_ProcessDead_RelaunchesIt()
    {
        var probe = new Mock<IProcessProbe>();
        probe.Setup(p => p.IsRunning("popup-dismisser.exe")).Returns(false);
        var launcher = new Mock<IProcessLauncher>();

        var sup = new DismisserSupervisor("popup-dismisser.exe", "C:\\Program Files\\GraceKeeper", probe.Object, launcher.Object);
        sup.CheckOnce();

        launcher.Verify(l => l.Launch("C:\\Program Files\\GraceKeeper\\popup-dismisser.exe"), Times.Once);
    }

    [Fact]
    public void CheckOnce_FivePriorFailures_GivesUpAndReportsRed()
    {
        var probe = new Mock<IProcessProbe>();
        probe.Setup(p => p.IsRunning("popup-dismisser.exe")).Returns(false);
        var launcher = new Mock<IProcessLauncher>();

        var sup = new DismisserSupervisor("popup-dismisser.exe", "C:\\Program Files\\GraceKeeper", probe.Object, launcher.Object);
        for (int i = 0; i < 6; i++) sup.CheckOnce();

        Assert.True(sup.HasFailedTerminally);
        launcher.Verify(l => l.Launch(It.IsAny<string>()), Times.Exactly(5));  // capped at 5
    }
}
