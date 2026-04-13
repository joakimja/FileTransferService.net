using System.Security.Cryptography;
using System.Text;

namespace FileTransferServiceApi.Services;

public interface IApiKeyProtectionService
{
    string Protect(string sharedKey, string clientIp);

    bool TryMatchEncrypted(string encryptedValue, string expectedSharedKey, IEnumerable<string> candidateClientIps);
}

public sealed class ApiKeyProtectionService : IApiKeyProtectionService
{
    public string Protect(string sharedKey, string clientIp)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sharedKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(clientIp);

        using var aes = Aes.Create();
        aes.Key = CreateKey(clientIp);
        aes.GenerateIV();

        using var encryptor = aes.CreateEncryptor();
        var plainBytes = Encoding.UTF8.GetBytes(sharedKey);
        var cipherBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);

        var result = new byte[aes.IV.Length + cipherBytes.Length];
        Buffer.BlockCopy(aes.IV, 0, result, 0, aes.IV.Length);
        Buffer.BlockCopy(cipherBytes, 0, result, aes.IV.Length, cipherBytes.Length);

        return Convert.ToBase64String(result);
    }

    public bool TryMatchEncrypted(string encryptedValue, string expectedSharedKey, IEnumerable<string> candidateClientIps)
    {
        if (string.IsNullOrWhiteSpace(encryptedValue))
        {
            return false;
        }

        foreach (var candidateClientIp in candidateClientIps.Where(static ip => !string.IsNullOrWhiteSpace(ip)))
        {
            if (TryUnprotect(encryptedValue, candidateClientIp, out var decryptedValue) &&
                string.Equals(decryptedValue, expectedSharedKey, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryUnprotect(string encryptedValue, string clientIp, out string? decryptedValue)
    {
        decryptedValue = null;

        try
        {
            var protectedBytes = Convert.FromBase64String(encryptedValue);

            using var aes = Aes.Create();
            aes.Key = CreateKey(clientIp);

            var ivLength = aes.BlockSize / 8;

            if (protectedBytes.Length <= ivLength)
            {
                return false;
            }

            var iv = new byte[ivLength];
            var cipherBytes = new byte[protectedBytes.Length - ivLength];

            Buffer.BlockCopy(protectedBytes, 0, iv, 0, ivLength);
            Buffer.BlockCopy(protectedBytes, ivLength, cipherBytes, 0, cipherBytes.Length);

            aes.IV = iv;

            using var decryptor = aes.CreateDecryptor();
            var plainBytes = decryptor.TransformFinalBlock(cipherBytes, 0, cipherBytes.Length);
            decryptedValue = Encoding.UTF8.GetString(plainBytes);
            return true;
        }
        catch (CryptographicException)
        {
            return false;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static byte[] CreateKey(string clientIp)
    {
        return SHA256.HashData(Encoding.UTF8.GetBytes(clientIp.Trim()));
    }
}
