using System;
using System.Windows;
using GraceKeeper.Core;

namespace GraceKeeper.UI;

public partial class App : Application
{
    private ThemeMonitor? _themeMonitor;

    private void OnStartup(object sender, StartupEventArgs e)
    {
        _themeMonitor = new ThemeMonitor();
        _themeMonitor.ThemeChanged += (_, theme) => ApplyTheme(theme);
        ApplyTheme(_themeMonitor.Current);

        var window = new MainWindow();
        window.Show();
    }

    private void ApplyTheme(AppTheme theme)
    {
        var src = theme == AppTheme.Dark ? "Themes/Dark.xaml" : "Themes/Light.xaml";
        var dict = new ResourceDictionary { Source = new Uri(src, UriKind.Relative) };
        Resources.MergedDictionaries.Clear();
        Resources.MergedDictionaries.Add(dict);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _themeMonitor?.Dispose();
        base.OnExit(e);
    }
}
