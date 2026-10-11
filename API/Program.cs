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

var builder = WebApplication.CreateBuilder(args);

var jwtOptions = JwtOptions.Load(builder.Configuration);
builder.Services.AddSingleton(jwtOptions);
builder.Services.AddSingleton<TokenService>();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = jwtOptions.ValidationParameters;
        options.IncludeErrorDetails = false;
    });

builder.Services.AddAuthorization(Policies.Configure);
builder.Services.AddScoped<IAuthorizationHandler, StokvelAccessHandler>();
builder.Services.AddScoped<IClaimsTransformation, PlatformRoleClaimsTransformation>();
builder.Services.AddScoped<StokvelAuthorization>();

builder.Services.AddOpenApi();

builder.Services.AddControllers(options =>
{
    options.Filters.Add<ValidationFilter>();
});

builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.SuppressModelStateInvalidFilter = true;
});

builder.Services.AddValidatorsFromAssemblyContaining<Program>();

builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

var connectionString =
    builder.Configuration.GetConnectionString("RondiTrackDb")
    ?? throw new InvalidOperationException(
        "Connection string 'RondiTrackDb' was not found.");

builder.Services.AddDbContext<RondiTrackDbContext>(options =>
{
    options.UseNpgsql(
        connectionString,
        npgsqlOptions =>
        {
            npgsqlOptions.MigrationsAssembly(typeof(RondiTrackDbContext).Assembly.GetName().Name);
            npgsqlOptions.EnableRetryOnFailure(
                maxRetryCount: 3,
                maxRetryDelay: TimeSpan.FromSeconds(2),
                errorCodesToAdd: null);
        });
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

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages(async statusContext =>
{
    var http = statusContext.HttpContext;
    if (http.Response.StatusCode is not (401 or 403)) return;
    var problem = new ProblemDetails
    {
        Type = "about:blank", Status = http.Response.StatusCode,
        Title = http.Response.StatusCode == 401 ? "Unauthorized" : "Forbidden",
        Detail = http.Response.StatusCode == 401 ? "Authentication is required." : "You do not have permission for this operation.",
        Instance = http.Request.Path
    };
    problem.Extensions["correlationId"] = http.TraceIdentifier;
    await http.Response.WriteAsJsonAsync(problem, options: null,
        contentType: "application/problem+json");
});

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous();
    app.MapScalarApiReference().AllowAnonymous();
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

public partial class Program { }
