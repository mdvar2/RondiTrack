using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RondiTrack.Domain.Entities;
using RondiTrack.Infrastructure.Data;
using RondiTrack.Tests.TestSupport;

namespace RondiTrack.Tests;

[Collection(PostgresCollection.Name)]
public sealed class Assignment91ApiTests(PostgresApiFactory factory)
{
    [Fact]
    public async Task InvitePreview_ContainsOnlyDecisionFields_AndJoinIsIdempotentlyRejected()
    {
        var scenario = await AuthScenario.CreateAsync(factory);
        using var treasurer = factory.CreateClientFor(scenario.Treasurer);
        using var member = factory.CreateClientFor(scenario.Outsider);

        var created = await treasurer.PostAsync($"/api/v1/stokvels/{scenario.StokvelId}/invites", null);
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        var invite = await created.Content.ReadFromJsonAsync<JsonElement>();
        var code = invite.GetProperty("code").GetString();
        Assert.False(string.IsNullOrWhiteSpace(code));

        var previewResponse = await member.GetAsync($"/api/v1/invites/{code}");
        Assert.Equal(HttpStatusCode.OK, previewResponse.StatusCode);
        var preview = await previewResponse.Content.ReadFromJsonAsync<JsonElement>();
        var fields = preview.EnumerateObject().Select(property => property.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var expectedFields = new HashSet<string>(
            ["stokvelId", "name", "contributionAmountCents", "schedule", "rules", "memberCount"],
            StringComparer.OrdinalIgnoreCase);
        Assert.Equal(expectedFields, fields);
        Assert.DoesNotContain("members", fields, StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain("balances", fields, StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain("payoutOrder", fields, StringComparer.OrdinalIgnoreCase);

        var firstJoin = await member.PostAsync($"/api/v1/invites/{code}/join", null);
        var secondJoin = await member.PostAsync($"/api/v1/invites/{code}/join", null);
        Assert.Equal(HttpStatusCode.NoContent, firstJoin.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, secondJoin.StatusCode);
    }

    [Fact]
    public async Task InviteCreation_RequiresTreasurer_AndUnknownOrExpiredCodesAreNotAccepted()
    {
        var scenario = await AuthScenario.CreateAsync(factory);
        using var ordinaryMember = factory.CreateClientFor(scenario.Member);
        using var treasurer = factory.CreateClientFor(scenario.Treasurer);

        Assert.Equal(HttpStatusCode.Forbidden,
            (await ordinaryMember.PostAsync($"/api/v1/stokvels/{scenario.StokvelId}/invites", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await treasurer.GetAsync("/api/v1/invites/unknown-code")).StatusCode);

        await using var db = new RondiTrackDbContext(factory.CreateDbContextOptions());
        var expired = new StokvelInvite(scenario.StokvelId, DateTime.UtcNow.AddMinutes(-1));
        db.StokvelInvites.Add(expired);
        await db.SaveChangesAsync();

        Assert.Equal(HttpStatusCode.Gone,
            (await treasurer.GetAsync($"/api/v1/invites/{expired.Code}")).StatusCode);
    }

    [Theory]
    [InlineData("pdf", "application/pdf", "JVBERi0xLjc=")]
    [InlineData("jpg", "image/jpeg", "/9j/AA==")]
    [InlineData("png", "image/png", "iVBORw0KGgo=")]
    public async Task ProofUpload_AcceptsSupportedSignatures(string extension, string contentType, string signatureBase64)
    {
        var scenario = await SeedContributionAsync();
        using var client = factory.CreateClientFor(scenario.Member);
        using var form = new MultipartFormDataContent();
        var content = new ByteArrayContent(Convert.FromBase64String(signatureBase64));
        content.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        form.Add(content, "file", $"proof.{extension}");

        var response = await client.PostAsync($"/api/v1/contributions/{scenario.ContributionId}/proof", form);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ProofUpload_RejectsOversizeAndRenamedExecutable()
    {
        var scenario = await SeedContributionAsync();
        using var client = factory.CreateClientFor(scenario.Member);

        using var oversized = new MultipartFormDataContent();
        oversized.Add(new ByteArrayContent([.. "%PDF-"u8, .. new byte[5 * 1024 * 1024]]), "file", "large.pdf");
        Assert.Equal(HttpStatusCode.BadRequest,
            (await client.PostAsync($"/api/v1/contributions/{scenario.ContributionId}/proof", oversized)).StatusCode);

        using var executable = new MultipartFormDataContent();
        executable.Add(new ByteArrayContent("MZ-not-a-pdf"u8.ToArray()), "file", "renamed.pdf");
        Assert.Equal(HttpStatusCode.BadRequest,
            (await client.PostAsync($"/api/v1/contributions/{scenario.ContributionId}/proof", executable)).StatusCode);
    }

    [Fact]
    public async Task ProofDownload_UsesPrivacy404AndSecureHeaders()
    {
        var scenario = await SeedContributionAsync();
        using var uploader = factory.CreateClientFor(scenario.Member);
        await UploadProofAsync(uploader, scenario.ContributionId);

        var download = await uploader.GetAsync($"/api/v1/contributions/{scenario.ContributionId}/proof/download");
        Assert.Equal(HttpStatusCode.OK, download.StatusCode);
        Assert.Equal("nosniff", download.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.NotNull(download.Headers.CacheControl);
        Assert.True(download.Headers.CacheControl!.Private);
        Assert.True(download.Headers.CacheControl.NoStore);

        using var treasurer = factory.CreateClientFor(scenario.Treasurer);
        Assert.Equal(HttpStatusCode.OK,
            (await treasurer.GetAsync($"/api/v1/contributions/{scenario.ContributionId}/proof/download")).StatusCode);

        using var ordinaryMember = factory.CreateClientFor(scenario.Outsider);
        Assert.Equal(HttpStatusCode.NotFound,
            (await ordinaryMember.GetAsync($"/api/v1/contributions/{scenario.ContributionId}/proof/download")).StatusCode);
        var otherScenario = await AuthScenario.CreateAsync(factory);
        using var otherTreasurer = factory.CreateClientFor(otherScenario.Treasurer);
        Assert.Equal(HttpStatusCode.NotFound,
            (await otherTreasurer.GetAsync($"/api/v1/contributions/{scenario.ContributionId}/proof/download")).StatusCode);
    }

    [Fact]
    public async Task ClosedCycle_RejectsProofReplacementAndRemoval()
    {
        var scenario = await SeedContributionAsync();
        using var client = factory.CreateClientFor(scenario.Member);
        await UploadProofAsync(client, scenario.ContributionId);

        await using (var db = new RondiTrackDbContext(factory.CreateDbContextOptions()))
        {
            var cycle = await db.ContributionCycles.SingleAsync(x => x.Id == scenario.CycleId);
            cycle.Close();
            await db.SaveChangesAsync();
        }

        Assert.Equal(HttpStatusCode.Conflict,
            (await UploadProofAsync(client, scenario.ContributionId)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,
            (await client.DeleteAsync($"/api/v1/contributions/{scenario.ContributionId}/proof")).StatusCode);
    }

    [Fact]
    public async Task ClosePreview_IsAuthoritative_RejectsStaleVersion_AndIsIdempotent()
    {
        var scenario = await SeedCloseScenarioAsync();
        using var treasurer = factory.CreateClientFor(scenario.Treasurer);
        var previewResponse = await treasurer.GetAsync($"/api/v1/stokvels/{scenario.StokvelId}/cycles/{scenario.CycleId}/close/preview");
        Assert.Equal(HttpStatusCode.OK, previewResponse.StatusCode);
        var preview = await previewResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(preview.GetProperty("totalCents").GetInt64() > 0);
        var johannesburg = TimeZoneInfo.FindSystemTimeZoneById("Africa/Johannesburg");
        var expectedAsOf = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, johannesburg).DateTime);
        Assert.Equal(expectedAsOf.ToString("yyyy-MM-dd"), preview.GetProperty("asOfDate").GetString());

        await using (var db = new RondiTrackDbContext(factory.CreateDbContextOptions()))
        {
            db.Contributions.Add(new Contribution(scenario.StokvelId, scenario.Member.Id, scenario.CycleId, 500m));
            await db.SaveChangesAsync();
        }

        using var staleRequest = new HttpRequestMessage(HttpMethod.Post,
            $"/api/v1/stokvels/{scenario.StokvelId}/cycles/{scenario.CycleId}/close")
        {
            Content = JsonContent.Create(new { version = preview.GetProperty("version").GetString() }),
        };
        staleRequest.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        Assert.Equal(HttpStatusCode.Conflict, (await treasurer.SendAsync(staleRequest)).StatusCode);

        var fresh = await (await treasurer.GetAsync($"/api/v1/stokvels/{scenario.StokvelId}/cycles/{scenario.CycleId}/close/preview"))
            .Content.ReadFromJsonAsync<JsonElement>();
        using var close = new HttpRequestMessage(HttpMethod.Post,
            $"/api/v1/stokvels/{scenario.StokvelId}/cycles/{scenario.CycleId}/close")
        {
            Content = JsonContent.Create(new { version = fresh.GetProperty("version").GetString() }),
        };
        close.Headers.Add("Idempotency-Key", "assignment-91-" + Guid.NewGuid());
        var closed = await treasurer.SendAsync(close);
        Assert.Equal(HttpStatusCode.OK, closed.StatusCode);

        using var repeated = new HttpRequestMessage(HttpMethod.Post,
            $"/api/v1/stokvels/{scenario.StokvelId}/cycles/{scenario.CycleId}/close")
        {
            Content = JsonContent.Create(new { version = fresh.GetProperty("version").GetString() }),
        };
        repeated.Headers.Add("Idempotency-Key", close.Headers.GetValues("Idempotency-Key").Single());
        var repeatedResponse = await treasurer.SendAsync(repeated);
        Assert.Equal(HttpStatusCode.OK, repeatedResponse.StatusCode);
        var closedJson = JsonDocument.Parse(await closed.Content.ReadAsStringAsync());
        var repeatedJson = JsonDocument.Parse(await repeatedResponse.Content.ReadAsStringAsync());
        Assert.True(JsonElement.DeepEquals(closedJson.RootElement, repeatedJson.RootElement));
    }

    private async Task<(Guid StokvelId, Guid CycleId, Guid ContributionId, User Member, User Treasurer, User Outsider)> SeedContributionAsync()
    {
        var scenario = await AuthScenario.CreateAsync(factory);
        await using var db = new RondiTrackDbContext(factory.CreateDbContextOptions());
        var contribution = new Contribution(scenario.StokvelId, scenario.Member.Id, scenario.CycleId, 500m);
        db.Contributions.Add(contribution);
        await db.SaveChangesAsync();
        return (scenario.StokvelId, scenario.CycleId, contribution.Id, scenario.Member, scenario.Treasurer, scenario.Outsider);
    }

    private async Task<(Guid StokvelId, Guid CycleId, User Member, User Treasurer)> SeedCloseScenarioAsync()
    {
        var scenario = await AuthScenario.CreateAsync(factory);
        await using var db = new RondiTrackDbContext(factory.CreateDbContextOptions());
        var cycle = await db.ContributionCycles.SingleAsync(x => x.Id == scenario.CycleId);
        cycle.UpdateDetails(cycle.Period, cycle.TargetAmount, new DateOnly(2026, 10, 31));
        await db.SaveChangesAsync();
        return (scenario.StokvelId, scenario.CycleId, scenario.Member, scenario.Treasurer);
    }

    private static async Task<HttpResponseMessage> UploadProofAsync(HttpClient client, Guid contributionId)
    {
        using var form = new MultipartFormDataContent();
        form.Add(new ByteArrayContent("%PDF-1.7"u8.ToArray()), "file", "proof.pdf");
        return await client.PostAsync($"/api/v1/contributions/{contributionId}/proof", form);
    }
}
