using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using GraceKeeper.Core;

namespace GraceKeeper.UI.ViewModels;

public sealed class DashboardViewModel : ObservableObject
{
    private readonly LogTailer _dismisserTailer;
    private readonly LogTailer _cleanerTailer;
    private readonly ConfigStore _configStore;
    private readonly DisabledSentinel _sentinel;
    private readonly DispatcherTimer _pollTimer;
    private static readonly Regex _deletedCountRegex = new(@"(?:deleted|refreshed)=(\d+)", RegexOptions.Compiled);

    private long _popupsDismissedLifetime;
    private long _rnlFilesDeletedLifetime;
    private int _popupsToday;
    private int _filesToday;
    private string _statusText = "Healthy";
    private string _timeSinceLastCleanText = "no runs yet";
    private CleanerButtonState _cleanerState = CleanerButtonState.Idle;

    public CleanerButtonState CleanerState
    {
        get => _cleanerState;
        private set => Set(ref _cleanerState, value);
    }

    public long PopupsDismissedLifetime { get => _popupsDismissedLifetime; private set => Set(ref _popupsDismissedLifetime, value); }
    public long RnlFilesDeletedLifetime { get => _rnlFilesDeletedLifetime; private set => Set(ref _rnlFilesDeletedLifetime, value); }
    public int PopupsToday { get => _popupsToday; private set => Set(ref _popupsToday, value); }
    public int FilesToday { get => _filesToday; private set => Set(ref _filesToday, value); }
    public string StatusText { get => _statusText; private set => Set(ref _statusText, value); }
    public string TimeSinceLastCleanText { get => _timeSinceLastCleanText; private set => Set(ref _timeSinceLastCleanText, value); }

    public ObservableCollection<LogEvent> RecentActivity { get; } = new();

    public DashboardViewModel()
    {
        _configStore = new ConfigStore(PathResolver.ConfigPath);
        _dismisserTailer = new LogTailer(PathResolver.DismisserLogPath);
        _cleanerTailer = new LogTailer(PathResolver.CleanerLogPath);
        _sentinel = new DisabledSentinel(PathResolver.DisabledSentinelPath);

        var cfg = _configStore.Load();
        _popupsDismissedLifetime = cfg.Counters.PopupsDismissedLifetime;
        _rnlFilesDeletedLifetime = cfg.Counters.RnlFilesDeletedLifetime;

        _pollTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _pollTimer.Tick += (_, _) => RefreshFromLogs();
        _pollTimer.Start();
        RefreshFromLogs();
    }

    private void RefreshFromLogs()
    {
        var cfg = _configStore.Load();

        var newDismiss = _dismisserTailer.ReadNew(cfg.LogOffsets.DismisserLogOffset, out var newDismissOffset);
        var newClean = _cleanerTailer.ReadNew(cfg.LogOffsets.CleanerLogOffset, out var newCleanOffset);

        var midnight = DateTime.Today;

        foreach (var ev in newDismiss.Where(e => e.Kind == LogEventKind.Dismiss))
        {
            PopupsDismissedLifetime++;
            if (ev.Timestamp >= midnight) PopupsToday++;
            RecentActivity.Insert(0, ev);
        }
        foreach (var ev in newClean.Where(e => e.Kind == LogEventKind.Clean))
        {
            var m = _deletedCountRegex.Match(ev.Description);
            if (m.Success && int.TryParse(m.Groups[1].Value, out var n))
            {
                RnlFilesDeletedLifetime += n;
                if (ev.Timestamp >= midnight) FilesToday += n;
            }
            RecentActivity.Insert(0, ev);
        }

        while (RecentActivity.Count > 50) RecentActivity.RemoveAt(RecentActivity.Count - 1);

        cfg.LogOffsets.DismisserLogOffset = newDismissOffset;
        cfg.LogOffsets.CleanerLogOffset = newCleanOffset;
        cfg.Counters.PopupsDismissedLifetime = PopupsDismissedLifetime;
        cfg.Counters.RnlFilesDeletedLifetime = RnlFilesDeletedLifetime;
        _configStore.Save(cfg);

        var lastClean = RecentActivity.FirstOrDefault(e => e.Kind == LogEventKind.Clean);
        TimeSinceLastCleanText = lastClean == null ? "no runs yet" : FormatElapsed(DateTime.Now - lastClean.Timestamp);

        StatusText = _sentinel.IsDisabled ? "Paused" : "Healthy";
    }

    private static string FormatElapsed(TimeSpan t)
    {
        if (t.TotalHours >= 1) return $"{(int)t.TotalHours}h ago";
        if (t.TotalMinutes >= 1) return $"{(int)t.TotalMinutes}m ago";
        return "just now";
    }

    public async Task RunCleanerNowAsync()
    {
        if (CleanerState != CleanerButtonState.Idle) return;

        CleanerState = CleanerButtonState.Running;
        var logWriter = new CleanerLogWriter(PathResolver.CleanerLogPath, new SystemClock());
        try
        {
            var probe = new EchoControllerProbe(new WmiProcessTreeReader());
            var activity = probe.GetActivity();
            var mode = CleanupMode.Runtime;
            if (activity.Count > 0)
            {
                var families = string.Join(", ", activity.FamilyNames);
                var msg = $"Echo has {activity.Count} active controller(s) ({families}). Cleanup may fault them if a service bounce becomes necessary.";
                var result = MessageBox.Show(msg, "Confirm Run Cleaner", MessageBoxButton.OKCancel, MessageBoxImage.Warning);
                if (result != MessageBoxResult.OK)
                {
                    CleanerState = CleanerButtonState.Idle;
                    return;
                }
                mode = CleanupMode.ManualForce;
            }

            var bouncer = new ServiceBouncer(
                new Win32ServiceController(),
                new[]
                {
                    "FactoryTalk Logix Echo Message Broker",
                    "FactoryTalk Logix Echo Service",
                    "FactoryTalk Activation Service",
                    "FTActivationBoost"
                },
                TimeSpan.FromSeconds(10));
            var cleaner = new RnlCleaner(PathResolver.RnlTargetDir, probe, bouncer);

            var runResult = await Task.Run(() => cleaner.RunAsync(mode, default));
            logWriter.WriteResult(runResult);

            CleanerState = CleanerButtonState.Done;
            await Task.Delay(1200);
            CleanerState = CleanerButtonState.Idle;
        }
        catch (Exception ex)
        {
            logWriter.WriteFailed(ex.GetType().Name, ex.Message);
            CleanerState = CleanerButtonState.Idle;
        }
    }

    public void TogglePause()
    {
        if (_sentinel.IsDisabled) _sentinel.Enable();
        else _sentinel.Disable();
        StatusText = _sentinel.IsDisabled ? "Paused" : "Healthy";
    }
}

public enum CleanerButtonState
{
    Idle,
    Running,
    Done,
}
