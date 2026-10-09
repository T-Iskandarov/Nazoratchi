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
        // ChatGPT / OpenAI
        "chatgpt.com",
        "openai.com",
        "oaistatic.com",
        "oaiusercontent.com",

        // Gemini / Google Bard
        "gemini.google.com",
        "bard.google.com",

        // Grok / xAI
        "grok.com",
        "x.ai",

        // Qwen / Alibaba
        "qwen.ai",
        "qwenlm.ai",
        "tongyi.aliyun.com",

        // Meta AI
        "meta.ai",

        // Microsoft Copilot
        "copilot.microsoft.com",

        // Claude / Anthropic
        "claude.ai",
        "anthropic.com",
        "claudeusercontent.com",

        // DeepSeek
        "deepseek.com",
        "chat.deepseek.com",

        // Perplexity
        "perplexity.ai",
        "pplx.ai"
    };
}
