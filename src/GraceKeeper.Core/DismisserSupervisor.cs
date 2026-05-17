using System.Diagnostics;
using System.IO;
using System.Linq;

namespace GraceKeeper.Core;

public interface IProcessProbe
{
    bool IsRunning(string exeNameWithExtension);
}

public interface IProcessLauncher
{
    void Launch(string fullPath);
}

public sealed class DismisserSupervisor
{
    private readonly string _exeName;
    private readonly string _installDir;
    private readonly IProcessProbe _probe;
    private readonly IProcessLauncher _launcher;
    private int _consecutiveLaunches;
    private const int MaxLaunches = 5;

    public bool HasFailedTerminally => _consecutiveLaunches >= MaxLaunches;

    public DismisserSupervisor(string exeName, string installDir, IProcessProbe? probe = null, IProcessLauncher? launcher = null)
    {
        _exeName = exeName;
        _installDir = installDir;
        _probe = probe ?? new DefaultProcessProbe();
        _launcher = launcher ?? new DefaultProcessLauncher();
    }

    public void CheckOnce()
    {
        if (_probe.IsRunning(_exeName))
        {
            _consecutiveLaunches = 0;  // reset on success
            return;
        }
        if (HasFailedTerminally) return;

        var fullPath = Path.Combine(_installDir, _exeName);
        _launcher.Launch(fullPath);
        _consecutiveLaunches++;
    }
}

internal sealed class DefaultProcessProbe : IProcessProbe
{
    public bool IsRunning(string exeNameWithExtension)
    {
        var name = Path.GetFileNameWithoutExtension(exeNameWithExtension);
        return Process.GetProcessesByName(name).Any();
    }
}

internal sealed class DefaultProcessLauncher : IProcessLauncher
{
    public void Launch(string fullPath)
    {
        Process.Start(new ProcessStartInfo { FileName = fullPath, UseShellExecute = false, CreateNoWindow = true });
    }
}
