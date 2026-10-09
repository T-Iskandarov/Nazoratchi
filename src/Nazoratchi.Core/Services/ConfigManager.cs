using System.Text.Json;
using System.Text.Json.Serialization;
using Nazoratchi.Core.Models;

namespace Nazoratchi.Core.Services;

/// <summary>
/// Manages thread-safe and cross-process safe persistence and caching of configuration, sites, and logs.
/// </summary>
public class ConfigManager
{
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly object _lock = new();

    // In-memory cached copies to eliminate hot-path disk I/O in worker loops
    private volatile AppConfig? _cachedConfig;
    private volatile SiteList? _cachedSites;
    private DateTime _configLastRead = DateTime.MinValue;
    private DateTime _sitesLastRead = DateTime.MinValue;

    private const int MaxLogEntries = 5000;

    public event Action? ConfigReloaded;
    public event Action? SitesReloaded;

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
        try
        {
            if (!Directory.Exists(Constants.ConfigDir))
            {
                Directory.CreateDirectory(Constants.ConfigDir);
            }
        }
        catch
        {
            // Ignore if directory already exists or created concurrently
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

            // Retry loop for cross-process file sharing tolerance
            for (int attempt = 0; attempt < 3; attempt++)
            {
                try
                {
                    using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    using var reader = new StreamReader(stream);
                    var content = reader.ReadToEnd();
                    if (string.IsNullOrWhiteSpace(content))
                    {
                        return defaultValue;
                    }
                    return JsonSerializer.Deserialize<T>(content, _jsonOptions) ?? defaultValue;
                }
                catch (IOException)
                {
                    if (attempt < 2) Thread.Sleep(50);
                }
                catch
                {
                    return defaultValue;
                }
            }

            return defaultValue;
        }
    }

    private void SaveToFile<T>(string path, T data)
    {
        EnsureConfigDirectory();

        lock (_lock)
        {
            var content = JsonSerializer.Serialize(data, _jsonOptions);
            var tempPath = path + ".tmp." + Guid.NewGuid().ToString("N");

            for (int attempt = 0; attempt < 3; attempt++)
            {
                try
                {
                    // Write to temp file first to ensure atomic updates
                    using (var stream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.Read))
                    using (var writer = new StreamWriter(stream))
                    {
                        writer.Write(content);
                        writer.Flush();
                    }

                    // Atomic replacement
                    File.Move(tempPath, path, overwrite: true);
                    return;
                }
                catch (IOException)
                {
                    if (attempt < 2) Thread.Sleep(60);
                }
                catch
                {
                    break;
                }
                finally
                {
                    if (File.Exists(tempPath))
                    {
                        try { File.Delete(tempPath); } catch { }
                    }
                }
            }
        }
    }

    /// <summary>
    /// Loads AppConfig, using in-memory cache if file has not changed.
    /// </summary>
    public AppConfig LoadConfig()
    {
        try
        {
            var fileInfo = new FileInfo(Constants.ConfigFile);
            if (_cachedConfig != null && fileInfo.Exists && fileInfo.LastWriteTimeUtc <= _configLastRead)
            {
                return _cachedConfig;
            }

            var config = LoadFromFile(Constants.ConfigFile, new AppConfig());
            _cachedConfig = config;
            _configLastRead = fileInfo.Exists ? fileInfo.LastWriteTimeUtc : DateTime.UtcNow;
            return config;
        }
        catch
        {
            return _cachedConfig ?? new AppConfig();
        }
    }

    /// <summary>
    /// Saves AppConfig and updates cache immediately.
    /// </summary>
    public void SaveConfig(AppConfig config)
    {
        SaveToFile(Constants.ConfigFile, config);
        _cachedConfig = config;
        _configLastRead = DateTime.UtcNow;

        try
        {
            BrowserPolicyHelper.ClearUrlBlocklist();
        }
        catch { }

        ConfigReloaded?.Invoke();
    }

    /// <summary>
    /// Loads SiteList, using in-memory cache if file has not changed.
    /// </summary>
    public SiteList LoadSites()
    {
        try
        {
            var fileInfo = new FileInfo(Constants.SitesFile);
            if (_cachedSites != null && fileInfo.Exists && fileInfo.LastWriteTimeUtc <= _sitesLastRead)
            {
                return _cachedSites;
            }

            var siteList = LoadFromFile(Constants.SitesFile, new SiteList());
            if (siteList.Sites != null && siteList.Sites.Count > 0)
            {
                siteList.BlacklistSites ??= new List<SiteRule>();
                if (siteList.BlacklistSites.Count == 0)
                {
                    siteList.BlacklistSites = new List<SiteRule>(siteList.Sites);
                }
                siteList.Sites.Clear();
                SaveToFile(Constants.SitesFile, siteList);
            }

            _cachedSites = siteList;
            _sitesLastRead = fileInfo.Exists ? fileInfo.LastWriteTimeUtc : DateTime.UtcNow;
            return siteList;
        }
        catch
        {
            return _cachedSites ?? new SiteList();
        }
    }

    /// <summary>
    /// Saves SiteList and updates cache immediately.
    /// </summary>
    public void SaveSites(SiteList sites)
    {
        sites.Sites?.Clear();
        SaveToFile(Constants.SitesFile, sites);
        _cachedSites = sites;
        _sitesLastRead = DateTime.UtcNow;

        try
        {
            BrowserPolicyHelper.ClearUrlBlocklist();
        }
        catch { }

        SitesReloaded?.Invoke();
    }

    public LogStore LoadLogs() => LoadFromFile(Constants.LogsFile, new LogStore());
    
    public void SaveLogs(LogStore logs)
    {
        // Keep logs capped to prevent unbounded disk/memory growth
        if (logs.Logs.Count > MaxLogEntries)
        {
            logs.Logs = logs.Logs.TakeLast(MaxLogEntries).ToList();
        }
        SaveToFile(Constants.LogsFile, logs);
    }

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
