using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Nazoratchi.Core.Models;
using Nazoratchi.Core.Services;

namespace Nazoratchi.Panel.Views;

public partial class LogsView : UserControl
{
    public class LogModel
    {
        public string Timestamp { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string Detail { get; set; } = string.Empty;
        public string Result { get; set; } = string.Empty;
        public string Color => Result == "Bloklandi" ? "Red" : "LimeGreen";
    }

    private readonly ConfigManager _configManager;
    private readonly LogService _logService;
    private ObservableCollection<LogModel> _logs = new();

    public LogsView()
    {
        InitializeComponent();
        _configManager = new ConfigManager();
        _logService = new LogService(_configManager);
        LogsGrid.ItemsSource = _logs;
        StartDatePicker.SelectedDate = DateTime.Today;
        EndDatePicker.SelectedDate = DateTime.Today;
        LoadData();
    }

    public void LoadData()
    {
        _logs.Clear();
        var logs = _logService.GetRecentLogs(200);
        foreach (var log in logs)
        {
            _logs.Add(ToModel(log));
        }
    }

    private static LogModel ToModel(LogEntry entry) => new()
    {
        Timestamp = entry.Timestamp.ToString("yyyy-MM-dd HH:mm:ss"),
        Type = entry.Type == LogEventType.SiteBlocked ? "Sayt" : "Dastur",
        Detail = entry.Detail,
        Result = entry.Result == LogResult.Blocked ? "Bloklandi" : "Ruxsat berildi"
    };

    private void FilterChanged(object sender, SelectionChangedEventArgs e)
    {
        // Trigger filter when type combo changes
        ApplyFilter();
    }

    private void FilterButton_Click(object sender, RoutedEventArgs e)
    {
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        if (!IsLoaded) return;

        var from = StartDatePicker.SelectedDate ?? DateTime.Today;
        var to = (EndDatePicker.SelectedDate ?? DateTime.Today).AddDays(1).AddSeconds(-1);

        var logs = _logService.GetLogsByDate(from, to);

        // Filter by type
        var typeIndex = TypeComboBox.SelectedIndex;
        if (typeIndex == 1) // Saytlar
        {
            logs = logs.Where(l => l.Type == LogEventType.SiteBlocked).ToList();
        }
        else if (typeIndex == 2) // Dasturlar
        {
            logs = logs.Where(l => l.Type == LogEventType.AppBlocked).ToList();
        }

        _logs.Clear();
        foreach (var log in logs)
        {
            _logs.Add(ToModel(log));
        }
    }

    private void ClearLogsButton_Click(object sender, RoutedEventArgs e)
    {
        var result = MessageBox.Show("Barcha loglarni tozalashni xohlaysizmi?", "Tasdiqlash", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (result == MessageBoxResult.Yes)
        {
            _logService.ClearLogs();
            _logs.Clear();
        }
    }
}
