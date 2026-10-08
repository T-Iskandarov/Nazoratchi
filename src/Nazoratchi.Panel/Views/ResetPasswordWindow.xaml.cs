using System.Windows;
using System.Windows.Input;
using Nazoratchi.Core.Services;

namespace Nazoratchi.Panel.Views;

public partial class ResetPasswordWindow : Window
{
    private readonly ConfigManager _configManager;
    private readonly PasswordService _passwordService;

    public ResetPasswordWindow(ConfigManager configManager, PasswordService passwordService)
    {
        InitializeComponent();
        _configManager = configManager;
        _passwordService = passwordService;
    }

    private void Input_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            ResetButton_Click(sender, e);
        }
    }

    private void ResetButton_Click(object sender, RoutedEventArgs e)
    {
        ErrorMessage.Visibility = Visibility.Collapsed;

        var recoveryKey = RecoveryKeyBox.Text.Trim();
        var newPassword = NewPasswordBox.Password;
        var confirmPassword = ConfirmNewPasswordBox.Password;

        if (string.IsNullOrWhiteSpace(recoveryKey))
        {
            ShowError("Tiklash kalitini kiriting.");
            return;
        }

        // Verify recovery key
        var config = _configManager.LoadConfig();
        if (string.IsNullOrEmpty(config.RecoveryKeyHash) || 
            !_passwordService.VerifyPassword(recoveryKey, config.RecoveryKeyHash))
        {
            ShowError("Tiklash kaliti noto'g'ri.");
            return;
        }

        if (string.IsNullOrWhiteSpace(newPassword))
        {
            ShowError("Yangi parol bo'sh bo'lishi mumkin emas.");
            return;
        }

        if (newPassword.Length < 4)
        {
            ShowError("Parol kamida 4 ta belgidan iborat bo'lishi kerak.");
            return;
        }

        if (newPassword != confirmPassword)
        {
            ShowError("Parollar mos kelmadi.");
            return;
        }

        // Save new password
        config.PasswordHash = _passwordService.HashPassword(newPassword);
        _configManager.SaveConfig(config);

        DialogResult = true;
        Close();
    }

    private void ShowError(string message)
    {
        ErrorMessage.Text = message;
        ErrorMessage.Visibility = Visibility.Visible;
    }
}
