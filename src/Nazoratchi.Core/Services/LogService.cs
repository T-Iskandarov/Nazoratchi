using Nazoratchi.Core.Models;

namespace Nazoratchi.Core.Services;

/// <summary>
/// Service for logging events and retrieving logs.
/// </summary>
public class LogService
{
    private readonly ConfigManager _configManager;

    public LogService(ConfigManager configManager)
    {
        _configManager = configManager;
    }

    public void LogSiteBlocked(string domain)
    {
        _configManager.AddLog(new LogEntry
        {
            Type = LogEventType.SiteBlocked,
            Detail = domain,
            Result = LogResult.Blocked
        });
    }

    public void LogAppBlocked(string appName)
    {
        _configManager.AddLog(new LogEntry
        {
            Type = LogEventType.AppBlocked,
            Detail = appName,
            Result = LogResult.Blocked
        });
    }

    public void LogAdminAllowed(LogEventType type, string detail)
    {
        _configManager.AddLog(new LogEntry
        {
            Type = type,
            Detail = detail,
            Result = LogResult.AllowedByAdmin
        });
    }

    public List<LogEntry> GetRecentLogs(int count)
    {
        return _configManager.LoadLogs().Logs
            .OrderByDescending(l => l.Timestamp)
            .Take(count)
            .ToList();
    }

    public List<LogEntry> GetLogsByDate(DateTime from, DateTime to)
    {
        return _configManager.LoadLogs().Logs
            .Where(l => l.Timestamp >= from && l.Timestamp <= to)
            .OrderByDescending(l => l.Timestamp)
            .ToList();
    }

    public void ClearLogs()
    {
        _configManager.SaveLogs(new LogStore());
    }
}
