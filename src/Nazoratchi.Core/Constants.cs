namespace Nazoratchi.Core;

/// <summary>
/// Application constants.
/// </summary>
public static class Constants
{
    public const string AppName = "Nazoratchi";
    public static readonly string ConfigDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), AppName);
    public static readonly string ConfigFile = Path.Combine(ConfigDir, "config.json");
    public static readonly string SitesFile = Path.Combine(ConfigDir, "sites.json");
    public static readonly string LogsFile = Path.Combine(ConfigDir, "logs.json");
    
    public const string DnsUpstreamDefault = "8.8.8.8";
    public const string LocalDnsIp = "127.0.0.1";
    public const string ServiceName = "NazoratchiService";
    
    public const string Author = "Tursunpo'lat Iskandarov";
    public const string Company = "CUBO kompaniyasi";
    public const string Website = "https://cubo.uz";
    public const string AppVersion = "1.0.0";

    /// <summary>
    /// Standard AI chatbot domains to block when BlockAiChatbots is active.
    /// </summary>
    public static readonly string[] AiChatbotDomains = new[]
    {
        "chatgpt.com",
        "openai.com",
        "gemini.google.com",
        "bard.google.com",
        "grok.com",
        "x.ai",
        "qwen.ai",
        "qwenlm.ai",
        "tongyi.aliyun.com",
        "meta.ai",
        "copilot.microsoft.com",
        "claude.ai",
        "anthropic.com",
        "deepseek.com",
        "perplexity.ai"
    };
}
