using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace GraceKeeper.Core;

public interface IProcessProbe
{
    bool IsAlive(int pid);
    string? GetExePath(int pid);
    IReadOnlyList<int> EnumerateByName(string nameWithoutExtension);
}

public interface IProcessLauncher
{
    /// <summary>Starts a detached child process. Returns the PID.</summary>
    int Launch(string exePath, string arguments);
}

public sealed class DismisserSupervisor
{
    private readonly string _installDir;
    private readonly string _ahkExeName;        // e.g. "AutoHotkey64.exe"
    private readonly string _scriptName;        // e.g. "popup-dismisser.ahk"
    private readonly string _disabledSentinelPath;
    private readonly DismisserPidFile _pidFile;
    private readonly IProcessProbe _probe;
    private readonly IProcessLauncher _launcher;
    private readonly IProcessCommandLineReader _cmd;
    private readonly ISupervisorLogger _log;

    public string AhkExePath => Path.Combine(_installDir, _ahkExeName);
    public string ScriptPath => Path.Combine(_installDir, _scriptName);

    public DismisserSupervisor(
        string installDir,
        string ahkExeName,
        string scriptName,
        string disabledSentinelPath,
        DismisserPidFile pidFile,
        IProcessProbe probe,
        IProcessLauncher launcher,
        IProcessCommandLineReader cmdReader,
        ISupervisorLogger logger)
    {
        _installDir = installDir;
        _ahkExeName = ahkExeName;
        _scriptName = scriptName;
        _disabledSentinelPath = disabledSentinelPath;
        _pidFile = pidFile;
        _probe = probe;
        _launcher = launcher;
        _cmd = cmdReader;
        _log = logger;
    }

    public void EnsureRunning()
    {
        if (File.Exists(_disabledSentinelPath))
        {
            _log.Log("paused (DISABLED sentinel present) - not spawning");
            return;
        }

        // 1. PID-file path.
        var rec = _pidFile.Read();
        if (rec != null && _probe.IsAlive(rec.Pid))
        {
            var exe = _probe.GetExePath(rec.Pid);
            var line = _cmd.ReadCommandLine(rec.Pid);
            if (PathsEqual(exe, AhkExePath) && CommandLineMatches(line, ScriptPath))
            {
                // Still our process. Nothing to do.
                return;
            }
            _log.Log($"pid {rec.Pid} is alive but does not match (exe={exe}); falling through");
        }

        // 2. Orphan-discovery path.
        var baseName = Path.GetFileNameWithoutExtension(_ahkExeName);
        foreach (var pid in _probe.EnumerateByName(baseName))
        {
            if (!_probe.IsAlive(pid)) continue;
            if (!PathsEqual(_probe.GetExePath(pid), AhkExePath)) continue;
            var line = _cmd.ReadCommandLine(pid);
            if (!CommandLineMatches(line, ScriptPath)) continue;

            _pidFile.Write(new DismisserRecord
            {
                Pid = pid,
                ExePath = AhkExePath,
                ScriptPath = ScriptPath,
                StartedUtc = DateTime.UtcNow
            });
            _log.Log($"adopted existing dismisser pid={pid}");
            return;
        }

        // 3. Spawn fresh.
        try
        {
            var newPid = _launcher.Launch(AhkExePath, $"\"{ScriptPath}\"");
            _pidFile.Write(new DismisserRecord
            {
                Pid = newPid,
                ExePath = AhkExePath,
                ScriptPath = ScriptPath,
                StartedUtc = DateTime.UtcNow
            });
            _log.Log($"spawned dismisser pid={newPid}");
        }
        catch (Exception ex)
        {
            _log.Log($"FAILED to spawn dismisser: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private static bool PathsEqual(string? a, string? b) =>
        !string.IsNullOrEmpty(a) && !string.IsNullOrEmpty(b)
        && string.Equals(Path.GetFullPath(a!), Path.GetFullPath(b!), StringComparison.OrdinalIgnoreCase);

    private static bool CommandLineMatches(string? cmdLine, string scriptPath) =>
        !string.IsNullOrEmpty(cmdLine)
        && cmdLine!.IndexOf(Path.GetFileName(scriptPath), StringComparison.OrdinalIgnoreCase) >= 0;
}

public sealed class DefaultProcessProbe : IProcessProbe
{
    public bool IsAlive(int pid)
    {
        try { using var p = Process.GetProcessById(pid); return !p.HasExited; }
        catch (ArgumentException) { return false; }
        catch (InvalidOperationException) { return false; }
    }

    public string? GetExePath(int pid)
    {
        try { using var p = Process.GetProcessById(pid); return p.MainModule?.FileName; }
        catch { return null; }
    }

    public IReadOnlyList<int> EnumerateByName(string nameWithoutExtension)
    {
        try { return Process.GetProcessesByName(nameWithoutExtension).Select(p => p.Id).ToList(); }
        catch { return Array.Empty<int>(); }
    }
}

public sealed class DefaultProcessLauncher : IProcessLauncher
{
    public int Launch(string exePath, string arguments)
    {
        var psi = new ProcessStartInfo
        {
            FileName = exePath,
            Arguments = arguments,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = Path.GetDirectoryName(exePath) ?? ""
        };
        var p = Process.Start(psi)
            ?? throw new InvalidOperationException($"Process.Start returned null for {exePath}");
        return p.Id;
    }
}
