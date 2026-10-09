using System;
using System.Diagnostics;
using System.IO;
using System.ServiceProcess;
using System.Threading.Tasks;
using Nazoratchi.Core;
using Nazoratchi.Core.Services;

namespace Nazoratchi.Panel.Helpers;

/// <summary>
/// Centralized manager for interacting with the Nazoratchi Windows Service.
/// Supports start, stop, restart, status checks, and silent background restarts.
/// </summary>
public static class ServiceManager
{
    public static bool IsServiceInstalled()
    {
        try
        {
            using var sc = new ServiceController(Constants.ServiceName);
            _ = sc.Status;
            return true;
        }
        catch
        {
            return false;
        }
    }

    public static ServiceControllerStatus? GetServiceStatus()
    {
        try
        {
            using var sc = new ServiceController(Constants.ServiceName);
            return sc.Status;
        }
        catch
        {
            return null;
        }
    }

    public static async Task StartServiceAsync()
    {
        if (!IsServiceInstalled())
        {
            var exePath = FindServiceExePath();
            if (!File.Exists(exePath))
            {
                throw new FileNotFoundException($"Xizmat fayli topilmadi: {exePath}");
            }

            await RunElevatedAsync("sc.exe", $"create {Constants.ServiceName} binPath= \"\\\"{exePath}\\\"\" start= auto DisplayName= \"Nazoratchi Xavfsizlik Xizmati\"");
            await Task.Delay(1500);
        }

        await RunElevatedAsync("net.exe", $"start {Constants.ServiceName}");
        await Task.Delay(1000);
        FlushDnsSilently();
    }

    public static async Task StopServiceAsync()
    {
        await RunElevatedAsync("net.exe", $"stop {Constants.ServiceName}");
        await Task.Delay(1000);

        // Reset all active network adapters' DNS to DHCP (handles Russian / any language adapter names)
        await RunElevatedAsync("powershell.exe", "-NoProfile -Command \"Get-NetAdapter | Where-Object { $_.Status -eq 'Up' } | ForEach-Object { Set-DnsClientServerAddress -InterfaceAlias $_.Name -ResetServerAddresses }\"");

        // Clear browser policy from registry
        BrowserPolicyHelper.ClearUrlBlocklist();

        FlushDnsSilently();
    }

    public static async Task RestartServiceAsync()
    {
        await RunElevatedAsync("net.exe", $"stop {Constants.ServiceName}");
        await Task.Delay(1500);
        await RunElevatedAsync("net.exe", $"start {Constants.ServiceName}");
        await Task.Delay(1000);
        FlushDnsSilently();
    }

    /// <summary>
    /// Silently restarts the service in the background whenever configurations/lists are saved.
    /// Does not block UI or throw exceptions.
    /// </summary>
    public static async Task RestartServiceSilentlyAsync()
    {
        try
        {
            var status = GetServiceStatus();
            await Task.Run(() =>
            {
                try
                {
                    string args;
                    if (status == ServiceControllerStatus.Running)
                    {
                        // If running, stop and start (use '&' so net start runs even if net stop returns non-zero)
                        args = $"/c net stop {Constants.ServiceName} & net start {Constants.ServiceName} & ipconfig /flushdns";
                    }
                    else
                    {
                        // If stopped or any other state, start service directly
                        args = $"/c net start {Constants.ServiceName} & ipconfig /flushdns";
                    }

                    var startInfo = new ProcessStartInfo
                    {
                        FileName = "cmd.exe",
                        Arguments = args,
                        UseShellExecute = true,
                        Verb = "runas",
                        WindowStyle = ProcessWindowStyle.Hidden,
                        CreateNoWindow = true
                    };
                    using var proc = Process.Start(startInfo);
                    proc?.WaitForExit(12000);
                }
                catch
                {
                    // User might cancel UAC or already running
                }
            });

            FlushDnsSilently();
        }
        catch
        {
            // Never break UI flow on silent restart
        }
    }

    public static void FlushDnsSilently()
    {
        Task.Run(() =>
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "ipconfig.exe",
                    Arguments = "/flushdns",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                };
                using var proc = Process.Start(psi);
                proc?.WaitForExit(3000);
            }
            catch { }
        });
    }

    public static string FindServiceExePath()
    {
        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        var direct = Path.Combine(baseDir, "Nazoratchi.Service.exe");
        if (File.Exists(direct)) return direct;

        var candidates = new[]
        {
            Path.GetFullPath(Path.Combine(baseDir, @"..\..\..\..\Nazoratchi.Service\bin\Debug\net8.0-windows\win-x64\Nazoratchi.Service.exe")),
            Path.GetFullPath(Path.Combine(baseDir, @"..\..\..\..\Nazoratchi.Service\bin\Release\net8.0-windows\win-x64\Nazoratchi.Service.exe")),
            Path.GetFullPath(Path.Combine(baseDir, @"..\..\..\..\Nazoratchi.Service\bin\Debug\net8.0-windows\Nazoratchi.Service.exe")),
            Path.GetFullPath(Path.Combine(baseDir, @"..\..\..\..\Nazoratchi.Service\bin\Release\net8.0-windows\Nazoratchi.Service.exe"))
        };

        foreach (var candidate in candidates)
        {
            if (File.Exists(candidate)) return candidate;
        }

        try
        {
            using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\" + Constants.ServiceName);
            var imagePath = key?.GetValue("ImagePath")?.ToString();
            if (!string.IsNullOrEmpty(imagePath))
            {
                imagePath = imagePath.Trim('"');
                if (File.Exists(imagePath)) return imagePath;
            }
        }
        catch { }

        return direct;
    }

    public static async Task RunElevatedAsync(string fileName, string arguments)
    {
        await Task.Run(() =>
        {
            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = arguments,
                    UseShellExecute = true,
                    Verb = "runas",
                    WindowStyle = ProcessWindowStyle.Hidden,
                    CreateNoWindow = true
                };
                using var process = Process.Start(startInfo);
                process?.WaitForExit(10000);
            }
            catch { }
        });
    }
}
