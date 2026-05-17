using System;
using System.IO;
using GraceKeeper.Core;
using Xunit;

namespace GraceKeeper.Core.Tests;

public class ConfigStoreTests : IDisposable
{
    private readonly string _tmpFile;

    public ConfigStoreTests()
    {
        _tmpFile = Path.Combine(Path.GetTempPath(), $"gk-config-{Guid.NewGuid()}.json");
    }

    public void Dispose()
    {
        if (File.Exists(_tmpFile)) File.Delete(_tmpFile);
        var corrupt = _tmpFile + ".corrupt";
        if (File.Exists(corrupt)) File.Delete(corrupt);
    }

    [Fact]
    public void Load_NoFile_ReturnsDefaults()
    {
        var store = new ConfigStore(_tmpFile);
        var cfg = store.Load();

        Assert.Equal(1, cfg.Version);
        Assert.Equal(720, cfg.Schedule.IntervalMinutes);
        Assert.Equal("auto", cfg.Theme);
        Assert.Equal(0, cfg.Counters.PopupsDismissedLifetime);
    }

    [Fact]
    public void SaveAndLoad_Roundtrip_PreservesValues()
    {
        var store = new ConfigStore(_tmpFile);
        var original = new ConfigModel
        {
            Schedule = new ScheduleConfig { IntervalMinutes = 360, StartTime = "06:30" },
            Theme = "dark",
            Counters = new Counters { PopupsDismissedLifetime = 42, RnlFilesDeletedLifetime = 17 }
        };

        store.Save(original);
        var loaded = store.Load();

        Assert.Equal(360, loaded.Schedule.IntervalMinutes);
        Assert.Equal("06:30", loaded.Schedule.StartTime);
        Assert.Equal("dark", loaded.Theme);
        Assert.Equal(42, loaded.Counters.PopupsDismissedLifetime);
        Assert.Equal(17, loaded.Counters.RnlFilesDeletedLifetime);
    }

    [Fact]
    public void Load_CorruptFile_ReturnsDefaultsAndPreservesCorruptCopy()
    {
        File.WriteAllText(_tmpFile, "{ this is not valid json");
        var store = new ConfigStore(_tmpFile);

        var cfg = store.Load();

        Assert.Equal(1, cfg.Version);
        Assert.True(File.Exists(_tmpFile + ".corrupt"));
    }
}
