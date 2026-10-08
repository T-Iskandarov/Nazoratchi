using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using MaterialDesignThemes.Wpf;
using Nazoratchi.Core.Services;

namespace Nazoratchi.Panel.Views;

public partial class ResetPasswordWindow : Window
{
    private readonly ConfigManager _configManager;
    private readonly PasswordService _passwordService;
    private bool _isNewPasswordVisible;
    private bool _isConfirmPasswordVisible;

    public ResetPasswordWindow(ConfigManager configManager, PasswordService passwordService)
    {
        InitializeComponent();
        _configManager = configManager;
        _passwordService = passwordService;
    }

    private void RecoveryKeyBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        RecoveryKeyPlaceholder.Visibility = string.IsNullOrEmpty(RecoveryKeyBox.Text) ? Visibility.Visible : Visibility.Collapsed;
    }

    private void NewPasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (!_isNewPasswordVisible)
        {
            NewPasswordPlaceholder.Visibility = string.IsNullOrEmpty(NewPasswordBox.Password) ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    private void NewPasswordTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_isNewPasswordVisible)
        {
            NewPasswordPlaceholder.Visibility = string.IsNullOrEmpty(NewPasswordTextBox.Text) ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    private void ConfirmNewPasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (!_isConfirmPasswordVisible)
        {
            ConfirmNewPasswordPlaceholder.Visibility = string.IsNullOrEmpty(ConfirmNewPasswordBox.Password) ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    private void ConfirmNewPasswordTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_isConfirmPasswordVisible)
        {
            ConfirmNewPasswordPlaceholder.Visibility = string.IsNullOrEmpty(ConfirmNewPasswordTextBox.Text) ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    private void ToggleNewPasswordVisibility_Click(object sender, RoutedEventArgs e)
    {
        _isNewPasswordVisible = !_isNewPasswordVisible;
        if (_isNewPasswordVisible)
        {
            NewPasswordTextBox.Text = NewPasswordBox.Password;
            NewPasswordBox.Visibility = Visibility.Collapsed;
            NewPasswordTextBox.Visibility = Visibility.Visible;
            NewPasswordEyeIcon.Kind = PackIconKind.EyeOutline;
            NewPasswordPlaceholder.Visibility = string.IsNullOrEmpty(NewPasswordTextBox.Text) ? Visibility.Visible : Visibility.Collapsed;
            NewPasswordTextBox.Focus();
        }
        else
        {
            NewPasswordBox.Password = NewPasswordTextBox.Text;
            NewPasswordTextBox.Visibility = Visibility.Collapsed;
            NewPasswordBox.Visibility = Visibility.Visible;
            NewPasswordEyeIcon.Kind = PackIconKind.EyeOffOutline;
            NewPasswordPlaceholder.Visibility = string.IsNullOrEmpty(NewPasswordBox.Password) ? Visibility.Visible : Visibility.Collapsed;
            NewPasswordBox.Focus();
        }
    }

    private void ToggleConfirmPasswordVisibility_Click(object sender, RoutedEventArgs e)
    {
        _isConfirmPasswordVisible = !_isConfirmPasswordVisible;
        if (_isConfirmPasswordVisible)
        {
            ConfirmNewPasswordTextBox.Text = ConfirmNewPasswordBox.Password;
            ConfirmNewPasswordBox.Visibility = Visibility.Collapsed;
            ConfirmNewPasswordTextBox.Visibility = Visibility.Visible;
            ConfirmPasswordEyeIcon.Kind = PackIconKind.EyeOutline;
            ConfirmNewPasswordPlaceholder.Visibility = string.IsNullOrEmpty(ConfirmNewPasswordTextBox.Text) ? Visibility.Visible : Visibility.Collapsed;
            ConfirmNewPasswordTextBox.Focus();
        }
        else
        {
            ConfirmNewPasswordBox.Password = ConfirmNewPasswordTextBox.Text;
            ConfirmNewPasswordTextBox.Visibility = Visibility.Collapsed;
            ConfirmNewPasswordBox.Visibility = Visibility.Visible;
            ConfirmPasswordEyeIcon.Kind = PackIconKind.EyeOffOutline;
            ConfirmNewPasswordPlaceholder.Visibility = string.IsNullOrEmpty(ConfirmNewPasswordBox.Password) ? Visibility.Visible : Visibility.Collapsed;
            ConfirmNewPasswordBox.Focus();
        }
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
        var newPassword = _isNewPasswordVisible ? NewPasswordTextBox.Text : NewPasswordBox.Password;
        var confirmPassword = _isConfirmPasswordVisible ? ConfirmNewPasswordTextBox.Text : ConfirmNewPasswordBox.Password;

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
