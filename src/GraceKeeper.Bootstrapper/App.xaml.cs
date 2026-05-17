using System;
using System.Windows;
using WixToolset.Mba.Core;
using GraceKeeper.Bootstrapper.ViewModels;

namespace GraceKeeper.Bootstrapper;

public class App : Application
{
    private readonly IEngine _engine;
    private readonly IBootstrapperCommand _command;
    private readonly GraceKeeperBootstrapper _ba;

    public App(IEngine engine, IBootstrapperCommand command, GraceKeeperBootstrapper ba)
    {
        _engine = engine;
        _command = command;
        _ba = ba;

        // Load theme resources — replaces InitializeComponent() which is unavailable in Library OutputType
        var theme = new ResourceDictionary
        {
            Source = new Uri("pack://application:,,,/GraceKeeper.Bootstrapper;component/Themes/Theme.xaml",
                             UriKind.Absolute)
        };
        Resources.MergedDictionaries.Add(theme);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;
    }

    protected override void OnStartup(System.Windows.StartupEventArgs e)
    {
        base.OnStartup(e);
        var vm = new MainViewModel(_engine, _command, _ba);
        var win = new MainWindow { DataContext = vm };
        vm.RequestClose += () => { win.Close(); Shutdown(); };
        win.Show();
        vm.Initialize();
    }
}
