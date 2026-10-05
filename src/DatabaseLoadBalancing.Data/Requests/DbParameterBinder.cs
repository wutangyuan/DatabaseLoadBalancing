using System.Data.Common;
using System.Text.Json;

namespace DatabaseLoadBalancing.Data.Requests;

public static class DbParameterBinder
{
    public static void Bind(DbCommand command, IReadOnlyDictionary<string, object?> parameters)
    {
        foreach (var pair in parameters)
        {
            Add(command, pair.Key, Normalize(pair.Value));
        }
    }

    public static void Add(DbCommand command, string key, object? value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = key.StartsWith('@') || key.StartsWith(':') || key.StartsWith('$') ? key : $"@{key}";
        parameter.Value = value ?? DBNull.Value;
        command.Parameters.Add(parameter);
    }

    private static object? Normalize(object? value)
    {
        if (value is not JsonElement element) return value;

        return element.ValueKind switch
        {
            JsonValueKind.Null or JsonValueKind.Undefined => null,
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Number when element.TryGetInt64(out var integer) => integer,
            JsonValueKind.Number when element.TryGetDecimal(out var decimalValue) => decimalValue,
            JsonValueKind.String => element.GetString(),
            _ => element.GetRawText()
        };
    }
}

