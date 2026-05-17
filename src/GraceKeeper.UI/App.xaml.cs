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
