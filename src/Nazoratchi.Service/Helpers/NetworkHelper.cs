using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace Nazoratchi.Service.Helpers;

/// <summary>
/// Utilities for managing network configuration, DNS enforcement, and safe per-adapter state restoration.
/// </summary>
public static class NetworkHelper
{
    private static readonly Dictionary<string, List<string>> _adapterOriginalDns = new();
    private static readonly object _dnsStateLock = new();

    /// <summary>
    /// Backs up original DNS servers per adapter and returns the active primary DNS servers.
    /// Never backs up loopback/127.0.0.1 addresses.
    /// </summary>
    public static string[] BackupAndGetOriginalDns()
    {
        lock (_dnsStateLock)
        {
            _adapterOriginalDns.Clear();
            var primaryDnsList = new List<string>();

            try
            {
                var networkInterfaces = NetworkInterface.GetAllNetworkInterfaces();
                foreach (var ni in networkInterfaces)
                {
                    if (ni.OperationalStatus == OperationalStatus.Up &&
                        (ni.NetworkInterfaceType == NetworkInterfaceType.Ethernet ||
                         ni.NetworkInterfaceType == NetworkInterfaceType.Wireless80211))
                    {
                        var ipProps = ni.GetIPProperties();
                        var adapterDns = new List<string>();

                        foreach (var dns in ipProps.DnsAddresses)
                        {
                            if (dns.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                            {
                                var dnsStr = dns.ToString();
                                // NEVER save 127.0.0.1 or loopback as original DNS!
                                if (!IsLoopbackOrLocal(dnsStr))
                                {
                                    adapterDns.Add(dnsStr);
                                    if (!primaryDnsList.Contains(dnsStr))
                                    {
                                        primaryDnsList.Add(dnsStr);
                                    }
                                }
                            }
                        }

                        if (adapterDns.Count > 0)
                        {
                            _adapterOriginalDns[ni.Name] = adapterDns;
                        }
                    }
                }
            }
            catch
            {
                // Fallback to empty
            }

            if (primaryDnsList.Count == 0)
            {
                // Fallback upstream DNS if none was found or only 127.0.0.1 was present
                primaryDnsList.Add("8.8.8.8");
                primaryDnsList.Add("1.1.1.1");
            }

            return primaryDnsList.ToArray();
        }
    }

    private static bool IsLoopbackOrLocal(string? ip)
    {
        if (string.IsNullOrWhiteSpace(ip)) return true;
        return ip.StartsWith("127.", StringComparison.OrdinalIgnoreCase) ||
               ip.Equals("::1", StringComparison.OrdinalIgnoreCase) ||
               ip.Equals("0.0.0.0", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Backward-compatible alias for BackupAndGetOriginalDns.
    /// </summary>
    public static string[] GetCurrentDnsServers() => BackupAndGetOriginalDns();

    /// <summary>
    /// Sets local DNS interceptor on all active Ethernet and Wi-Fi adapters.
    /// </summary>
    public static void SetSystemDns(string dnsIp)
    {
        try
        {
            var networkInterfaces = NetworkInterface.GetAllNetworkInterfaces();
            foreach (var ni in networkInterfaces)
            {
                if (ni.OperationalStatus == OperationalStatus.Up &&
                    (ni.NetworkInterfaceType == NetworkInterfaceType.Ethernet ||
                     ni.NetworkInterfaceType == NetworkInterfaceType.Wireless80211))
                {
                    var safeName = SanitizeAdapterName(ni.Name);
                    int ifIndex = -1;
                    try { ifIndex = ni.GetIPProperties().GetIPv4Properties().Index; } catch { }

                    // Set IPv4 DNS to local interceptor (127.0.0.1) via netsh and powershell
                    RunCommand("netsh", $"interface ip set dns name=\"{safeName}\" static {dnsIp}");
                    if (ifIndex > 0)
                    {
                        RunCommand("netsh", $"interface ip set dns name={ifIndex} static {dnsIp}");
                        RunCommand("powershell.exe", $"-NoProfile -Command \"Set-DnsClientServerAddress -InterfaceIndex {ifIndex} -ServerAddresses ('{dnsIp}')\"");
                    }
                    else
                    {
                        RunCommand("powershell.exe", $"-NoProfile -Command \"Set-DnsClientServerAddress -InterfaceAlias '{safeName}' -ServerAddresses ('{dnsIp}')\"");
                    }

                    // CRITICAL: Block IPv6 DNS bypass! Clear/disable IPv6 DNS so Windows cannot query router IPv6 DNS (e.g. fe80::1)
                    RunCommand("netsh", $"interface ipv6 set dnsservers name=\"{safeName}\" source=static address=none");
                    if (ifIndex > 0)
                    {
                        RunCommand("netsh", $"interface ipv6 set dnsservers name={ifIndex} source=static address=none");
                        RunCommand("powershell.exe", $"-NoProfile -Command \"$adapter = Get-DnsClientServerAddress -InterfaceIndex {ifIndex} -AddressFamily IPv6 -ErrorAction SilentlyContinue; if ($adapter) {{ Set-DnsClientServerAddress -InputObject $adapter -ServerAddresses @() }}\"");
                    }
                }
            }

            // Disable DNS-over-HTTPS in Chrome and Edge so they respect system DNS
            DisableDnsOverHttps();

            // Clear Windows DNS cache
            FlushDns();
        }
        catch
        {
            // Ignore
        }
    }

    /// <summary>
    /// Flushes Windows DNS resolver cache.
    /// </summary>
    public static void FlushDns()
    {
        try
        {
            RunCommand("ipconfig", "/flushdns");
        }
        catch { }
    }

    /// <summary>
    /// Restores each adapter to its exact original DNS configuration (DHCP or static servers).
    /// </summary>
    public static void RestoreOriginalDns(string[]? fallbackDns = null)
    {
        try
        {
            lock (_dnsStateLock)
            {
                var networkInterfaces = NetworkInterface.GetAllNetworkInterfaces();
                foreach (var ni in networkInterfaces)
                {
                    if (ni.OperationalStatus == OperationalStatus.Up &&
                        (ni.NetworkInterfaceType == NetworkInterfaceType.Ethernet ||
                         ni.NetworkInterfaceType == NetworkInterfaceType.Wireless80211))
                    {
                        var safeName = SanitizeAdapterName(ni.Name);

                    int ifIndex = -1;
                    try { ifIndex = ni.GetIPProperties().GetIPv4Properties().Index; } catch { }

                    if (_adapterOriginalDns.TryGetValue(ni.Name, out var originalList) &&
                        originalList.Count > 0 &&
                        !IsLoopbackOrLocal(originalList[0]))
                    {
                        RunCommand("netsh", $"interface ip set dns name=\"{safeName}\" static {originalList[0]}");
                        if (ifIndex > 0)
                        {
                            RunCommand("netsh", $"interface ip set dns name={ifIndex} static {originalList[0]}");
                        }
                        for (int i = 1; i < originalList.Count; i++)
                        {
                            if (!IsLoopbackOrLocal(originalList[i]))
                            {
                                RunCommand("netsh", $"interface ip add dns name=\"{safeName}\" {originalList[i]} index={i + 1}");
                                if (ifIndex > 0)
                                {
                                    RunCommand("netsh", $"interface ip add dns name={ifIndex} {originalList[i]} index={i + 1}");
                                }
                            }
                        }
                    }
                    else if (fallbackDns != null && fallbackDns.Length > 0 && !IsLoopbackOrLocal(fallbackDns[0]))
                    {
                        RunCommand("netsh", $"interface ip set dns name=\"{safeName}\" static {fallbackDns[0]}");
                        if (ifIndex > 0)
                        {
                            RunCommand("netsh", $"interface ip set dns name={ifIndex} static {fallbackDns[0]}");
                        }
                        for (int i = 1; i < fallbackDns.Length; i++)
                        {
                            if (!IsLoopbackOrLocal(fallbackDns[i]))
                            {
                                RunCommand("netsh", $"interface ip add dns name=\"{safeName}\" {fallbackDns[i]} index={i + 1}");
                                if (ifIndex > 0)
                                {
                                    RunCommand("netsh", $"interface ip add dns name={ifIndex} {fallbackDns[i]} index={i + 1}");
                                }
                            }
                        }
                    }
                    else
                    {
                        // Reset to DHCP
                        ResetAdapterDnsToDhcp(safeName, ifIndex);
                    }

                    // Restore IPv6 DNS to DHCP
                    RunCommand("netsh", $"interface ipv6 set dnsservers name=\"{safeName}\" source=dhcp");
                    if (ifIndex > 0)
                    {
                        RunCommand("netsh", $"interface ipv6 set dnsservers name={ifIndex} source=dhcp");
                        RunCommand("powershell.exe", $"-NoProfile -Command \"Set-DnsClientServerAddress -InterfaceIndex {ifIndex} -ResetServerAddresses\"");
                    }
                }
            }
        }

        // Restore DNS-over-HTTPS settings
        RestoreDnsOverHttps();

        // Clear Windows DNS cache
        FlushDns();
    }
    catch
    {
        // Ignore
    }
}

public static void ResetAdapterDnsToDhcp(string adapterName, int ifIndex = -1)
{
    try
    {
        RunCommand("netsh", $"interface ip set dns name=\"{adapterName}\" dhcp");
        RunCommand("netsh", $"interface ipv6 set dnsservers name=\"{adapterName}\" source=dhcp");
        RunCommand("powershell.exe", $"-NoProfile -Command \"Set-DnsClientServerAddress -InterfaceAlias '{adapterName}' -ResetServerAddresses\"");
        if (ifIndex > 0)
        {
            RunCommand("netsh", $"interface ip set dns name={ifIndex} dhcp");
            RunCommand("netsh", $"interface ipv6 set dnsservers name={ifIndex} source=dhcp");
            RunCommand("powershell.exe", $"-NoProfile -Command \"Set-DnsClientServerAddress -InterfaceIndex {ifIndex} -ResetServerAddresses\"");
        }
    }
    catch { }
}

    private static string SanitizeAdapterName(string name)
    {
        // Remove quotes or unsafe characters to prevent command injection
        return (name ?? string.Empty).Replace("\"", "").Trim();
    }

    private static void DisableDnsOverHttps()
    {
        try
        {
            Registry.SetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Google\Chrome", "DnsOverHttpsMode", "off");
            Registry.SetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Edge", "DnsOverHttpsMode", "off");
        }
        catch
        {
            // Ignore
        }
    }

    private static void RestoreDnsOverHttps()
    {
        try
        {
            using (var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Policies\Google\Chrome", true))
            {
                key?.DeleteValue("DnsOverHttpsMode", false);
            }
            using (var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Policies\Microsoft\Edge", true))
            {
                key?.DeleteValue("DnsOverHttpsMode", false);
            }
        }
        catch
        {
            // Ignore
        }
    }

    private static void RunCommand(string fileName, string arguments)
    {
        try
        {
            using var process = new Process();
            process.StartInfo.FileName = fileName;
            process.StartInfo.Arguments = arguments;
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.CreateNoWindow = true;
            process.Start();
            process.WaitForExit(4000);
        }
        catch
        {
            // Ignore
        }
    }
}
