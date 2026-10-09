using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Nazoratchi.Core;
using Nazoratchi.Core.Services;
using Nazoratchi.Service.Helpers;

namespace Nazoratchi.Service.Workers;

/// <summary>
/// Worker service responsible for monitoring configuration changes and DNS consistency.
/// Informs ConfigManager to reload cached state immediately when files change.
/// </summary>
public class ConfigWatcherWorker : BackgroundService
{
    private readonly ILogger<ConfigWatcherWorker> _logger;
    private readonly ConfigManager _configManager;
    private FileSystemWatcher? _watcher;

    public ConfigWatcherWorker(ILogger<ConfigWatcherWorker> logger, ConfigManager configManager)
    {
        _logger = logger;
        _configManager = configManager;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ConfigWatcherWorker starting...");

        try
        {
            if (!Directory.Exists(Constants.ConfigDir))
            {
                Directory.CreateDirectory(Constants.ConfigDir);
            }

            _watcher = new FileSystemWatcher(Constants.ConfigDir);
            _watcher.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName;
            _watcher.Filter = "*.json";

            _watcher.Changed += OnChanged;
            _watcher.Created += OnChanged;
            _watcher.Renamed += OnRenamed;

            _watcher.EnableRaisingEvents = true;
            SyncBrowserPolicies();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start FileSystemWatcher.");
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Verify that system DNS still points to 127.0.0.1
                var currentDns = NetworkHelper.GetCurrentDnsServers();
                if (currentDns.Length == 0 || currentDns[0] != Constants.LocalDnsIp)
                {
                    _logger.LogWarning("System DNS was altered. Restoring to {Dns}.", Constants.LocalDnsIp);
                    NetworkHelper.SetSystemDns(Constants.LocalDnsIp);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking DNS status.");
            }

            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        }
    }

    private void OnRenamed(object sender, RenamedEventArgs e) => HandleConfigFileChange(e.Name);

    private void OnChanged(object sender, FileSystemEventArgs e) => HandleConfigFileChange(e.Name);

    private void HandleConfigFileChange(string? fileName)
    {
        try
        {
            _logger.LogInformation("Configuration file changed on disk: {FileName}", fileName);
            if (string.Equals(fileName, "config.json", StringComparison.OrdinalIgnoreCase))
            {
                _configManager.LoadConfig();
                SyncBrowserPolicies();
                NetworkHelper.FlushDns();
            }
            else if (string.Equals(fileName, "sites.json", StringComparison.OrdinalIgnoreCase))
            {
                _configManager.LoadSites();
                SyncBrowserPolicies();
                NetworkHelper.FlushDns();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing configuration file change.");
        }
    }

    private void SyncBrowserPolicies()
    {
        try
        {
            BrowserPolicyHelper.ClearUrlBlocklist();
            _logger.LogInformation("Ensured browser URLBlocklist policies are cleared (Pure DNS mode).");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error clearing browser policies.");
        }
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        if (_watcher != null)
        {
            _watcher.EnableRaisingEvents = false;
            _watcher.Dispose();
        }

        try
        {
            BrowserPolicyHelper.ClearUrlBlocklist();
            _logger.LogInformation("Cleared browser URLBlocklist policies on service stop.");
        }
        catch { }

        return base.StopAsync(cancellationToken);
    }
}
