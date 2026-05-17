using System;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Threading;
using Hardcodet.Wpf.TaskbarNotification;
using GraceKeeper.Core;

namespace GraceKeeper.UI;

public partial class App : Application
{
    private TaskbarIcon? _tray;
    private ThemeMonitor? _themeMonitor;
    private MainWindow? _mainWindow;
    private DismisserSupervisor? _supervisor;
    private DispatcherTimer? _supervisorTimer;

    private void OnStartup(object sender, StartupEventArgs e)
    {
        _themeMonitor = new ThemeMonitor();
        _themeMonitor.ThemeChanged += (_, theme) => ApplyTheme(theme);
        ApplyTheme(_themeMonitor.Current);

        _tray = (TaskbarIcon)FindResource("TrayIcon");

        StartDismisserSupervisor();
        StartUpdateCheckOnce();
    }

    private void StartDismisserSupervisor()
    {
        var installDir = Path.GetDirectoryName(Assembly.GetEntryAssembly()!.Location)!;
        _supervisor = new DismisserSupervisor(
            installDir: installDir,
            ahkExeName: "AutoHotkey64.exe",
            scriptName: "popup-dismisser.ahk",
            disabledSentinelPath: PathResolver.DisabledSentinelPath,
            pidFile: new DismisserPidFile(PathResolver.DismisserPidFilePath),
            probe: new DefaultProcessProbe(),
            launcher: new DefaultProcessLauncher(),
            cmdReader: new ProcessCommandLineReader(),
            logger: new SupervisorLogger(PathResolver.SupervisorLogPath));

        // Initial check + every 30s thereafter.
        _supervisor.EnsureRunning();
        _supervisorTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
        _supervisorTimer.Tick += (_, _) => _supervisor.EnsureRunning();
        _supervisorTimer.Start();
    }

    private void StartUpdateCheckOnce()
    {
        _ = System.Threading.Tasks.Task.Run(async () =>
        {
            try
            {
                var store = new ConfigStore(PathResolver.ConfigPath);
                var cfg = store.Load();
                if (cfg.UpdateCheck.LastCheckedUtc.HasValue
                    && (DateTime.UtcNow - cfg.UpdateCheck.LastCheckedUtc.Value).TotalHours < 24)
                    return;

                using var client = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(8) };
                var checker = new UpdateChecker(
                    client,
                    "https://api.github.com/repos/ncason/gracekeeper/releases/latest");
                var current = Assembly.GetEntryAssembly()?.GetName().Version ?? new Version(0, 1, 0);
                var info = await checker.CheckAsync(current);
                cfg.UpdateCheck.LastCheckedUtc = DateTime.UtcNow;
                if (info != null) cfg.UpdateCheck.LatestKnownVersion = info.Version.ToString();
                store.Save(cfg);
            }
            catch { /* truly silent */ }
        });
    }

    private void ApplyTheme(AppTheme theme)
    {
        // App.xaml merges Themes/Light.xaml + Controls/AnimatedCounter.xaml +
        // Controls/RunCleanerButton.xaml. We must only swap the theme dictionary
        // — clearing all merged dicts here would also wipe the control style
        // dicts, leaving AnimatedCounter and RunCleanerButton without templates
        // (rendered as invisible).
        var src = theme == AppTheme.Dark ? "Themes/Dark.xaml" : "Themes/Light.xaml";
        var dict = new ResourceDictionary { Source = new Uri(src, UriKind.Relative) };
        for (var i = Resources.MergedDictionaries.Count - 1; i >= 0; i--)
        {
            var existing = Resources.MergedDictionaries[i];
            if (existing.Source != null
                && existing.Source.OriginalString.StartsWith("Themes/", StringComparison.OrdinalIgnoreCase))
            {
                Resources.MergedDictionaries.RemoveAt(i);
            }
        }
        Resources.MergedDictionaries.Add(dict);
    }

    private void TrayIcon_DoubleClick(object sender, RoutedEventArgs e) => ShowMainWindow();
    private void OpenWindow_Click(object sender, RoutedEventArgs e) => ShowMainWindow();
    private void OpenSettings_Click(object sender, RoutedEventArgs e) { ShowMainWindow(); _mainWindow!.NavigateToSettings(); }
    private void Pause_Click(object sender, RoutedEventArgs e)
    {
        var sentinel = new DisabledSentinel(PathResolver.DisabledSentinelPath);
        if (sentinel.IsDisabled) sentinel.Enable(); else sentinel.Disable();
    }

    private async void RunCleanerNow_Click(object sender, RoutedEventArgs e)
    {
        var scheduler = new ScheduledTaskClient("GraceKeeper - Cleanup RNL");
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
        _supervisorTimer?.Stop();
        _themeMonitor?.Dispose();
        _tray?.Dispose();
        base.OnExit(e);
    }
}
