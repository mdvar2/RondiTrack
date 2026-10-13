using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using RondiTrack.Domain.Entities;
using RondiTrack.Infrastructure.Data;
using RondiTrack.Tests.TestSupport;

namespace RondiTrack.Tests;

[Collection(PostgresCollection.Name)]
public class RegistrationTests(PostgresApiFactory factory)
{
    [Fact]
    public async Task Register_PersistsPasswordHash_WithoutReturningIt()
    {
        using var client = factory.CreateClient();
        var password = Guid.NewGuid().ToString("N");
        var email = $"Register.{Guid.NewGuid()}@Example.com";
        var response = await client.PostAsJsonAsync("/api/v1/auth/register", new { name = "Registered user", email, password });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain(password, body);
        Assert.DoesNotContain("passwordHash", body);
        var id = JsonDocument.Parse(body).RootElement.GetProperty("id").GetGuid();
        await using var context = new RondiTrackDbContext(factory.CreateDbContextOptions());
        var user = await context.Users.SingleAsync(row => row.Id == id);
        Assert.Equal(User.NormalizeEmail(email), user.NormalizedEmail);
        Assert.NotEqual(password, user.PasswordHash);
        Assert.NotEqual(PasswordVerificationResult.Failed,
            new PasswordHasher<User>().VerifyHashedPassword(user, user.PasswordHash!, password));
    }

    [Fact]
    public async Task Register_ShortPassword_Returns400ProblemJson()
    {
        using var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/v1/auth/register", new { name = "User", email = $"{Guid.NewGuid()}@example.com", password = "too-short" });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Register_EmailCaseAndWhitespace_DoNotBypassUniqueIndex()
    {
        using var client = factory.CreateClient();
        var email = $"{Guid.NewGuid()}@example.com";
        var password = Guid.NewGuid().ToString("N");
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync("/api/v1/auth/register", new { name = "First", email, password })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync("/api/v1/auth/register", new { name = "Second", email = email.ToUpperInvariant(), password })).StatusCode);
        await using var context = new RondiTrackDbContext(factory.CreateDbContextOptions());
        context.Users.Add(new User("Bypass attempt", $" {email.ToUpperInvariant()} "));
        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }
}
