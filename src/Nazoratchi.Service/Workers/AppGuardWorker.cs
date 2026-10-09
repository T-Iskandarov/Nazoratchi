using System.Diagnostics;
using System.Management;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using Nazoratchi.Core.Services;

namespace Nazoratchi.Service.Workers;

/// <summary>
/// Guard worker for preventing unauthorized application installation and uninstallation.
/// Combines process inspection, registry policy lockdown, and safe window inspection.
/// </summary>
public class AppGuardWorker : BackgroundService
{
    private readonly ILogger<AppGuardWorker> _logger;
    private readonly ConfigManager _configManager;
    private readonly LogService _logService;
    private ManagementEventWatcher? _watcher;
    private bool _policiesApplied = false;

    #region Win32 API
    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

    private const uint WM_CLOSE = 0x0010;
    #endregion

    /// <summary>
    /// Critical processes that must never be terminated under any circumstances.
    /// </summary>
    private static readonly HashSet<string> ProtectedProcesses = new(StringComparer.OrdinalIgnoreCase)
    {
        "explorer", "taskmgr", "system", "svchost", "csrss", "winlogon", "services",
        "lsass", "smss", "dwm", "devenv", "code", "rider", "chrome", "msedge", "firefox", "brave", "opera",
        "python", "pythonw", "node", "cmd", "powershell", "pwsh", "git", "dotnet", "bash", "wt"
    };

    /// <summary>
    /// Keywords in window titles that indicate an installation or uninstallation wizard.
    /// </summary>
    private static readonly string[] BlockedWindowTitles =
    {
        "install wizard", "setup wizard", "installation wizard",
        "uninstallation wizard", "uninstaller", "приложения и возможности",
        "apps & features", "программы и компоненты", "programs and features"
    };

    /// <summary>
    /// Process names that should be blocked immediately when app blocking is active.
    /// </summary>
    private static readonly string[] BlockedProcessNames =
    {
        "msiexec.exe"
    };

    /// <summary>
    /// Keywords in standalone installer executable names.
    /// </summary>
    private static readonly string[] BlockedProcessKeywords =
    {
        "setup.exe", "installer.exe", "uninst.exe", "uninstall.exe", "unins000.exe", "unins001.exe"
    };

    /// <summary>
    /// Command line arguments that indicate install/uninstall operations.
    /// </summary>
    private static readonly string[] BlockedCommandLinePatterns =
    {
        ".msi", "-install", "/install", "/uninstall", "-uninstall",
        "/x", "/remove", "-remove", "appwiz.cpl"
    };

    public AppGuardWorker(ILogger<AppGuardWorker> logger, ConfigManager configManager, LogService logService)
    {
        _logger = logger;
        _configManager = configManager;
        _logService = logService;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("AppGuardWorker active with multi-layered protection.");

        StartWmiWatcher();

        var windowGuardianTask = RunWindowGuardianAsync(stoppingToken);
        var policySyncTask = RunPolicySyncAsync(stoppingToken);

        await Task.WhenAll(windowGuardianTask, policySyncTask);
    }

    #region Layer 1: Window Guardian
    private async Task RunWindowGuardianAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var config = _configManager.LoadConfig();
                if (config.IsAppBlockingEnabled)
                {
                    ScanAndKillBlockedWindows();
                }
            }
            catch (Exception ex)
            {
                _logger.LogTrace(ex, "Window Guardian scan trace.");
            }

            await Task.Delay(500, stoppingToken);
        }
    }

    private void ScanAndKillBlockedWindows()
    {
        var currentPid = (uint)Process.GetCurrentProcess().Id;

        EnumWindows((hWnd, lParam) =>
        {
            if (!IsWindowVisible(hWnd))
                return true;

            var sb = new StringBuilder(256);
            if (GetWindowText(hWnd, sb, 256) <= 0)
                return true;

            var title = sb.ToString().ToLowerInvariant();

            // Do not block our own app windows
            if (title.Contains("nazoratchi"))
                return true;

            // Check if window title matches installation wizard patterns
            if (BlockedWindowTitles.Any(kw => title.Contains(kw)))
            {
                GetWindowThreadProcessId(hWnd, out uint processId);
                if (processId > 0 && processId != currentPid)
                {
                    try
                    {
                        var process = Process.GetProcessById((int)processId);
                        var procName = process.ProcessName.ToLowerInvariant();

                        if (ProtectedProcesses.Contains(procName))
                        {
                            // If it's a browser or shell window, close just the window instead of killing the process
                            SendMessage(hWnd, WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
                            _logger.LogInformation("Window Guardian closed window: {Title} on {Process}", title, procName);
                            _logService.LogAppBlocked($"Oyna yopildi: \"{title}\"");
                        }
                        else
                        {
                            process.Kill();
                            _logger.LogInformation("Window Guardian killed installer process: {Process} (PID: {Pid}) - Window: {Title}", procName, processId, title);
                            _logService.LogAppBlocked($"O'rnatuvchi oynasi: \"{title}\" ({procName})");
                        }
                    }
                    catch (ArgumentException)
                    {
                        // Already exited
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to close window for: {Title}", title);
                    }
                }
            }

            return true;
        }, IntPtr.Zero);
    }
    #endregion

    #region Layer 2: System Registry Policies
    private async Task RunPolicySyncAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var config = _configManager.LoadConfig();
                if (config.IsAppBlockingEnabled)
                {
                    if (!_policiesApplied)
                    {
                        ApplySystemPolicies();
                        _policiesApplied = true;
                    }
                }
                else
                {
                    if (_policiesApplied)
                    {
                        RemoveSystemPolicies();
                        _policiesApplied = false;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error syncing system policies.");
            }

            await Task.Delay(5000, stoppingToken);
        }
    }

    private void ApplySystemPolicies()
    {
        try
        {
            // 1. Disable Windows Installer (MSI) completely (2 = Always Disabled)
            Registry.SetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\Installer", "DisableMSI", 2, RegistryValueKind.DWord);

            // 2. Disable Add/Remove Programs in Control Panel & Settings
            Registry.SetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\Uninstall", "NoAddRemovePrograms", 1, RegistryValueKind.DWord);
            Registry.SetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\Uninstall", "NoRemovePage", 1, RegistryValueKind.DWord);

            _logger.LogInformation("System security policies applied (DisableMSI, NoAddRemovePrograms).");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to apply system security policies.");
        }
    }

    private void RemoveSystemPolicies()
    {
        try
        {
            using (var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Policies\Microsoft\Windows\Installer", true))
            {
                key?.DeleteValue("DisableMSI", false);
            }
            using (var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\Uninstall", true))
            {
                key?.DeleteValue("NoAddRemovePrograms", false);
                key?.DeleteValue("NoRemovePage", false);
            }

            _logger.LogInformation("System security policies removed.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to remove system security policies.");
        }
    }
    #endregion

    #region Layer 3: WMI Process Creation Interceptor
    private void StartWmiWatcher()
    {
        try
        {
            var query = new WqlEventQuery("SELECT * FROM __InstanceCreationEvent WITHIN 1 WHERE TargetInstance ISA 'Win32_Process'");
            _watcher = new ManagementEventWatcher(query);
            _watcher.EventArrived += OnProcessStarted;
            _watcher.Start();
            _logger.LogInformation("WMI process creation watcher active.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start WMI process watcher.");
        }
    }

    private void OnProcessStarted(object sender, EventArrivedEventArgs e)
    {
        try
        {
            var config = _configManager.LoadConfig();
            if (!config.IsAppBlockingEnabled)
                return;

            var targetInstance = (ManagementBaseObject)e.NewEvent["TargetInstance"];
            var processName = targetInstance["Name"]?.ToString()?.ToLowerInvariant();
            var commandLine = targetInstance["CommandLine"]?.ToString()?.ToLowerInvariant() ?? string.Empty;
            var processIdStr = targetInstance["ProcessId"]?.ToString();

            if (processName == null || string.IsNullOrEmpty(processIdStr) || !int.TryParse(processIdStr, out int processId))
                return;

            // Never block Nazoratchi or critical system processes
            if (processName.Contains("nazoratchi") || ProtectedProcesses.Contains(processName.Replace(".exe", "")))
                return;

            bool shouldBlock = false;
            string reason = "";

            if (BlockedProcessNames.Any(p => processName == p))
            {
                shouldBlock = true;
                reason = $"Taqiqlangan tizim jarayoni: {processName}";
            }
            else if (BlockedProcessKeywords.Any(kw => processName.EndsWith(kw, StringComparison.OrdinalIgnoreCase)))
            {
                shouldBlock = true;
                reason = $"O'rnatish dasturi: {processName}";
            }
            else if (BlockedCommandLinePatterns.Any(pattern => commandLine.Contains(pattern)))
            {
                shouldBlock = true;
                reason = $"Taqiqlangan o'rnatish parametri: {processName}";
            }
            else
            {
                try
                {
                    var proc = Process.GetProcessById(processId);
                    var desc = proc.MainModule?.FileVersionInfo?.FileDescription?.ToLowerInvariant() ?? "";
                    var orig = proc.MainModule?.FileVersionInfo?.OriginalFilename?.ToLowerInvariant() ?? "";
                    if ((desc.Contains("setup") || desc.Contains("installer") || desc.Contains("uninstall")) &&
                        !desc.Contains("driver") && !desc.Contains("microsoft"))
                    {
                        shouldBlock = true;
                        reason = $"Tavsifi bo'yicha o'rnatuvchi: {desc}";
                    }
                }
                catch
                {
                    // Ignore access check errors
                }
            }

            if (shouldBlock)
            {
                try
                {
                    var process = Process.GetProcessById(processId);
                    process.Kill();
                    _logger.LogInformation("Killed process: {Process} (PID: {Pid}) — {Reason}", processName, processId, reason);
                    _logService.LogAppBlocked($"{processName} — {reason}");
                }
                catch (ArgumentException)
                {
                    // Already exited
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to kill process {Process} (PID: {Pid})", processName, processId);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing process creation event.");
        }
    }
    #endregion

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("AppGuardWorker stopping...");
        try
        {
            _watcher?.Stop();
            _watcher?.Dispose();
            RemoveSystemPolicies();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping AppGuardWorker.");
        }

        return base.StopAsync(cancellationToken);
    }
}
