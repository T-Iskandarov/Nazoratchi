using System.Text.Json;
using System.Text.Json.Serialization;
using Nazoratchi.Core.Models;

namespace Nazoratchi.Core.Services;

/// <summary>
/// Manages loading and saving of configuration and data files.
/// </summary>
public class ConfigManager
{
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly object _lock = new();

    public ConfigManager()
    {
        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
        };
    }

    /// <summary>
    /// Ensures that the configuration directory exists.
    /// </summary>
    public void EnsureConfigDirectory()
    {
        if (!Directory.Exists(Constants.ConfigDir))
        {
            Directory.CreateDirectory(Constants.ConfigDir);
        }
    }

    private T LoadFromFile<T>(string path, T defaultValue)
    {
        EnsureConfigDirectory();
        
        lock (_lock)
        {
            if (!File.Exists(path))
            {
                SaveToFile(path, defaultValue);
                return defaultValue;
            }

            try
            {
                var content = File.ReadAllText(path);
                return JsonSerializer.Deserialize<T>(content, _jsonOptions) ?? defaultValue;
            }
            catch
            {
                return defaultValue;
            }
        }
    }

    private void SaveToFile<T>(string path, T data)
    {
        EnsureConfigDirectory();
        
        lock (_lock)
        {
            var content = JsonSerializer.Serialize(data, _jsonOptions);
            File.WriteAllText(path, content);
        }
    }

    public AppConfig LoadConfig() => LoadFromFile(Constants.ConfigFile, new AppConfig());
    public void SaveConfig(AppConfig config) => SaveToFile(Constants.ConfigFile, config);

    public SiteList LoadSites()
    {
        var siteList = LoadFromFile(Constants.SitesFile, new SiteList());
        if (siteList.BlacklistSites.Count == 0 && siteList.Sites.Count > 0)
        {
            siteList.BlacklistSites = new List<SiteRule>(siteList.Sites);
        }
        return siteList;
    }
    public void SaveSites(SiteList sites) => SaveToFile(Constants.SitesFile, sites);

    public LogStore LoadLogs() => LoadFromFile(Constants.LogsFile, new LogStore());
    public void SaveLogs(LogStore logs) => SaveToFile(Constants.LogsFile, logs);

    public void AddLog(LogEntry entry)
    {
        lock (_lock)
        {
            var logs = LoadLogs();
            logs.Logs.Add(entry);
            SaveLogs(logs);
        }
    }
}
