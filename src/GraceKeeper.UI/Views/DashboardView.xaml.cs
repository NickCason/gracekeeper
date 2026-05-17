using System.Windows;
using System.Windows.Controls;
using GraceKeeper.UI.ViewModels;

namespace GraceKeeper.UI.Views;

public partial class DashboardView : Page
{
    public DashboardView()
    {
        InitializeComponent();
    }

    private DashboardViewModel Vm => (DashboardViewModel)DataContext;

    private async void RunCleaner_Click(object sender, RoutedEventArgs e)
        => await Vm.RunCleanerNowAsync();

    private void Pause_Click(object sender, RoutedEventArgs e)
        => Vm.TogglePause();

    private void Settings_Click(object sender, RoutedEventArgs e)
        => ((MainWindow)Window.GetWindow(this)).NavigateToSettings();
}
