using System.Diagnostics;
using System.IO;
using System.ServiceProcess;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Navigation;
using Nazoratchi.Core;
using Nazoratchi.Core.Services;

namespace Nazoratchi.Panel.Views;

public partial class SettingsView : UserControl
{
    private readonly ConfigManager _configManager;
    private readonly PasswordService _passwordService;

    public SettingsView()
    {
        InitializeComponent();
        _configManager = new ConfigManager();
        _passwordService = new PasswordService(_configManager);
        LoadSettings();
        RefreshServiceStatus();
    }

    private void LoadSettings()
    {
        var config = _configManager.LoadConfig();
        DnsTextBox.Text = config.DnsUpstream;
    }

    public void RefreshServiceStatus()
    {
        try
        {
            using var sc = new ServiceController(Constants.ServiceName);
            var status = sc.Status;
            if (status == ServiceControllerStatus.Running)
            {
                ServiceStatusLabel.Text = "Ishlamoqda (Faol)";
                ServiceStatusLabel.Foreground = new SolidColorBrush(Color.FromRgb(46, 125, 50)); // Green
            }
            else if (status == ServiceControllerStatus.Stopped)
            {
                ServiceStatusLabel.Text = "To'xtatilgan";
                ServiceStatusLabel.Foreground = new SolidColorBrush(Color.FromRgb(198, 40, 40)); // Red
            }
            else
            {
                ServiceStatusLabel.Text = status.ToString();
                ServiceStatusLabel.Foreground = new SolidColorBrush(Color.FromRgb(245, 124, 0)); // Orange
            }
        }
        catch
        {
            ServiceStatusLabel.Text = "O'rnatilmagan (Boshlash tugmasini bosing)";
            ServiceStatusLabel.Foreground = new SolidColorBrush(Color.FromRgb(245, 124, 0)); // Orange
        }
    }

    private void ChangePasswordButton_Click(object sender, RoutedEventArgs e)
    {
        var config = _configManager.LoadConfig();

        // Verify old password
        if (!_passwordService.VerifyPassword(OldPasswordBox.Password, config.PasswordHash ?? ""))
        {
            MessageBox.Show("Joriy parol noto'g'ri.", "Xato", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        if (string.IsNullOrWhiteSpace(NewPasswordBox.Password) || NewPasswordBox.Password.Length < 4)
        {
            MessageBox.Show("Yangi parol kamida 4 ta belgidan iborat bo'lishi kerak.", "Xato", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        if (NewPasswordBox.Password != ConfirmPasswordBox.Password)
        {
            MessageBox.Show("Yangi parollar mos kelmadi.", "Xato", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        // Save new password
        config.PasswordHash = _passwordService.HashPassword(NewPasswordBox.Password);
        _configManager.SaveConfig(config);

        MessageBox.Show("Parol muvaffaqiyatli o'zgartirildi.", "Ma'lumot", MessageBoxButton.OK, MessageBoxImage.Information);
        OldPasswordBox.Clear();
        NewPasswordBox.Clear();
        ConfirmPasswordBox.Clear();
    }

    private void SaveDnsButton_Click(object sender, RoutedEventArgs e)
    {
        var dns = DnsTextBox.Text.Trim();
        if (string.IsNullOrEmpty(dns))
        {
            MessageBox.Show("DNS server manzilini kiriting.", "Xato", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        var config = _configManager.LoadConfig();
        config.DnsUpstream = dns;
        _configManager.SaveConfig(config);

        MessageBox.Show("DNS sozlamalari saqlandi.", "Ma'lumot", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void StartServiceButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            // If service is not registered, register it first
            if (!IsServiceInstalled())
            {
                var exePath = FindServiceExePath();
                if (!File.Exists(exePath))
                {
                    MessageBox.Show($"Xizmat fayli topilmadi:\n{exePath}", "Xato", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                RunElevated("sc.exe", $"create {Constants.ServiceName} binPath= \"\\\"{exePath}\\\"\" start= auto DisplayName= \"Nazoratchi Xavfsizlik Xizmati\"");
                Thread.Sleep(1500);
            }

            // Start the service
            RunElevated("net.exe", $"start {Constants.ServiceName}");
            Thread.Sleep(1000);

            RefreshServiceStatus();
            MessageBox.Show("Xizmat muvaffaqiyatli ishga tushirildi! Saytlar va dasturlar nazorati faollashdi.", "Muvaffaqiyatli", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Xizmatni ishga tushirishda xato: {ex.Message}", "Xato", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void StopServiceButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            RunElevated("net.exe", $"stop {Constants.ServiceName}");
            Thread.Sleep(1000);

            RefreshServiceStatus();
            MessageBox.Show("Xizmat to'xtatildi.", "Ma'lumot", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Xizmatni to'xtatishda xato: {ex.Message}", "Xato", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void RestartServiceButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            RunElevated("net.exe", $"stop {Constants.ServiceName}");
            Thread.Sleep(1500);
            RunElevated("net.exe", $"start {Constants.ServiceName}");
            Thread.Sleep(1000);

            RefreshServiceStatus();
            MessageBox.Show("Xizmat qayta ishga tushirildi.", "Ma'lumot", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Xizmatni qayta ishga tushirishda xato: {ex.Message}", "Xato", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private static bool IsServiceInstalled()
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

    private static string FindServiceExePath()
    {
        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        var direct = Path.Combine(baseDir, "Nazoratchi.Service.exe");
        if (File.Exists(direct)) return direct;

        var devPath = Path.GetFullPath(Path.Combine(baseDir, @"..\..\..\..\Nazoratchi.Service\bin\Debug\net8.0-windows\win-x64\Nazoratchi.Service.exe"));
        if (File.Exists(devPath)) return devPath;

        var standardPath = @"C:\Users\CUBO\Desktop\Loyihalar\Nazoratchi\src\Nazoratchi.Service\bin\Debug\net8.0-windows\win-x64\Nazoratchi.Service.exe";
        if (File.Exists(standardPath)) return standardPath;

        return direct;
    }

    private static void RunElevated(string fileName, string arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            UseShellExecute = true,
            Verb = "runas",
            WindowStyle = ProcessWindowStyle.Hidden
        };
        using var process = Process.Start(startInfo);
        process?.WaitForExit(10000);
    }

    private void CuboLogo_MouseDown(object sender, MouseButtonEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo("https://cubo.uz") { UseShellExecute = true });
        }
        catch { }
    }

    private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
        e.Handled = true;
    }
}
