namespace Nazoratchi.Core.Models;

/// <summary>
/// Container for log entries.
/// </summary>
public class LogStore
{
    public List<LogEntry> Logs { get; set; } = new();
}
