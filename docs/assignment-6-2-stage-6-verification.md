# Assignment 6.2 — Stage 6 verification

Verified on 13 October 2026. Scope: liveness/readiness probes and isolated outage
verification. Previous assignment evidence and the full POPIA audit are preserved.

## Changed files

| File | Purpose |
|---|---|
| API/Program.cs | Register EF readiness check and its timeout; map anonymous short-circuit probes. |
| API/Health/HealthProbes.cs | Shared predicates, explicit status mapping and minimal response writer. |
| RondiTrack.Tests/TestSupport/PostgresApiFactory.cs | Separate outage factory with stable port and container-only stop/start helpers. |
| RondiTrack.Tests/HealthProbeTests.cs | Four cases: two healthy probes, HTTP tracing exclusion, isolated outage/recovery. |
| README.md | Probe configuration, actual timings and limitations. |
| docs/assignment-6-2-stage-6-health-timings.json | Sanitized machine-readable measured values. |
| This report | Verification details and scope. |

## Registration and behaviour

`AddDbContextCheck<RondiTrackDbContext>` registers the PostgreSQL connectivity
check with the tag ready. It uses the package's default CanConnectAsync check.
That registration has a 3-second timeout. The existing EF execution strategy
retains maxRetryCount=3 and maxRetryDelay=2 seconds. No dependency, package,
connection target, migration, authentication rule or business rule changed.

Liveness's predicate is always false and runs zero checks. Readiness selects the
ready tag. Healthy maps to 200; Unhealthy and Degraded map to 503. The response
writer emits exactly Healthy or Unhealthy as text/plain, without a newline,
dependency descriptions, exception details or other report fields.

Both endpoints AllowAnonymous and ShortCircuit at routing. This avoids JWT
middleware's database-backed user-existence validation even if a caller supplies
a valid bearer token. Ordinary endpoints keep their existing middleware and
authorization semantics. The existing /health request-tracing filter is unchanged.

## Real outage measurement

The test creates and initializes its own PostgresApiFactory and PostgreSQL 17
container; it never stops the shared collection fixture. Its normal preserved
migration chain is applied only to that disposable database during initialization.
The same API host/client stays running while the container is stopped/restarted.
The test also verifies liveness while the database is stopped with a supplied
valid token. No development PostgreSQL service or records were accessed/changed.

| State / configuration | Response | Elapsed |
|---|---|---:|
| Available, cap enabled | 200 Healthy | 10.0566 ms |
| Unavailable, cap enabled | 503 Unhealthy | 3026.6431 ms |
| Unavailable, liveness | 200 Healthy | 2.2974 ms |
| Unavailable, cap disabled only in isolated test | 503 Unhealthy | 11280.96 ms |
| Restarted, cap restored | 200 Healthy | 17.4975 ms |

The capped failure took about 3 seconds rather than 11.28 seconds uncapped.
Retries remain enabled; cancellation interrupts the aggregate connection/retry
work. The test allows 2 seconds of scheduling/response overhead beyond the
3-second deadline, asserting completion within 5 seconds.

These are actual single Stopwatch measurements of request plus body completion,
not training-guide timings. TestServer handles HTTP in memory; database I/O is
real. Results exclude TLS/external network and container lifecycle time. Capped
measurement preceded the uncapped comparison, so pooling/order effects and retry
jitter may affect comparison. This is not Windows development-database-stopped
evidence and not a performance benchmark.

An initial run passed 98 cases and failed recovery in the new outage test,
returning 503. The outage-only factory now explicitly binds an available ephemeral
host port to avoid possible endpoint changes across restart. The original port
transition was not captured, so the exact cause of that initial recovery failure
is not proven. The shared fixture keeps its original Docker-assigned random port
behaviour. Final recovery succeeds with the fixed mapping.

## Actual final results

```text
dotnet build RondiTrack.sln --no-restore -m:1
Build succeeded.
    0 Warning(s)
    0 Error(s)
Time Elapsed 00:00:06.53
BUILD_EXIT_CODE=0

dotnet test RondiTrack.Tests/RondiTrack.Tests.csproj --no-build --no-restore -m:1
Passed! - Failed: 0, Passed: 99, Skipped: 0, Total: 99, Duration: 29 s
TEST_EXIT_CODE=0
```

Tests run outside the sandbox for Docker pipe access, with OTLP endpoint unset.
The test runner writes the timing artifact only when the dedicated
RONDITRACK_HEALTH_EVIDENCE_DIR environment variable names an output directory.
The committed JSON is the sanitized artifact from this successful run. Raw local
evidence is ignored under obj/stage6-build.txt, obj/stage6-tests.txt and
obj/stage6-test-results. No existing assertion was edited.

Tracing tests inspect both probe HTTP activities, assert IsAllDataRequested and
Recorded are false, and verify an ordinary control request is recorded. Existing
correlation, security, business telemetry and authorization tests all pass.

## Remaining limitations

Timeout cancellation depends on provider cooperation and process scheduling;
the measured response slightly exceeds the registration deadline. Readiness
checks connectivity, not migrations or business correctness. The development
duplicate-email migration blocker remains prominently documented.

Health-check infrastructure can generate internal unhealthy-check diagnostics;
the public writer never copies their descriptions or exception fields. The
Stage 5 library-diagnostic and retention/access risks still apply. No new
application ILogger calls or exception-message sites were introduced. Stage 5
audit source-line links describe that earlier snapshot and are preserved.

Full combined leak/isolation verification remains Stage 7; genuine dashboard
screenshots remain Stage 8. No known Stage 6 failures remain. No push was made.
