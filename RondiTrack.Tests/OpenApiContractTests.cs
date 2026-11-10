using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Mvc.Testing;
using RondiTrack.Tests.TestSupport;

namespace RondiTrack.Tests;

// The frontend's types are generated from openapi/v1.json, so that file is the
// contract. This test fails when what the running API serves differs from it.
//
// Regenerate the committed document after an intended API change:
//   bash:        OPENAPI_UPDATE=1 dotnet test RondiTrack.Tests --filter OpenApiContractTests
//   PowerShell:  $env:OPENAPI_UPDATE=1; dotnet test RondiTrack.Tests --filter OpenApiContractTests; Remove-Item Env:OPENAPI_UPDATE
// then, in web-next: npm run generate:api
[Collection(PostgresCollection.Name)]
public class OpenApiContractTests(PostgresApiFactory factory)
{
    private const string UpdateVariable = "OPENAPI_UPDATE";

    [Fact]
    public async Task ServedDocument_MatchesCommittedContract()
    {
        var served = await FetchServedDocumentAsync();
        var path = CommittedDocumentPath();

        // The document must not depend on the build OS (Windows newlines from XML
        // doc comments once made a Windows-generated file fail on Linux CI).
        Assert.DoesNotContain('\r', served.ToJsonString());

        if (Environment.GetEnvironmentVariable(UpdateVariable) == "1")
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            await File.WriteAllTextAsync(path, Serialize(served), new UTF8Encoding(false));
            return;
        }

        Assert.True(File.Exists(path),
            $"The committed contract {path} is missing. Run with {UpdateVariable}=1 to create it.");
        var committed = Normalize(JsonNode.Parse(await File.ReadAllTextAsync(path))!);

        var differences = new List<string>();
        Compare(committed, served, "", differences);
        Assert.True(differences.Count == 0,
            "The running API no longer matches openapi/v1.json. If the change is intended, run " +
            $"{UpdateVariable}=1 dotnet test RondiTrack.Tests --filter OpenApiContractTests, then " +
            "regenerate the frontend types (cd web-next && npm run generate:api).\n" +
            string.Join("\n", differences.Take(40)));
    }

    [Fact]
    public void Compare_ReportsRenamedField()
    {
        var before = JsonNode.Parse("""{"properties":{"amount":{"type":"integer"}}}""")!;
        var after = JsonNode.Parse("""{"properties":{"amountCents":{"type":"integer"}}}""")!;
        var differences = new List<string>();

        Compare(before, after, "", differences);

        Assert.Contains("/properties/amount: in openapi/v1.json, missing from the running API", differences);
        Assert.Contains("/properties/amountCents: served by the running API, missing from openapi/v1.json", differences);
    }

    private async Task<JsonNode> FetchServedDocumentAsync()
    {
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
            { BaseAddress = new Uri("https://localhost") });
        using var response = await client.GetAsync("/openapi/v1.json");
        response.EnsureSuccessStatusCode();
        return Normalize(JsonNode.Parse(await response.Content.ReadAsStringAsync())!);
    }

    // "servers" echoes the host the request arrived on, so it differs between
    // this test, `dotnet run` and Docker. Everything else is the contract.
    private static JsonNode Normalize(JsonNode document)
    {
        document.AsObject().Remove("servers");
        return document;
    }

    private static string Serialize(JsonNode document) =>
        document.ToJsonString(new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        }).ReplaceLineEndings("\n") + "\n";

    private static string CommittedDocumentPath()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "RondiTrack.sln")))
            directory = directory.Parent;
        if (directory is null)
            throw new InvalidOperationException("Could not find the repository root (RondiTrack.sln).");
        return Path.Combine(directory.FullName, "openapi", "v1.json");
    }

    private static void Compare(JsonNode? expected, JsonNode? actual, string pointer, List<string> differences)
    {
        switch (expected, actual)
        {
            case (JsonObject e, JsonObject a):
                foreach (var (key, value) in e)
                {
                    var child = $"{pointer}/{Escape(key)}";
                    if (!a.ContainsKey(key))
                        differences.Add($"{child}: in openapi/v1.json, missing from the running API");
                    else
                        Compare(value, a[key], child, differences);
                }
                foreach (var (key, _) in a)
                    if (!e.ContainsKey(key))
                        differences.Add($"{pointer}/{Escape(key)}: served by the running API, missing from openapi/v1.json");
                break;
            case (JsonArray e, JsonArray a):
                if (e.Count != a.Count)
                    differences.Add($"{pointer}: {e.Count} items in openapi/v1.json, {a.Count} served");
                for (var i = 0; i < Math.Min(e.Count, a.Count); i++)
                    Compare(e[i], a[i], $"{pointer}/{i}", differences);
                break;
            default:
                if (!JsonNode.DeepEquals(expected, actual))
                    differences.Add($"{pointer}: {expected?.ToJsonString() ?? "null"} in openapi/v1.json, " +
                        $"{actual?.ToJsonString() ?? "null"} served");
                break;
        }
    }

    private static string Escape(string key) => key.Replace("~", "~0").Replace("/", "~1");
}
