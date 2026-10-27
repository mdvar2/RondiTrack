# Assignment 6.2 — Stage 7 verification

Verified on 13 October 2026. Starting baseline: 99 passing cases. Final result:
101 passed, 0 failed, 0 skipped, with development connections rejected throughout
the final test invocation. OTLP was disabled; no dashboard dependency was required.

## Changed files

| File | Change |
|---|---|
| RondiTrack.Tests/EndToEndLogLeakTests.cs | Required combined HTTP scenario and leak-detector positive control. |
| RondiTrack.Tests/TestSupport/CapturedLogProvider.cs | External/nested scope capture alongside existing state/message/exception capture. |
| RondiTrack.Tests/TestSupport/LogInspection.cs | Decode nested structured values, scopes and exception data for in-memory inspection. |
| RondiTrack.Tests/TestSupport/PostgresApiFactory.cs | Force OTLP endpoint blank for dashboard-independent tests. |
| RondiTrack.Tests/BusinessTelemetryTests.cs | Add SDK listener precondition and complete observed payout/cycle SQL parent checks. |
| scripts/verify-assignment-6-2-isolation.ps1 | Guarded connection-admission isolation, verified restoration and sanitized result capture. |
| README.md | Scenario, review, isolation method, final results and limitations. |
| This report | Detailed verification and scope. |
| docs/assignment-6-2-stage-7-isolation-evidence.txt | Sanitized evidence copied from the successful final run. |

No application production code, package, business rule, migration or existing
assertion was changed. All original assertion lines in the strengthened test
remain present; assertions were added, not replaced or relaxed. LoggingTests.cs
is unchanged from Stage 6.

## End-to-end scenario and sensitive checks

The new test uses the real API and existing PostgreSQL Testcontainers fixture:

1. Register a uniquely named fixture user with distinctive email and password.
2. Log in and capture access token and refresh cookie/token.
3. Refresh using that original cookie; capture rotated access/refresh tokens and
   assert refresh rotation actually issued a different token.
4. Use the rotated access token to create a stokvel and cycle through authorized
   HTTP operations. Creation grants the existing chairperson membership; no role
   bypass or authorization mock is used.
5. Record the assignment-specified distinctive contribution amount and verify
   the corresponding committed database record and amount.
6. Attempt duplicate registration and assert 409 with a matching structured
   correlation/status error event and no Error/Critical entries.

The capture covers the whole sequence, not just the conflict response. Prohibited
values include email, password, distinctive user/stokvel names, original/rotated
access and refresh tokens, encoded refresh values, refresh hashes, stored password
hash, generated signing-key representations and the amount in dot/comma forms.
No actual fixture credentials, identities, tokens or values are published here.

Inspection includes formatted messages, structured state, nested values, exception
ToString/data, rendered scopes and structured scope properties. JSON strings are
decoded before comparison so escaping cannot conceal a marker. Assertions require
real sign-in/refresh events, real EF command-category entries and nonempty scopes.
The detector's positive control deliberately supplies harmless marker strings in
each surface, including a scope value with escaping-sensitive characters; every
marker must be detected. Failure messages never print the prohibited values or
raw capture.

## Review of existing Stage 2–6 tests

| Requirement | Existing evidence / review |
|---|---|
| Caller trace on anonymous 401 and non-member 404 | CorrelationTests validates sent trace IDs and captured structured CorrelationId; includes platform-admin nonmembership. |
| Generic 500 and internal full exception | LoggingTests and CorrelationTests inspect response, original exception object/stack and structured correlation. |
| Log state and exception inspection | LoggingTests already inspected state and exception text. Its provider previously omitted scopes; provider now captures scopes, and the new combined test checks them explicitly. |
| Payout source and HTTP/business/SQL hierarchy | BusinessTelemetryTests listens to real activities. Added HasListeners assertion before the test listener so manual sampling cannot mask missing SDK source registration; added parent assertions for all observed payout/cycle SQL commands. |
| Payout success/failure and bounded tags | Real MeterListener callbacks assert exactly one measurement and exact allowed tag; Activity tags/events/status are inspected. |
| Retries / commit boundary | Synthetic transient query failure proves one span/measurement after retry; commit interceptor failure proves rollback and failure-only measurement. |
| Contribution replay | Two real authorized requests return the same contribution; listener sees one measurement and database contains one record. |
| Authorization refusal tags | Real handler/service paths observe only not_found/forbidden measurements and no payout instrumentation when refused. |
| Health anonymous / trace exclusion | Exact body/status assertions plus recorded-state flags, with a recorded ordinary-request control. |
| Genuine readiness outage | Separate real PostgreSQL container stop/restart, fixed endpoint, actual 503 and recovery; no mock check. Existing Stage 6 timing evidence is preserved. |
| Authentication / authorization preservation | All earlier JWT, rotation, status-code and membership tests passed with original assertions intact. |

An intermediate scope extension broadened the old refusal helper to generated
route identifiers and failed its two cases. That uncommitted helper change was
removed: the original event-state assertions remain exactly as before, while the
new combined scope test checks all explicitly seeded sensitive values. The first
isolation wrapper also exposed Windows PowerShell's terminating treatment of native
test stderr; its test-command block now captures the real exit code and completes
the process before restoration. Intermediate attempts are not final passing proof.

## Development isolation method and exact commands

The Windows PostgreSQL service can serve unrelated databases, so it was left
running. The script reads local development credentials into memory without
printing them, verifies the target is the existing local development database,
and requires connection admission initially enabled and zero existing sessions.

It executes ALLOW_CONNECTIONS=false only for that database, verifies no sessions
appeared, and attempts a real SELECT 1 connection. The expected PostgreSQL
connection-admission rejection is required before tests run. This changes only
connection-admission metadata, not records, schema or migration history. It does
not terminate sessions or stop the service. In finally it restores admission,
verifies SELECT 1 succeeds, and restores environment variables.

Commands actually invoked from the repository root:

```powershell
dotnet build RondiTrack.sln --no-restore -m:1
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/verify-assignment-6-2-isolation.ps1
```

The script's exact test invocation is:

```powershell
dotnet test 'RondiTrack.Tests/RondiTrack.Tests.csproj' --no-build --no-restore -m:1 --logger 'trx;LogFileName=stage7-tests.trx' --results-directory $resultsPath
```

`$resultsPath` resolves to the workspace's obj/stage7-test-results directory.
OTEL_EXPORTER_OTLP_ENDPOINT is cleared before invocation and the factory also
explicitly overrides it to empty; the combined test verifies actual configuration.
The fixture verifies its API connection matches the disposable PostgreSQL target
before applying the existing migrations. Development remains inaccessible until
the test process completes. This proves independence from that database; it does
not claim Windows PostgreSQL was stopped.

## Actual final results

```text
Build succeeded.
    0 Warning(s)
    0 Error(s)
Time Elapsed 00:00:06.91
BUILD_EXIT_CODE=0

Passed! - Failed: 0, Passed: 101, Skipped: 0, Total: 101, Duration: 29 s
TEST_EXIT_CODE=0
```

The sanitized [isolation evidence](assignment-6-2-stage-7-isolation-evidence.txt)
records rejection, test counts and successful restoration. Windows service state
was checked afterward and remained Running. Raw outputs/TRX remain ignored under
obj/stage7-build.txt, obj/stage7-isolation-raw.txt and obj/stage7-test-results.
Only sanitized aggregate lines were copied into the repository.

## Remaining risks / Stage 8

This is evidence for the real exercised traffic and seeded values, not universal
anonymity of every future diagnostic. Framework scopes can contain generated route
IDs, and full unexpected third-party 5xx exceptions can contain sensitive text or
data. Scope capture here is test-only and does not enable production scope export.
The previous POPIA audit's framework-conflict, retention/access and external-log
limitations still apply. Debug-level paths and arbitrary future DTO/scope types
are not exhaustively exercised.

Listeners observe real metrics/activities, but real OTLP delivery and an actual
Aspire waterfall screenshot remain Stage 8 evidence. Sampling, retention and
alerting policies remain operational gaps. The development migration blocker is
unchanged and prominently documented. No known Stage 7 test failures remain.
No push was made.
