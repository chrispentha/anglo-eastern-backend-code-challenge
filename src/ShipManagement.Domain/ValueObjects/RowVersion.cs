using System.Buffers.Binary;
using System.Globalization;

namespace ShipManagement.Domain.ValueObjects;

/// <summary>
/// A SQL Server rowversion (8 bytes, increases on every change), exposed to clients as a 16-character hex ETag.
/// Used for optimistic concurrency (D-30).
/// </summary>
public readonly record struct RowVersion(ulong Value)
{
    public const int ByteLength = 8;

    public static RowVersion FromBytes(byte[] bytes)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        if (bytes.Length != ByteLength)
        {
            throw new ArgumentException($"A rowversion is exactly {ByteLength} bytes.", nameof(bytes));
        }

        return new RowVersion(BinaryPrimitives.ReadUInt64BigEndian(bytes));
    }

    /// <summary>Parses the 16 hexadecimal characters produced by <see cref="ToString"/>.</summary>
    public static bool TryParse(string? hex, out RowVersion version)
    {
        version = default;
        if (hex is null || hex.Length != ByteLength * 2
            || !ulong.TryParse(hex, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var value))
        {
            return false;
        }

        version = new RowVersion(value);
        return true;
    }

    public byte[] ToBytes()
    {
        var bytes = new byte[ByteLength];
        BinaryPrimitives.WriteUInt64BigEndian(bytes, Value);
        return bytes;
    }

    public override string ToString() => Value.ToString("X16", CultureInfo.InvariantCulture);
}
