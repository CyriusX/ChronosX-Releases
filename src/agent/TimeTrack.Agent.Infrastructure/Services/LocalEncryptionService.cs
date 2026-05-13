using System.Runtime.Versioning;
using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using TimeTrack.Agent.Contracts.Services;

namespace TimeTrack.Agent.Infrastructure.Services;

/// <summary>
/// Criptografia local AES-256-GCM com chave derivada via DPAPI (Windows).
/// Protege screenshots em disco antes do upload.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class LocalEncryptionService : ILocalEncryptionService
{
    private readonly ILogger<LocalEncryptionService> _logger;
    private readonly byte[] _key;
    private readonly string _keyFilePath;

    public LocalEncryptionService(ILogger<LocalEncryptionService> logger)
    {
        _logger = logger;

        var appDataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "TimeTrack");
        Directory.CreateDirectory(appDataPath);
        _keyFilePath = Path.Combine(appDataPath, "evidence.key");

        _key = LoadOrGenerateKey();
    }

    public byte[] Encrypt(byte[] plaintext, out byte[] iv)
    {
        iv = RandomNumberGenerator.GetBytes(12); // 96-bit nonce for AES-GCM
        var tag = new byte[16];
        var ciphertext = new byte[plaintext.Length];

        using var aes = new AesGcm(_key, 16);
        aes.Encrypt(iv, plaintext, ciphertext, tag);

        // Prepend tag to ciphertext: [tag(16)][ciphertext]
        var result = new byte[tag.Length + ciphertext.Length];
        Buffer.BlockCopy(tag, 0, result, 0, tag.Length);
        Buffer.BlockCopy(ciphertext, 0, result, tag.Length, ciphertext.Length);

        return result;
    }

    public byte[] Decrypt(byte[] ciphertextWithTag, byte[] iv)
    {
        if (ciphertextWithTag.Length < 16)
            throw new ArgumentException("Ciphertext too short");

        var tag = new byte[16];
        var ciphertext = new byte[ciphertextWithTag.Length - 16];
        Buffer.BlockCopy(ciphertextWithTag, 0, tag, 0, 16);
        Buffer.BlockCopy(ciphertextWithTag, 16, ciphertext, 0, ciphertext.Length);

        var plaintext = new byte[ciphertext.Length];
        using var aes = new AesGcm(_key, 16);
        aes.Decrypt(iv, ciphertext, tag, plaintext);

        return plaintext;
    }

    private byte[] LoadOrGenerateKey()
    {
        if (File.Exists(_keyFilePath))
        {
            try
            {
                var encryptedKey = File.ReadAllBytes(_keyFilePath);
                return ProtectedData.Unprotect(encryptedKey, null, DataProtectionScope.CurrentUser);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load encryption key, generating new one");
            }
        }

        var key = RandomNumberGenerator.GetBytes(32); // AES-256
        var encrypted = ProtectedData.Protect(key, null, DataProtectionScope.CurrentUser);
        File.WriteAllBytes(_keyFilePath, encrypted);

        _logger.LogInformation("Generated new evidence encryption key protected via DPAPI");
        return key;
    }
}
