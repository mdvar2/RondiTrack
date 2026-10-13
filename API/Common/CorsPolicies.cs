namespace RondiTrack.Common;

public static class CorsPolicies
{
    public const string Frontend = "Frontend";

    public static IServiceCollection AddFrontendCors(this IServiceCollection services, IConfiguration configuration)
    {
        var origins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
        if (origins.Any(origin => !Uri.TryCreate(origin, UriKind.Absolute, out var uri)
            || uri.Scheme is not ("http" or "https") || origin.Contains('*')
            || origin != uri.GetLeftPart(UriPartial.Authority)))
            throw new InvalidOperationException("CORS requires exact origins without paths or wildcards.");

        services.AddCors(options => options.AddPolicy(Frontend, policy => policy
            .WithOrigins(origins)
            .AllowCredentials()
            .WithMethods("GET", "POST", "PUT", "DELETE", "OPTIONS")
            .WithHeaders("Authorization", "Content-Type", "Idempotency-Key", "If-Match", "traceparent", "tracestate")
            .WithExposedHeaders("Retry-After", "ETag", "Location")
            .SetPreflightMaxAge(TimeSpan.FromMinutes(10))));
        return services;
    }
}
