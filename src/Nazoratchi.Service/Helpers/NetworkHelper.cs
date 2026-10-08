using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Win32;

namespace Nazoratchi.Service.Helpers;

/// <summary>
/// Utilities for managing network configuration and preventing DNS bypassing.
/// </summary>
public static class NetworkHelper
{
    public static string[] GetCurrentDnsServers()
    {
        var dnsServers = new List<string>();
        try
        {
            var networkInterfaces = NetworkInterface.GetAllNetworkInterfaces();
            foreach (var ni in networkInterfaces)
            {
                if (ni.OperationalStatus == OperationalStatus.Up && 
                    (ni.NetworkInterfaceType == NetworkInterfaceType.Ethernet || ni.NetworkInterfaceType == NetworkInterfaceType.Wireless80211))
                {
                    var ipProps = ni.GetIPProperties();
                    foreach (var dns in ipProps.DnsAddresses)
                    {
                        if (dns.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                        {
                            dnsServers.Add(dns.ToString());
                        }
                    }
                }
            }
        }
        catch
        {
            // Ignore
        }
        return dnsServers.Distinct().ToArray();
    }

    public static void SetSystemDns(string dnsIp)
    {
        try
        {
            var networkInterfaces = NetworkInterface.GetAllNetworkInterfaces();
            foreach (var ni in networkInterfaces)
            {
                if (ni.OperationalStatus == OperationalStatus.Up && 
                    (ni.NetworkInterfaceType == NetworkInterfaceType.Ethernet || ni.NetworkInterfaceType == NetworkInterfaceType.Wireless80211))
                {
                    // Set IPv4 DNS to local interceptor
                    RunCommand("netsh", $"interface ip set dns name=\"{ni.Name}\" static {dnsIp}");
                    // Set IPv6 DNS to loopback to avoid bypassing IPv4 filter
                    RunCommand("netsh", $"interface ipv6 set dnsservers name=\"{ni.Name}\" static ::1");
                }
            }

            // Disable DNS-over-HTTPS in Chrome and Edge so they respect system DNS
            DisableDnsOverHttps();

            // Clear Windows DNS cache
            RunCommand("ipconfig", "/flushdns");
        }
        catch
        {
            // Ignore
        }
    }

    public static void RestoreOriginalDns(string[] originalDns)
    {
        try
        {
            var networkInterfaces = NetworkInterface.GetAllNetworkInterfaces();
            foreach (var ni in networkInterfaces)
            {
                if (ni.OperationalStatus == OperationalStatus.Up && 
                    (ni.NetworkInterfaceType == NetworkInterfaceType.Ethernet || ni.NetworkInterfaceType == NetworkInterfaceType.Wireless80211))
                {
                    if (originalDns.Length > 0)
                    {
                        RunCommand("netsh", $"interface ip set dns name=\"{ni.Name}\" static {originalDns[0]}");
                        for (int i = 1; i < originalDns.Length; i++)
                        {
                            RunCommand("netsh", $"interface ip add dns name=\"{ni.Name}\" {originalDns[i]} index={i + 1}");
                        }
                    }
                    else
                    {
                        RunCommand("netsh", $"interface ip set dns name=\"{ni.Name}\" dhcp");
                    }

                    // Reset IPv6 DNS to DHCP
                    RunCommand("netsh", $"interface ipv6 set dnsservers name=\"{ni.Name}\" dhcp");
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
            process.WaitForExit(5000);
        }
        catch
        {
            // Ignore
        }
    }
}
