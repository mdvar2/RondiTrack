using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using RondiTrack.Observability;

namespace RondiTrack.Common;

public static class RateLimitPolicies
{
    public const string Login = "Login";
    public const string Contributions = "Contributions";
    public const string Payouts = "Payouts";

    public static IServiceCollection AddApiRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        var loginLimit = Positive(configuration, "Login:PermitLimit");
        var loginWindow = TimeSpan.FromSeconds(Positive(configuration, "Login:WindowSeconds"));
        var contribution = Bucket(configuration, Contributions);
        var payout = Bucket(configuration, Payouts);
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy(Login, context => RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = loginLimit, Window = loginWindow, QueueLimit = 0, AutoReplenishment = true
                }));
            options.AddPolicy(Contributions, context => UserBucket(context, contribution));
            options.AddPolicy(Payouts, context => UserBucket(context, payout));
            options.OnRejected = async (context, cancellationToken) =>
            {
                var http = context.HttpContext;
                var delay = context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter)
                    ? retryAfter : TimeSpan.FromSeconds(1);
                var seconds = Math.Max(1, (int)Math.Ceiling(delay.TotalSeconds));
                http.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                http.Response.Headers.RetryAfter = seconds.ToString(CultureInfo.InvariantCulture);
                var problem = new ProblemDetails
                {
                    Type = "about:blank", Title = "Too Many Requests", Status = 429,
                    Detail = $"Too many requests. Try again in {seconds} seconds.", Instance = http.Request.Path
                };
                Correlation.Apply(http, problem);
                await http.Response.WriteAsJsonAsync(problem, options: null,
                    contentType: "application/problem+json", cancellationToken: cancellationToken);
            };
        });
        return services;
    }

    private static RateLimitPartition<string> UserBucket(HttpContext context, TokenBucketRateLimiterOptions options)
    {
        // Anonymous callers must reach the existing 401 policy without consuming a user's quota.
        var subject = context.User.FindFirst("sub")?.Value;
        if (context.User.Identity?.IsAuthenticated != true || !Guid.TryParse(subject, out var userId))
            return RateLimitPartition.GetNoLimiter("anonymous");
        return RateLimitPartition.GetTokenBucketLimiter(userId.ToString("N"), _ => options);
    }

    private static TokenBucketRateLimiterOptions Bucket(IConfiguration configuration, string name)
    {
        var limit = Positive(configuration, $"{name}:TokenLimit");
        var replenishment = Positive(configuration, $"{name}:TokensPerPeriod");
        if (replenishment > limit) throw new InvalidOperationException("Rate-limit replenishment cannot exceed capacity.");
        return new TokenBucketRateLimiterOptions
        {
            TokenLimit = limit, TokensPerPeriod = replenishment,
            ReplenishmentPeriod = TimeSpan.FromSeconds(Positive(configuration, $"{name}:ReplenishmentSeconds")),
            QueueLimit = 0, AutoReplenishment = true
        };
    }

    private static int Positive(IConfiguration configuration, string key)
    {
        var value = configuration.GetValue<int>($"RateLimiting:{key}");
        return value > 0 ? value : throw new InvalidOperationException("Rate-limit settings must be positive integers.");
    }
}
