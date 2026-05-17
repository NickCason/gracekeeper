using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using GraceKeeper.Core;
using GraceKeeper.UI.ViewModels;

namespace GraceKeeper.UI.Views;

public partial class SettingsView : Page
{
    public SettingsView()
    {
        InitializeComponent();
    }

    private SettingsViewModel Vm => (SettingsViewModel)DataContext;

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        await Vm.SaveAsync();
        MessageBox.Show("Saved.", "GraceKeeper", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void Back_Click(object sender, RoutedEventArgs e)
        => ((MainWindow)Window.GetWindow(this)).NavigateToDashboard();

    private void OpenGitHub_Click(object sender, RoutedEventArgs e)
        => Process.Start(new ProcessStartInfo("https://github.com/ncason/gracekeeper") { UseShellExecute = true });

    private void OpenInstallDir_Click(object sender, RoutedEventArgs e)
        => Process.Start("explorer.exe", System.AppContext.BaseDirectory);

    private void OpenLogDir_Click(object sender, RoutedEventArgs e)
        => Process.Start("explorer.exe", PathResolver.LogsDir);
}
