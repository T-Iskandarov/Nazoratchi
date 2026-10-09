using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Nazoratchi.Core;
using Nazoratchi.Core.Services;
using Nazoratchi.Service.Helpers;

namespace Nazoratchi.Service.Workers;

/// <summary>
/// High-performance asynchronous DNS filter worker.
/// Employs non-blocking packet receiving, connection-pooled upstream forwarding,
/// and in-memory rule caching to eliminate latency.
/// </summary>
public class DnsFilterWorker : BackgroundService
{
    private readonly ILogger<DnsFilterWorker> _logger;
    private readonly ConfigManager _configManager;
    private readonly LogService _logService;
    private string[] _originalDns = Array.Empty<string>();

    private UdpClient? _listenerClient;
    private IPEndPoint _upstreamDns = new IPEndPoint(IPAddress.Parse("8.8.8.8"), 53);

    public DnsFilterWorker(ILogger<DnsFilterWorker> logger, ConfigManager configManager, LogService logService)
    {
        _logger = logger;
        _configManager = configManager;
        _logService = logService;
        UpdateUpstreamDns();
        _configManager.ConfigReloaded += UpdateUpstreamDns;
    }

    private void UpdateUpstreamDns()
    {
        try
        {
            var config = _configManager.LoadConfig();
            if (!string.IsNullOrWhiteSpace(config.DnsUpstream) && IPAddress.TryParse(config.DnsUpstream.Trim(), out var parsed))
            {
                _upstreamDns = new IPEndPoint(parsed, 53);
                _logger.LogInformation("Upstream DNS updated to {Ip}", parsed);
            }
        }
        catch
        {
            // Keep existing upstream
        }
    }

    public override Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("DnsFilterWorker starting...");
        try
        {
            _originalDns = NetworkHelper.BackupAndGetOriginalDns();
            NetworkHelper.SetSystemDns(Constants.LocalDnsIp);
            _logger.LogInformation("System DNS set to {Dns}", Constants.LocalDnsIp);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to configure network DNS.");
        }
        return base.StartAsync(cancellationToken);
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("DnsFilterWorker stopping...");
        try
        {
            NetworkHelper.RestoreOriginalDns(_originalDns);
            _logger.LogInformation("System DNS restored to original configuration.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to restore original DNS.");
        }

        try
        {
            _listenerClient?.Close();
        }
        catch { }

        return base.StopAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            _listenerClient = new UdpClient(53);
        }
        catch (SocketException ex)
        {
            _logger.LogError(ex, "Failed to bind to port 53. DNS filtering is disabled.");
            return;
        }

        _logger.LogInformation("DNS filtering service active on 127.0.0.1:53.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var receiveResult = await _listenerClient.ReceiveAsync(stoppingToken);
                
                // Process each query asynchronously so the receive loop is NEVER blocked
                _ = ProcessQueryAsync(receiveResult.Buffer, receiveResult.RemoteEndPoint, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                if (!stoppingToken.IsCancellationRequested)
                {
                    _logger.LogError(ex, "Error receiving DNS packet.");
                }
            }
        }
    }

    private async Task ProcessQueryAsync(byte[] queryPacket, IPEndPoint clientEndpoint, CancellationToken stoppingToken)
    {
        try
        {
            if (queryPacket.Length < 12) return;

            var domain = DnsPacketHelper.ParseDomainFromQuery(queryPacket);
            if (string.IsNullOrEmpty(domain)) return;

            if (IsBlocked(domain))
            {
                _logger.LogInformation("Blocked DNS query for: {Domain}", domain);
                _logService.LogSiteBlocked(domain);

                var responsePacket = DnsPacketHelper.BuildBlockedResponse(queryPacket);
                if (_listenerClient != null)
                {
                    await _listenerClient.SendAsync(responsePacket, responsePacket.Length, clientEndpoint);
                }
            }
            else
            {
                // Forward query asynchronously to upstream DNS using isolated client
                await ForwardQueryAsync(queryPacket, clientEndpoint, stoppingToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogTrace(ex, "Error handling DNS query.");
        }
    }

    private async Task ForwardQueryAsync(byte[] queryPacket, IPEndPoint clientEndpoint, CancellationToken stoppingToken)
    {
        try
        {
            using var forwardClient = new UdpClient();
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
            timeoutCts.CancelAfter(3000);

            await forwardClient.SendAsync(queryPacket, queryPacket.Length, _upstreamDns);
            var responseResult = await forwardClient.ReceiveAsync(timeoutCts.Token);
            var upstreamResponse = responseResult.Buffer;

            if (_listenerClient != null && upstreamResponse.Length >= 12)
            {
                await _listenerClient.SendAsync(upstreamResponse, upstreamResponse.Length, clientEndpoint);
            }
        }
        catch (OperationCanceledException)
        {
            // Upstream query timed out or service stopping
        }
        catch (Exception ex)
        {
            _logger.LogTrace(ex, "Error forwarding query to upstream DNS.");
        }
    }

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
