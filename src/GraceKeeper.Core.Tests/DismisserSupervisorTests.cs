using System;
using System.IO;
using GraceKeeper.Core;
using Moq;
using Xunit;

namespace GraceKeeper.Core.Tests;

public class DismisserSupervisorTests
{
    private const string InstallDir = @"C:\Program Files\GraceKeeper";
    private static readonly string AhkExe = Path.Combine(InstallDir, "AutoHotkey64.exe");
    private static readonly string AhkScript = Path.Combine(InstallDir, "popup-dismisser.ahk");

    private static string TempPidPath() =>
        Path.Combine(Path.GetTempPath(), $"gk-pid-{Guid.NewGuid():N}.json");

    private static DismisserSupervisor Build(
        Mock<IProcessProbe> probe,
        Mock<IProcessLauncher> launcher,
        Mock<IProcessCommandLineReader> cmd,
        string pidPath,
        ISupervisorLogger? log = null)
    {
        return new DismisserSupervisor(
            installDir: InstallDir,
            ahkExeName: "AutoHotkey64.exe",
            scriptName: "popup-dismisser.ahk",
            disabledSentinelPath: Path.Combine(Path.GetTempPath(), $"gk-disabled-{Guid.NewGuid():N}"),
            pidFile: new DismisserPidFile(pidPath),
            probe: probe.Object,
            launcher: launcher.Object,
            cmdReader: cmd.Object,
            logger: log ?? new Mock<ISupervisorLogger>().Object);
    }

    [Fact]
    public void EnsureRunning_NoPidFile_NoOrphan_SpawnsFresh()
    {
        var pidPath = TempPidPath();
        var probe = new Mock<IProcessProbe>();
        probe.Setup(p => p.IsAlive(It.IsAny<int>())).Returns(false);
        probe.Setup(p => p.GetExePath(It.IsAny<int>())).Returns((string?)null);
        probe.Setup(p => p.EnumerateByName("AutoHotkey64")).Returns(Array.Empty<int>());

        var cmd = new Mock<IProcessCommandLineReader>();
        var launcher = new Mock<IProcessLauncher>();
        launcher.Setup(l => l.Launch(AhkExe, $"\"{AhkScript}\"")).Returns(7777);

        var sup = Build(probe, launcher, cmd, pidPath);
        sup.EnsureRunning();

        launcher.Verify(l => l.Launch(AhkExe, $"\"{AhkScript}\""), Times.Once);
        var rec = new DismisserPidFile(pidPath).Read();
        Assert.NotNull(rec);
        Assert.Equal(7777, rec!.Pid);
        Assert.Equal(AhkExe, rec.ExePath);
        Assert.Equal(AhkScript, rec.ScriptPath);
    }

    [Fact]
    public void EnsureRunning_ValidPidFile_AlivePid_PathAndScriptMatch_DoesNotSpawn()
    {
        var pidPath = TempPidPath();
        new DismisserPidFile(pidPath).Write(new DismisserRecord
        {
            Pid = 5555,
            ExePath = AhkExe,
            ScriptPath = AhkScript,
            StartedUtc = DateTime.UtcNow.AddMinutes(-3)
        });

        var probe = new Mock<IProcessProbe>();
        probe.Setup(p => p.IsAlive(5555)).Returns(true);
        probe.Setup(p => p.GetExePath(5555)).Returns(AhkExe);

        var cmd = new Mock<IProcessCommandLineReader>();
        cmd.Setup(c => c.ReadCommandLine(5555))
           .Returns($"\"{AhkExe}\" \"{AhkScript}\"");

        var launcher = new Mock<IProcessLauncher>();

        var sup = Build(probe, launcher, cmd, pidPath);
        sup.EnsureRunning();

        launcher.Verify(l => l.Launch(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public void EnsureRunning_PidFilePresent_PidDead_SpawnsFresh()
    {
        var pidPath = TempPidPath();
        new DismisserPidFile(pidPath).Write(new DismisserRecord
        {
            Pid = 1111, ExePath = AhkExe, ScriptPath = AhkScript, StartedUtc = DateTime.UtcNow
        });

        var probe = new Mock<IProcessProbe>();
        probe.Setup(p => p.IsAlive(1111)).Returns(false);
        probe.Setup(p => p.EnumerateByName("AutoHotkey64")).Returns(Array.Empty<int>());

        var cmd = new Mock<IProcessCommandLineReader>();
        var launcher = new Mock<IProcessLauncher>();
        launcher.Setup(l => l.Launch(AhkExe, $"\"{AhkScript}\"")).Returns(2222);

        var sup = Build(probe, launcher, cmd, pidPath);
        sup.EnsureRunning();

        launcher.Verify(l => l.Launch(AhkExe, $"\"{AhkScript}\""), Times.Once);
        var rec = new DismisserPidFile(pidPath).Read();
        Assert.Equal(2222, rec!.Pid);
    }

    [Fact]
    public void EnsureRunning_PidPointsToWrongExePath_DoesNotAdoptSpawnsFresh()
    {
        var pidPath = TempPidPath();
        new DismisserPidFile(pidPath).Write(new DismisserRecord
        {
            Pid = 3333, ExePath = AhkExe, ScriptPath = AhkScript, StartedUtc = DateTime.UtcNow
        });

        var probe = new Mock<IProcessProbe>();
        probe.Setup(p => p.IsAlive(3333)).Returns(true);
        probe.Setup(p => p.GetExePath(3333)).Returns(@"C:\OtherTools\AutoHotkey64.exe");
        probe.Setup(p => p.EnumerateByName("AutoHotkey64")).Returns(new[] { 3333 });

        var cmd = new Mock<IProcessCommandLineReader>();
        var launcher = new Mock<IProcessLauncher>();
        launcher.Setup(l => l.Launch(AhkExe, It.IsAny<string>())).Returns(4444);

        var sup = Build(probe, launcher, cmd, pidPath);
        sup.EnsureRunning();

        launcher.Verify(l => l.Launch(AhkExe, $"\"{AhkScript}\""), Times.Once);
    }

    [Fact]
    public void EnsureRunning_NoPidFile_OrphanFoundWithMatchingScript_AdoptsOrphan()
    {
        var pidPath = TempPidPath();
        var probe = new Mock<IProcessProbe>();
        probe.Setup(p => p.EnumerateByName("AutoHotkey64")).Returns(new[] { 9001, 9002 });
        probe.Setup(p => p.IsAlive(9001)).Returns(true);
        probe.Setup(p => p.IsAlive(9002)).Returns(true);
        probe.Setup(p => p.GetExePath(9001)).Returns(@"C:\OtherTools\AutoHotkey64.exe");
        probe.Setup(p => p.GetExePath(9002)).Returns(AhkExe);

        var cmd = new Mock<IProcessCommandLineReader>();
        cmd.Setup(c => c.ReadCommandLine(9001)).Returns(@"""C:\OtherTools\AutoHotkey64.exe"" ""C:\Users\Foo\other.ahk""");
        cmd.Setup(c => c.ReadCommandLine(9002)).Returns($"\"{AhkExe}\" \"{AhkScript}\"");

        var launcher = new Mock<IProcessLauncher>();
        var sup = Build(probe, launcher, cmd, pidPath);
        sup.EnsureRunning();

        launcher.Verify(l => l.Launch(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        var rec = new DismisserPidFile(pidPath).Read();
        Assert.Equal(9002, rec!.Pid);
    }

    [Fact]
    public void EnsureRunning_DisabledSentinelPresent_DoesNotSpawn()
    {
        var pidPath = TempPidPath();
        var disabledPath = Path.Combine(Path.GetTempPath(), $"gk-disabled-{Guid.NewGuid():N}");
        File.WriteAllText(disabledPath, "");

        try
        {
            var probe = new Mock<IProcessProbe>();
            probe.Setup(p => p.EnumerateByName("AutoHotkey64")).Returns(Array.Empty<int>());

            var cmd = new Mock<IProcessCommandLineReader>();
            var launcher = new Mock<IProcessLauncher>();

            var sup = new DismisserSupervisor(
                installDir: InstallDir,
                ahkExeName: "AutoHotkey64.exe",
                scriptName: "popup-dismisser.ahk",
                disabledSentinelPath: disabledPath,
                pidFile: new DismisserPidFile(pidPath),
                probe: probe.Object,
                launcher: launcher.Object,
                cmdReader: cmd.Object,
                logger: new Mock<ISupervisorLogger>().Object);
            sup.EnsureRunning();

            launcher.Verify(l => l.Launch(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }
        finally { File.Delete(disabledPath); }
    }

    [Fact]
    public void EnsureRunning_CorruptPidFile_FallsThroughToDiscoveryAndSpawn()
    {
        var pidPath = TempPidPath();
        File.WriteAllText(pidPath, "{ corrupt");

        try
        {
            var probe = new Mock<IProcessProbe>();
            probe.Setup(p => p.EnumerateByName("AutoHotkey64")).Returns(Array.Empty<int>());

            var cmd = new Mock<IProcessCommandLineReader>();
            var launcher = new Mock<IProcessLauncher>();
            launcher.Setup(l => l.Launch(AhkExe, $"\"{AhkScript}\"")).Returns(8888);

            var sup = Build(probe, launcher, cmd, pidPath);
            sup.EnsureRunning();

            launcher.Verify(l => l.Launch(AhkExe, $"\"{AhkScript}\""), Times.Once);
        }
        finally { File.Delete(pidPath); }
    }
}
