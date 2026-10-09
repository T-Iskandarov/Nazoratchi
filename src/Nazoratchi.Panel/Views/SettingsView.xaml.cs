using System;
using System.Diagnostics;
using System.IO;
using System.ServiceProcess;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Navigation;
using System.Windows.Threading;
using Nazoratchi.Core;
using Nazoratchi.Core.Services;

namespace Nazoratchi.Panel.Views;

public partial class SettingsView : UserControl
{
    private readonly ConfigManager _configManager;
    private readonly PasswordService _passwordService;

    private bool _isOldPasswordRevealed;
    private bool _isNewPasswordRevealed;
    private bool _isConfirmPasswordRevealed;

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
        DnsTextBox.Text = config.DnsUpstream ?? "1.1.1.1";
        DnsPlaceholder.Visibility = string.IsNullOrEmpty(DnsTextBox.Text) ? Visibility.Visible : Visibility.Collapsed;
    }

    public void RefreshServiceStatus()
    {
        try
        {
            using var sc = new ServiceController(Constants.ServiceName);
            var status = sc.Status;
            if (status == ServiceControllerStatus.Running)
            {
                ServiceStatusBadge.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F0FDF4"));
                ServiceStatusBadge.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#BBF7D0"));
                ServiceStatusDot.Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#16A34A"));
                ServiceStatusLabel.Text = "Ishlamoqda (Faol)";
                ServiceStatusLabel.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#16A34A"));
            }
            else if (status == ServiceControllerStatus.Stopped)
            {
                ServiceStatusBadge.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FEF2F2"));
                ServiceStatusBadge.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FECACA"));
                ServiceStatusDot.Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#DC2626"));
                ServiceStatusLabel.Text = "To'xtatilgan";
                ServiceStatusLabel.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#DC2626"));
            }
            else
            {
                ServiceStatusBadge.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFFBEB"));
                ServiceStatusBadge.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FDE68A"));
                ServiceStatusDot.Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#D97706"));
                ServiceStatusLabel.Text = status.ToString();
                ServiceStatusLabel.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#D97706"));
            }
        }
        catch
        {
            ServiceStatusBadge.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFFBEB"));
            ServiceStatusBadge.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FDE68A"));
            ServiceStatusDot.Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#D97706"));
            ServiceStatusLabel.Text = "O'rnatilmagan (Boshlash tugmasini bosing)";
            ServiceStatusLabel.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#D97706"));
        }
    }

    #region Password Eye Toggles
    private void OldPasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        var val = OldPasswordBox.Password;
        OldPasswordPlaceholder.Visibility = string.IsNullOrEmpty(val) ? Visibility.Visible : Visibility.Collapsed;
        if (!_isOldPasswordRevealed)
        {
            OldPasswordTextBox.Text = val;
        }
    }

    private void OldPasswordTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        var val = OldPasswordTextBox.Text;
        OldPasswordPlaceholder.Visibility = string.IsNullOrEmpty(val) ? Visibility.Visible : Visibility.Collapsed;
        if (_isOldPasswordRevealed)
        {
            OldPasswordBox.Password = val;
        }
    }

    private void ToggleOldPassword_Click(object sender, RoutedEventArgs e)
    {
        _isOldPasswordRevealed = !_isOldPasswordRevealed;
        if (_isOldPasswordRevealed)
        {
            OldPasswordTextBox.Text = OldPasswordBox.Password;
            OldPasswordBox.Visibility = Visibility.Collapsed;
            OldPasswordTextBox.Visibility = Visibility.Visible;
            OldPasswordEyeIcon.Kind = MaterialDesignThemes.Wpf.PackIconKind.EyeOffOutline;
            OldPasswordTextBox.Focus();
            OldPasswordTextBox.CaretIndex = OldPasswordTextBox.Text.Length;
        }
        else
        {
            OldPasswordBox.Password = OldPasswordTextBox.Text;
            OldPasswordTextBox.Visibility = Visibility.Collapsed;
            OldPasswordBox.Visibility = Visibility.Visible;
            OldPasswordEyeIcon.Kind = MaterialDesignThemes.Wpf.PackIconKind.EyeOutline;
            OldPasswordBox.Focus();
        }
    }

    private void NewPasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        var val = NewPasswordBox.Password;
        NewPasswordPlaceholder.Visibility = string.IsNullOrEmpty(val) ? Visibility.Visible : Visibility.Collapsed;
        if (!_isNewPasswordRevealed)
        {
            NewPasswordTextBox.Text = val;
        }
    }

    private void NewPasswordTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        var val = NewPasswordTextBox.Text;
        NewPasswordPlaceholder.Visibility = string.IsNullOrEmpty(val) ? Visibility.Visible : Visibility.Collapsed;
        if (_isNewPasswordRevealed)
        {
            NewPasswordBox.Password = val;
        }
    }

    private void ToggleNewPassword_Click(object sender, RoutedEventArgs e)
    {
        _isNewPasswordRevealed = !_isNewPasswordRevealed;
        if (_isNewPasswordRevealed)
        {
            NewPasswordTextBox.Text = NewPasswordBox.Password;
            NewPasswordBox.Visibility = Visibility.Collapsed;
            NewPasswordTextBox.Visibility = Visibility.Visible;
            NewPasswordEyeIcon.Kind = MaterialDesignThemes.Wpf.PackIconKind.EyeOffOutline;
            NewPasswordTextBox.Focus();
            NewPasswordTextBox.CaretIndex = NewPasswordTextBox.Text.Length;
        }
        else
        {
            NewPasswordBox.Password = NewPasswordTextBox.Text;
            NewPasswordTextBox.Visibility = Visibility.Collapsed;
            NewPasswordBox.Visibility = Visibility.Visible;
            NewPasswordEyeIcon.Kind = MaterialDesignThemes.Wpf.PackIconKind.EyeOutline;
            NewPasswordBox.Focus();
        }
    }

    private void ConfirmPasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        var val = ConfirmPasswordBox.Password;
        ConfirmPasswordPlaceholder.Visibility = string.IsNullOrEmpty(val) ? Visibility.Visible : Visibility.Collapsed;
        if (!_isConfirmPasswordRevealed)
        {
            ConfirmPasswordTextBox.Text = val;
        }
    }

    private void ConfirmPasswordTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        var val = ConfirmPasswordTextBox.Text;
        ConfirmPasswordPlaceholder.Visibility = string.IsNullOrEmpty(val) ? Visibility.Visible : Visibility.Collapsed;
        if (_isConfirmPasswordRevealed)
        {
            ConfirmPasswordBox.Password = val;
        }
    }

    private void ToggleConfirmPassword_Click(object sender, RoutedEventArgs e)
    {
        _isConfirmPasswordRevealed = !_isConfirmPasswordRevealed;
        if (_isConfirmPasswordRevealed)
        {
            ConfirmPasswordTextBox.Text = ConfirmPasswordBox.Password;
            ConfirmPasswordBox.Visibility = Visibility.Collapsed;
            ConfirmPasswordTextBox.Visibility = Visibility.Visible;
            ConfirmPasswordEyeIcon.Kind = MaterialDesignThemes.Wpf.PackIconKind.EyeOffOutline;
            ConfirmPasswordTextBox.Focus();
            ConfirmPasswordTextBox.CaretIndex = ConfirmPasswordTextBox.Text.Length;
        }
        else
        {
            ConfirmPasswordBox.Password = ConfirmPasswordTextBox.Text;
            ConfirmPasswordTextBox.Visibility = Visibility.Collapsed;
            ConfirmPasswordBox.Visibility = Visibility.Visible;
            ConfirmPasswordEyeIcon.Kind = MaterialDesignThemes.Wpf.PackIconKind.EyeOutline;
            ConfirmPasswordBox.Focus();
        }
    }
    #endregion

    private void DnsTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        DnsPlaceholder.Visibility = string.IsNullOrEmpty(DnsTextBox.Text) ? Visibility.Visible : Visibility.Collapsed;
    }

    private void ShowPasswordNotification(string message, bool isSuccess)
    {
        PasswordNotificationBadge.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(isSuccess ? "#ECFDF5" : "#FEF2F2"));
        PasswordNotificationBadge.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(isSuccess ? "#A7F3D0" : "#FECACA"));
        PasswordNotificationText.Text = message;
        PasswordNotificationText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(isSuccess ? "#065F46" : "#DC2626"));
        PasswordNotificationIcon.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(isSuccess ? "#059669" : "#DC2626"));
        PasswordNotificationIcon.Kind = isSuccess ? MaterialDesignThemes.Wpf.PackIconKind.CheckCircleOutline : MaterialDesignThemes.Wpf.PackIconKind.AlertCircleOutline;
        PasswordNotificationBadge.Visibility = Visibility.Visible;

        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3.5) };
        timer.Tick += (s, args) =>
        {
            PasswordNotificationBadge.Visibility = Visibility.Collapsed;
            timer.Stop();
        };
        timer.Start();
    }

    private void ShowDnsNotification(string message, bool isSuccess)
    {
        DnsNotificationBadge.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(isSuccess ? "#ECFDF5" : "#FEF2F2"));
        DnsNotificationBadge.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(isSuccess ? "#A7F3D0" : "#FECACA"));
        DnsNotificationText.Text = message;
        DnsNotificationText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(isSuccess ? "#065F46" : "#DC2626"));
        DnsNotificationIcon.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(isSuccess ? "#059669" : "#DC2626"));
        DnsNotificationIcon.Kind = isSuccess ? MaterialDesignThemes.Wpf.PackIconKind.CheckCircleOutline : MaterialDesignThemes.Wpf.PackIconKind.AlertCircleOutline;
        DnsNotificationBadge.Visibility = Visibility.Visible;

        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3.5) };
        timer.Tick += (s, args) =>
        {
            DnsNotificationBadge.Visibility = Visibility.Collapsed;
            timer.Stop();
        };
        timer.Start();
    }

    private void ChangePasswordButton_Click(object sender, RoutedEventArgs e)
    {
        var config = _configManager.LoadConfig();

        var oldPass = _isOldPasswordRevealed ? OldPasswordTextBox.Text : OldPasswordBox.Password;
        var newPass = _isNewPasswordRevealed ? NewPasswordTextBox.Text : NewPasswordBox.Password;
        var confirmPass = _isConfirmPasswordRevealed ? ConfirmPasswordTextBox.Text : ConfirmPasswordBox.Password;

        // Verify old password
        if (!_passwordService.VerifyPassword(oldPass, config.PasswordHash ?? ""))
        {
            ShowPasswordNotification("Joriy parol noto'g'ri", false);
            return;
        }

        if (string.IsNullOrWhiteSpace(newPass) || newPass.Length < 4)
        {
            ShowPasswordNotification("Yangi parol kamida 4 ta belgidan iborat bo'lishi kerak", false);
            return;
        }

        if (newPass != confirmPass)
        {
            ShowPasswordNotification("Yangi parollar bir-biriga mos kelmadi", false);
            return;
        }

        // Save new password
        config.PasswordHash = _passwordService.HashPassword(newPass);
        _configManager.SaveConfig(config);

        ShowPasswordNotification("Parol muvaffaqiyatli o'zgartirildi", true);
        OldPasswordBox.Clear();
        OldPasswordTextBox.Clear();
        NewPasswordBox.Clear();
        NewPasswordTextBox.Clear();
        ConfirmPasswordBox.Clear();
        ConfirmPasswordTextBox.Clear();
    }

    private void DnsPreset_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string ip)
        {
            DnsTextBox.Text = ip;
        }
    }

    private void SaveDnsButton_Click(object sender, RoutedEventArgs e)
    {
        var dns = DnsTextBox.Text.Trim();
        if (string.IsNullOrEmpty(dns))
        {
            ShowDnsNotification("DNS server manzilini kiriting", false);
            return;
        }

        if (!System.Net.IPAddress.TryParse(dns, out var ip) || 
            ip.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)
        {
            ShowDnsNotification("Yaroqsiz IPv4 manzil (Masalan: 8.8.8.8)", false);
            return;
        }

        var config = _configManager.LoadConfig();
        config.DnsUpstream = dns;
        _configManager.SaveConfig(config);

        ShowDnsNotification("DNS sozlamalari muvaffaqiyatli saqlandi", true);
    }

    private async void StartServiceButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            SetServiceBusy(true);

            if (!IsServiceInstalled())
            {
                var exePath = FindServiceExePath();
                if (!File.Exists(exePath))
                {
                    MessageBox.Show($"Xizmat fayli topilmadi:\n{exePath}", "Xato", MessageBoxButton.OK, MessageBoxImage.Error);
                    SetServiceBusy(false);
                    return;
                }

                await RunElevatedAsync("sc.exe", $"create {Constants.ServiceName} binPath= \"\\\"{exePath}\\\"\" start= auto DisplayName= \"Nazoratchi Xavfsizlik Xizmati\"");
                await Task.Delay(1500);
            }

            await RunElevatedAsync("net.exe", $"start {Constants.ServiceName}");
            await Task.Delay(1000);

            RefreshServiceStatus();
            MessageBox.Show("Xizmat muvaffaqiyatli ishga tushirildi! Saytlar va dasturlar nazorati faollashdi.", "Muvaffaqiyatli", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Xizmatni ishga tushirishda xato: {ex.Message}", "Xato", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            SetServiceBusy(false);
        }
    }

    private async void StopServiceButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            SetServiceBusy(true);
            await RunElevatedAsync("net.exe", $"stop {Constants.ServiceName}");
            await Task.Delay(1000);

            RefreshServiceStatus();
            MessageBox.Show("Xizmat to'xtatildi.", "Ma'lumot", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Xizmatni to'xtatishda xato: {ex.Message}", "Xato", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            SetServiceBusy(false);
        }
    }

    private async void RestartServiceButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            SetServiceBusy(true);
            await RunElevatedAsync("net.exe", $"stop {Constants.ServiceName}");
            await Task.Delay(1500);
            await RunElevatedAsync("net.exe", $"start {Constants.ServiceName}");
            await Task.Delay(1000);

            RefreshServiceStatus();
            MessageBox.Show("Xizmat qayta ishga tushirildi.", "Ma'lumot", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Xizmatni qayta ishga tushirishda xato: {ex.Message}", "Xato", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            SetServiceBusy(false);
        }
    }

    private void SetServiceBusy(bool isBusy)
    {
        StartServiceButton.IsEnabled = !isBusy;
        StopServiceButton.IsEnabled = !isBusy;
        RestartServiceButton.IsEnabled = !isBusy;
        ServiceOperationProgress.Visibility = isBusy ? Visibility.Visible : Visibility.Collapsed;
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

    private static async Task RunElevatedAsync(string fileName, string arguments)
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
                    WindowStyle = ProcessWindowStyle.Hidden
                };
                using var process = Process.Start(startInfo);
                process?.WaitForExit(10000);
            }
            catch { }
        });
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
        try
        {
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
            e.Handled = true;
        }
        catch { }
    }
}
