using System.Data;
using Dapper;

namespace ShipManagement.Infrastructure.Persistence;

/// <summary>
/// Typed parameter helpers. Explicit types and sizes give SQL Server stable, reusable plans and make every
/// value a parameter, never part of the command text (SEC-06). Sizes match the procedure signatures.
/// </summary>
internal static class ParameterExtensions
{
    public static DynamicParameters AddInt(this DynamicParameters parameters, string name, int? value)
    {
        parameters.Add(name, value, DbType.Int32);
        return parameters;
    }

    /// <summary>VARCHAR parameter (codes, identifiers).</summary>
    public static DynamicParameters AddAnsi(this DynamicParameters parameters, string name, string? value, int size)
    {
        parameters.Add(name, value, DbType.AnsiString, size: size);
        return parameters;
    }

    /// <summary>NVARCHAR parameter (human text: names, search terms).</summary>
    public static DynamicParameters AddUnicode(this DynamicParameters parameters, string name, string? value, int size)
    {
        parameters.Add(name, value, DbType.String, size: size);
        return parameters;
    }

    public static DynamicParameters AddDate(this DynamicParameters parameters, string name, DateOnly value)
    {
        parameters.Add(name, value.ToDateTime(TimeOnly.MinValue), DbType.Date);
        return parameters;
    }

    public static DynamicParameters AddOutput(this DynamicParameters parameters, string name, DbType type, int? size = null)
    {
        parameters.Add(name, dbType: type, direction: ParameterDirection.Output, size: size);
        return parameters;
    }
}
