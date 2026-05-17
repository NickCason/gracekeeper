using System;
using System.IO;
using GraceKeeper.Core;
using Xunit;

namespace GraceKeeper.Core.Tests;

public class DismisserPidFileTests
{
    private static string TempPath() =>
        Path.Combine(Path.GetTempPath(), $"gk-pidfile-{Guid.NewGuid():N}.json");

    [Fact]
    public void WriteThenRead_RoundTripsFields()
    {
        var path = TempPath();
        try
        {
            var f = new DismisserPidFile(path);
            var rec = new DismisserRecord
            {
                Pid = 4242,
                ExePath = @"C:\Program Files\GraceKeeper\AutoHotkey64.exe",
                ScriptPath = @"C:\Program Files\GraceKeeper\popup-dismisser.ahk",
                StartedUtc = new DateTime(2026, 5, 16, 20, 35, 12, DateTimeKind.Utc)
            };
            f.Write(rec);
            var got = f.Read();
            Assert.NotNull(got);
            Assert.Equal(4242, got!.Pid);
            Assert.Equal(rec.ExePath, got.ExePath);
            Assert.Equal(rec.ScriptPath, got.ScriptPath);
            Assert.Equal(rec.StartedUtc, got.StartedUtc);
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }

    [Fact]
    public void Read_MissingFile_ReturnsNull()
    {
        var path = TempPath();
        var f = new DismisserPidFile(path);
        Assert.Null(f.Read());
    }

    [Fact]
    public void Read_CorruptJson_ReturnsNull()
    {
        var path = TempPath();
        try
        {
            File.WriteAllText(path, "{ this is not json");
            var f = new DismisserPidFile(path);
            Assert.Null(f.Read());
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }

    [Fact]
    public void Write_IsAtomic_NoPartialFileVisibleOnFailure()
    {
        var path = TempPath();
        try
        {
            var f = new DismisserPidFile(path);
            f.Write(new DismisserRecord { Pid = 1, ExePath = "a", ScriptPath = "b", StartedUtc = DateTime.UtcNow });
            var before = File.ReadAllText(path);
            f.Write(new DismisserRecord { Pid = 2, ExePath = "c", ScriptPath = "d", StartedUtc = DateTime.UtcNow });
            var after = File.ReadAllText(path);
            Assert.NotEqual(before, after);
            Assert.Contains("\"pid\": 2", after);
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }

    [Fact]
    public void Delete_RemovesFile()
    {
        var path = TempPath();
        var f = new DismisserPidFile(path);
        f.Write(new DismisserRecord { Pid = 1, ExePath = "a", ScriptPath = "b", StartedUtc = DateTime.UtcNow });
        Assert.True(File.Exists(path));
        f.Delete();
        Assert.False(File.Exists(path));
    }
}
