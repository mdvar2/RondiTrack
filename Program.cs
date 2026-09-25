using Microsoft.AspNetCore.Mvc;
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

builder.Services.AddSingleton<IUserRepository, UserRepository>();
builder.Services.AddSingleton<IStokvelRepository, StokvelRepository>();
builder.Services.AddSingleton<IContributionRepository, ContributionRepository>();

builder.Services.AddSingleton<
    IContributionCycleRepository,
    ContributionCycleRepository>();

builder.Services.AddSingleton<IIdempotencyStore, IdempotencyStore>();

builder.Services.AddScoped<MembershipService>();
builder.Services.AddScoped<ContributionService>();

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
