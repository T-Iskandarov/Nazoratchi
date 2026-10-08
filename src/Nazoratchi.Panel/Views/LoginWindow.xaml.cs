using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Navigation;
using Nazoratchi.Core;
using Nazoratchi.Core.Services;

namespace Nazoratchi.Panel.Views;

public partial class LoginWindow : Window
{
    private readonly ConfigManager _configManager;
    private readonly PasswordService _passwordService;
    private bool _isFirstRun;

    public LoginWindow()
    {
        InitializeComponent();
        _configManager = new ConfigManager();
        _passwordService = new PasswordService(_configManager);
        _configManager.EnsureConfigDirectory();
        CheckFirstRun();
    }

    private void CheckFirstRun()
    {
        _isFirstRun = _passwordService.IsFirstRun();

        if (_isFirstRun)
        {
            InstructionText.Text = "Yangi parol o'rnating:";
            ConfirmPasswordBox.Visibility = Visibility.Visible;
            LoginButton.Content = "SAQLASH VA KIRISH";
            ResetButton.Visibility = Visibility.Collapsed;
        }
        else
        {
            InstructionText.Text = "Parolni kiriting:";
            ConfirmPasswordBox.Visibility = Visibility.Collapsed;
            LoginButton.Content = "KIRISH";
            ResetButton.Visibility = Visibility.Visible;
        }
    }

    private void PasswordBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            LoginButton_Click(sender, e);
        }
    }

    private void LoginButton_Click(object sender, RoutedEventArgs e)
    {
        ErrorMessage.Visibility = Visibility.Collapsed;

        if (_isFirstRun)
        {
            var password = PasswordBox.Password;
            var confirmPassword = ConfirmPasswordBox.Password;

            if (string.IsNullOrWhiteSpace(password))
            {
                ShowError("Parol bo'sh bo'lishi mumkin emas.");
                return;
            }

            if (password.Length < 4)
            {
                ShowError("Parol kamida 4 ta belgidan iborat bo'lishi kerak.");
                return;
            }

            if (password != confirmPassword)
            {
                ShowError("Parollar mos kelmadi.");
                return;
            }

            // Save password hash
            var hash = _passwordService.HashPassword(password);
            var config = _configManager.LoadConfig();
            config.PasswordHash = hash;
            _configManager.SaveConfig(config);

            // Generate recovery key
            var recoveryKey = _passwordService.GenerateRecoveryKey();
            config = _configManager.LoadConfig();
            config.RecoveryKeyHash = _passwordService.HashPassword(recoveryKey);
            _configManager.SaveConfig(config);

            // Save recovery key to a file for admin
            var recoveryFile = Path.Combine(Constants.ConfigDir, "TIKLASH_KALITI.txt");
            File.WriteAllText(recoveryFile, $"=== NAZORATCHI PAROL TIKLASH KALITI ===\r\n\r\n" +
                $"Bu kalit parolni unutganingizda kerak bo'ladi.\r\n" +
                $"Uni xavfsiz joyda saqlang va o'quvchilar ko'rmasin!\r\n\r\n" +
                $"KALIT: {recoveryKey}\r\n\r\n" +
                $"Yaratilgan sana: {DateTime.Now:yyyy-MM-dd HH:mm:ss}\r\n" +
                $"========================================\r\n");

            MessageBox.Show(
                $"Parol muvaffaqiyatli o'rnatildi!\n\n" +
                $"📋 Parol tiklash kaliti:\n{recoveryKey}\n\n" +
                $"⚠️ Bu kalitni xavfsiz joyda saqlang!\n" +
                $"Fayl sifatida saqlanadi:\n{recoveryFile}",
                "Muhim!", MessageBoxButton.OK, MessageBoxImage.Warning);

            OpenMainWindow();
        }
        else
        {
            var config = _configManager.LoadConfig();
            if (_passwordService.VerifyPassword(PasswordBox.Password, config.PasswordHash ?? ""))
            {
                OpenMainWindow();
            }
            else
            {
                ShowError("Noto'g'ri parol.");
            }
        }
    }

    private void ResetButton_Click(object sender, RoutedEventArgs e)
    {
        var resetWindow = new ResetPasswordWindow(_configManager, _passwordService);
        resetWindow.Owner = this;
        var result = resetWindow.ShowDialog();
        if (result == true)
        {
            MessageBox.Show("Parol muvaffaqiyatli tiklandi! Yangi parol bilan kiring.", "Ma'lumot", MessageBoxButton.OK, MessageBoxImage.Information);
            PasswordBox.Clear();
            PasswordBox.Focus();
        }
    }

    private void ShowError(string message)
    {
        ErrorMessage.Text = message;
        ErrorMessage.Visibility = Visibility.Visible;
    }

    private void OpenMainWindow()
    {
        var mainWindow = new MainWindow();
        mainWindow.Show();
        this.Close();
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
