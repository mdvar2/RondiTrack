using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RondiTrack.Data;
using RondiTrack.Filters;
using RondiTrack.Handlers;
using FluentValidation;
using RondiTrack.Services;
using RondiTrack.Idempotency;
using RondiTrack.Repositories;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

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

builder.Services.AddScoped<MembershipService>();
builder.Services.AddScoped<ContributionService>();
builder.Services.AddScoped<PayoutService>();

var app = builder.Build();

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseHttpsRedirection();

app.MapControllers();

app.Run();

public partial class Program { }
