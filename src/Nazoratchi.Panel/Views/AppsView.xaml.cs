using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Nazoratchi.Core.Models;
using Nazoratchi.Core.Services;

namespace Nazoratchi.Panel.Views;

public partial class AppsView : UserControl
{
    private readonly ConfigManager _configManager;
    private readonly LogService _logService;

    public AppsView()
    {
        InitializeComponent();
        _configManager = new ConfigManager();
        _logService = new LogService(_configManager);
        LoadData();
    }

    public void LoadData()
    {
        try
        {
            var config = _configManager.LoadConfig();
            AppBlockToggle.IsChecked = config.IsAppBlockingEnabled;

            // Load recently blocked apps
            var appLogs = _logService.GetRecentLogs(50)
                .Where(l => l.Type == LogEventType.AppBlocked)
                .Select(l => new
                {
                    Timestamp = l.Timestamp.ToString("yyyy-MM-dd HH:mm:ss"),
                    Detail = l.Detail,
                    Result = l.Result == LogResult.Blocked ? "Bloklandi" : "Ruxsat berildi"
                })
                .ToList();

            BlockedAppsGrid.ItemsSource = appLogs;
        }
        catch
        {
            // Ignore
        }
    }

    private void AppBlockToggle_CheckedChanged(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded) return;

        var config = _configManager.LoadConfig();
        config.IsAppBlockingEnabled = AppBlockToggle.IsChecked == true;
        _configManager.SaveConfig(config);
    }
}
