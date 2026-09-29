using ShipManagement.Domain.Errors;
using ShipManagement.Domain.ValueObjects;

namespace ShipManagement.Api.Http;

/// <summary>
/// ETag / If-Match handling for optimistic concurrency (RFC 9110, D-30). The ETag is the resource's rowversion
/// as a strong entity tag, e.g. <c>"00000000000007D3"</c>.
/// </summary>
internal static class EntityTags
{
    public static string Format(string version) => $"\"{version}\"";

    /// <summary>
    /// Returns the version the client expects, or null when there is no precondition (header absent or <c>*</c>).
    /// A malformed header is a 400. A well-formed tag that cannot be one of ours (e.g. weak) can never match, so it is a 412.
    /// </summary>
    public static RowVersion? ParseIfMatch(string? ifMatch)
    {
        if (string.IsNullOrWhiteSpace(ifMatch) || ifMatch.Trim() == "*")
        {
            return null;
        }

        var value = ifMatch.Trim();
        var weak = value.StartsWith("W/", StringComparison.Ordinal);
        var tag = weak ? value[2..] : value;
        if (tag.Length < 2 || tag[0] != '"' || tag[^1] != '"' || tag.Contains(',', StringComparison.Ordinal))
        {
            throw RequestValidationException.ForField("If-Match", "If-Match must be a single quoted ETag, e.g. \"00000000000007D3\", or *.");
        }

        // If-Match uses strong comparison: a weak tag never matches (RFC 9110 §13.1.1).
        if (weak || !RowVersion.TryParse(tag[1..^1], out var version))
        {
            throw new PreconditionFailedException("The If-Match ETag does not match the current version of the resource.");
        }

        return version;
    }
}
