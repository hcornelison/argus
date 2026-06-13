using System.Security.Cryptography;
using System.Text;

namespace Argus.Styx.Security;

public static class ApiKeyHasher
{
    /// <summary>Lowercase hex SHA-256 of the raw API key. Stored on Host.ApiKeyHash.</summary>
    public static string Hash(string rawKey)
        => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(rawKey)));
}
