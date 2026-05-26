using System;
using System.Threading.Tasks;
using System.Windows.Data;
using GraceKeeper.Core;

namespace GraceKeeper.UI.ViewModels;

public sealed class SettingsViewModel : ObservableObject
{
    private readonly ConfigStore _store;
    private int _intervalHours;
    private string _startTime = "03:00";
    private string _theme = "auto";

    public int IntervalHours { get => _intervalHours; set => Set(ref _intervalHours, value); }
    public string StartTime { get => _startTime; set => Set(ref _startTime, value); }
    public string Theme { get => _theme; set => Set(ref _theme, value); }

    public string Version =>
        System.Reflection.Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? "dev";

    public SettingsViewModel()
    {
        _store = new ConfigStore(PathResolver.ConfigPath);
        var cfg = _store.Load();
        _intervalHours = cfg.Cleaner.IntervalHours;
        _startTime = cfg.Schedule.StartTime;
        _theme = cfg.Theme;
    }

    public Task SaveAsync()
    {
        var cfg = _store.Load();
        cfg.Cleaner.IntervalHours = IntervalHours;
        cfg.Schedule.StartTime = StartTime;
        cfg.Theme = Theme;
        _store.Save(cfg);
        // The CleanupScheduler re-reads IntervalHours from config on every tick via its
        // intervalHoursProvider delegate — no further action needed here.
        // The schtask is registered at install time with a fixed schedule and is never
        // updated by the dashboard in v0.4.
        return Task.CompletedTask;
    }
}

public sealed class ThemeRadioConverter : IValueConverter
{
    public static readonly ThemeRadioConverter Auto = new("auto");
    public static readonly ThemeRadioConverter Light = new("light");
    public static readonly ThemeRadioConverter Dark = new("dark");

    private readonly string _target;
    private ThemeRadioConverter(string t) { _target = t; }

    public object Convert(object value, Type t, object p, System.Globalization.CultureInfo c)
        => (value as string) == _target;

    public object ConvertBack(object value, Type t, object p, System.Globalization.CultureInfo c)
        => (value is bool b && b) ? _target : Binding.DoNothing;
}
