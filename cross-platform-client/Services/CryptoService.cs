using System.Security.Cryptography;
using System.Text;

namespace TempChat.Services;

/// <summary>
/// AES-256-GCM encryption with PBKDF2-HMAC-SHA256 key derivation.
///
/// Key  = PBKDF2(password, salt=roomCode, 200_000 iters, 256 bits)
/// Wire = Base64( IV[12] || ciphertext[n] || tag[16] )
///
/// The server stores and relays Base64 ciphertext — it never sees plaintext.
/// </summary>
public sealed class CryptoService
{
    private const int IvSize      = 12;
    private const int TagSize     = 16;
    private const int Pbkdf2Iters = 200_000;
    private const int KeySize     = 32; // 256 bits

    private readonly byte[] _key;

    public CryptoService(string password, string roomCode)
    {
        _key = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(password),
            Encoding.UTF8.GetBytes(roomCode),
            Pbkdf2Iters,
            HashAlgorithmName.SHA256,
            KeySize);
    }

    public string Encrypt(string plaintext)
    {
        byte[] iv             = RandomNumberGenerator.GetBytes(IvSize);
        byte[] plaintextBytes = Encoding.UTF8.GetBytes(plaintext);
        byte[] ciphertext     = new byte[plaintextBytes.Length];
        byte[] tag            = new byte[TagSize];

        using var aes = new AesGcm(_key, TagSize);
        aes.Encrypt(iv, plaintextBytes, ciphertext, tag);

        byte[] result = new byte[IvSize + ciphertext.Length + TagSize];
        iv.CopyTo(result, 0);
        ciphertext.CopyTo(result, IvSize);
        tag.CopyTo(result, IvSize + ciphertext.Length);

        return Convert.ToBase64String(result);
    }

    /// <summary>Returns decrypted plaintext, or null on failure (wrong key / corrupted data).</summary>
    public string? Decrypt(string encoded)
    {
        try
        {
            byte[] data = Convert.FromBase64String(encoded);
            if (data.Length < IvSize + TagSize + 1) return null;

            byte[] iv         = data[..IvSize];
            byte[] tag        = data[^TagSize..];
            byte[] ciphertext = data[IvSize..^TagSize];
            byte[] plaintext  = new byte[ciphertext.Length];

            using var aes = new AesGcm(_key, TagSize);
            aes.Decrypt(iv, ciphertext, tag, plaintext);

            return Encoding.UTF8.GetString(plaintext);
        }
        catch
        {
            return null;
        }
    }
}
