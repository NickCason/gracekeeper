using System.Windows;
using GraceKeeper.UI.Views;

namespace GraceKeeper.UI;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Loaded += (_, _) => MainFrame.Navigate(new DashboardView());
    }

    public void NavigateToSettings() => MainFrame.Navigate(new SettingsView());
    public void NavigateToDashboard() => MainFrame.Navigate(new DashboardView());
}
