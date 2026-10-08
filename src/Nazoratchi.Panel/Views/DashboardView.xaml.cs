using System;
using System.Linq;
using System.ServiceProcess;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using MaterialDesignThemes.Wpf;
using Nazoratchi.Core;
using Nazoratchi.Core.Models;
using Nazoratchi.Core.Services;

namespace Nazoratchi.Panel.Views;

public partial class DashboardView : UserControl
{
    private readonly ConfigManager _configManager;
    private readonly LogService _logService;

    public DashboardView()
    {
        InitializeComponent();
        _configManager = new ConfigManager();
        _logService = new LogService(_configManager);
        Loaded += (s, e) => LoadData();
        LoadData();
    }

    private void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        LoadData();
    }

    public void LoadData()
    {
        try
        {
            var config = _configManager.LoadConfig();
            var todayLogs = _logService.GetLogsByDate(DateTime.Today, DateTime.Now);

            BlockedSitesCount.Text = todayLogs.Count(l => l.Type == LogEventType.SiteBlocked).ToString();
            BlockedAppsCount.Text = todayLogs.Count(l => l.Type == LogEventType.AppBlocked).ToString();
            FilterModeText.Text = config.FilterMode == FilterMode.BlackList ? "Qora ro'yxat" : "Oq ro'yxat";

            // Check service status
            try
            {
                using var sc = new ServiceController(Nazoratchi.Core.Constants.ServiceName);
                if (sc.Status == ServiceControllerStatus.Running)
                {
                    ServiceStatusText.Text = "Ishlamoqda";
                    ServiceStatusText.Foreground = new SolidColorBrush(Color.FromRgb(46, 125, 50)); // Green
                    ServiceStatusIcon.Kind = PackIconKind.CheckCircle;
                    ServiceStatusIcon.Foreground = new SolidColorBrush(Color.FromRgb(46, 125, 50));
                }
                else
                {
                    ServiceStatusText.Text = "To'xtatilgan";
                    ServiceStatusText.Foreground = new SolidColorBrush(Color.FromRgb(198, 40, 40)); // Red
                    ServiceStatusIcon.Kind = PackIconKind.CloseCircle;
                    ServiceStatusIcon.Foreground = new SolidColorBrush(Color.FromRgb(198, 40, 40));
                }
            }
            catch
            {
                ServiceStatusText.Text = "O'rnatilmagan";
                ServiceStatusText.Foreground = new SolidColorBrush(Color.FromRgb(245, 124, 0)); // Orange
                ServiceStatusIcon.Kind = PackIconKind.AlertCircle;
                ServiceStatusIcon.Foreground = new SolidColorBrush(Color.FromRgb(245, 124, 0));
            }

            // Load recent logs
            var recentLogs = _logService.GetRecentLogs(10);
            RecentLogsGrid.ItemsSource = recentLogs.Select(l => new
            {
                Timestamp = l.Timestamp.ToString("yyyy-MM-dd HH:mm:ss"),
                Type = l.Type == LogEventType.SiteBlocked ? "Sayt" : "Dastur",
                Detail = l.Detail,
                Result = l.Result == LogResult.Blocked ? "Bloklandi" : "Ruxsat berildi"
            }).ToList();
        }
        catch
        {
            // Dashboard should never crash
        }
    }
}
