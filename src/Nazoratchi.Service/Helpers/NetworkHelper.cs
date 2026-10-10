using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Text.RegularExpressions;
using Microsoft.Win32;
using Nazoratchi.Core;

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
    /// Employs multiple layers of defense: Windows Firewall IPv6 block, RDNSS disabling, and ms_tcpip6 unbinding.
    /// </summary>
    public static void SetSystemDns(string dnsIp)
    {
        try
        {
            // Layer 1: Windows Firewall rule to block all outbound IPv6 DNS queries (port 53)
            AddFirewallDnsRules();

            // Layer 2: Tell Windows DNS Client to prefer IPv4 over IPv6
            try
            {
                Registry.SetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\Tcpip6\Parameters", "DisabledComponents", 32, RegistryValueKind.DWord);
            }
            catch { }

            // Layer 3: Enforce on all Ethernet and Wi-Fi interfaces
            var networkInterfaces = NetworkInterface.GetAllNetworkInterfaces();
            foreach (var ni in networkInterfaces)
            {
                if (ni.NetworkInterfaceType == NetworkInterfaceType.Ethernet ||
                    ni.NetworkInterfaceType == NetworkInterfaceType.Wireless80211)
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

                        // Stop router advertisement DNS (RFC 6106 / 8106) and router discovery
                        RunCommand("netsh", $"interface ipv6 set interface {ifIndex} rabaseddnsconfig=disabled");
                        RunCommand("netsh", $"interface ipv6 set interface {ifIndex} routerdiscovery=disabled");
                        RunCommand("netsh", $"interface ipv6 set dnsservers name={ifIndex} source=static address=none");

                        // Disable IPv6 binding on this adapter so zero IPv6 traffic/DNS can leak
                        RunCommand("powershell.exe", $"-NoProfile -Command \"Disable-NetAdapterBinding -InterfaceIndex {ifIndex} -ComponentId ms_tcpip6 -ErrorAction SilentlyContinue\"");
                    }
                    else
                    {
                        RunCommand("powershell.exe", $"-NoProfile -Command \"Set-DnsClientServerAddress -InterfaceAlias '{safeName}' -ServerAddresses ('{dnsIp}')\"");
                        RunCommand("netsh", $"interface ipv6 set dnsservers name=\"{safeName}\" source=static address=none");
                        RunCommand("powershell.exe", $"-NoProfile -Command \"Disable-NetAdapterBinding -Name '{safeName}' -ComponentId ms_tcpip6 -ErrorAction SilentlyContinue\"");
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
    /// Checks whether Nazoratchi DNS (127.0.0.1) is actively enforced on all active adapters.
    /// Returns false if Wi-Fi reconnected, DHCP overwrote DNS, or IPv6 DNS leaked.
    /// </summary>
    public static bool IsSystemDnsEnforced()
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
                    var ipProps = ni.GetIPProperties();
                    bool hasLocalIpv4 = false;

                    foreach (var dns in ipProps.DnsAddresses)
                    {
                        if (dns.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                        {
                            if (dns.ToString() == Constants.LocalDnsIp)
                            {
                                hasLocalIpv4 = true;
                            }
                        }
                        else if (dns.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6)
                        {
                            var v6 = dns.ToString();
                            // If an external/router IPv6 DNS (like fe80::1) is active, DNS is leaked!
                            if (!v6.Equals("::1") && !v6.StartsWith("fec0:", StringComparison.OrdinalIgnoreCase))
                            {
                                return false;
                            }
                        }
                    }

                    if (!hasLocalIpv4)
                    {
                        return false;
                    }
                }
            }
            return true;
        }
        catch
        {
            return true;
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
            // Remove Windows Firewall rules
            RemoveFirewallDnsRules();

            // Restore Windows IPv6 configuration
            try
            {
                Registry.SetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\Tcpip6\Parameters", "DisabledComponents", 0, RegistryValueKind.DWord);
            }
            catch { }

            lock (_dnsStateLock)
            {
                var networkInterfaces = NetworkInterface.GetAllNetworkInterfaces();
                foreach (var ni in networkInterfaces)
                {
                    if (ni.NetworkInterfaceType == NetworkInterfaceType.Ethernet ||
                        ni.NetworkInterfaceType == NetworkInterfaceType.Wireless80211)
                    {
                        var safeName = SanitizeAdapterName(ni.Name);
                        int ifIndex = -1;
                        try { ifIndex = ni.GetIPProperties().GetIPv4Properties().Index; } catch { }

                        // Re-enable IPv6 binding and router discovery
                        if (ifIndex > 0)
                        {
                            RunCommand("powershell.exe", $"-NoProfile -Command \"Enable-NetAdapterBinding -InterfaceIndex {ifIndex} -ComponentId ms_tcpip6 -ErrorAction SilentlyContinue\"");
                            RunCommand("netsh", $"interface ipv6 set interface {ifIndex} rabaseddnsconfig=enabled");
                            RunCommand("netsh", $"interface ipv6 set interface {ifIndex} routerdiscovery=enabled");
                            RunCommand("netsh", $"interface ipv6 set dnsservers name={ifIndex} source=dhcp");
                            RunCommand("powershell.exe", $"-NoProfile -Command \"Set-DnsClientServerAddress -InterfaceIndex {ifIndex} -ResetServerAddresses\"");
                        }
                        else
                        {
                            RunCommand("powershell.exe", $"-NoProfile -Command \"Enable-NetAdapterBinding -Name '{safeName}' -ComponentId ms_tcpip6 -ErrorAction SilentlyContinue\"");
                            RunCommand("netsh", $"interface ipv6 set dnsservers name=\"{safeName}\" source=dhcp");
                            RunCommand("powershell.exe", $"-NoProfile -Command \"Set-DnsClientServerAddress -InterfaceAlias '{safeName}' -ResetServerAddresses\"");
                        }

                        // Restore IPv4
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

    private static void AddFirewallDnsRules()
    {
        try
        {
            RunCommand("netsh", "advfirewall firewall add rule name=\"Nazoratchi_BlockIPv6Dns_UDP\" dir=out action=block protocol=UDP remoteport=53 remoteip=::/0");
            RunCommand("netsh", "advfirewall firewall add rule name=\"Nazoratchi_BlockIPv6Dns_TCP\" dir=out action=block protocol=TCP remoteport=53 remoteip=::/0");
        }
        catch { }
    }

    private static void RemoveFirewallDnsRules()
    {
        try
        {
            RunCommand("netsh", "advfirewall firewall delete rule name=\"Nazoratchi_BlockIPv6Dns_UDP\"");
            RunCommand("netsh", "advfirewall firewall delete rule name=\"Nazoratchi_BlockIPv6Dns_TCP\"");
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
