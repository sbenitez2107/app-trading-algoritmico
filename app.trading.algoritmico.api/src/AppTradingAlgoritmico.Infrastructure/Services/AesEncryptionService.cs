using AppTradingAlgoritmico.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using System.Security.Cryptography;
using System.Text;

namespace AppTradingAlgoritmico.Infrastructure.Services;

/// <summary>
/// AES-256-CBC encryption using a key and IV derived from configuration.
/// </summary>
public sealed class AesEncryptionService : IEncryptionService
{
    private readonly byte[] _key;
    private readonly byte[] _iv;

    public AesEncryptionService(IConfiguration configuration)
    {
        var keyBase64 = configuration["Encryption:Key"]
            ?? throw new InvalidOperationException("Encryption:Key is not configured.");
        var ivBase64 = configuration["Encryption:IV"]
            ?? throw new InvalidOperationException("Encryption:IV is not configured.");

        _key = Convert.FromBase64String(keyBase64);
        _iv = Convert.FromBase64String(ivBase64);

        if (_key.Length != 32)
            throw new InvalidOperationException("Encryption:Key must be 256 bits (32 bytes) Base64-encoded.");
        if (_iv.Length != 16)
            throw new InvalidOperationException("Encryption:IV must be 128 bits (16 bytes) Base64-encoded.");
    }

    public string Encrypt(string plainText)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(plainText);

        using var aes = Aes.Create();
        aes.Key = _key;
        aes.IV = _iv;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;

        using var encryptor = aes.CreateEncryptor();
        var plainBytes = Encoding.UTF8.GetBytes(plainText);
        var cipherBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);
        return Convert.ToBase64String(cipherBytes);
    }

    public string Decrypt(string cipherText)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cipherText);

        using var aes = Aes.Create();
        aes.Key = _key;
        aes.IV = _iv;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;

        using var decryptor = aes.CreateDecryptor();
        var cipherBytes = Convert.FromBase64String(cipherText);
        var plainBytes = decryptor.TransformFinalBlock(cipherBytes, 0, cipherBytes.Length);
        return Encoding.UTF8.GetString(plainBytes);
    }
}
