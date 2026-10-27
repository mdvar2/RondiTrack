# Assignment 6.2 — Observability: traces, metrics, safe logs and health checks

| | |
|---|---|
| Official title | Not recorded. The brief was `Assignment_6_2_RondiTrack.pdf`, which isn't in the repository. |
| Dates | 12–13 October 2026 |
| Final commit on `main` | [`c06d617`](https://github.com/mdvar2/RondiTrack/commit/c06d617) "Add Assignment 6.2 verification evidence" |
| Branch / PR | Committed directly to `main`; no PR |
| README as submitted | [README at `c06d617`](https://github.com/mdvar2/RondiTrack/blob/c06d617/README.md). Its content was carried into the [technical notes](technical-notes-to-6.3.md) |
| Re-tested | **Yes, 27 Oct 2026**: the `c06d617` code, exported unchanged, passed **101/101** on disposable PostgreSQL |

## 1. Purpose and learning objectives
The brief's principles are quoted in the [compliance matrix](evidence/assignment-6-2-final-compliance-matrix.md):
- "Logs answer what happened to this request? Metrics answer how often, and is it getting worse? Traces answer where did the time go, and which step failed?"
- "Telemetry is a Data Store: Everything you emit is retained, copied and read by people who never use the app. POPIA applies to it exactly as it applies to your database."
- "Bounded by Design: A metric tag or span attribute is a promise about how many distinct values will ever exist."

## 2. Original requirements
This is the only assignment before 8.1 whose requirements are recorded **word for word**: the [compliance matrix](evidence/assignment-6-2-final-compliance-matrix.md) audits all four pages of the brief, line by line. In summary:
- **Tracing:** OpenTelemetry tracing, metrics and logging with the service name `ronditrack-api`. Trace requests (except `/health`) and Npgsql commands. Export over OTLP **only** when `OTEL_EXPORTER_OTLP_ENDPOINT` is set.
- **Correlation:** every problem response carries a `correlationId` equal to the request's W3C trace ID, or the caller's `traceparent` if sent. The same ID appears on that request's log line.
- **Payout span:** its own span from my `ActivitySource`, with SQL spans nested beneath it, safe debugging tags, no amounts, names, emails or bank details, and error status on failure.
- **Metrics:** a `Meter` with counters for contributions recorded, payouts processed (by outcome) and authorization refusals (by result). Every tag value comes from a small fixed set.
- **POPIA log audit:** list every `_logger` call and exception message and what it can contain. 4xx is logged at Information without the message; 5xx at Error, with a generic detail for the client. Security events identify users only by ID. No `EnableSensitiveDataLogging`.
- **Health checks:** `/health/live` runs no checks and `/health/ready` checks PostgreSQL. Both are anonymous and return only Healthy or Unhealthy. Time readiness with the database stopped, before and after capping the timeout.
- **Tests:** a log-capture provider proving no email, password, token or amount is logged, plus a metric assertion. No existing assertion changed.
- **Constraints:** only the listed OpenTelemetry packages. No Serilog, APM agents or commercial backends. The Aspire dashboard runs locally only.

## 3. What I implemented
- **Telemetry:** the OpenTelemetry pipeline in `API/Program.cs`, and `API/Observability/RondiTrackTelemetry.cs` (the `ActivitySource` and `Meter`).
- **Correlation:** `API/Observability/Correlation.cs`, which puts the W3C trace ID on every problem response.
- **Payout span and counters:** the payout span and bounded counters in `PayoutService`, `ContributionService` and `StokvelAuthorization`.
- **Safe logging:** security events reduced to a minimum (`SecurityEvents.cs`). The full POPIA audit of every log line is [Appendix A of the technical notes](technical-notes-to-6.3.md#appendix-a-complete-popia-logging-audit).
- **Health checks:** `API/Health/HealthProbes.cs`, with a bounded PostgreSQL readiness check.
- **Evidence tooling:** a real trace captured in the Aspire dashboard (`scripts/evidence/`) and an isolation script ([`scripts/verify-assignment-6-2-isolation.ps1`](../../scripts/verify-assignment-6-2-isolation.ps1)).

## 4. Technologies, tools and dependencies
New packages, as listed in the current `API/RondiTrack.csproj`:

| Package | Version |
|---|---|
| OpenTelemetry.Extensions.Hosting | 1.19.1 |
| OpenTelemetry.Exporter.OpenTelemetryProtocol | 1.19.1 |
| OpenTelemetry.Instrumentation.AspNetCore | 1.19.0 |
| Npgsql.OpenTelemetry | 10.0.3 |
| Microsoft.Extensions.Diagnostics.HealthChecks.EntityFrameworkCore | 10.0.4 |

Tools: the **Aspire dashboard** container, for local viewing only.

## 5. Prerequisites and installation
As for [6.1](assignment-6.1.md#5-prerequisites-and-installation), plus Docker if you want to view telemetry.

## 6. Environment configuration
`OTEL_EXPORTER_OTLP_ENDPOINT`. Leave it unset for normal runs, tests and CI. EF command logging is set per environment: `Warning` in `appsettings.json`, `Information` in Development.

## 7. How to run or reproduce
**As submitted** (README at `c06d617`). This is the local-only dashboard used for the evidence:
```powershell
docker run --rm -d --name ronditrack-assignment62-dashboard -p 127.0.0.1:18888:18888 -p 127.0.0.1:4317:18889 -e DOTNET_DASHBOARD_UNSECURED_ALLOW_ANONYMOUS=true -e ASPIRE_DASHBOARD_UNSECURED_ALLOW_ANONYMOUS=true mcr.microsoft.com/dotnet/aspire-dashboard:13
```
The UI is at `http://127.0.0.1:18888`. Point the API at `OTEL_EXPORTER_OTLP_ENDPOINT=http://127.0.0.1:4317`. The README warns that this unsecured dashboard is "for a trusted laptop only".

Re-run on 27 Oct 2026: the `c06d617` tests passed **101 of 101**. I didn't re-run the dashboard trace capture.

**In the current codebase:** all of this is still present. Run the current API with the same variable set to see live traces. Tests: `BusinessTelemetryTests.cs`, `CorrelationTests.cs`, `EndToEndLogLeakTests.cs`, `HealthProbeTests.cs`, `LoggingTests.cs`.

## 8. Important files and components
- `API/Observability/`: `RondiTrackTelemetry.cs`, `Correlation.cs`, `SecurityEvents.cs`
- `API/Health/HealthProbes.cs`
- `RondiTrack.Tests/TestSupport/CapturedLogProvider.cs`, `CorrelationLogProvider.cs`, `LogInspection.cs`
- `scripts/evidence/` (the evidence host)

## 9. How it works
- ASP.NET Core and Npgsql instrumentation create the request and SQL spans automatically.
- `PayoutService` starts a `payout.process` span around its transaction, so the SQL spans nest under it.
- Counters are incremented where each business event happens, with tags such as the outcome, never IDs or amounts.
- The exception handler and the framework's 401 and 403 responses all receive the current trace ID as `correlationId`, so a user's reference number leads straight to the trace and the log line.

## 10. Testing and recorded results
| Point | Result |
|---|---|
| Full suite ([compliance matrix](evidence/assignment-6-2-final-compliance-matrix.md)) | **101 passed**, 0 failed, 0 skipped. No assertion mismatches against the pre-assignment baseline |
| Re-run 27 Oct 2026 of `c06d617` | **101 passed, 0 failed** |

Some items are only partially verified: running with the dev database stopped, the readiness timings, and the secret-history check. Each one's reason is given in the matrix.

## 11. Challenges and solutions
The matrix and stage reports record them stage by stage: [stage 2](evidence/assignment-6-2-stage-2-verification.md) to [stage 8](evidence/assignment-6-2-stage-8-verification.md), plus [supplementary diagnostics](evidence/assignment-6-2-supplementary-diagnostics.md). The "chairperson's question" — why "how many payouts this year?" is a database query and not a metric — is answered in the technical notes.

## 12. Learning outcomes
From [technical notes §6](technical-notes-to-6.3.md#6-assignments-and-learning-journey): "The observability work helped me distinguish event logs, aggregate measurements and the timed steps of a request. It also required treating financial telemetry as data that needs privacy protection."

## 13. Evidence, commits and pull requests
- [Final compliance matrix](evidence/assignment-6-2-final-compliance-matrix.md)
- Stage verifications: [2](evidence/assignment-6-2-stage-2-verification.md), [3](evidence/assignment-6-2-stage-3-verification.md), [4](evidence/assignment-6-2-stage-4-verification.md), [5](evidence/assignment-6-2-stage-5-verification.md), [6](evidence/assignment-6-2-stage-6-verification.md), [7](evidence/assignment-6-2-stage-7-verification.md), [8](evidence/assignment-6-2-stage-8-verification.md)
- [Payout trace screenshot](evidence/artifacts/payout-trace.png)
- [Sanitized log excerpts](evidence/artifacts/sanitized-log-excerpts.txt)
- [Dashboard verification](evidence/artifacts/dashboard-verification.json)
- [Technical notes §9–11](technical-notes-to-6.3.md#9-opentelemetry-tracing-and-metrics)

## 14. Relationship to other assignments
- **Before:** [6.1](assignment-6.1.md)'s security events are what the POPIA audit made safe.
- **Next:** [6.3](assignment-6.3.md) built its single error contract on this `correlationId`.
- **Later:** the reference that [7.3](assignment-7.3.md)'s and [8.1](assignment-8.1.md)'s error screens show is this `correlationId`.
