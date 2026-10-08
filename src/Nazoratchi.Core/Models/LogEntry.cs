namespace Nazoratchi.Core.Models;

/// <summary>
/// Represents a single log entry.
/// </summary>
public class LogEntry
{
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public LogEventType Type { get; set; }
    public string Detail { get; set; } = string.Empty;
    public LogResult Result { get; set; }
}
