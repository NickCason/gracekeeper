using System.Linq;
using GraceKeeper.Core;
using Moq;
using Xunit;

namespace GraceKeeper.Core.Tests;

public class ProcessTreeReaderContractTests
{
    [Fact]
    public void ProcessInfo_RecordExposesPidParentAndName()
    {
        var p = new ProcessInfo(Pid: 1234, ParentPid: 56, Name: "EmulateCompactLogix5380.exe");
        Assert.Equal(1234, p.Pid);
        Assert.Equal(56, p.ParentPid);
        Assert.Equal("EmulateCompactLogix5380.exe", p.Name);
    }

    [Fact]
    public void IProcessTreeReader_CanBeMocked_AndReturnsEnumerableOfProcessInfo()
    {
        var mock = new Mock<IProcessTreeReader>();
        mock.Setup(m => m.GetProcesses()).Returns(new[]
        {
            new ProcessInfo(1, 0, "System"),
            new ProcessInfo(2, 1, "child.exe")
        });
        var procs = mock.Object.GetProcesses().ToList();
        Assert.Equal(2, procs.Count);
        Assert.Equal("child.exe", procs[1].Name);
    }
}
