# Assignment 6.2 — Stage 2 verification

Verified on 12 October 2026. This stage implements the telemetry pipeline only.

## Changes

- `API/Program.cs`: register OpenTelemetry tracing, metrics and logging with
  `service.name=ronditrack-api`; exclude `/health` and child paths from incoming
  request traces; subscribe to Npgsql command activities; collect ASP.NET Core
  and System.Runtime metrics. Register the combined OTLP exporter only when
  `OTEL_EXPORTER_OTLP_ENDPOINT` is nonblank.
- `API/RondiTrack.csproj`: add the packages below. The health-check dependency
  is installed now; probes and timeouts remain Stage 6 work.
- `API/appsettings.json`: explicitly set EF command logging to Warning.
  The existing Development override remains Information. No LogTo path exists.
- `README.md`: explain the pipeline, export condition and SQL logging choice.

| Added direct dependency | Version |
|---|---|
| OpenTelemetry.Extensions.Hosting | 1.19.1 |
| OpenTelemetry.Instrumentation.AspNetCore | 1.19.0 |
| OpenTelemetry.Exporter.OpenTelemetryProtocol | 1.19.1 |
| Npgsql.OpenTelemetry | 10.0.3 |
| Microsoft.Extensions.Diagnostics.HealthChecks.EntityFrameworkCore | 10.0.4 |

The restored graph retains EF Core 10.0.4 and Npgsql 10.0.3. OpenTelemetry SDK/API
resolve to 1.19.1. Restore reports no dependency warnings.

## Actual verification

`dotnet build RondiTrack.sln --no-restore -m:1` returned exit code 0:

```text
Build succeeded.
    0 Warning(s)
    0 Error(s)
Time Elapsed 00:00:13.12
```

With `OTEL_EXPORTER_OTLP_ENDPOINT` unset, ran:

```text
dotnet test RondiTrack.Tests/RondiTrack.Tests.csproj --no-build --no-restore -m:1
Passed! - Failed: 0, Passed: 62, Skipped: 0, Total: 62, Duration: 8 s
TEST_EXIT_CODE=0
```

Tests used the existing disposable PostgreSQL 17 fixture and its preserved
migration chain. No development database operations or service changes were
performed. This was not a development-database-stopped isolation run.

Initial verification attempts reported all 62 cases failed during fixture
initialization because Docker was unavailable; no test bodies ran. Docker Desktop
startup then succeeded. The successful run required execution outside the sandbox
for Docker pipe access. No assertions or application behaviour were changed to
resolve that environmental issue.

Raw build output and the final test/TRX output are local ignored artifacts in
`obj/stage2-build.txt`, `obj/stage2-tests.txt`, and `obj/stage2-test-results`.
Do not publish raw test diagnostic output without a separate privacy review.

## Remaining stages

Correlation IDs and the approved login-test setup adjustment remain Stage 3.
Business telemetry, POPIA logging fixes, health probes, new observability tests,
dashboard screenshots and outage/isolation evidence remain later-stage work.
No dashboard export or trace screenshot was verified here. The test factory's
existing logging override clears providers and adds Console, so this regression
run alone does not prove delivery of logs to an OTLP backend.

The existing development migration blocker remains unchanged. No push was made.
