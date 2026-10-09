using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Navigation;

namespace Nazoratchi.Panel.Views;

public partial class MainWindow : Window
{
    private DashboardView _dashboardView;
    private SitesView _sitesView;
    private AppsView _appsView;
    private LogsView _logsView;
    private SettingsView _settingsView;

    public MainWindow()
    {
        InitializeComponent();
        
        _dashboardView = new DashboardView();
        _sitesView = new SitesView();
        _appsView = new AppsView();
        _logsView = new LogsView();
        _settingsView = new SettingsView();

        NavListBox.SelectedIndex = 0;
    }

    private void NavListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (NavListBox.SelectedItem is ListBoxItem item && item.Tag != null)
        {
            switch (item.Tag.ToString())
            {
                case "Dashboard":
                    _dashboardView.LoadData();
                    MainContent.Content = _dashboardView;
                    break;
                case "Sites":
                    MainContent.Content = _sitesView;
                    break;
                case "Apps":
                    MainContent.Content = _appsView;
                    break;
                case "Logs":
                    _logsView.LoadData();
                    MainContent.Content = _logsView;
                    break;
                case "Settings":
                    _settingsView.RefreshServiceStatus();
                    MainContent.Content = _settingsView;
                    break;
            }
        }
    }

    private void CuboLogo_MouseDown(object sender, MouseButtonEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo("https://cubo.uz") { UseShellExecute = true });
        }
        catch { }
    }

    private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
        e.Handled = true;
    }
}
