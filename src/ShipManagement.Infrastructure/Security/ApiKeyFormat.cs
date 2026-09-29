using System.Security.Cryptography;
using System.Text;

namespace ShipManagement.Infrastructure.Security;

/// <summary>
/// API key format: <c>sm_&lt;prefix&gt;_&lt;secret&gt;</c>, where prefix is 8 lower-case letters/digits (non-secret,
/// identifies the key in logs and listings) and secret is 32 CSPRNG bytes in Base64Url (43 characters).
/// Malformed input is rejected before any hashing or database work.
/// </summary>
public static class ApiKeyFormat
{
    public const string Scheme = "sm_";
    public const int PrefixLength = 8;
    public const int SecretLength = 43;
    public const int TotalLength = 3 + PrefixLength + 1 + SecretLength;

    public static bool IsWellFormed(string? key)
    {
        if (key is null || key.Length != TotalLength || !key.StartsWith(Scheme, StringComparison.Ordinal))
        {
            return false;
        }

        var prefix = key.AsSpan(Scheme.Length, PrefixLength);
        foreach (var c in prefix)
        {
            if (!char.IsAsciiLetterLower(c) && !char.IsAsciiDigit(c))
            {
                return false;
            }
        }

        if (key[Scheme.Length + PrefixLength] != '_')
        {
            return false;
        }

        foreach (var c in key.AsSpan(Scheme.Length + PrefixLength + 1))
        {
            if (!char.IsAsciiLetterOrDigit(c) && c is not '-' and not '_')
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>The non-secret prefix; call only on well-formed keys.</summary>
    public static string PrefixOf(string key) => key.Substring(Scheme.Length, PrefixLength);

    /// <summary>SHA-256 of the ASCII key: 256-bit random keys need no slow hash (SEC-03).</summary>
    public static byte[] Hash(string key) => SHA256.HashData(Encoding.ASCII.GetBytes(key));
}
