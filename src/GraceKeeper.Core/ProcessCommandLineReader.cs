using System.Management;

namespace GraceKeeper.Core;

public interface IProcessCommandLineReader
{
    string? ReadCommandLine(int pid);
}

public sealed class ProcessCommandLineReader : IProcessCommandLineReader
{
    public string? ReadCommandLine(int pid)
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(
                $"SELECT CommandLine FROM Win32_Process WHERE ProcessId = {pid}");
            using var results = searcher.Get();
            foreach (ManagementObject mo in results)
            {
                using (mo)
                {
                    var cmd = mo["CommandLine"] as string;
                    return cmd;
                }
            }
            return null;
        }
        catch (ManagementException) { return null; }
        catch (System.Runtime.InteropServices.COMException) { return null; }
    }
}
