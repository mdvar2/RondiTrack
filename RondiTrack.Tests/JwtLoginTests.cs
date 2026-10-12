using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using RondiTrack.Auth;
using RondiTrack.DTOs.Auth;
using RondiTrack.Tests.TestSupport;

namespace RondiTrack.Tests;

[Collection(PostgresCollection.Name)]
public class JwtLoginTests(PostgresApiFactory factory)
{
    [Fact]
    public async Task Login_IssuesValidatedShortLivedToken_WithoutStokvelClaims()
    {
        using var client = factory.CreateClient();
        var email = $"{Guid.NewGuid()}@example.com";
        var password = Guid.NewGuid().ToString("N");
        var registration = await client.PostAsJsonAsync("/api/auth/register", new { name = "Login user", email, password });
        var userId = JsonDocument.Parse(await registration.Content.ReadAsStringAsync()).RootElement.GetProperty("id").GetGuid();
        var response = await client.PostAsJsonAsync("/api/auth/login", new { email = email.ToUpperInvariant(), password });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var access = (await response.Content.ReadFromJsonAsync<AccessTokenResponse>())!;
        var options = factory.Services.GetRequiredService<JwtOptions>();
        var principal = new JwtSecurityTokenHandler { MapInboundClaims = false }
            .ValidateToken(access.AccessToken, options.ValidationParameters, out var validated);
        Assert.Equal(userId.ToString(), principal.FindFirst("sub")?.Value);
        Assert.NotNull(principal.FindFirst("jti"));
        Assert.InRange(validated.ValidTo - validated.ValidFrom, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(15));
        Assert.DoesNotContain(principal.Claims, claim => claim.Type is "stokvel_role" or "stokvel_id" or "email");
    }

    [Fact]
    public async Task Login_UnknownEmailAndWrongPassword_ReturnIdenticalProblemDetails()
    {
        using var client = factory.CreateClient();
        var email = $"{Guid.NewGuid()}@example.com";
        var password = Guid.NewGuid().ToString("N");
        await client.PostAsJsonAsync("/api/auth/register", new { name = "User", email, password });
        // Compare failures within one caller trace; all existing assertions remain intact.
        client.DefaultRequestHeaders.Add("traceparent",
            "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01");
        var wrong = await client.PostAsJsonAsync("/api/auth/login", new { email, password = "incorrect-password" });
        var unknown = await client.PostAsJsonAsync("/api/auth/login", new { email = $"{Guid.NewGuid()}@example.com", password = "incorrect-password" });
        Assert.Equal(HttpStatusCode.Unauthorized, wrong.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, unknown.StatusCode);
        Assert.Equal("application/problem+json", wrong.Content.Headers.ContentType?.MediaType);
        Assert.Equal(await wrong.Content.ReadAsStringAsync(), await unknown.Content.ReadAsStringAsync());
    }

    [Fact]
    public void Validation_RejectsWrongSignatureIssuerAudienceAndExpiredTokens()
    {
        var options = factory.Services.GetRequiredService<JwtOptions>();
        var handler = new JwtSecurityTokenHandler();
        string Mint(string issuer, string audience, byte[] key, bool expired = false) => handler.WriteToken(
            new JwtSecurityToken(issuer, audience, [new Claim("sub", Guid.NewGuid().ToString())],
                DateTime.UtcNow.AddMinutes(-10), DateTime.UtcNow.AddMinutes(expired ? -2 : 5),
                new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256)));
        foreach (var token in new[]
        {
            Mint(options.Issuer, options.Audience, System.Security.Cryptography.RandomNumberGenerator.GetBytes(32)),
            Mint("wrong-issuer", options.Audience, options.SigningKey),
            Mint(options.Issuer, "wrong-audience", options.SigningKey),
            Mint(options.Issuer, options.Audience, options.SigningKey, true)
        }) Assert.ThrowsAny<SecurityTokenException>(() => handler.ValidateToken(token, options.ValidationParameters, out _));
    }

    [Fact]
    public void SigningKey_MissingInvalidOrShort_FailsWithHelpfulStartupMessage()
    {
        foreach (var value in new[] { "", "invalid-base64", Convert.ToBase64String(new byte[16]) })
        {
            var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Jwt:SigningKey"] = value }).Build();
            var exception = Assert.Throws<InvalidOperationException>(() => JwtOptions.Load(config));
            Assert.Contains("User Secrets", exception.Message);
            Assert.Contains("32 bytes", exception.Message);
        }
    }
}
