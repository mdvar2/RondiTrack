using RondiTrack.Services;
using RondiTrack.Idempotency;
using RondiTrack.Repositories;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddControllers();

builder.Services.AddSingleton<IUserRepository, UserRepository>();
builder.Services.AddSingleton<IStokvelRepository, StokvelRepository>();
builder.Services.AddSingleton<IContributionRepository, ContributionRepository>();

builder.Services.AddSingleton<IIdempotencyStore, IdempotencyStore>();

builder.Services.AddScoped<MembershipService>();
builder.Services.AddScoped<ContributionService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseHttpsRedirection();

app.MapControllers();

app.Run();
