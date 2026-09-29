using System.Security.Cryptography;
using ShipManagement.Application.Security;

namespace ShipManagement.Infrastructure.Security;

/// <summary>Generates API keys from the operating system CSPRNG (<see cref="RandomNumberGenerator"/>).</summary>
public sealed class ApiKeyGenerator : IApiKeyGenerator
{
    private const string PrefixAlphabet = "abcdefghijklmnopqrstuvwxyz0123456789";
    private const int SecretBytes = 32;

    public GeneratedApiKey Generate()
    {
        var prefix = RandomNumberGenerator.GetString(PrefixAlphabet, ApiKeyFormat.PrefixLength);
        var secret = Convert.ToBase64String(RandomNumberGenerator.GetBytes(SecretBytes))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

        var rawKey = $"{ApiKeyFormat.Scheme}{prefix}_{secret}";
        return new GeneratedApiKey(rawKey, prefix, ApiKeyFormat.Hash(rawKey));
    }
}
