namespace RondiTrack.Common;

// Keeps the served OpenAPI document byte-for-byte the same on every build OS.
public static class OpenApiText
{
    public static string? Lf(string? text) =>
        text?.Replace("\r\n", "\n").Replace('\r', '\n');
}
