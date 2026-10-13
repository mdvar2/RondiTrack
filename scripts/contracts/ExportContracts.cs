using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Hosting;
using RondiTrack.Tests.TestSupport;

namespace RondiTrack.Contracts;

public static class ExportContracts
{
    private sealed class ContractFactory : PostgresApiFactory
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.UseContentRoot(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../API")));
        }
    }

    public static async Task Main(string[] args)
    {
        if (args.Length != 1) throw new ArgumentException("Supply the output OpenAPI JSON path.");
        var factory = new ContractFactory();
        try
        {
            await factory.InitializeAsync();
            using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
                { BaseAddress = new Uri("https://localhost") });
            using var response = await client.GetAsync("/openapi/v1.json");
            response.EnsureSuccessStatusCode();
            var document = await response.Content.ReadAsStringAsync();
            using var parsed = JsonDocument.Parse(document);
            if (!parsed.RootElement.GetProperty("paths").TryGetProperty("/api/v1/me/home", out _))
                throw new InvalidOperationException("The actual home endpoint is missing from OpenAPI.");
            await File.WriteAllTextAsync(Path.GetFullPath(args[0]), document);
            Console.WriteLine("OPENAPI_EXPORTED: actual API document from PostgreSQL Testcontainers host.");
        }
        finally
        {
            await ((Xunit.IAsyncLifetime)factory).DisposeAsync();
        }
    }
}
