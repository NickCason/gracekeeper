using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Controls;
using WixToolset.Mba.Core;

namespace GraceKeeper.Bootstrapper.ViewModels;

public sealed class MainViewModel : INotifyPropertyChanged
{
    private readonly IEngine _engine;
    private readonly IBootstrapperCommand _command;
    private readonly GraceKeeperBootstrapper _ba;
    private UserControl? _currentPage;

    public event Action? RequestClose;
    public event PropertyChangedEventHandler? PropertyChanged;

    public UserControl? CurrentPage
    {
        get => _currentPage;
        set { _currentPage = value; OnPropertyChanged(); }
    }

    public MainViewModel(IEngine engine, IBootstrapperCommand command, GraceKeeperBootstrapper ba)
    {
        _engine = engine;
        _command = command;
        _ba = ba;
    }

    public void Initialize()
    {
        // Placeholder. Task 15 replaces this with real page navigation.
        var tb = new TextBlock { Text = "GraceKeeper Setup (BA loaded)", Margin = new System.Windows.Thickness(20) };
        CurrentPage = new UserControl { Content = tb };
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
