using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace RondiTrack.Health;

public static class HealthProbes
{
    public const string DatabaseCheckName = "postgres";
    public static readonly TimeSpan ReadinessTimeout = TimeSpan.FromSeconds(3);

    public static HealthCheckOptions Options(bool readiness) => new()
    {
        Predicate = registration => readiness && registration.Tags.Contains("ready"),
        ResultStatusCodes =
        {
            [HealthStatus.Healthy] = StatusCodes.Status200OK,
            [HealthStatus.Degraded] = StatusCodes.Status503ServiceUnavailable,
            [HealthStatus.Unhealthy] = StatusCodes.Status503ServiceUnavailable
        },
        ResponseWriter = (context, report) =>
        {
            context.Response.ContentType = "text/plain; charset=utf-8";
            return context.Response.WriteAsync(
                report.Status == HealthStatus.Healthy ? "Healthy" : "Unhealthy",
                context.RequestAborted);
        }
    };
}
