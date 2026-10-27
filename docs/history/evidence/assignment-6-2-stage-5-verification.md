# Assignment 6.2 — Stage 5 verification

Verified on 12 October 2026. Scope: POPIA logging and security-event auditing.

## Changed files

| File | Change |
|---|---|
| API/Auth/AuthService.cs | Safe sign-in, refresh/reuse and logout events; remove identities/session identifiers. |
| API/Auth/StokvelAuthorization.cs | Safe not_found/forbidden security events; decisions and metrics unchanged. |
| API/Handlers/GlobalExceptionHandler.cs | Information for 4xx with type/status/correlation only; full exception at Error for 5xx. |
| API/Observability/SecurityEvents.cs | Current activity trace ID for service security events; null outside an activity rather than inventing an ID. |
| API/Program.cs | Explicitly disable Npgsql parameter logging and row error details on the existing connection target. |
| RondiTrack.Tests/TestSupport/CapturedLogProvider.cs | In-memory formatted message/state/exception capture. |
| RondiTrack.Tests/TestSupport/PostgresApiFactory.cs | Register that provider alongside the existing providers. |
| RondiTrack.Tests/LoggingTests.cs | Twelve focused cases, including database tracing and financial-parameter checks. |
| README.md | Complete 181-row individual-site audit, examples, risks and current response documentation. |
| This report | Actual results and scope/limitations. |

## Every application logger call

The README gives each call its own source location, method and before/after row.
There are 11 current application calls: 3 added, 6 modified, 2 retained. No logger
call was removed without replacement.

| Call | Action / level and reason |
|---|---|
| AuthService.LoginAsync failure | Added Information: expected credential rejection; sign_in/failure/correlation only. |
| AuthService.LoginAsync success | Modified Information: normal security event; sign_in/success/correlation, no UserId. |
| AuthService.RefreshAsync reuse | Modified Warning: possible replay/theft; refresh_reuse/correlation, no FamilyId. |
| AuthService.RefreshAsync success | Modified Information: normal session event; refresh/success/correlation, no UserId/FamilyId. |
| AuthService.LogoutAsync | Modified Information: logout/correlation only. |
| StokvelAuthorization.EnsureAsync missing membership | Added Information: expected refusal; stokvel_refusal/not_found/correlation only. |
| StokvelAuthorization.EnsureAsync insufficient permission | Added Information: expected refusal; stokvel_refusal/forbidden/correlation only. |
| GlobalExceptionHandler.TryHandleAsync 4xx | Modified Information: status/type/correlation; no exception object, message or stack. |
| GlobalExceptionHandler.TryHandleAsync 5xx | Modified Error: status/correlation and full internal exception; generic client detail. |
| Program CustomizeProblemDetails | Retained status/correlation and Information for 4xx, Error for 5xx; no submitted data. |
| Program status-code response callback | Retained Information framework 401/403 with status/correlation; no submitted data. |

Security-event fields contain fixed event/outcome/result values and W3C IDs.
There are no amounts, names, emails, account/member/resource/family IDs, raw/hash
tokens, credentials or payload objects in these application event templates.

No application exception messages were changed. The README audits each fixed
message, every repeated occurrence, dynamic validation aggregation, constructor
forwarding, rethrows, test-only diagnostics, default validator messages and
framework/database sources. Existing safe client details and authorization codes
remain intact; generic 500 detail and correlation remain from Stage 3.

## Framework/database safeguards

No sensitive EF data logging flag or LogTo call appears in application source or
configuration. Development command Information and base Warning levels are
unchanged. Npgsql LogParameters and IncludeErrorDetail are explicitly false even
if configured otherwise; no connection target, credential or retry setting is
changed. Parameterized commands still produce useful SQL spans.

Focused captures check formatted logs, structured state and exception text.
Npgsql ActivityListener checks tags/events for fixture email, password, raw/hash
refresh-token and financial-parameter values. Sign-in checks additionally cover
access tokens, password hashes and generated signing keys. Failure assertions
avoid printing those values or raw log output. Test capture stays in memory and
is not published. Log scopes and external proxies are not exhaustively audited
by this provider; future instrumentation must preserve minimization.

## Actual final results

```text
dotnet build RondiTrack.sln --no-restore -m:1
Build succeeded.
    0 Warning(s)
    0 Error(s)
Time Elapsed 00:00:06.80
BUILD_EXIT_CODE=0

dotnet test RondiTrack.Tests/RondiTrack.Tests.csproj --no-build --no-restore -m:1
Passed! - Failed: 0, Passed: 95, Skipped: 0, Total: 95, Duration: 9 s
TEST_EXIT_CODE=0
```

The suite uses disposable PostgreSQL 17 and its preserved migration chain.
OTLP endpoint was unset. Tests ran outside the sandbox for Docker pipe access;
build used the proven single-node command inside the sandbox. Earlier intermediate
runs passed 91 and 94 cases; 95 is the result for the final code. An initial new-test
build lacked a dependency-injection namespace import; fixed before final build.

Added coverage: wrong-password and successful login, unknown-account login,
refresh replay Warning, nonmember 404, member 403, framework 401/403,
duplicate-registration 409, generic 500 with the same full internal exception and
stack, credential-free database tracing, and financial-parameter suppression.
Expected 4xx checks examine all captured request entries for Error/Critical, not
just entries with the application correlation property.

The existing test assertions were not modified. Authentication, rotation,
revocation, resource decisions, business rules and Stage 2–4 telemetry tests pass.
No migrations, development database records or PostgreSQL service state changed.
No outage/isolation or dashboard evidence is claimed. The development migration
blocker remains prominently documented.

Local ignored artifacts: `obj/stage5-build.txt`, `obj/stage5-tests.txt`,
`obj/stage5-test-results/stage5-tests.trx`. An ignored audit-generation script
helped inventory source sites; coverage was checked against the source searches.

## Remaining risks / next stage

Full third-party 5xx exception text/inner chains/data can still contain sensitive
diagnostics; retaining full exceptions is the assignment's diagnostic tradeoff,
not a guarantee that arbitrary third-party messages are anonymous. Npgsql row
details are off, but EF may independently log failed commands/saves before a
database-originated conflict becomes a 409. Prechecked duplicate registration
has no Error logs; concurrent database-originated conflicts are not proven free
of all framework Error events. No blanket suppression hides those diagnostics.

Current parameterized paths are tested; new literal SQL, custom enrichers or
third-party logging require review. Real sinks still need access controls, secure
shipping, retention and incident-response policies. Historical evidence remains
unchanged and predates current log minimization.

The full combined registration/login/refresh/contribution/duplicate-registration
leak scenario remains Stage 7. Health probes remain Stage 6; screenshots and
actual outage timings remain their assigned evidence stages. No known Stage 5
test failures remain. No push was made.
