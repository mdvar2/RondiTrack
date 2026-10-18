using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using RondiTrack.Auth;
using RondiTrack.Domain.Entities;
using RondiTrack.Infrastructure.Repositories;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RondiTrack.Infrastructure.Data;
using RondiTrack.Filters;
using RondiTrack.Handlers;
using FluentValidation;
using RondiTrack.Services;
using RondiTrack.Idempotency;
using RondiTrack.Domain.Repositories;
using Scalar.AspNetCore;
using Npgsql;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using RondiTrack.Observability;
using RondiTrack.Health;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using RondiTrack.Common;
using System.Text.Json.Serialization;
using RondiTrack.BackgroundJobs;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddFrontendCors(builder.Configuration);
builder.Services.AddApiRateLimiting(builder.Configuration);
builder.Services.AddSingleton<RondiTrackTelemetry>();

// Keep collection enabled without a dashboard; only exporting depends on an endpoint.
var telemetry = builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService("ronditrack-api"))
    .WithTracing(tracing => tracing
        .AddSource(RondiTrackTelemetry.SourceName)
        .AddAspNetCoreInstrumentation(options =>
            options.Filter = context => !context.Request.Path.StartsWithSegments("/health"))
        .AddNpgsql())
    .WithMetrics(metrics => metrics
        .AddMeter(RondiTrackTelemetry.MeterName)
        .AddAspNetCoreInstrumentation()
        .AddMeter("System.Runtime"))
    .WithLogging();

if (!string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]))
    telemetry.UseOtlpExporter();

var jwtOptions = JwtOptions.Load(builder.Configuration);
builder.Services.AddSingleton(jwtOptions);
builder.Services.AddSingleton<TokenService>();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = jwtOptions.ValidationParameters;
        options.IncludeErrorDetails = false;
        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = async context =>
            {
                var repository = context.HttpContext.RequestServices.GetRequiredService<IUserRepository>();
                if (!Guid.TryParse(context.Principal?.FindFirst("sub")?.Value, out var userId)
                    || await repository.GetByIdAsync(userId) is null)
                    context.Fail("The authenticated user no longer exists.");
            }
        };
    });

builder.Services.AddAuthorization(Policies.Configure);
builder.Services.AddScoped<IAuthorizationHandler, StokvelAccessHandler>();
builder.Services.AddScoped<IClaimsTransformation, PlatformRoleClaimsTransformation>();
builder.Services.AddScoped<StokvelAuthorization>();

builder.Services.AddOpenApi();

builder.Services.AddControllers(options =>
{
    options.Filters.Add<ValidationFilter>();
}).AddJsonOptions(options =>
{
    options.AllowInputFormatterExceptionMessages = false;
    options.JsonSerializerOptions.NumberHandling = JsonNumberHandling.Strict;
});
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.NumberHandling = JsonNumberHandling.Strict);

builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.SuppressModelStateInvalidFilter = true;
});

builder.Services.AddValidatorsFromAssemblyContaining<Program>();

builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails(options =>
{
    options.CustomizeProblemDetails = context =>
    {
        Correlation.Apply(context.HttpContext, context.ProblemDetails);
        // Retain one caller-facing ID rather than an additional span-specific traceId.
        context.ProblemDetails.Extensions.Remove("traceId");
        var status = context.ProblemDetails.Status ?? context.HttpContext.Response.StatusCode;
        context.ProblemDetails.Type = "about:blank";
        context.ProblemDetails.Instance ??= context.HttpContext.Request.Path;
        context.ProblemDetails.Title ??= Microsoft.AspNetCore.WebUtilities.ReasonPhrases.GetReasonPhrase(status);
        context.ProblemDetails.Detail ??= status switch
        {
            400 => "The request body is invalid.",
            401 => "Authentication is required.",
            403 => "You do not have permission for this operation.",
            404 => "The requested endpoint was not found.",
            405 => "The HTTP method is not allowed for this endpoint.",
            415 => "The request content type is not supported.",
            _ => "The request could not be completed."
        };
        if (status >= 500)
            context.ProblemDetails.Detail = "An unexpected error occurred.";
        var logger = context.HttpContext.RequestServices.GetRequiredService<ILoggerFactory>()
            .CreateLogger("RondiTrack.ProblemDetails");
        logger.Log(status >= 500 ? LogLevel.Error : LogLevel.Information,
            "Request failed with {StatusCode}. CorrelationId: {CorrelationId}",
            status, Correlation.GetId(context.HttpContext));
    };
});

var connectionString =
    builder.Configuration.GetConnectionString("RondiTrackDb")
    ?? throw new InvalidOperationException(
        "Connection string 'RondiTrackDb' was not found.");

// Keep parameter values and PostgreSQL row details out of all diagnostic sinks.
var diagnosticSafeConnectionString = new NpgsqlConnectionStringBuilder(connectionString)
{
    LogParameters = false,
    IncludeErrorDetail = false
}.ConnectionString;

builder.Services.AddDbContext<RondiTrackDbContext>(options =>
{
    options.UseNpgsql(
        diagnosticSafeConnectionString,
        npgsqlOptions =>
        {
            npgsqlOptions.MigrationsAssembly(typeof(RondiTrackDbContext).Assembly.GetName().Name);
            npgsqlOptions.EnableRetryOnFailure(
                maxRetryCount: 3,
                maxRetryDelay: TimeSpan.FromSeconds(2),
                errorCodesToAdd: null);
        });
});

builder.Services.AddHealthChecks()
    .AddDbContextCheck<RondiTrackDbContext>(HealthProbes.DatabaseCheckName, tags: ["ready"]);
builder.Services.Configure<HealthCheckServiceOptions>(options =>
{
    foreach (var registration in options.Registrations)
        if (registration.Name == HealthProbes.DatabaseCheckName)
            registration.Timeout = HealthProbes.ReadinessTimeout;
});

builder.Services.AddScoped<
    IUserRepository,
    UserRepository>();

builder.Services.AddScoped<
    IStokvelRepository,
    StokvelRepository>();

builder.Services.AddScoped<
    IContributionRepository,
    ContributionRepository>();

builder.Services.AddScoped<
    IContributionCycleRepository,
    EfContributionCycleRepository>();

builder.Services.AddScoped<
    IStokvelMemberRepository,
    EfStokvelMemberRepository>();

builder.Services.AddSingleton<
    IIdempotencyStore,
    IdempotencyStore>();

builder.Services.AddScoped<RondiTrack.Domain.Repositories.IRefreshTokenRepository, EfRefreshTokenRepository>();

builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();
builder.Services.AddScoped<AuthService>();

builder.Services.AddScoped<MembershipService>();
builder.Services.AddScoped<ContributionService>();
builder.Services.AddScoped<PayoutService>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<MemberHomeService>();
builder.Services.AddOptions<CycleCloseOptions>()
    .Bind(builder.Configuration.GetSection(CycleCloseOptions.SectionName))
    .Validate(
        options => options.IntervalSeconds > 0,
        "CycleClose:IntervalSeconds must be greater than zero.")
    .ValidateOnStart();

builder.Services.AddScoped<CycleCloseProcessor>();
builder.Services.AddHostedService<CycleCloseWorker>();

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages(async statusContext =>
{
    var http = statusContext.HttpContext;
    if (http.Response.StatusCode < 400) return;
    var problem = new ProblemDetails
    {
        Type = "about:blank", Status = http.Response.StatusCode,
        Title = Microsoft.AspNetCore.WebUtilities.ReasonPhrases.GetReasonPhrase(http.Response.StatusCode),
        Detail = http.Response.StatusCode switch
        {
            401 => "Authentication is required.",
            403 => "You do not have permission for this operation.",
            404 => "The requested endpoint was not found.",
            405 => "The HTTP method is not allowed for this endpoint.",
            415 => "The request content type is not supported.",
            >= 500 => "An unexpected error occurred.",
            _ => "The request could not be completed."
        },
        Instance = http.Request.Path
    };
    // Preserve the existing JSON response even when Accept excludes JSON.
    Correlation.Apply(http, problem);
    http.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("RondiTrack.ProblemDetails")
        .LogInformation("Request rejected with {StatusCode}. CorrelationId: {CorrelationId}",
            http.Response.StatusCode, Correlation.GetId(http));
    await http.Response.WriteAsJsonAsync(problem, options: null,
        contentType: "application/problem+json");
});

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous();
    app.MapScalarApiReference().AllowAnonymous();
}

app.UseHttpsRedirection();

app.UseRouting();
app.UseCors(CorsPolicies.Frontend);
app.UseAuthentication();
app.UseRateLimiter();
app.UseAuthorization();

app.MapControllers();
// Run these dependency probes before authentication, including when a token is supplied.
app.MapHealthChecks("/health/live", HealthProbes.Options(readiness: false)).AllowAnonymous().ShortCircuit();
app.MapHealthChecks("/health/ready", HealthProbes.Options(readiness: true)).AllowAnonymous().ShortCircuit();

app.Run();

public partial class Program { }
