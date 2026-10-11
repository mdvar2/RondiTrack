using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using RondiTrack.Controllers;
using RondiTrack.Infrastructure.Data;
using RondiTrack.Tests.TestSupport;

namespace RondiTrack.Tests;

[Collection(PostgresCollection.Name)]
public class RefreshSessionTests(PostgresApiFactory factory)
{
    private HttpClient Client() => factory.CreateClient(new WebApplicationFactoryClientOptions
        { BaseAddress = new Uri("https://localhost"), HandleCookies = false });

    private static string Cookie(HttpResponseMessage response) => response.Headers.GetValues("Set-Cookie")
        .Single(value => value.StartsWith(AuthController.RefreshCookieName + "=", StringComparison.Ordinal)).Split(';')[0];

    private static async Task<HttpResponseMessage> Post(HttpClient client, string path, string cookie)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path);
        request.Headers.Add("Cookie", cookie);
        return await client.SendAsync(request);
    }

    private static async Task<string> Login(HttpClient client)
    {
        var email = $"{Guid.NewGuid()}@example.com";
        var password = Guid.NewGuid().ToString("N");
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync("/api/auth/register", new { name = "Session user", email, password })).StatusCode);
        var response = await client.PostAsJsonAsync("/api/auth/login", new { email, password });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var setCookie = response.Headers.GetValues("Set-Cookie").Single();
        Assert.Contains("httponly", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secure", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("path=/api/auth", setCookie, StringComparison.OrdinalIgnoreCase);
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("refreshToken", body);
        Assert.DoesNotContain(Uri.UnescapeDataString(Cookie(response).Split('=', 2)[1]), body);
        return Cookie(response);
    }

    [Fact]
    public async Task Refresh_Rotates_HashesAtRest_AndReplayRevokesSuccessor()
    {
        using var client = Client();
        var original = await Login(client);
        var refresh = await Post(client, "/api/auth/refresh", original);
        Assert.Equal(HttpStatusCode.OK, refresh.StatusCode);
        var successor = Cookie(refresh);
        Assert.NotEqual(original, successor);
        Assert.DoesNotContain("refreshToken", await refresh.Content.ReadAsStringAsync());
        await using var context = new RondiTrackDbContext(factory.CreateDbContextOptions());
        var raw = Uri.UnescapeDataString(original.Split('=', 2)[1]);
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw)));
        var stored = await context.RefreshTokens.AsNoTracking().SingleAsync(token => token.TokenHash == hash);
        Assert.NotNull(stored.UsedAtUtc);
        Assert.NotNull(stored.ReplacedByTokenId);
        Assert.NotEqual(raw, stored.TokenHash);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Post(client, "/api/auth/refresh", original)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Post(client, "/api/auth/refresh", successor)).StatusCode);
    }

    [Fact]
    public async Task Logout_IsIdempotent_AndRefreshAfterLogoutFails()
    {
        using var client = Client();
        var cookie = await Login(client);
        Assert.Equal(HttpStatusCode.NoContent, (await Post(client, "/api/auth/logout", cookie)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await Post(client, "/api/auth/logout", cookie)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync("/api/auth/logout", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Post(client, "/api/auth/refresh", cookie)).StatusCode);
    }

    [Fact]
    public async Task ConcurrentRefresh_OnlyOneWins_AndReplayInvalidatesTheWinner()
    {
        using var client = Client();
        var cookie = await Login(client);
        var responses = await Task.WhenAll(Post(client, "/api/auth/refresh", cookie), Post(client, "/api/auth/refresh", cookie));
        Assert.Single(responses, response => response.StatusCode == HttpStatusCode.OK);
        Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Unauthorized);
        var successor = Cookie(responses.Single(response => response.StatusCode == HttpStatusCode.OK));
        Assert.Equal(HttpStatusCode.Unauthorized, (await Post(client, "/api/auth/refresh", successor)).StatusCode);
    }

    [Fact]
    public async Task ReuseDetection_RevokesOnlyTheAffectedLoginFamily()
    {
        using var client = Client();
        var first = await Login(client);
        var other = await Login(client);
        Assert.Equal(HttpStatusCode.OK, (await Post(client, "/api/auth/refresh", first)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Post(client, "/api/auth/refresh", first)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Post(client, "/api/auth/refresh", other)).StatusCode);
    }
}
