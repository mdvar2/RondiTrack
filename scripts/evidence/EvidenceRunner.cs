using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using RondiTrack.Infrastructure.Data;
using Testcontainers.PostgreSql;

public static class EvidenceRunner
{
    private static readonly string Root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
    private static readonly string Private = Path.Combine(Root, "obj/stage8-evidence");
    public static async Task Main(string[] args)
    {
        try { await RunAsync(args); }
        catch (Exception)
        {
            // Evidence tools must not publish third-party connection/fixture diagnostics.
            Console.Error.WriteLine("EVIDENCE_FAILED: verify local prerequisites; raw diagnostics were not published.");
            Environment.ExitCode = 1;
        }
    }
    private static async Task RunAsync(string[] args)
    {
        Directory.CreateDirectory(Private);
        if (args.Length > 0 && args[0] == "snapshot")
        {
            if (args.Length != 2 || args[1] is not ("before" or "after"))
                throw new InvalidOperationException("Snapshot requires a before or after label.");
            var secretsId = XDocument.Load(Path.Combine(Root, "API/RondiTrack.csproj")).Descendants("UserSecretsId").Single().Value;
            using var secrets = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Microsoft/UserSecrets", secretsId, "secrets.json")));
            var settings = new NpgsqlConnectionStringBuilder(secrets.RootElement.GetProperty("ConnectionStrings:RondiTrackDb").GetString())
                { IncludeErrorDetail = false, LogParameters = false, Timeout = 5 };
            if (settings.Database != "ronditrack" || settings.Host is not ("localhost" or "127.0.0.1" or "::1"))
                throw new InvalidOperationException("Snapshot target must be local development.");
            await using var conn = new NpgsqlConnection(settings.ConnectionString);
            await conn.OpenAsync();
            await using var tx = await conn.BeginTransactionAsync(System.Data.IsolationLevel.RepeatableRead);
            await using (var command = new NpgsqlCommand("SET TRANSACTION READ ONLY", conn, tx)) await command.ExecuteNonQueryAsync();
            var tables = new List<string>();
            await using (var command = new NpgsqlCommand("SELECT tablename FROM pg_catalog.pg_tables WHERE schemaname='public' ORDER BY tablename", conn, tx))
            await using (var reader = await command.ExecuteReaderAsync())
                while (await reader.ReadAsync()) tables.Add(reader.GetString(0));
            var fingerprints = new StringBuilder();
            using var quotes = new NpgsqlCommandBuilder();
            foreach (var table in tables)
            {
                var sql = "SELECT md5(COALESCE(string_agg(md5(to_jsonb(t)::text), '' ORDER BY md5(to_jsonb(t)::text)),'')) FROM public."
                    + quotes.QuoteIdentifier(table) + " AS t";
                await using var command = new NpgsqlCommand(sql, conn, tx);
                fingerprints.Append(table).Append(':').Append(await command.ExecuteScalarAsync()).Append('\n');
            }
            const string schemaSql = "SELECT md5(COALESCE(string_agg(table_name||':'||column_name||':'||data_type||':'||is_nullable||':'||COALESCE(column_default,''), '|' ORDER BY table_name,ordinal_position),'')) FROM information_schema.columns WHERE table_schema='public'";
            await using (var command = new NpgsqlCommand(schemaSql, conn, tx)) fingerprints.Append(await command.ExecuteScalarAsync());
            await tx.CommitAsync();
            var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(fingerprints.ToString())));
            await File.WriteAllTextAsync(Path.Combine(Private, args[1]+".sha256"), hash);
            Console.WriteLine("READ_ONLY_DEVELOPMENT_SNAPSHOT_CAPTURED");
            return;
        }
        await using var postgres = new PostgreSqlBuilder("postgres:17").WithDatabase("ronditrack_evidence")
            .WithUsername("postgres").WithPassword(Guid.NewGuid().ToString("N")).Build();
        await postgres.StartAsync();
        await using (var db = new RondiTrackDbContext(new DbContextOptionsBuilder<RondiTrackDbContext>().UseNpgsql(postgres.GetConnectionString()).Options))
            await db.Database.MigrateAsync();
        var logs = new ConcurrentQueue<string>();
        var start = new ProcessStartInfo("dotnet", Path.Combine(Root, "API/bin/Debug/net10.0/RondiTrack.dll"))
        {
            WorkingDirectory = Path.Combine(Root, "API"), UseShellExecute = false,
            RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true
        };
        var reservation = new TcpListener(IPAddress.Loopback,0); reservation.Start();
        var apiPort = ((IPEndPoint)reservation.LocalEndpoint).Port; reservation.Stop();
        var apiUrl = $"http://127.0.0.1:{apiPort}";
        start.Environment["ASPNETCORE_URLS"] = apiUrl;
        start.Environment["ASPNETCORE_ENVIRONMENT"] = "Development";
        start.Environment["ConnectionStrings__RondiTrackDb"] = postgres.GetConnectionString();
        start.Environment["Jwt__SigningKey"] = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        start.Environment["OTEL_EXPORTER_OTLP_ENDPOINT"] = "http://127.0.0.1:4317";
        start.Environment["OTEL_EXPORTER_OTLP_PROTOCOL"] = "grpc";
        start.Environment["OTEL_METRIC_EXPORT_INTERVAL"] = "1000";
        using var api = new Process { StartInfo = start, EnableRaisingEvents = true };
        api.OutputDataReceived += (_, line) => { if (line.Data is not null) logs.Enqueue(line.Data); };
        api.ErrorDataReceived += (_, line) => { if (line.Data is not null) logs.Enqueue(line.Data); };
        api.Start(); api.BeginOutputReadLine(); api.BeginErrorReadLine();
        try
        {
            using var client = new HttpClient(new HttpClientHandler { UseCookies = false }) { BaseAddress = new Uri(apiUrl) };
            for (var attempt = 0; ; attempt++)
            {
                if(api.HasExited) throw new InvalidOperationException("Evidence API exited before readiness.");
                try { if ((await client.GetAsync("/health/live")).IsSuccessStatusCode) break; }
                catch (HttpRequestException) { }
                if (attempt > 60) throw new InvalidOperationException("Evidence API did not start.");
                await Task.Delay(250);
            }
            var excluded = new List<string>();
            async Task<string> RegisterLogin(string marker, string trace)
            {
                var email = marker+Guid.NewGuid().ToString("N")+"@tests.example";
                var password = Guid.NewGuid().ToString("N");
                var name = marker+Guid.NewGuid().ToString("N");
                excluded.AddRange([email,password,name]);
                client.DefaultRequestHeaders.Remove("traceparent");
                using var reg = await client.PostAsJsonAsync("/api/auth/register", new { name,email,password }); reg.EnsureSuccessStatusCode();
                client.DefaultRequestHeaders.Remove("traceparent");
                client.DefaultRequestHeaders.Add("traceparent", $"00-{trace}-00f067aa0ba902b7-01");
                using var login = await client.PostAsJsonAsync("/api/auth/login", new { email,password }); login.EnsureSuccessStatusCode();
                using var body = JsonDocument.Parse(await login.Content.ReadAsStringAsync());
                var token = body.RootElement.GetProperty("accessToken").GetString()!; excluded.Add(token);
                excluded.Add(Uri.UnescapeDataString(login.Headers.GetValues("Set-Cookie").Single().Split(';')[0].Split('=',2)[1]));
                return token;
            }
            var loginTrace = ActivityTraceId.CreateRandom().ToString();
            var owner = await RegisterLogin("EvidenceOwner", loginTrace);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",owner);
            var stokvelName = "EvidenceStokvel"+Guid.NewGuid().ToString("N"); excluded.Add(stokvelName);
            const decimal amount = 4321.87m; excluded.Add(amount.ToString(System.Globalization.CultureInfo.InvariantCulture));
            using var st = await client.PostAsJsonAsync("/api/stokvels",new { name=stokvelName, contributionAmount=amount }); st.EnsureSuccessStatusCode();
            using var stBody = JsonDocument.Parse(await st.Content.ReadAsStringAsync()); var stokvel = stBody.RootElement.GetProperty("id").GetGuid();
            using var cy = await client.PostAsJsonAsync($"/api/stokvels/{stokvel}/cycles",new { period="2026-10",targetAmount=amount }); cy.EnsureSuccessStatusCode();
            using var cyBody = JsonDocument.Parse(await cy.Content.ReadAsStringAsync()); var cycle = cyBody.RootElement.GetProperty("id").GetGuid();
            var payoutTrace = ActivityTraceId.CreateRandom().ToString();
            client.DefaultRequestHeaders.Remove("traceparent"); client.DefaultRequestHeaders.Add("traceparent",$"00-{payoutTrace}-00f067aa0ba902b7-01");
            var offset = logs.Count;
            using var pay = await client.PostAsJsonAsync($"/api/stokvels/{stokvel}/cycles/{cycle}/payouts",new { amount }); pay.EnsureSuccessStatusCode();
            await Task.Delay(200);
            var payoutWindow = logs.Skip(offset).ToArray();
            var payoutLines = logs.Skip(offset).Where(line => line.Contains("Executed DbCommand",StringComparison.Ordinal)).ToArray();
            var queryIndex = Array.FindIndex(payoutWindow,line=>line.Contains("FROM \"Payouts\"",StringComparison.Ordinal));
            var headerIndex = queryIndex;
            while(headerIndex>=0 && !payoutWindow[headerIndex].Contains("Executed DbCommand",StringComparison.Ordinal)) headerIndex--;
            if(headerIndex<0) throw new InvalidOperationException("A genuine payout SQL log was not captured.");
            var payoutExcerpt = payoutWindow.Skip(headerIndex).Take(queryIndex-headerIndex+1).ToList();
            for(var index=queryIndex+1;index<payoutWindow.Length && !payoutWindow[index].StartsWith("info:",StringComparison.Ordinal);index++)
                payoutExcerpt.Add(payoutWindow[index]);
            client.DefaultRequestHeaders.Authorization = null;
            var outsider = await RegisterLogin("EvidenceOutsider",ActivityTraceId.CreateRandom().ToString());
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",outsider);
            var refusalTrace = ActivityTraceId.CreateRandom().ToString();
            client.DefaultRequestHeaders.Remove("traceparent"); client.DefaultRequestHeaders.Add("traceparent",$"00-{refusalTrace}-00f067aa0ba902b7-01");
            using var denied = await client.GetAsync($"/api/stokvels/{stokvel}");
            if (denied.StatusCode != HttpStatusCode.NotFound) throw new InvalidOperationException("Evidence refusal status mismatch.");
            using var problem = JsonDocument.Parse(await denied.Content.ReadAsStringAsync());
            if (problem.RootElement.GetProperty("correlationId").GetString()!=refusalTrace) throw new InvalidOperationException("Evidence correlation mismatch.");
            await Task.Delay(3000);
            var selected = logs.Where(line => line.Contains("Security event sign_in with success",StringComparison.Ordinal)
                || line.Contains("Security event stokvel_refusal with not_found",StringComparison.Ordinal)
                || line.Contains("Request rejected with 404 (NotFoundException)",StringComparison.Ordinal))
                .Concat(payoutExcerpt).ToArray();
            if (payoutLines.Length==0 || selected.Length<4) throw new InvalidOperationException("Required genuine log evidence was not captured.");
            if (selected.Any(line=>excluded.Any(value=>line.Contains(value,StringComparison.OrdinalIgnoreCase))))
                throw new InvalidOperationException("Sensitive evidence value detected; no artifact written.");
            await File.WriteAllLinesAsync(Path.Combine(Private,"sanitized-log-excerpts.txt"),selected);
            await File.WriteAllTextAsync(Path.Combine(Private,"manifest.json"),JsonSerializer.Serialize(new { loginTrace,payoutTrace,refusalTrace,
                service="ronditrack-api",payoutStatus=(int)pay.StatusCode,refusalStatus=(int)denied.StatusCode, sqlLogEvents=payoutLines.Length },new JsonSerializerOptions{WriteIndented=true}));
            Console.WriteLine("REAL_API_EVIDENCE_READY");
            for(var i=0;i<1200 && !File.Exists(Path.Combine(Private,"stop"));i++) await Task.Delay(500);
        }
        finally { if(!api.HasExited) { api.Kill(entireProcessTree:true); await api.WaitForExitAsync(); } }
    }
}
