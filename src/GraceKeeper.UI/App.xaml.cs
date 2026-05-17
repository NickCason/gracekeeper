using System;
using System.Windows;
using Hardcodet.Wpf.TaskbarNotification;
using GraceKeeper.Core;

namespace GraceKeeper.UI;

public partial class App : Application
{
    private TaskbarIcon? _tray;
    private ThemeMonitor? _themeMonitor;
    private MainWindow? _mainWindow;

    private void OnStartup(object sender, StartupEventArgs e)
    {
        _themeMonitor = new ThemeMonitor();
        _themeMonitor.ThemeChanged += (_, theme) => ApplyTheme(theme);
        ApplyTheme(_themeMonitor.Current);

        _tray = (TaskbarIcon)FindResource("TrayIcon");

        // Supervisor for the AHK dismisser is unwired in v1: since the dismisser
        // runs via `AutoHotkey64.exe popup-dismisser.ahk`, we'd need PID/command-line
        // tracking to distinguish our script from any other AHK script the user
        // happens to be running. If the dismisser crashes, logging out + back in
        // restarts it via HKLM Run. DismisserSupervisor in Core stays for v2.

        // Best-effort update check: once per launch, max once per 24h. Silent on failure.
        _ = System.Threading.Tasks.Task.Run(async () =>
        {
            try
            {
                var store = new GraceKeeper.Core.ConfigStore(GraceKeeper.Core.PathResolver.ConfigPath);
                var cfg = store.Load();
                if (cfg.UpdateCheck.LastCheckedUtc.HasValue
                    && (System.DateTime.UtcNow - cfg.UpdateCheck.LastCheckedUtc.Value).TotalHours < 24)
                    return;

                using var client = new System.Net.Http.HttpClient { Timeout = System.TimeSpan.FromSeconds(8) };
                var checker = new GraceKeeper.Core.UpdateChecker(
                    client,
                    "https://api.github.com/repos/ncason/gracekeeper/releases/latest");
                var current = System.Reflection.Assembly.GetEntryAssembly()?.GetName().Version
                              ?? new System.Version(0, 1, 0);
                var info = await checker.CheckAsync(current);
                cfg.UpdateCheck.LastCheckedUtc = System.DateTime.UtcNow;
                if (info != null) cfg.UpdateCheck.LatestKnownVersion = info.Version.ToString();
                store.Save(cfg);
            }
            catch { /* truly silent — air-gapped VMs never see this */ }
        });
    }

    private void ApplyTheme(AppTheme theme)
    {
        var src = theme == AppTheme.Dark ? "Themes/Dark.xaml" : "Themes/Light.xaml";
        var dict = new ResourceDictionary { Source = new Uri(src, UriKind.Relative) };
        Resources.MergedDictionaries.Clear();
        Resources.MergedDictionaries.Add(dict);
    }

    private void TrayIcon_DoubleClick(object sender, RoutedEventArgs e) => ShowMainWindow();
    private void OpenWindow_Click(object sender, RoutedEventArgs e) => ShowMainWindow();
    private void OpenSettings_Click(object sender, RoutedEventArgs e) { ShowMainWindow(); _mainWindow!.NavigateToSettings(); }
    private void Pause_Click(object sender, RoutedEventArgs e)
    {
        var sentinel = new GraceKeeper.Core.DisabledSentinel(GraceKeeper.Core.PathResolver.DisabledSentinelPath);
        if (sentinel.IsDisabled) sentinel.Enable(); else sentinel.Disable();
    }

    private async void RunCleanerNow_Click(object sender, RoutedEventArgs e)
    {
        var scheduler = new GraceKeeper.Core.ScheduledTaskClient("GraceKeeper - Cleanup RNL");
        try { await scheduler.RunNowAsync(); } catch { /* task may not exist yet */ }
    }
    private void Exit_Click(object sender, RoutedEventArgs e) => Shutdown();

    private void ShowMainWindow()
    {
        if (_mainWindow == null || !_mainWindow.IsLoaded)
            _mainWindow = new MainWindow();
        _mainWindow.Show();
        _mainWindow.Activate();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _themeMonitor?.Dispose();
        _tray?.Dispose();
        base.OnExit(e);
    }
}
