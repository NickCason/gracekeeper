using System.Diagnostics;
using GraceKeeper.Core;
using Xunit;

namespace GraceKeeper.Core.Tests;

public class ProcessCommandLineReaderTests
{
    [Fact]
    public void ReadCommandLine_OwnProcess_ReturnsNonEmpty()
    {
        var reader = new ProcessCommandLineReader();
        var ownPid = Process.GetCurrentProcess().Id;
        var cmd = reader.ReadCommandLine(ownPid);
        Assert.False(string.IsNullOrWhiteSpace(cmd));
    }

    [Fact]
    public void ReadCommandLine_NonexistentPid_ReturnsNull()
    {
        var reader = new ProcessCommandLineReader();
        // PID 0 is the System Idle Process; WMI returns null cmdline for it.
        // Use a guaranteed-not-present PID (very large).
        var cmd = reader.ReadCommandLine(int.MaxValue - 1);
        Assert.Null(cmd);
    }
}
