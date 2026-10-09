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
    private UdpClient? _upstreamClient;
    private IPEndPoint _upstreamDns = new IPEndPoint(IPAddress.Parse("8.8.8.8"), 53);

    // Map DNS Transaction ID -> TaskCompletionSource for upstream response demultiplexing
    private readonly ConcurrentDictionary<ushort, TaskCompletionSource<byte[]>> _pendingQueries = new();

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
            _upstreamClient?.Close();
        }
        catch { }

        return base.StopAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            _listenerClient = new UdpClient(53);
            _upstreamClient = new UdpClient();
        }
        catch (SocketException ex)
        {
            _logger.LogError(ex, "Failed to bind to port 53. DNS filtering is disabled.");
            return;
        }

        // Start background worker for reading responses from upstream DNS
        var upstreamReceiverTask = ReceiveUpstreamResponsesAsync(stoppingToken);

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

        await upstreamReceiverTask;
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
                // Forward upstream using shared client and transaction ID mapping
                ushort queryId = (ushort)((queryPacket[0] << 8) | queryPacket[1]);
                var tcs = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);
                _pendingQueries[queryId] = tcs;

                try
                {
                    if (_upstreamClient != null)
                    {
                        await _upstreamClient.SendAsync(queryPacket, queryPacket.Length, _upstreamDns);
                    }

                    // Await response with 3.5s timeout
                    var timeoutTask = Task.Delay(3500, stoppingToken);
                    var completedTask = await Task.WhenAny(tcs.Task, timeoutTask);

                    if (completedTask == tcs.Task)
                    {
                        var upstreamResponse = await tcs.Task;
                        if (_listenerClient != null)
                        {
                            await _listenerClient.SendAsync(upstreamResponse, upstreamResponse.Length, clientEndpoint);
                        }
                    }
                }
                finally
                {
                    _pendingQueries.TryRemove(queryId, out _);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogTrace(ex, "Error handling DNS query.");
        }
    }

    private async Task ReceiveUpstreamResponsesAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (_upstreamClient == null) break;

                var result = await _upstreamClient.ReceiveAsync(stoppingToken);
                var buffer = result.Buffer;

                if (buffer.Length >= 12)
                {
                    ushort responseId = (ushort)((buffer[0] << 8) | buffer[1]);
                    if (_pendingQueries.TryGetValue(responseId, out var tcs))
                    {
                        tcs.TrySetResult(buffer);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                if (!stoppingToken.IsCancellationRequested)
                {
                    _logger.LogTrace(ex, "Error receiving upstream response.");
                }
            }
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
