namespace Nazoratchi.Core.Models;

/// <summary>
/// Application configuration settings.
/// </summary>
public class AppConfig
{
    public string? PasswordHash { get; set; }
    public string? RecoveryKeyHash { get; set; }
    public FilterMode FilterMode { get; set; } = FilterMode.BlackList;
    public bool IsAppBlockingEnabled { get; set; }
    public string DnsUpstream { get; set; } = Constants.DnsUpstreamDefault;
    public bool ServiceAutoStart { get; set; } = true;
    public bool BlockAiChatbots { get; set; } = false;
}
