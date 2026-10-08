using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Nazoratchi.Core;
using Nazoratchi.Core.Services;
using Nazoratchi.Core.Models;
using Nazoratchi.Service.Helpers;
using System.Net;
using System.Net.Sockets;

namespace Nazoratchi.Service.Workers;

/// <summary>
/// Worker service responsible for DNS filtering.
/// Intercepts DNS queries and blocks/allows based on Black/White list configuration.
/// </summary>
public class DnsFilterWorker : BackgroundService
{
    private readonly ILogger<DnsFilterWorker> _logger;
    private readonly ConfigManager _configManager;
    private readonly LogService _logService;
    private string[] _originalDns = Array.Empty<string>();

    public DnsFilterWorker(ILogger<DnsFilterWorker> logger, ConfigManager configManager, LogService logService)
    {
        _logger = logger;
        _configManager = configManager;
        _logService = logService;
    }

    public override Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("DnsFilterWorker starting...");
        try
        {
            _originalDns = NetworkHelper.GetCurrentDnsServers();
            NetworkHelper.SetSystemDns(Constants.LocalDnsIp);
            _logger.LogInformation("System DNS set to {Dns}", Constants.LocalDnsIp);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to set initial DNS.");
        }
        return base.StartAsync(cancellationToken);
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("DnsFilterWorker stopping...");
        try
        {
            NetworkHelper.RestoreOriginalDns(_originalDns);
            _logger.LogInformation("System DNS restored.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to restore original DNS.");
        }
        return base.StopAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        IPAddress upstreamIp = IPAddress.Parse("8.8.8.8");
        try
        {
            var config = _configManager.LoadConfig();
            if (!string.IsNullOrWhiteSpace(config.DnsUpstream) && IPAddress.TryParse(config.DnsUpstream.Trim(), out var parsed))
            {
                upstreamIp = parsed;
            }
        }
        catch
        {
            // Fallback to 8.8.8.8
        }
        var upstreamDns = new IPEndPoint(upstreamIp, 53);

        UdpClient? udpClient = null;
        try
        {
            udpClient = new UdpClient(53);
        }
        catch (SocketException ex)
        {
            _logger.LogError(ex, "Failed to bind to port 53. DNS filtering is disabled.");
            return;
        }

        using (udpClient)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var receiveResult = await udpClient.ReceiveAsync(stoppingToken);
                    var queryPacket = receiveResult.Buffer;
                    var endpoint = receiveResult.RemoteEndPoint;

                    var domain = DnsPacketHelper.ParseDomainFromQuery(queryPacket);
                    if (string.IsNullOrEmpty(domain))
                    {
                        continue;
                    }

                    if (IsBlocked(domain))
                    {
                        _logger.LogInformation("Blocked DNS query for: {Domain}", domain);
                        _logService.LogSiteBlocked(domain);
                        var responsePacket = DnsPacketHelper.BuildBlockedResponse(queryPacket);
                        await udpClient.SendAsync(responsePacket, responsePacket.Length, endpoint);
                    }
                    else
                    {
                        // Forward to upstream DNS
                        using var forwardClient = new UdpClient();
                        await forwardClient.SendAsync(queryPacket, queryPacket.Length, upstreamDns);

                        var forwardResultTask = forwardClient.ReceiveAsync();
                        if (await Task.WhenAny(forwardResultTask, Task.Delay(3000, stoppingToken)) == forwardResultTask)
                        {
                            var upstreamResponse = forwardResultTask.Result.Buffer;
                            await udpClient.SendAsync(upstreamResponse, upstreamResponse.Length, endpoint);
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing DNS request.");
                }
            }
        }
    }

    /// <summary>
    /// Checks whether the given domain should be blocked based on the current filter configuration.
    /// </summary>
    private bool IsBlocked(string domain)
    {
        try
        {
            var config = _configManager.LoadConfig();
            var sites = _configManager.LoadSites();
            return SiteMatcher.IsBlocked(domain, config.FilterMode, sites);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking if domain is blocked.");
            return false;
        }
    }
}
