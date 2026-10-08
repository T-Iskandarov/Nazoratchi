using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Nazoratchi.Service.Helpers;
using System.IO;

namespace Nazoratchi.Service.Workers;

/// <summary>
/// Worker service responsible for monitoring configuration changes and DNS consistency.
/// </summary>
public class ConfigWatcherWorker : BackgroundService
{
    private readonly ILogger<ConfigWatcherWorker> _logger;
    private FileSystemWatcher? _watcher;
    private readonly string _configDirectory;

    public ConfigWatcherWorker(ILogger<ConfigWatcherWorker> logger)
    {
        _logger = logger;
        _configDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Nazoratchi");
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ConfigWatcherWorker starting...");
        
        try
        {
            if (!Directory.Exists(_configDirectory))
            {
                Directory.CreateDirectory(_configDirectory);
            }

            _watcher = new FileSystemWatcher(_configDirectory);
            _watcher.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.DirectoryName;
            _watcher.Filter = "*.json";
            
            _watcher.Changed += OnChanged;
            _watcher.Created += OnChanged;
            
            _watcher.EnableRaisingEvents = true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start FileSystemWatcher.");
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Check if DNS is still 127.0.0.1
                var currentDns = NetworkHelper.GetCurrentDnsServers();
                if (currentDns.Length == 0 || currentDns[0] != "127.0.0.1")
                {
                    _logger.LogWarning("System DNS was changed. Restoring to 127.0.0.1.");
                    NetworkHelper.SetSystemDns("127.0.0.1");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking DNS status.");
            }

            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        }
    }

    private void OnChanged(object sender, FileSystemEventArgs e)
    {
        _logger.LogInformation("Configuration file changed: {FullPath}", e.FullPath);
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        if (_watcher != null)
        {
            _watcher.EnableRaisingEvents = false;
            _watcher.Dispose();
        }
        return base.StopAsync(cancellationToken);
    }
}
