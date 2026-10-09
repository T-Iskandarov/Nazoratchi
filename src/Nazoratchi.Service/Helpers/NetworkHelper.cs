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
                                adapterDns.Add(dnsStr);
                                if (!primaryDnsList.Contains(dnsStr))
                                {
                                    primaryDnsList.Add(dnsStr);
                                }
                            }
                        }

                        _adapterOriginalDns[ni.Name] = adapterDns;
                    }
                }
            }
            catch
            {
                // Fallback to empty
            }

            return primaryDnsList.ToArray();
        }
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

                    // Set IPv4 DNS to local interceptor
                    RunCommand("netsh", $"interface ip set dns name=\"{safeName}\" static {dnsIp}");
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

                        if (_adapterOriginalDns.TryGetValue(ni.Name, out var originalList) && originalList.Count > 0)
                        {
                            RunCommand("netsh", $"interface ip set dns name=\"{safeName}\" static {originalList[0]}");
                            for (int i = 1; i < originalList.Count; i++)
                            {
                                RunCommand("netsh", $"interface ip add dns name=\"{safeName}\" {originalList[i]} index={i + 1}");
                            }
                        }
                        else if (fallbackDns != null && fallbackDns.Length > 0)
                        {
                            RunCommand("netsh", $"interface ip set dns name=\"{safeName}\" static {fallbackDns[0]}");
                            for (int i = 1; i < fallbackDns.Length; i++)
                            {
                                RunCommand("netsh", $"interface ip add dns name=\"{safeName}\" {fallbackDns[i]} index={i + 1}");
                            }
                        }
                        else
                        {
                            // Reset to DHCP
                            RunCommand("netsh", $"interface ip set dns name=\"{safeName}\" dhcp");
                        }
                    }
                }
            }

            // Restore DNS-over-HTTPS settings
            RestoreDnsOverHttps();

            // Clear Windows DNS cache
            RunCommand("ipconfig", "/flushdns");
        }
        catch
        {
            // Ignore
        }
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
