using System.Diagnostics;

namespace RondiTrack.Observability;

public static class SecurityEvents
{
    // Called from request services while the HTTP activity (or its child) is current.
    public static string? CorrelationId => Activity.Current?.TraceId.ToString();
}
