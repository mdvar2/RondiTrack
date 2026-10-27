# Assignment 6.2 Stage 8 final verification

Verified on 13 October 2026. Stage 8 changes evidence, documentation and
verification tools only. No application code, migrations, business rules,
authentication/authorization decisions or existing test assertions changed.
The [final compliance matrix](assignment-6-2-final-compliance-matrix.md) maps
every PDF requirement to implementation, tests, documentation and status.

## Actual final build and complete suite

Executed from the repository root:

```powershell
dotnet clean RondiTrack.sln -m:1
dotnet build RondiTrack.sln --no-restore -m:1
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/verify-assignment-6-2-isolation.ps1
```

Actual results:

```text
CLEAN_EXIT_CODE=0
Clean: 0 Warning(s), 0 Error(s), Time Elapsed 00:00:02.28
BUILD_EXIT_CODE=0
Build succeeded. 0 Warning(s), 0 Error(s), Time Elapsed 00:00:21.42
Passed! - Failed: 0, Passed: 101, Skipped: 0, Total: 101, Duration: 30 s
TEST_EXIT_CODE=0
```

The isolation script invokes exactly:

```powershell
dotnet test RondiTrack.Tests/RondiTrack.Tests.csproj --no-build --no-restore -m:1 --logger 'trx;LogFileName=stage7-tests.trx' --results-directory obj/stage7-test-results
```

The `stage7` filenames are intentionally retained by the reusable script; this
was a fresh Stage 8 execution, not copied Stage 7 results. Raw stdout/TRX remain
ignored under `obj`; the [safe aggregate artifact](assignment-6-2-stage-8-isolation-evidence.txt)
was copied only after checking the new exit code and TRX counters.
No tests were added or changed in Stage 8. All existing 101 cases passed.
The earlier `dotnet clean ... --no-restore` invocation failed because clean
does not accept that switch; the corrected command above passed. No code was
changed to work around the command error.

Before tests, the script required zero development sessions, changed only
database connection-admission metadata to `ALLOW_CONNECTIONS=false`, and proved
a real development connection was rejected. Disposable Testcontainers PostgreSQL
remained available. It cleared the OTLP endpoint for the child test process; the
factory independently overrides the endpoint to empty and validates its actual
API database connection before applying migrations to the disposable database.
The evidence dashboard could remain running without being used by tests.
The Windows PostgreSQL service was not stopped. This is the explicitly approved
connection-isolation method, not evidence for a service-stop claim.

The script's `finally` restored admission, verified a real `SELECT 1`, and
restored caller environment variables. A separate authorized fault-injection run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/verify-assignment-6-2-isolation.ps1 -FailAfterIsolation
```

returned the expected exit 1 after the rejection proof, before launching tests;
its `finally` restored connectivity successfully. See the
[restoration evidence](assignment-6-2-stage-8-restoration-evidence.txt).
An ordinary PowerShell exception exercises this guarantee; process termination,
machine shutdown or loss of PostgreSQL administration connectivity cannot be
guaranteed recoverable by an in-process `finally`. No existing sessions were killed.

## Development data preservation

Read-only snapshots before evidence generation and after the final suite used
a repeatable-read transaction with `SET TRANSACTION READ ONLY`. For every public
table, sorted per-row JSON fingerprints were aggregated, and public column names,
types, defaults and nullability were fingerprinted. Migration history is included
as a public table. The final aggregate comparison was:

```text
DEVELOPMENT_PUBLIC_RECORDS_AND_COLUMN_SCHEMA_UNCHANGED=True
```

Hashes and records remain private under ignored `obj/stage8-evidence`; none are
published. This verifies equality of the observed public records and column
definitions, including migration-history rows, across the work. It is not a
backup, proof about other schemas/server metadata, or a forensic cryptographic
guarantee against deliberately crafted MD5 collisions. Admission metadata was
temporarily changed and restored as described above. No development data writes,
schema updates or migrations were performed. The duplicate-email migration
blocker remains unchanged.

## Genuine Aspire export and capture procedure

Use Docker Desktop with Linux containers, .NET10 and the repository's restored
packages. The tested dashboard image was `mcr.microsoft.com/dotnet/aspire-dashboard:13`,
resolved to:

```text
mcr.microsoft.com/dotnet/aspire-dashboard@sha256:613f297a274958abd85e7b71664f51fb42b0ddeb697ebd4c159677774433345b
```

The actual startup command was:

```powershell
docker run --rm -d --name ronditrack-assignment62-dashboard -p 127.0.0.1:18888:18888 -p 127.0.0.1:4317:18889 -e DOTNET_DASHBOARD_UNSECURED_ALLOW_ANONYMOUS=true -e ASPIRE_DASHBOARD_UNSECURED_ALLOW_ANONYMOUS=true mcr.microsoft.com/dotnet/aspire-dashboard:13
```

Both UI and OTLP published ports are loopback-only. UI is
`http://127.0.0.1:18888`; OTLP/gRPC is `http://127.0.0.1:4317` mapped to the
container's 18889. Both anonymous flags were supplied for the tested image. The
dashboard visibly warns that the endpoint is unsecured. **Laptop-only temporary
evidence tool: never expose it to a LAN/public interface or describe this setup
as deployable.** Anonymous local telemetry still needs a trusted laptop and
trusted local processes. The setup follows the
[standalone dashboard documentation](https://aspire.dev/dashboard/standalone/)
and the [official image documentation](https://github.com/dotnet/dotnet-docker/blob/main/README.aspire-dashboard.md);
the recorded image digest pins what was actually tested, rather than assuming
future `latest` behaviour.

The evidence runner was initially executed from an ignored scratch project under
`obj/stage8-host`; the same procedure is now reusable as
`scripts/evidence/EvidenceHost.csproj`, outside the solution. It references the
API and the **already used Testcontainers.PostgreSql 4.15.0**; no new application
dependency was installed. Its relocated project build passed with zero warnings
and errors (5.36 seconds). Its read-only after-snapshot matched the original
fingerprint, and an invalid snapshot label returned a fixed safe failure message
with expected exit 1, without printing an exception or touching the database.
Reproduction:

```powershell
dotnet restore scripts/evidence/EvidenceHost.csproj
dotnet build scripts/evidence/EvidenceHost.csproj --no-restore -m:1
# Optional read-only preservation proof, using existing local user-secrets:
dotnet scripts/evidence/bin/Debug/net10.0/EvidenceHost.dll snapshot before
# Remove a previous stop marker only, if present:
Remove-Item -LiteralPath obj/stage8-evidence/stop -ErrorAction SilentlyContinue
dotnet scripts/evidence/bin/Debug/net10.0/EvidenceHost.dll
```

The runner creates its **own disposable PostgreSQL 17 database**, migrates only
that database with the preserved migration chain, and launches the actual built
API/Kestrel DLL on a reserved loopback port. Child-only environment overrides
provide the disposable connection and random signing key, Development mode,
OTLP endpoint/protocol above, and a one-second metric export interval for evidence
collection. Credentials are generated in memory, never printed or placed in
command arguments. Parent settings are not changed. The development database is
not an evidence API target; snapshots are an explicitly separate read-only mode.

It registers a fresh owner, logs in, creates an owned stokvel and open cycle
through authorized requests, and processes a real payout 201. It registers/logs
in an outsider and verifies a non-member 404 with the caller's trace ID. Traceparent
is removed before each registration and changed before the payout/refusal so the
payout capture contains just the intended request hierarchy. Console lines are
captured in memory; selected safe entries must be nonempty and free of all
generated email/password/name/token/financial fixture values before writing
sanitized excerpts and a safe trace/status manifest. No raw logs are published.
The runner waits for UI capture and has a ten-minute maximum wait, then kills
only its own API child and disposes only its own PostgreSQL container in `finally`.

After `REAL_API_EVIDENCE_READY`, open the trace ID from the private manifest in
the dashboard Traces page, select `payout.process`, expand its SQL children,
and inspect its tags. In Structured Logs select the login event to inspect its
actual EventType/Outcome/CorrelationId properties; in Metrics select
`ronditrack.payouts.processed` and inspect its `outcome=success` series.
The actual capture used installed Python Playwright and Microsoft Edge headless
with an 1800×1200 viewport. No image generation, cropping, redaction, injected DOM
or fabricated waterfall was used. The reusable capture command, which was also
run successfully against the genuine retained dashboard data, is:

```powershell
python scripts/evidence/capture-dashboard.py
```

Playwright is an optional external evidence tool, not an API/test dependency.
The script uses the documented installed Windows Edge path and selectors for
this pinned dashboard; another installation or dashboard version may need a
path/selector adjustment. A human must still inspect the resulting screenshot.

The [genuine screenshot](artifacts/payout-trace.png) was visually inspected:
one HTTP payout request, its business child, nine indented Npgsql command spans
under the business child, and three authentication/authorization SQL spans as
siblings before it. Total 14 spans, depth 3. Authentication database work correctly
precedes the payout service; it is not falsely claimed to occur inside the business
span. The selected business span shows Source=`RondiTrack.Business`,
Name=`payout.process`, Kind=`Internal`, only custom tag `outcome=success`, zero
events, service.name=`ronditrack-api`, and the real trace/parent IDs. Trace context
and SDK resource fields are not custom financial tags. No fixture names, emails,
tokens, amounts or member/stokvel identifiers appear in the captured view.

Actual traces, structured login properties and the payout counter success series
were received in Aspire over OTLP. See the safe
[dashboard verification manifest](artifacts/dashboard-verification.json).
The screenshot's times are an individual development execution, not benchmark
claims. No metric screenshot or failed-payout dashboard screenshot is claimed;
automated listeners verify failure behaviour separately.

After capture, stop the runner from another terminal, compare the after snapshot
if requested, and remove only the owned dashboard:

```powershell
New-Item -ItemType File -Path obj/stage8-evidence/stop -Force | Out-Null
dotnet scripts/evidence/bin/Debug/net10.0/EvidenceHost.dll snapshot after
docker stop ronditrack-assignment62-dashboard
```

The dashboard uses `--rm`; its in-memory telemetry is temporary, with no mounted
volume or persistent private exports. The final run's API and disposable DB were
disposed; `docker stop ronditrack-assignment62-dashboard` succeeded and its
`--rm` container was removed. Refresh the generated safe
manifest/excerpts from `obj` only after reviewing them. The committed screenshot
and manifest describe this actual run, not a newly regenerated run's IDs.

## Genuine sanitized logs

[sanitized-log-excerpts.txt](artifacts/sanitized-log-excerpts.txt) contains original
selected console lines from the real run, not invented templates. The two sign-in
events are owner/outsider successful logins. The refusal and handler 404 share
CorrelationId=`bafaae4972b0b8bf5cecc0a82143153d`, which was also asserted against
the real ProblemDetails response. Their application fields are fixed outcome/type
and W3C correlation, without account/resource identities.

The payout example is an actual EF command captured in the successful payout's
HTTP window, querying the Payouts table with a redacted parameter `?`. It contains
column/table/type/timing metadata, **not a recipient ID or financial value**.
There is no dedicated payout-success ILogger event in this implementation; the
business activity and counter provide the payout outcome. The excerpt alone
does not establish an SQL log correlation property; the actual request window,
trace screenshot and listener tests establish the SQL/business association.
Separate console and OTLP sinks delivering one EF event do not mean the SQL
command is instrumented twice. Historical before/after structural illustrations
in the README remain clearly labelled; no historical before logs were fabricated.

## Security and repository inspection

- Targeted scan of 438 distinct reachable history blobs, including all local
  refs, checked JWT/provider-token/private-key/credential-bearing connection-string
  patterns. Eighteen connection-example blob candidates were reviewed and had
  placeholder password values. No confirmed credential/token finding in those
  classes. This is not a universal secret detector or an external security audit.
- The existing local branch `backup/assignments-before-safe-push-20261011`
  retains two private development duplicate-email cleanup exports. This was
  already recorded in `safe-push-verification.md`; it is a real local privacy
  risk, not a newly introduced file. Both blobs are absent from main and all of
  main's ancestors. No exported rows/identities are reproduced here. The branch
  and any enclosing `.git` archive must remain local; do not publish all refs.
  No history was rewritten and no backup/evidence was removed without approval.
- No tracked bin/obj/DLL/PDB/TRX/database backup or raw diagnostic artifacts.
  Scratch captures/fingerprints live only in ignored obj. The intentional PNG,
  sanitized excerpts and safe manifests were individually reviewed. New tools
  contain synthetic fixtures/random secrets, not development data or credentials.
- No application package changes or unnecessary monitoring libraries in Stage 8.
  Optional runner reuses Testcontainers 4.15.0 outside the solution. Existing
  Npgsql driver/OTel10.0.3 and EF/health10.0.4 match; pipeline versions are recorded
  in Stage2. No Serilog/commercial APM agent/backend was introduced.
- No sensitive EF logging invocation, no LogTo path, forced Npgsql parameter/detail
  flags false. Base EF command Warning / Development Information remain explicit.
  One EF ILogger command source; console and OTLP are separate destinations.
- Custom telemetry fields are only binary outcome/result; contribution untagged.
  No telemetry calls in controllers. Framework URL/scope/resource metadata can
  still be linkable; the tests do not prove all SDK metadata is anonymous.
- Preassignment-to-current diff review found authentication/authorization decisions,
  refresh repository/token/role policies and payout transaction/retry body preserved.
  Changes wrap payout completion/failure and add safe logs/counters; Stage3's
  generic binding-error detail is an approved privacy change. No migration file
  changes. Whitespace-normalized existing Assert statements compared with
  e418e7c found zero missing/changed statements, including byte-for-byte login
  equality. This textual check supplements, rather than replaces, the full suite.
- Admission restoration passed normal and injected-error executions; development
  public data/columns/migration history snapshots matched. Catastrophic termination
  limitations and active-session abort behaviour are explicitly documented.

## Remaining limitations and submission checklist

- [x] Requested genuine trace screenshot, safe tags, login/payout/refusal logs,
  real three-signal OTLP delivery, clean build and full isolated 101-test result.
- [x] Complete matrix, metric cardinality, POPIA audit, health timings and prior
  evidence preserved; current README count is101, earlier counts are labelled.
- [x] Existing development records/migrations protected; connection admission
  restored; no dashboard requirement for automated tests.
- [x] Stage8 is a logical local evidence/documentation/tools commit; no push.
- [ ] Final user approval to publish/submit. Submit URL once approved:
  https://github.com/mdvar2/RondiTrack .
- [ ] Strict literal PDF service-stop evidence: not performed. User-approved
  isolated outage and blocked-development-admission evidence are supplied instead.
- [ ] Repository-wide private-backup disposal/sanitization: not authorized.
  Main-only review excludes those exports, but a blanket all-history privacy
  claim is not warranted. See existing safe-publication guidance.

Sampling, retention, sink access controls and alerting remain operational work:
this local assignment verifies instrumentation and data minimization, not a
production monitoring platform. No explicit sampler override or durable backend,
retention policy, alerts or authenticated remote dashboard was added. Sampling
and export loss affect metric/trace completeness; operational counters are not
a financial ledger. Full unexpected 5xx and independent EF database-error messages
can carry unforeseen sensitive diagnostics, as already detailed in the POPIA audit.
Literal SQL/URLs/proxy/debug logs require continuing review. The existing
duplicate-email migration blocker prevents using the upgraded authentication
schema on the preserved development DB until separately resolved and approved.

**All requested Stage 8 evidence is complete and ready for final review. An
unqualified submission-ready/PDF-complete claim is withheld because of the
explicitly documented literal substitutions and retained private local backup.**
Main-only publication can be reviewed independently; no publication or submission
has occurred. Do not push backup/all refs, rewrite history, resolve the migration
blocker or start new features as part of this handoff.

Local commits ahead of the recorded origin/main before Stage8:
`c76ba54`, `6a89c2f`, `9c2fc9e`, `8f96516`, `01d3176`, `e15d1a4`, `cf4e212`,
`94881e0`; Stage8 adds one logical local commit. This compares the existing
remote-tracking ref, not a fresh live remote fetch. The Stage8 commit is identifiable
by its evidence/documentation title in `git log origin/main..HEAD --oneline`.
Git status at final preparation: branch main; only the twelve paths below are
modified/new for Stage 8. The final handoff verifies the post-commit clean working
tree and ahead count; inspect `git status --short --branch` and
`git log origin/main..HEAD --oneline` for the live state rather than treating
this historical document as a permanent Git-status assertion.

Every changed file:

- README.md
- docs/assignment-6-2-stage-8-verification.md
- docs/assignment-6-2-final-compliance-matrix.md
- docs/assignment-6-2-stage-8-isolation-evidence.txt
- docs/assignment-6-2-stage-8-restoration-evidence.txt
- docs/evidence/payout-trace.png
- docs/evidence/sanitized-log-excerpts.txt
- docs/evidence/dashboard-verification.json
- scripts/verify-assignment-6-2-isolation.ps1
- scripts/evidence/EvidenceHost.csproj
- scripts/evidence/EvidenceRunner.cs
- scripts/evidence/capture-dashboard.py
