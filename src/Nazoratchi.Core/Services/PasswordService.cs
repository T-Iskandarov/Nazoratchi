using System.Security.Cryptography;
using System.Text;
using Nazoratchi.Core.Models;

namespace Nazoratchi.Core.Services;

/// <summary>
/// Handles password hashing and verification.
/// </summary>
public class PasswordService
{
    private readonly ConfigManager _configManager;

    public PasswordService(ConfigManager configManager)
    {
        _configManager = configManager;
    }

    public byte[] GenerateSalt()
    {
        return RandomNumberGenerator.GetBytes(16);
    }

    public string HashPassword(string password)
    {
        var salt = GenerateSalt();
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(password).Concat(salt).ToArray());
        return $"{Convert.ToBase64String(salt)}:{Convert.ToBase64String(hash)}";
    }

    public bool VerifyPassword(string password, string storedHash)
    {
        if (string.IsNullOrEmpty(storedHash)) return false;

        var parts = storedHash.Split(':');
        if (parts.Length != 2) return false;

        var salt = Convert.FromBase64String(parts[0]);
        var expectedHash = parts[1];

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(password).Concat(salt).ToArray());
        return Convert.ToBase64String(hash) == expectedHash;
    }

    public bool IsFirstRun()
    {
        var config = _configManager.LoadConfig();
        return string.IsNullOrEmpty(config.PasswordHash);
    }

    /// <summary>
    /// Generates a random recovery key (8 uppercase alphanumeric characters).
    /// </summary>
    public string GenerateRecoveryKey()
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        var key = new char[8];
        var rng = RandomNumberGenerator.GetBytes(8);
        for (int i = 0; i < 8; i++)
        {
            key[i] = chars[rng[i] % chars.Length];
        }
        return new string(key);
    }
}
