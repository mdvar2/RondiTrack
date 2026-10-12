# Assignment 6.2 — Stage 3 verification

Verified on 12 October 2026. Scope: W3C error correlation only, plus the approved
login-test setup adjustment and relevant verification.

## Changed files

| File | Purpose |
|---|---|
| API/Observability/Correlation.cs | Shared request activity lookup and problem extension. |
| API/Program.cs | Customize framework ProblemDetails; correlate/log framework 401/403. |
| API/Handlers/GlobalExceptionHandler.cs | Correlate all handled errors, including login; make all 500 details generic. |
| API/Filters/ValidationFilter.cs | Replace model-binding text that may echo input with a fixed safe detail. |
| RondiTrack.Tests/JwtLoginTests.cs | Set the approved shared traceparent after registration; no assertion changes. |
| RondiTrack.Tests/TestSupport/CorrelationLogProvider.cs | Capture correlation properties and category only. |
| RondiTrack.Tests/TestSupport/PostgresApiFactory.cs | Register that provider alongside existing Console logging. |
| RondiTrack.Tests/CorrelationTests.cs | Fourteen new cases covering propagation, logs, response paths and sanitization. |
| README.md | Explain Stage 3 behaviour and actual verification. |
| This report | Record scope, decisions, verification and remaining work. |

## How one ID reaches the response and log

ASP.NET Core creates the incoming HTTP activity. OpenTelemetry remains registered
without an exporter, so no dashboard is needed. Valid W3C traceparent headers
continue the caller's trace; otherwise the server generates a trace ID.

`Correlation.GetId` prefers `IHttpActivityFeature.Activity`, preserving the
request's trace even inside a child activity, then uses `Activity.Current` where
the feature is unavailable. It requires a W3C activity and returns
`Activity.TraceId.ToString()`: 32 lowercase hexadecimal characters. It never
substitutes the local request identifier or parses headers independently of
ASP.NET Core. Missing/non-W3C activity is an explicit invariant failure, not a
silently fabricated ID.

The global exception handler calls this helper for its structured error log and
`correlationId` response extension, including authentication failures. Its
existing safe business-rule details remain intact. All statuses >=500 use a
fixed generic client detail.

Framework 401/403 responses retain the existing explicit JSON writer, so an
Accept header excluding JSON does not turn an authentication refusal into a
server error. They use the helper for both the extension and structured log.

The ProblemDetails customization callback covers other framework-produced
problems, demonstrated by MVC's unsupported-media-type 415. It adds correlation,
logs the same property and makes 500 details generic. It removes the extra
span-specific `traceId` extension so callers have one correlation field.

Model-binding messages may include submitted strings. They now return only
`The request body is invalid.` Authored validation/business-rule messages keep
their existing behaviour; database exceptions do not expose internal messages.

## Existing login test

The only existing test change adds the same valid traceparent to both compared
login requests, after registration. Both requests belong to one trace and each
can still have its own server span. The byte-for-byte body comparison, status
assertions and content-type assertion are unchanged. No other existing assertion
was edited.

## Actual results

```text
dotnet build RondiTrack.sln --no-restore -m:1
Build succeeded.
    0 Warning(s)
    0 Error(s)
Time Elapsed 00:00:10.75
BUILD_EXIT_CODE=0

dotnet test RondiTrack.Tests/RondiTrack.Tests.csproj --no-build --no-restore -m:1
Passed! - Failed: 0, Passed: 76, Skipped: 0, Total: 76, Duration: 8 s
TEST_EXIT_CODE=0
```

OTLP endpoint was unset. The complete suite used the existing disposable
PostgreSQL 17 fixture; the new direct handler 500 test uses no database. Tests
ran outside the sandbox for Docker pipe access. The earlier intermediate run
passed 71 cases; 76 is the result for the final implementation.

New cases verify anonymous 401 with/without traceparent, framework 403, nonmember
404 for ordinary and platform-admin callers, both login-failure cases,
independent generated IDs, sanitized binding 400, non-JSON Accept 401,
framework 415, application 403, duplicate-registration 409 and sanitized 500.
Each checks the response correlation against a captured structured log property.
The provider deliberately avoids storing messages, exception details or payloads.

Local ignored evidence: `obj/stage3-build.txt`, `obj/stage3-tests.txt` and
`obj/stage3-test-results/stage3-tests.trx`. Raw diagnostic output is not published.

## Boundaries and outstanding work

No authentication/authorization policy, payout, business metric, health probe,
package, migration or development database change was made. The development
migration blocker remains. No development-database-stopped isolation run or
dashboard evidence is claimed here.

Full POPIA logger-level/message cleanup remains Stage 5: the existing handler
still logs non-authentication 4xx exceptions at Error. This stage changes their
correlation and client safety, not the entire logging policy. Correlation-only
capture does not replace the comprehensive Stage 7 log-leak test.

No known Stage 3 test failures remain. No push was made.
