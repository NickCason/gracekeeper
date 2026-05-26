using System.Collections.Generic;
using System.Management;

namespace GraceKeeper.Core;

public sealed record ProcessInfo(int Pid, int ParentPid, string Name);

public interface IProcessTreeReader
{
    IReadOnlyList<ProcessInfo> GetProcesses();
}

public sealed class WmiProcessTreeReader : IProcessTreeReader
{
    public IReadOnlyList<ProcessInfo> GetProcesses()
    {
        var list = new List<ProcessInfo>(256);
        using var searcher = new ManagementObjectSearcher(
            "SELECT ProcessId, ParentProcessId, Name FROM Win32_Process");
        foreach (ManagementObject mo in searcher.Get())
        {
            using (mo)
            {
                var pid = (uint)mo["ProcessId"];
                var ppid = (uint)mo["ParentProcessId"];
                var name = (string?)mo["Name"] ?? string.Empty;
                list.Add(new ProcessInfo((int)pid, (int)ppid, name));
            }
        }
        return list;
    }
}
