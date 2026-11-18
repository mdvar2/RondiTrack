using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using RondiTrack.DevSeed;
using RondiTrack.DTOs.Auth;
using RondiTrack.DTOs.Home;
using RondiTrack.DTOs.Payouts;
using RondiTrack.Infrastructure.Data;
using RondiTrack.Tests.TestSupport;

namespace RondiTrack.Tests;

// The mobile app's dev flavour signs in as these personas, and its fakes
// hold the same world. These tests pin what the app's README promises.
[Collection(PostgresCollection.Name)]
public class DevSeedTests(PostgresApiFactory factory)
{
    // Disposable test value, never the development seed password.
    private const string Password = "test-seed-password-0001";

    private async Task SeedAsync()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<DevSeeder>().SeedAsync(Password);
    }

    private async Task<HttpClient> SignInAsync(string email)
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });
        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password = Password });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var token = (await login.Content.ReadFromJsonAsync<AccessTokenResponse>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);
        return client;
    }

    [Fact]
    public async Task Lerato_IsTreasurerOfOneStokvelAndMemberOfAnother()
    {
        await SeedAsync();
        using var client = await SignInAsync(DevSeeder.LeratoEmail);

        var home = (await client.GetFromJsonAsync<HomeResponse>("/api/v1/me/home"))!;

        Assert.Equal(
            [("Mabena Family", "Treasurer", 50000L, 0L), ("Soweto Teachers", "Member", 75000L, 75000L)],
            home.Stokvels.Select(s => (s.StokvelName, s.Role, s.AmountPaid, s.AmountOwed)));
        Assert.All(home.Stokvels, s => Assert.Equal("Open", s.CurrentCycle!.Status));
    }

    [Fact]
    public async Task Sipho_IsAMemberOnly_WithArrearsInMabenaFamily()
    {
        await SeedAsync();
        using var client = await SignInAsync(DevSeeder.SiphoEmail);

        var home = (await client.GetFromJsonAsync<HomeResponse>("/api/v1/me/home"))!;

        Assert.Equal(
            [("Mabena Family", "Member", 25000L), ("Soweto Teachers", "Member", 0L)],
            home.Stokvels.Select(s => (s.StokvelName, s.Role, s.ArrearsCents)));
    }

    [Fact]
    public async Task Thabo_IsInNoStokvel_AndGets404ForOne()
    {
        await SeedAsync();
        using var client = await SignInAsync(DevSeeder.ThaboEmail);

        var home = (await client.GetFromJsonAsync<HomeResponse>("/api/v1/me/home"))!;
        var stokvel = await client.GetAsync($"/api/v1/stokvels/{DevSeeder.MabenaFamilyId}");

        Assert.Empty(home.Stokvels);
        Assert.Equal(HttpStatusCode.NotFound, stokvel.StatusCode);
    }

    [Fact]
    public async Task Lerato_Gets404ForAStokvelSheIsNotIn_LikeOneThatDoesNotExist()
    {
        await SeedAsync();
        using var client = await SignInAsync(DevSeeder.LeratoEmail);

        var notHers = await client.GetAsync($"/api/v1/stokvels/{DevSeeder.MolefeBurialId}");
        var none = await client.GetAsync($"/api/v1/stokvels/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, notHers.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, none.StatusCode);
    }

    [Fact]
    public async Task MabenaFamily_PaidNomsaLastMonth_AndLeratoIsNext()
    {
        await SeedAsync();
        using var client = await SignInAsync(DevSeeder.SiphoEmail);

        var order = (await client.GetFromJsonAsync<PayoutOrderResponse>(
            $"/api/v1/stokvels/{DevSeeder.MabenaFamilyId}/cycles/{DevSeeder.MabenaCycleId}/payouts"))!;

        Assert.Equal(
            [("Nomsa Mabena", "Paid"), ("Lerato Mokoena", "Next"), ("Sipho Dlamini", "Waiting")],
            order.Items.Select(item => (item.MemberName, item.Status)));
    }

    [Fact]
    public async Task SeedingTwice_ChangesNothing()
    {
        await SeedAsync();
        await using var scope = factory.Services.CreateAsyncScope();

        var seededAgain = await scope.ServiceProvider.GetRequiredService<DevSeeder>().SeedAsync(Password);

        await using var context = new RondiTrackDbContext(factory.CreateDbContextOptions());
        Assert.False(seededAgain);
        Assert.Equal(1, await context.Users.CountAsync(user => user.Email == DevSeeder.LeratoEmail));
    }

    [Theory]
    [InlineData("Development", "a-password", true)]
    [InlineData("Development", "", false)]
    [InlineData("Development", null, false)]
    [InlineData("Production", "a-password", false)]
    [InlineData("Staging", "a-password", false)]
    public void RunsOnlyInDevelopment_WithAPassword(string environment, string? password, bool expected)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { [DevSeeder.PasswordKey] = password })
            .Build();

        Assert.Equal(expected, DevSeeder.ShouldRun(new Environment(environment), configuration));
    }

    private sealed class Environment(string name) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;
        public string ApplicationName { get; set; } = "RondiTrack";
        public string ContentRootPath { get; set; } = "";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
