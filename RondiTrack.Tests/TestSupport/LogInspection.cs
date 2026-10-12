using System.Text.Json;
using System.Text.Json.Serialization;

namespace RondiTrack.Tests.TestSupport;

public static class LogInspection
{
    private static readonly JsonSerializerOptions Options = new() { ReferenceHandler = ReferenceHandler.IgnoreCycles };

    private static string Describe(object? value)
    {
        if (value is null) return string.Empty;
        if (value is string text) return text;
        return DescribeJson(JsonSerializer.SerializeToElement(value, Options));
    }

    private static string DescribeJson(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.String => value.GetString() ?? string.Empty,
        JsonValueKind.Object => string.Join("\n", value.EnumerateObject().Select(property => property.Name + "=" + DescribeJson(property.Value))),
        JsonValueKind.Array => string.Join("\n", value.EnumerateArray().Select(DescribeJson)),
        _ => value.GetRawText()
    };

    // Only for in-memory assertions. Do not print this output on test failure.
    public static string Render(CapturedLog entry) => string.Join("\n", new[]
    {
        entry.Message,
        Describe(entry.Properties),
        entry.Exception?.ToString() ?? string.Empty,
        entry.Exception is null ? string.Empty : Describe(
            entry.Exception.Data.Keys.Cast<object>().ToDictionary(key => key.ToString()!, key => entry.Exception.Data[key])),
        string.Join("\n", entry.Scopes.Select(scope => scope.Rendered + "\n" + Describe(scope.Properties)))
    });

    public static bool ContainsAny(CapturedLog entry, IEnumerable<string> values)
    {
        var rendered = Render(entry);
        return values.Any(value => rendered.Contains(value, StringComparison.OrdinalIgnoreCase));
    }
}
