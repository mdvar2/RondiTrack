using System.Net.Http.Headers;
using System.Net;
using System.Net.Sockets;
using RondiTrack.Auth;
using RondiTrack.Domain.Entities;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;
using RondiTrack.Infrastructure.Data;
using Testcontainers.PostgreSql;

namespace RondiTrack.Tests.TestSupport;

public class PostgresApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public CorrelationLogProvider CorrelationLogs { get; } = new();
    public CapturedLogProvider CapturedLogs { get; } = new();

    // Outage tests must call these only on their own separately created factory.
    public Task StopDatabaseAsync() => _container.StopAsync();
    public Task StartDatabaseAsync() => _container.StartAsync();
    private readonly User _regressionUser = new("Regression fixture user", $"regression-{Guid.NewGuid()}@tests.example");

    private readonly string _testSigningKey = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));

    private readonly PostgreSqlContainer _container;

    public PostgresApiFactory() : this(null) { }

    private PostgresApiFactory(int? stablePort)
    {
        var container = new PostgreSqlBuilder("postgres:17")
            .WithDatabase("ronditrack_test")
            .WithUsername("postgres")
            // Disposable test-container credential, never a development secret.
            .WithPassword(Guid.NewGuid().ToString("N"));
        if (stablePort is not null)
            container = container.WithPortBinding(stablePort.Value, 5432);
        _container = container.Build();
    }

    public static PostgresApiFactory CreateForOutage()
    {
        // Explicit mapping keeps the API's configured target stable across Docker restarts.
        var reservation = new TcpListener(IPAddress.Loopback, 0);
        reservation.Start();
        var port = ((IPEndPoint)reservation.LocalEndpoint).Port;
        reservation.Stop();
        return new PostgresApiFactory(port);
    }

    public DbContextOptions<RondiTrackDbContext> CreateDbContextOptions() =>
        new DbContextOptionsBuilder<RondiTrackDbContext>()
            .UseNpgsql(_container.GetConnectionString())
            .Options;

    public HttpClient CreateAuthenticatedClient() => CreateClientFor(_regressionUser);

    public HttpClient CreateClientFor(User user)
    {
        var client = CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        var token = Services.GetRequiredService<TokenService>().CreateAccessToken(user);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);
        return client;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Program.cs reads this immediately. ConfigureAppConfiguration is too late.
        builder.UseSetting("ConnectionStrings:RondiTrackDb", _container.GetConnectionString());
        builder.UseSetting("Jwt:SigningKey", _testSigningKey);
        // Test logging should not require permission to write the Windows Event Log.
        builder.ConfigureLogging(logging => logging.ClearProviders().AddConsole()
            .AddProvider(CorrelationLogs).AddProvider(CapturedLogs));
    }

    public async Task InitializeAsync()
    {
        try
        {
            await _container.StartAsync();
            // Resolve the API's real context, proving its early-read setting was overridden.
            using var scope = Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<RondiTrackDbContext>();
            var expected = new NpgsqlConnectionStringBuilder(_container.GetConnectionString());
            var actual = new NpgsqlConnectionStringBuilder(context.Database.GetConnectionString());
            var configured = scope.ServiceProvider.GetRequiredService<IConfiguration>()
                .GetConnectionString("RondiTrackDb");
            if (configured != _container.GetConnectionString() ||
                actual.Host != expected.Host || actual.Port != expected.Port ||
                actual.Database != expected.Database || actual.Username != expected.Username ||
                actual.Password != expected.Password)
            {
                throw new InvalidOperationException("Tests must use the disposable PostgreSQL container.");
            }

            // Apply the preserved migration chain rather than bypassing it with EnsureCreated.
            await context.Database.MigrateAsync();
            _regressionUser.SetPlatformRole(Roles.PlatformAdmin);
            context.Users.Add(_regressionUser);
            await context.SaveChangesAsync();
            await context.Database.OpenConnectionAsync();
            using var command = context.Database.GetDbConnection().CreateCommand();
            command.CommandText = "SELECT current_database()";
            var database = (string?)await command.ExecuteScalarAsync();
            if (database != expected.Database)
                throw new InvalidOperationException("The API connected to an unexpected database.");
            Console.WriteLine($"TEST_DATABASE container={_container.Id} host={actual.Host} port={actual.Port} database={database} migrations={(await context.Database.GetAppliedMigrationsAsync()).Count()}");
        }
        catch
        {
            await DisposeAsync();
            await _container.DisposeAsync();
            throw;
        }
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        try
        {
            await DisposeAsync();
        }
        finally
        {
            await _container.DisposeAsync();
        }
    }
}
