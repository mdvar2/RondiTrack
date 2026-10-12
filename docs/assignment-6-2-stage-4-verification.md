# Assignment 6.2 — Stage 4 verification

Verified on 12 October 2026. Scope: payout tracing and bounded business counters.

## Changed files

| File | Change |
|---|---|
| API/Observability/RondiTrackTelemetry.cs | Singleton ActivitySource, factory-owned Meter and three counters. |
| API/Program.cs | Register singleton, source and meter with OpenTelemetry. |
| API/Services/PayoutService.cs | One activity and terminal measurement outside the existing retry strategy. |
| API/Services/ContributionService.cs | Increment immediately after successful repository persistence. |
| API/Auth/StokvelAuthorization.cs | Count the existing not-found/forbidden refusal before throwing. |
| RondiTrack.Tests/BusinessTelemetryTests.cs | Seven focused listener and database-backed cases. |
| RondiTrack.Tests/ContributionServiceTests.cs | Supply new telemetry constructor dependency; assertions unchanged. |
| RondiTrack.Tests/PayoutTransactionTests.cs | Supply new telemetry constructor dependency; assertions unchanged. |
| README.md | Instrument names, tags, safety, cardinality and database-query explanation. |
| This report | Actual verification and limitations. |

## Instrumentation and ownership

Source and Meter name: `RondiTrack.Business`. Span name: `payout.process`.
DI owns the singleton and disposes its ActivitySource. IMeterFactory owns the
Meter. No packages or migrations were added or changed.

The span starts before the existing execution strategy and is disposed at method
exit. All transaction commands run while that span is current, giving Npgsql
activities the business span as parent. Authentication/authorization SQL executed
before payout processing remains elsewhere under the incoming HTTP request.

One span tag: `outcome`, with `success` or `failure` (maximum 2 distinct values).
It reveals whether the operation completed without recording people, resources
or financial values. No custom span events, exception messages or status
descriptions are attached. Terminal failure sets Error status and rethrows.

| Counter | Tags / allowed values | Maximum distinct tag values / custom series |
|---|---|---|
| ronditrack.contributions.recorded | None | 0 / 1 |
| ronditrack.payouts.processed | outcome: success, failure | 2 / 2 |
| ronditrack.authorization.refusals | result: not_found, forbidden | 2 / 2 |

Limits are per instrument/resource, before backend resource dimensions. Methods
accept booleans rather than arbitrary tag strings, enforcing the fixed values.

Payout measurements are outside the retry callback: eventual success increments
after CommitAsync and ExecuteAsync return, and terminal failure increments once
in the outer catch. Retry attempts do not count independently. Existing payout
rules, transaction boundaries, rollback and retry configuration are preserved.

ContributionRepository.AddAsync performs SaveChangesAsync before returning.
ContributionService increments after that return. Idempotent replays return
before this point. A persisted contribution counts even if later idempotency
response storage fails, because the counter measures persistence.

StokvelAuthorization increments after a failed policy evaluation, immediately
before its existing 404 or 403 exception. No controller instrumentation or
permission decision changes were introduced.

## Actual verification

```text
dotnet build RondiTrack.sln --no-restore -m:1
Build succeeded.
    0 Warning(s)
    0 Error(s)
Time Elapsed 00:00:06.32
BUILD_EXIT_CODE=0

dotnet test RondiTrack.Tests/RondiTrack.Tests.csproj --no-build --no-restore -m:1
Passed! - Failed: 0, Passed: 83, Skipped: 0, Total: 83, Duration: 8 s
TEST_EXIT_CODE=0
```

The full suite used disposable PostgreSQL 17 with the preserved migrations and
OTLP disabled. Docker pipe access required running tests outside the sandbox.
The intermediate run passed 82 cases; the final 83 includes commit-failure
verification. An initial build had five new-test analyzer warnings; they were
fixed before the final zero-warning build.

Seven added cases prove successful payout measurement and committed records;
HTTP/business/SQL parent links with matching trace IDs; registered source and
exact safe tags; failed payout Error status and failure measurement; contribution
persistence and idempotent replay; both bounded authorization refusal results;
one span/measurement after a synthetic transient query failure and retry; and
failure-only measurement plus rollback when commit is deliberately refused.

Local ignored evidence: `obj/stage4-build.txt`, `obj/stage4-tests.txt` and
`obj/stage4-test-results/stage4-tests.trx`. Raw diagnostics are not published.

## Limits and later work

The hierarchy is programmatically verified; no Aspire screenshot or visual
dashboard inspection is claimed. Genuine screenshot evidence remains Stage 8.
Sampling, backend retention and alerting remain later operational decisions.
Counters are not a durable ledger or an exactly-once export guarantee: process
failure/export loss can affect telemetry. Existing ambiguous-commit/retry
behaviour is preserved rather than redesigned.

The chairperson's yearly scoped payout count requires an authorized database
query: metrics omit stokvel identity and cannot supply an authoritative count of
committed records for that resource and date range.

JWT, refresh sessions, authorization decisions, business rules, all existing
assertions, migrations and development records are unchanged. The development
migration blocker remains. This was not a development-database-stopped isolation
run. Stage 5 POPIA logging cleanup and later health/evidence work are pending.
No known Stage 4 failures remain. No push was made.
