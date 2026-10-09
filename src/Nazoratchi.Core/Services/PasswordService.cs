using System.Security.Cryptography;
using System.Text;
using Nazoratchi.Core.Models;

namespace Nazoratchi.Core.Services;

/// <summary>
/// Handles industry-standard PBKDF2 password hashing, recovery key management, and constant-time verification.
/// Backwards-compatible with legacy SHA-256 hashes.
/// </summary>
public class PasswordService
{
    private readonly ConfigManager _configManager;
    private const int Pbkdf2Iterations = 100_000;
    private const int SaltByteSize = 16;
    private const int HashByteSize = 32;

    public PasswordService(ConfigManager configManager)
    {
        _configManager = configManager;
    }

    public byte[] GenerateSalt()
    {
        return RandomNumberGenerator.GetBytes(SaltByteSize);
    }

    /// <summary>
    /// Hashes a password using PBKDF2 (HMAC-SHA256, 100,000 iterations).
    /// Format: "pbkdf2:{iterations}:{saltBase64}:{hashBase64}"
    /// </summary>
    public string HashPassword(string password)
    {
        if (password == null) throw new ArgumentNullException(nameof(password));

        var salt = GenerateSalt();
        var hash = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(password),
            salt,
            Pbkdf2Iterations,
            HashAlgorithmName.SHA256,
            HashByteSize);

        return $"pbkdf2:{Pbkdf2Iterations}:{Convert.ToBase64String(salt)}:{Convert.ToBase64String(hash)}";
    }

    /// <summary>
    /// Verifies password against stored hash using constant-time comparison.
    /// Supports modern PBKDF2 and legacy SHA-256 hashes.
    /// </summary>
    public bool VerifyPassword(string password, string storedHash)
    {
        if (string.IsNullOrEmpty(password) || string.IsNullOrEmpty(storedHash))
            return false;

        try
        {
            // Modern PBKDF2 format: "pbkdf2:iterations:salt:hash"
            if (storedHash.StartsWith("pbkdf2:"))
            {
                var parts = storedHash.Split(':');
                if (parts.Length != 4) return false;

                if (!int.TryParse(parts[1], out int iterations)) return false;
                var salt = Convert.FromBase64String(parts[2]);
                var expectedHash = Convert.FromBase64String(parts[3]);

                var actualHash = Rfc2898DeriveBytes.Pbkdf2(
                    Encoding.UTF8.GetBytes(password),
                    salt,
                    iterations,
                    HashAlgorithmName.SHA256,
                    expectedHash.Length);

                return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
            }

            // Legacy SHA-256 fallback: "salt:hash"
            var legacyParts = storedHash.Split(':');
            if (legacyParts.Length == 2)
            {
                var salt = Convert.FromBase64String(legacyParts[0]);
                var expectedHashBytes = Convert.FromBase64String(legacyParts[1]);

                var actualHash = SHA256.HashData(Encoding.UTF8.GetBytes(password).Concat(salt).ToArray());
                return CryptographicOperations.FixedTimeEquals(actualHash, expectedHashBytes);
            }

            return false;
        }
        catch
        {
            return false;
        }
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

    /// <summary>
    /// Hashes the recovery key (normalized to uppercase).
    /// </summary>
    public string HashRecoveryKey(string recoveryKey)
    {
        var normalized = (recoveryKey ?? string.Empty).Trim().ToUpperInvariant();
        return HashPassword(normalized);
    }

    /// <summary>
    /// Verifies the recovery key in a case-insensitive manner.
    /// </summary>
    public bool VerifyRecoveryKey(string recoveryKey, string storedHash)
    {
        var normalized = (recoveryKey ?? string.Empty).Trim().ToUpperInvariant();
        return VerifyPassword(normalized, storedHash);
    }
}
