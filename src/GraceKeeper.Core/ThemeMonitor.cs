using System;
using Microsoft.Win32;

namespace GraceKeeper.Core;

public enum AppTheme { Light, Dark }

public sealed class ThemeMonitor : IDisposable
{
    private const string KeyPath = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
    private const string ValueName = "AppsUseLightTheme";

    public event EventHandler<AppTheme>? ThemeChanged;

    public ThemeMonitor()
    {
        SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
    }

    public AppTheme Current
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(KeyPath);
            var v = key?.GetValue(ValueName) as int?;
            return v == 0 ? AppTheme.Dark : AppTheme.Light;  // default to Light if missing
        }
    }

    private void OnUserPreferenceChanged(object? sender, UserPreferenceChangedEventArgs e)
    {
        if (e.Category == UserPreferenceCategory.General)
            ThemeChanged?.Invoke(this, Current);
    }

    public void Dispose()
    {
        SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
    }
}
