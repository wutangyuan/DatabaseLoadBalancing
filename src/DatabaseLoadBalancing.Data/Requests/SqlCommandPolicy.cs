using System.Text.RegularExpressions;
using System.Text.Json;

namespace DatabaseLoadBalancing.Data.Requests;

public static partial class SqlCommandPolicy
{
    private static readonly HashSet<string> QueryCommands = ["SELECT", "WITH", "EXPLAIN"];
    private static readonly HashSet<string> MutationCommands = ["INSERT", "UPDATE", "DELETE"];

    public static SqlValidationResult Validate(string? sql, bool mutation)
    {
        if (string.IsNullOrWhiteSpace(sql))
        {
            return SqlValidationResult.Invalid("sql.empty", "SQL 不能为空。");
        }

        var normalized = sql.Trim();
        var withoutTrailingSemicolon = normalized.TrimEnd(';').Trim();
        if (withoutTrailingSemicolon.Contains(';', StringComparison.Ordinal))
        {
            return SqlValidationResult.Invalid("sql.multiple", "一次请求只允许执行一条 SQL。");
        }

        var command = FirstKeywordRegex().Match(withoutTrailingSemicolon).Value.ToUpperInvariant();
        if (mutation && !MutationCommands.Contains(command))
        {
            return SqlValidationResult.Invalid("sql.policy", "写操作只允许 INSERT、UPDATE、DELETE。");
        }

        if (!mutation && !QueryCommands.Contains(command))
        {
            return SqlValidationResult.Invalid("sql.policy", "查询操作只允许 SELECT、WITH、EXPLAIN。");
        }

        return SqlValidationResult.Valid(withoutTrailingSemicolon);
    }

    public static bool IsValidIdentifier(string value) => IdentifierRegex().IsMatch(value);

    public static SqlValidationResult ValidateParameters(string sql, IReadOnlyDictionary<string, object?> parameters)
    {
        var placeholders = PlaceholderRegex().Matches(sql)
            .Select(match => match.Value.TrimStart('@', ':', '$'))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var supplied = parameters.Keys
            .Select(key => key.TrimStart('@', ':', '$'))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (!placeholders.SetEquals(supplied))
        {
            return SqlValidationResult.Invalid("sql.parameters", "SQL 参数必须与占位符一一对应。");
        }

        foreach (var value in parameters.Values)
        {
            if (value is JsonElement element && (element.ValueKind is JsonValueKind.Array or JsonValueKind.Object))
            {
                return SqlValidationResult.Invalid("sql.parameters", "只允许 SQLite 标量参数。");
            }

            if (value is not null and not string and not bool and not byte[] and
                not sbyte and not byte and not short and not ushort and not int and not uint and
                not long and not ulong and not float and not double and not decimal and not JsonElement)
            {
                return SqlValidationResult.Invalid("sql.parameters", "只允许 SQLite 标量参数。");
            }
        }

        return SqlValidationResult.Valid(sql);
    }

    [GeneratedRegex("^[A-Za-z_][A-Za-z0-9_]*$")]
    private static partial Regex IdentifierRegex();

    [GeneratedRegex("^[A-Za-z]+")]
    private static partial Regex FirstKeywordRegex();

    [GeneratedRegex("[@:$][A-Za-z_][A-Za-z0-9_]*")]
    private static partial Regex PlaceholderRegex();
}

public sealed record SqlValidationResult(bool IsValid, string Sql, string? ErrorCode, string? ErrorMessage)
{
    public static SqlValidationResult Valid(string sql) => new(true, sql, null, null);

    public static SqlValidationResult Invalid(string code, string message) => new(false, string.Empty, code, message);
}

