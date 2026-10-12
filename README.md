# RondiTrack

RondiTrack is a .NET 10 ASP.NET Core API for users, stokvels, memberships,
contribution cycles, contributions and payouts, with PostgreSQL persistence,
JWT authentication and per-stokvel authorization.

**Development migration blocker:** the existing development database contains
duplicate normalized emails. The unique-email migration cannot be applied until
an approved resolution is completed. Development data is unchanged; the three
Assignment 6.1 migrations have not been applied there. **Do not run a database
update against it yet.** Authentication requires the new schema. See the
[sanitized migration status](docs/development-migration-status.md). Private
cleanup records are excluded from published Git history.

**Final reported suite: 62 tests, all passed, none failed or skipped.** These are
recorded implementation/publication results, not a new test run for this README
edit. Earlier counts are historical; see [verification evidence](#verification-evidence).

## Assignment 6.2 progress: Stages 2–5

The API collects OpenTelemetry traces, metrics and logs with service name
`ronditrack-api`. ASP.NET Core request traces exclude `/health` and its child
paths. Npgsql supplies database-command spans; ASP.NET Core and `System.Runtime`
supply metrics. Health probes remain reserved for Stage 6.

Stage 5 minimizes security-event fields, logs expected application 4xx exceptions
at Information without their messages/objects, and retains full 5xx exceptions
only for internal diagnosis. The [complete individual-site POPIA audit](#assignment-62-stage-5-complete-popia-logging-audit)
contains 181 rows. The final Stage 5 Testcontainers suite passed **95 tests,
0 failed, 0 skipped**; see the [verification report](docs/assignment-6-2-stage-5-verification.md).

Stage 3 adds a shared W3C correlation helper: every existing problem-response
path uses the HTTP request activity's 32-character trace ID, with the same value
in a structured `CorrelationId` error-log property. Valid caller `traceparent`
headers retain their trace ID; requests without one receive an ASP.NET Core
generated ID. Login failures now include correlation IDs. Their existing equality
test supplies one shared caller trace and retains every original assertion.
Model-binding failures use a fixed detail to avoid echoing submitted values;
500 responses remain generic.

The Stage 3 full Testcontainers run passed **76 tests, 0 failed, 0 skipped**
without a dashboard endpoint. See the [Stage 3 verification report](docs/assignment-6-2-stage-3-verification.md).
The 62-test results above remain historical evidence.

### Stage 4: payout tracing and business counters

`RondiTrackTelemetry` owns the singleton ActivitySource **RondiTrack.Business**
and uses `IMeterFactory` to create the Meter **RondiTrack.Business**. Both names
are registered with OpenTelemetry. `PayoutService` starts **payout.process**
before EF's retry strategy and disposes it when the whole operation finishes.
The active HTTP activity is its parent; Npgsql command activities inherit the
business activity as their parent. An ActivityListener integration test checks
the HTTP → payout.process → SQL hierarchy. No Aspire screenshot or visual
dashboard verification is claimed; that evidence remains the final stage.

Every custom payout span tag is listed below. There are no custom span events,
exception messages, amounts, names or resource/person identifiers.

| Span tag | Allowed values / maximum distinct values | Safety | Debugging benefit |
|---|---|---|---|
| outcome | success, failure / 2 | Fixed state only; no identity or financial value. | Separates completed operations from failures and links failures to their SQL timeline. |

A terminal failure sets Activity status to Error without an exception message
or status description. A successful operation emits its measurement only after
the transaction commits and the retry strategy returns. Attempts do not emit
measurements: one eventual success or failure is counted per service invocation.
This does not change existing transaction, rollback or retry behaviour.

| Business counter | Purpose | Tag | Permitted values | Maximum distinct tag values / series |
|---|---|---|---|---|
| ronditrack.contributions.recorded | Newly persisted contributions. | None | None | No tag values / 1 series |
| ronditrack.payouts.processed | Finished payout operations by outcome. | outcome | success, failure | 2 / 2 series |
| ronditrack.authorization.refusals | Refused stokvel operations. | result | not_found, forbidden | 2 / 2 series |

Series limits above apply to custom tags per instrument/resource; separate app
instances and backend resource labels can add series. The complete Stage 4
Testcontainers suite passed **83 tests, 0 failed, 0 skipped**. See the
[Stage 4 verification report](docs/assignment-6-2-stage-4-verification.md).

`ContributionService` increments after the repository successfully persists a
new contribution, before storing its idempotency response. A replay returns
earlier and emits nothing. `StokvelAuthorization` increments immediately before
the existing non-member 404 or insufficient-permission 403 exception. These
metrics describe resource authorization refusals; framework authentication/global
policy refusals are not counted as stokvel refusals. No controller emits telemetry.

These are per-process operational counters, not a financial ledger. A
chairperson asking how many payouts **their stokvel** processed this year needs
an authorized database query filtered by that stokvel and the agreed date range.
The database holds committed records and enforces access. Metrics deliberately
omit stokvel identifiers, reset with process lifetime and can suffer export loss
or use a different retention period; they cannot supply an authoritative scoped count.

OTLP export is registered only when `OTEL_EXPORTER_OTLP_ENDPOINT` is nonblank.
Leave it unset for tests and CI; no Aspire dashboard is required. No endpoint is
hardcoded in application or launch settings.

EF command logging is explicitly **Information in Development** and **Warning
in the base configuration used by Production**. Development emits successful
commands once through `ILogger`, useful for finding repeated queries. Production
suppresses routine command events to reduce noise, storage and exposure while
retaining warning/error diagnostics. There is no `LogTo` SQL output path. Console
and an enabled OTLP destination can each receive the same structured event;
that is separate delivery, not duplicate EF command instrumentation. SQL parameter
values remain disabled.

The health-check package is installed at EF-compatible version 10.0.4, but health
checks are not registered until Stage 6. Existing database records and migrations
are unchanged.

## Architecture and dependencies

Open [RondiTrack.sln](RondiTrack.sln) in VS Code from the repository root.

| Project | Responsibility and dependencies |
|---|---|
| [API](API/RondiTrack.csproj) | Controllers, DTOs, validators, application services, auth, error handling and DI composition; references Domain and Infrastructure. |
| [Domain](Domain/Domain.csproj) | Entities, exceptions, repository interfaces, query records and pagination types; no project or NuGet dependencies. |
| [Infrastructure](Infrastructure/Infrastructure.csproj) | RondiTrackDbContext, EF repositories and migrations; references Domain. |
| [RondiTrack.Tests](RondiTrack.Tests/RondiTrack.Tests.csproj) | xUnit business, regression, auth and database integration tests; references API and Infrastructure. |

All projects target net10.0. EF Core/Relational/Design are pinned to 10.0.4,
Npgsql provider to 10.0.3, JWT bearer/OpenAPI to 10.0.12, FluentValidation to
12.1.1, Scalar to 2.17.7 and Testcontainers.PostgreSql to 4.15.0. Linked project
files are authoritative.

Contribution flow: `StokvelsController.RecordContribution` ->
`StokvelAuthorization.EnsureAsync` -> `StokvelAccessHandler.HandleRequirementAsync`
(live membership lookup) -> `ContributionService.RecordContributionAsync` ->
`ContributionRepository.AddAsync` -> `RondiTrackDbContext.SaveChangesAsync` ->
PostgreSQL. Controllers also call repositories directly for simple CRUD/queries.
`PayoutService.CreatePayoutAsync` intentionally retains direct DbContext access
and its existing explicit transaction rather than adding a payout repository.
Infrastructure projects to Domain query records; API maps these to response DTOs.

### Dependency injection

Registrations are in [API/Program.cs](API/Program.cs).

| Registration | Lifetime | Reason |
|---|---|---|
| RondiTrackDbContext | Scoped | Request unit of work. |
| IUserRepository -> UserRepository | Scoped | Uses scoped DbContext. |
| IStokvelRepository -> StokvelRepository | Scoped | Uses scoped DbContext. |
| IContributionRepository -> ContributionRepository | Scoped | Uses scoped DbContext. |
| IContributionCycleRepository -> EfContributionCycleRepository | Scoped | Uses scoped DbContext. |
| IStokvelMemberRepository -> EfStokvelMemberRepository | Scoped | Uses scoped DbContext. |
| IRefreshTokenRepository -> EfRefreshTokenRepository | Scoped | Transactional family locking. |
| MembershipService, ContributionService, PayoutService, AuthService | Scoped | Scoped repository/DbContext dependencies. |
| `IPasswordHasher<User>` -> `PasswordHasher<User>` | Scoped | Authentication hashing/verification. |
| StokvelAuthorization, IAuthorizationHandler -> StokvelAccessHandler | Scoped | Live resource authorization. |
| IClaimsTransformation -> PlatformRoleClaimsTransformation | Scoped | Current platform role from PostgreSQL. |
| FluentValidation validators | Scoped | Assembly registration. |
| JwtOptions, TokenService | Singleton | Configuration/signing without scoped dependencies. |
| IIdempotencyStore -> IdempotencyStore | Singleton | Process-local state without DbContext. |

The unregistered in-memory ContributionCycleRepository remains for service
tests; production DI uses EfContributionCycleRepository.

## Windows setup

Run commands from the repository root in PowerShell. **Development uses
PostgreSQL 17 as a Windows service. Integration tests use Docker Desktop with
Linux containers and disposable PostgreSQL 17 through Testcontainers.** Docker
is not the development database.

Prerequisites: Git, .NET 10 SDK, PostgreSQL 17/psql for development, and a working
Docker engine for integration tests. EF CLI is needed for migration commands.

```powershell
git clone https://github.com/mdvar2/RondiTrack.git
cd RondiTrack
dotnet --version
dotnet restore RondiTrack.sln
dotnet build RondiTrack.sln -m:1
```

If already cloned, use the existing checkout. Building does not upgrade a database
or require a running PostgreSQL service.

### Development PostgreSQL

Check the Windows service and connect interactively without printing credentials:

```powershell
Get-Service -Name 'postgresql*'
& 'C:\Program Files\PostgreSQL\17\bin\psql.exe' -h localhost -U postgres -W -d postgres
```

Adjust the executable path if installed elsewhere. The local service used during
verification was postgresql-x64-17. For a **new, empty environment only**, create
ronditrack in psql:

```sql
CREATE DATABASE ronditrack;
```

Do not recreate, reset or delete the existing development database to bypass
the migration blocker. No private cleanup SQL or account exports are published.

### User Secrets and JWT configuration

[API/RondiTrack.csproj](API/RondiTrack.csproj) already defines UserSecretsId; do
not run user-secrets init or replace it. Development needs both
`ConnectionStrings:RondiTrackDb` and `Jwt:SigningKey` in API User Secrets.
Preserve existing configured secrets. User Secrets are local development storage,
not encrypted production secret storage.

On a new machine, enter a connection string with the shape
`Host=localhost;Port=5432;Database=ronditrack;Username=postgres;Password=<LOCAL_DB_PASSWORD>`.
This sends JSON through stdin without placing the entered value in shell history:

```powershell
$connectionInput = Read-Host 'Development connection string' -AsSecureString
$connectionValue = [System.Net.NetworkCredential]::new('', $connectionInput).Password
@{ 'ConnectionStrings:RondiTrackDb' = $connectionValue } | ConvertTo-Json -Compress | dotnet user-secrets --project API set
Remove-Variable connectionInput, connectionValue
```

For a **new signing key only**, generate 32 cryptographically random bytes and
store their base64 representation without printing it:

```powershell
$jwtBytes = New-Object byte[] 32
$jwtGenerator = [System.Security.Cryptography.RandomNumberGenerator]::Create()
$jwtGenerator.GetBytes($jwtBytes)
$jwtGenerator.Dispose()
@{ 'Jwt:SigningKey' = [Convert]::ToBase64String($jwtBytes) } | ConvertTo-Json -Compress | dotnet user-secrets --project API set
[Array]::Clear($jwtBytes, 0, $jwtBytes.Length)
Remove-Variable jwtBytes, jwtGenerator
```

Do not overwrite an existing key during routine setup: rotation invalidates
issued access tokens. Never publish secret files, connection strings, passwords,
token values or database backups, or capture them in logs/screenshots.

[API/appsettings.json](API/appsettings.json) contains public JWT settings:

| Setting | Current value |
|---|---|
| Jwt:Issuer | https://api.ronditrack.local |
| Jwt:Audience | ronditrack-web |
| Jwt:AccessTokenMinutes | 5 |
| Jwt:ClockSkewSeconds | 30 |
| Jwt:RefreshTokenDays | 7 |

Issuer is a validation identifier, not a site to browse. JwtOptions.Load fails
startup helpfully for missing/malformed keys or fewer than 32 decoded bytes.
Configuration enforces access lifetime 1-15 minutes, skew 0-60 seconds and
refresh lifetime 1-30 days; the table shows actual configured defaults.

### Migrations: inspect first, apply only when safe

**The existing development upgrade remains blocked by duplicate normalized
emails. Do not run the update command there until an approved resolution is
complete.** A new empty database can apply the full chain. Tests migrate their
own disposable container independently of the development blocker.

Check dotnet ef --version. If the CLI is absent, install an EF 10 tool matching
the pinned EF packages:

```powershell
dotnet tool install --global dotnet-ef --version 10.0.4
```

Infrastructure is the migration project; API is the startup project:

```powershell
dotnet ef migrations list --project Infrastructure --startup-project API --no-connect
dotnet ef migrations has-pending-model-changes --project Infrastructure --startup-project API
```

The --no-connect listing does not establish database applied/pending status.
Omit it to query the configured database history without applying migrations.
The model check compares the model with its snapshot, not the database's
migration status. Configure API secrets before using these startup-project tools.

There are **seven migration IDs** in
[Infrastructure/Migrations](Infrastructure/Migrations):

| Migration | Purpose |
|---|---|
| 20261001040953_InitialCreate | Initial domain schema. |
| 20261004202213_ConfigureRelationshipsAndQueryModel | Relationships and composite membership/query model. |
| 20261005211608_AddDatabaseDefenseConstraints | Database uniqueness guards. |
| 20261006014557_AddContributionPaginationIndex | Contribution query index. |
| 20261011174110_AddUserAuthentication | PasswordHash, computed NormalizedEmail and unique index. |
| 20261011174943_AddRefreshTokenSessions | Refresh tokens and login families. |
| 20261011175640_AddPlatformRole | Ordinary platform-role default. |

Development was last verified with the **first four** applied; the final three
remain unapplied. All seven applied successfully in disposable test databases.
Original migration IDs are preserved.

**Only for a new empty database or after an approved resolution of the blocker:**

```powershell
dotnet ef database update --project Infrastructure --startup-project API
```

Program.cs does not auto-migrate on startup. Running the API does not upgrade
the schema; creating new migrations is not part of routine setup.

### Run over HTTPS

After secrets and an appropriate database schema are ready:

```powershell
dotnet dev-certs https --trust
dotnet run --project API --launch-profile https
```

[Launch settings](API/Properties/launchSettings.json) configure
https://localhost:7125 and http://localhost:5101 in Development. **Use HTTPS**:
the refresh cookie is Secure. The http-only launch profile is insufficient for
the full session flow. HTTPS redirection is enabled.

Development documentation is anonymously accessible at
`https://localhost:7125/scalar/v1` and
`https://localhost:7125/openapi/v1.json`; these are only mapped in Development.
Their availability does not establish that the database schema is current.

## Authentication and session flow

[AuthController](API/Controllers/AuthController.cs) explicitly allows anonymous
access to these routes. Bodies use JSON except refresh/logout, which read cookies.

| Method and path | Input | Result |
|---|---|---|
| POST /api/auth/register | name, email, password (minimum 12 characters) | 201 UserResponse; invalid input 400; duplicate normalized email 409. |
| POST /api/auth/login | email, password | 200 accessToken/expiresAtUtc plus refresh cookie; unknown email/wrong password same safe 401 details, with request-specific correlation; invalid input 400. |
| POST /api/auth/refresh | Refresh cookie | 200 new access JWT and rotated cookie; missing, expired, revoked or replayed token 401. |
| POST /api/auth/logout | Refresh cookie if present | 204, family revoked and cookie deleted; repeated/cookie-free logout idempotent. |

Infrastructure outages can still fail requests. Registration returns a Location
under /api/users/{id}, but that resource is platform-admin restricted; ordinary
registrants cannot GET it.

Registration extends the existing User table, hashes with `PasswordHasher<User>`
and never returns the hash. Email lookup trims/lowers invariantly; PostgreSQL
computes `lower(btrim("Email"))` with a unique index, also protecting concurrent
registrations/direct inserts. Legacy users with null PasswordHash cannot sign in.
No public password-reset/setup or privileged-role assignment flow is implemented.
Registration does not accept a privileged role. Admin /api/users creation does
not set a password and is not a substitute for registration.

### Access JWTs

Send `Authorization: Bearer <ACCESS_TOKEN>` on protected requests.
[TokenService](API/Auth/TokenService.cs) signs with HS256. Validation requires
signature, allowed algorithm, issuer, audience and expiration. OnTokenValidated
rejects deleted users. Default validity is five minutes plus explicit 30-second
acceptance skew; configuration caps nominal lifetime at 15 minutes.

| Claim | Meaning and lifetime rationale |
|---|---|
| sub | Stable User.Id; current existence checked each request. |
| jti | Immutable token identifier, not a session revocation lookup. |
| iat | Fixed issuance timestamp. |
| nbf / exp | Fixed validity boundaries checked with explicit skew. |
| iss / aud | Fixed issuer and intended client identifiers. |

No email, stokvel-role or platform-role claims are issued in the JWT.
PlatformRoleClaimsTransformation adds the current platform role from PostgreSQL
to the request principal. Stokvel roles are separately queried per resource,
so demotion/removal affects the same JWT without waiting for its expiry.

### Refresh tokens, cookie flags and logout

Refresh tokens are 64 random bytes, not JWTs. Only SHA-256 hashes are stored in
PostgreSQL. Raw tokens appear exclusively in the __Secure-ronditrack_refresh
cookie, never JSON. Defaults: seven days per token, fixed 30-day absolute family
limit. Successor database expiry is capped by that limit; cookie expiry can be
later, but the server rejects the token at the boundary.

| Cookie option | Choice and purpose |
|---|---|
| HttpOnly | true; prevents JavaScript reads. |
| Secure | true; HTTPS only. |
| SameSite | Strict; limits cross-site delivery. |
| Path | /api/auth; limits routing. |
| Domain | Unset; host-only. |
| Expires | Set from the returned refresh expiry. |

[EfRefreshTokenRepository.RotateAsync](Infrastructure/Repositories/EfRefreshTokenRepository.cs)
locks the family row with PostgreSQL FOR UPDATE, spends the old token and inserts
the successor atomically. Logout uses the same lock. Reusing a spent token returns
401 and commits whole-family revocation, invalidating its successor. Concurrent
refreshes allow at most one success; replay then revokes that family. Another
login family is unaffected.

Logout deletes the cookie with matching options and revokes the family. Issued
access JWTs remain usable until short expiry plus skew; refresh revocation does
not itself revoke them. Live authorization still applies. Keep access tokens in
frontend memory; localStorage exposes tokens to XSS and would lose the refresh
token's HttpOnly protection. Strict cookies assume a same-site frontend; cross-site
deployment needs an explicit CSRF/CORS design.

## Authorization and endpoints

Authentication precedes authorization. Fallback requires authentication for
otherwise unannotated routes. [Roles](Domain/Entities/Roles.cs) centralizes names;
[Policies](API/Auth/Policies.cs) defines the rules. Exactly one plain role attribute
protects the global stokvel list. User CRUD uses PlatformAdministration; scoped
operations use resource policies.

[StokvelAccessHandler](API/Auth/StokvelAccessHandler.cs) queries current membership
for the requested stokvel. Authenticated nonmembers receive **404**, concealing
the resource; existing members without elevated permission receive **403**.
Treasurer/Chairperson can manage the resource; Member can view and record their
own contribution. PlatformAdmin has no scoped membership bypass. New stokvels
atomically give their creator Chairperson membership. Added members default to
Member; no public role-change API exists.

Success codes assume valid input and satisfied business rules. All protected
routes return 401 without a valid bearer token. Global list/user CRUD return
403 to authenticated callers lacking PlatformAdmin. Scoped routes return 404 to
nonmembers, including nonmember admins. Ordinary members receive 403 for elevated
operations or recording another member's contribution.

| Method | Endpoint | Required permission | Success |
|---|---|---|---:|
| GET | /api/users | PlatformAdmin | 200 |
| GET | /api/users/{id} | PlatformAdmin | 200 |
| POST | /api/users | PlatformAdmin | 201 |
| PUT | /api/users/{id} | PlatformAdmin + If-Match | 204 |
| DELETE | /api/users/{id} | PlatformAdmin | 204 |
| GET | /api/stokvels | PlatformAdmin (plain role attribute) | 200 |
| POST | /api/stokvels | Any authenticated user | 201 |
| GET | /api/stokvels/{id} | Any member of this stokvel | 200 |
| PUT | /api/stokvels/{id} | Treasurer/Chairperson + If-Match | 204 |
| DELETE | /api/stokvels/{id} | Treasurer/Chairperson | 204 |
| GET | /api/stokvels/{stokvelId}/members | Any member | 200 |
| POST | /api/stokvels/{stokvelId}/members/{userId} | Treasurer/Chairperson | 204 |
| DELETE | /api/stokvels/{stokvelId}/members/{userId} | Treasurer/Chairperson | 204 |
| GET | /api/stokvels/{stokvelId}/cycles | Any member | 200 |
| GET | /api/stokvels/{stokvelId}/cycles/{cycleId} | Any member | 200 |
| GET | /api/stokvels/{stokvelId}/cycles/{cycleId}/contributions | Any member | 200 |
| POST | /api/stokvels/{stokvelId}/cycles | Treasurer/Chairperson | 201 |
| PUT | /api/stokvels/{stokvelId}/cycles/{cycleId} | Treasurer/Chairperson + If-Match | 204 |
| DELETE | /api/stokvels/{stokvelId}/cycles/{cycleId} | Treasurer/Chairperson | 204 |
| POST | /api/stokvels/{stokvelId}/members/{userId}/contributions | Own userId as Member, or Treasurer/Chairperson; Idempotency-Key | 201 |
| POST | /api/stokvels/{stokvelId}/cycles/{cycleId}/payouts | Treasurer/Chairperson | 201 |

There is no standalone contribution CRUD/delete route, payout-list route or
role-management route. Auth and development documentation routes are listed above.

### Existing business contracts

- Invalid DTOs yield 400; errors use application/problem+json. Common domain
  outcomes are 404 not found, 409 business/uniqueness conflict and 412 stale
  If-Match. Every problem includes a W3C correlationId, including credential
  failures. Identical failure bodies are verified when both requests share the
  same caller trace; the safe authentication detail is identical for all callers.
- GET-by-ID users/stokvels/cycles returns ETag using PostgreSQL xmin row versions.
  PUT requires If-Match: missing/invalid is 400, stale is 412, database concurrency
  race is 409. DELETE does not require If-Match.
- Contributions take amount/contributionCycleId and require Idempotency-Key.
  Same key/request returns the original contribution with 201; same key/different
  request or duplicate member/cycle contribution is 409. Idempotency is
  process-local and lost on restart.
- Membership addition has no body. Cycles take period/targetAmount; payouts take
  amount. Payout processing selects the next eligible member, persists the payout
  and marks the cycle PaidOut in a transaction.
- GET members and GET cycle contributions return PagedResult: items,
  nextPageToken, pageSize. Default size 25, maximum 100. Sorting/filtering/cursors
  are validated and applied in EF repositories.
- Composite membership keys, restricted contribution/payout foreign keys and
  unique contribution/member/cycle and payout/stokvel/cycle indexes protect
  data. Npgsql retries are enabled; explicit transactions use EF's execution strategy.

## Tests and recorded verification

Start Docker Desktop in Linux-container mode. A working engine and access to
postgres:17 are required. Development PostgreSQL/secrets are not prerequisites
for the ordinary suite. [PostgresApiFactory](RondiTrack.Tests/TestSupport/PostgresApiFactory.cs)
provides runtime-generated container credentials and a separate signing key,
overrides the exact early-read setting ConnectionStrings:RondiTrackDb, applies
all seven migrations, verifies the real API connection and disposes the host/container.

```powershell
docker info
dotnet build RondiTrack.sln -m:1
dotnet test RondiTrack.Tests/RondiTrack.Tests.csproj --no-build --no-restore --logger "console;verbosity=minimal"
```

If a per-user Docker install is not on PATH, use its installed docker.exe path
or reopen the terminal after correcting PATH. Do not redirect tests to the
development database to bypass Docker errors.

### Verification evidence

| Recorded stage | Outcome | Evidence |
|---|---|---|
| Assignment 5.4 completion (historical) | Original 25 passed with development unavailable; four migrations at that stage. | [5.4 report](docs/assignment-5-4-verification-report.md), [isolation log](docs/assignment-5-4-isolation-evidence.txt) |
| Assignment 6.1 final implementation | 62 passed, 0 failed/skipped with development unavailable; seven migrations in disposable PostgreSQL; recorded total time 19.7887 seconds. | [6.1 report](docs/assignment-6-1-verification-report.md), [isolation log](docs/assignment-6-1-isolation-evidence.txt) |
| Safe-publication rerun | Build: 0 warnings/errors; 62 passed, 0 failed/skipped; recorded test duration 7 seconds. | [Publication verification](docs/safe-push-verification.md) |

Final suite: 25 original cases plus 37 additional cases. Original assertions were
preserved. Intermediate counts in assignment phase records are historical, not
the final total. This README review checks documentation, command syntax,
migration listing and test discovery; it does not claim a new integration run
or a development schema upgrade.

Documentation verification on 11 October 2026 matched all 25 controller
method/path/success-code combinations against source, checked all 43 Markdown
links across this guide and the archive, and parsed all 11 current PowerShell
blocks without executing setup mutations. Read-only CLI checks listed seven
migrations, reported no pending model changes, and discovered 62 test cases:

```powershell
dotnet test RondiTrack.Tests/RondiTrack.Tests.csproj --no-build --no-restore --list-tests
```

Discovery is not execution or proof of passing tests. Historical passes above
remain the reported integration results.

The isolation proof temporarily refused connections to the Windows development
database without stopping unrelated databases, then restored access in finally.
To reproduce separately, close development clients and use the existing secret:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/verify-assignment-5-4-isolation.ps1
```

This optional script temporarily changes development connection availability;
it is not ordinary test setup or a read-only documentation check. It refuses
remote/differently named databases, never kills sessions, and restores connections.
The execution-policy override applies only to the child process. It does not
apply development migrations or resolve duplicate emails.

## Historical assignments and remaining scope

Earlier explanations, query experiments and results are preserved in
[README-history.md](README-history.md), with outdated setup explicitly marked
as historical. Use this README for current instructions.

- [Assignment 5.4 completion report](docs/assignment-5-4-verification-report.md)
- [Assignment 5.4 initial audit](docs/assignment-5-4-phase-1-audit.md)
- [Assignment 5.4 phase record](docs/assignment-5-4-phase-verification.md)
- [Assignment 6.1 verification report](docs/assignment-6-1-verification-report.md)
- [Assignment 6.1 phase record](docs/assignment-6-1-phase-verification.md)
- [Assignment 6.1 file manifest](docs/assignment-6-1-file-manifest.txt)
- [Assignment 5.3 performance evidence](docs/assignment-5-3-performance-evidence.md)
- [Assignment 5.3 query-plan script](scripts/assignment-5-3-performance.sql)

Remaining limits: duplicate emails block development upgrade; legacy passwordless
accounts need an approved setup workflow; idempotency is in-memory; legacy
Stokvel.Members is ignored by EF; payout rotation/rollback behavior retains its
documented scope. No frontend, public password-reset flow or public role-assignment
API is provided. Private cleanup documents/backups remain excluded from publication.

## Assignment 6.2 Stage 5: complete POPIA logging audit

This inventory lists individual source sites, including repeated messages. File/line
references describe the Stage 5 source snapshot. Test-only synthetic exceptions,
exception wrappers, validator defaults and the existing isolation script are included;
the script was not run. No logger/exception sites exist in migrations. No separate
user service exists: user rules live in controllers, repositories and the User entity.
ContributionService, PayoutService and MembershipService have no ILogger calls;
their individual exception sites are listed below. StokvelAccessHandler has no log
or exception-message call site; the refusal helper owns the security event.

Information is used for normal sign-in/session events, failed credentials and
expected resource refusals. Warning is used for refresh reuse because it may signal
token theft. Error means an unexpected server failure and retains the full exception
for internal diagnosis. 4xx events retain type, status and correlation only.

The Npgsql defaults and explicit diagnostic flags are described in the
[official connection parameter documentation](https://www.npgsql.org/doc/connection-string-parameters.html).
The code forces parameter logging and PostgreSQL error detail off. Npgsql command
spans remain enabled, following its [tracing documentation](https://www.npgsql.org/doc/diagnostics/tracing.html).
.NET 10's [handled-exception diagnostics policy](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/error-handling?view=aspnetcore-10.0)
suppresses framework diagnostics for exceptions handled by IExceptionHandler by
default. Focused tests check actual captured output rather than assuming that
framework defaults alone prove privacy.

| Individual file/site and method | Event or exception type | Potential contents | Personal/financial risk | Before Stage 5 | After / minimization justification |
|---|---|---|---|---|---|
| [API/Auth/AuthService.cs:27](API/Auth/AuthService.cs#L27) / LoginAsync | AuthenticationFailedException | "Email or password is incorrect." | No submitted values in literal. | Literal or generated diagnostic; auth 401 was Information; unexpected 500 was Error. | Message unchanged. 4xx: Information, type/status/correlation only, no exception; 5xx: full exception internally, generic client detail. Fixed authored client details preserved. |
| [API/Auth/AuthService.cs:79](API/Auth/AuthService.cs#L79) / RegisterAsync | BusinessRuleException | "An account with this email already exists." | No submitted values in literal. | Expected domain/validation 4xx was logged with full exception at Error (except authentication). | Message unchanged. 4xx: Information, type/status/correlation only, no exception; 5xx: full exception internally, generic client detail. Fixed authored client details preserved. |
| [API/Auth/AuthService.cs:74](API/Auth/AuthService.cs#L74) / InvalidRefresh | AuthenticationFailedException (target-typed) | "Refresh session is invalid. Please sign in again." | Fixed text; no raw/hash/session value. | Information authentication rejection without type/status. | Message unchanged; handler now includes type/status/correlation only. |
| [API/Auth/AuthService.cs:25](API/Auth/AuthService.cs#L25) / LoginAsync | ILogger logger.LogInformation | "Security event {EventType} with {Outcome}. CorrelationId: {CorrelationId}", "sign_in", "failure", SecurityEvents.CorrelationId | Fixed event/state/type/status plus W3C ID; operational correlation is linkable and needs retention/access controls. | No service-owned failed-sign-in event. | Information sign_in/failure + correlation; no typed email or account identity. Expected credential rejection. |
| [API/Auth/AuthService.cs:38](API/Auth/AuthService.cs#L38) / LoginAsync | ILogger logger.LogInformation | "Security event {EventType} with {Outcome}. CorrelationId: {CorrelationId}", "sign_in", "success", SecurityEvents.CorrelationId | Fixed event/state/type/status plus W3C ID; operational correlation is linkable and needs retention/access controls. | Information: Login succeeded. UserId: [identifier]. | Information sign_in/success + correlation; no identity. Normal security event. |
| [API/Auth/AuthService.cs:53](API/Auth/AuthService.cs#L53) / RefreshAsync | ILogger logger.LogWarning | "Security event {EventType}; session revoked. CorrelationId: {CorrelationId}", "refresh_reuse", SecurityEvents.CorrelationId | Fixed event/state/type/status plus W3C ID; operational correlation is linkable and needs retention/access controls. | Warning reuse with FamilyId. | Warning refresh_reuse + correlation; session identifiers omitted. Reuse warrants investigation. |
| [API/Auth/AuthService.cs:59](API/Auth/AuthService.cs#L59) / RefreshAsync | ILogger logger.LogInformation | "Security event {EventType} with {Outcome}. CorrelationId: {CorrelationId}", "refresh", "success", SecurityEvents.CorrelationId | Fixed event/state/type/status plus W3C ID; operational correlation is linkable and needs retention/access controls. | Information refresh success with UserId and FamilyId. | Information refresh/success + correlation; omit account and family identifiers. Normal session event. |
| [API/Auth/AuthService.cs:68](API/Auth/AuthService.cs#L68) / LogoutAsync | ILogger logger.LogInformation | "Security event {EventType} completed. CorrelationId: {CorrelationId}", "logout", SecurityEvents.CorrelationId | Fixed event/state/type/status plus W3C ID; operational correlation is linkable and needs retention/access controls. | Information: Logout completed. | Information logout event + correlation only; no token or identity. |
| [API/Auth/JwtOptions.cs:13](API/Auth/JwtOptions.cs#L13) / Load | InvalidOperationException | guidance = "Configure Jwt:SigningKey in User Secrets as a base64-encoded random key of at least 32 bytes: dotnet user-secrets --project API set Jwt:SigningKey &lt;BASE64_KEY&gt;." | Constant configuration guidance and placeholder; never the signing key. | Literal or generated diagnostic; auth 401 was Information; unexpected 500 was Error. | Message unchanged. 4xx: Information, type/status/correlation only, no exception; 5xx: full exception internally, generic client detail. Fixed authored client details preserved. |
| [API/Auth/JwtOptions.cs:14](API/Auth/JwtOptions.cs#L14) / Load | InvalidOperationException | guidance = "Configure Jwt:SigningKey in User Secrets as a base64-encoded random key of at least 32 bytes: dotnet user-secrets --project API set Jwt:SigningKey &lt;BASE64_KEY&gt;." | Constant configuration guidance and placeholder; never the signing key. | Literal or generated diagnostic; auth 401 was Information; unexpected 500 was Error. | Message unchanged. 4xx: Information, type/status/correlation only, no exception; 5xx: full exception internally, generic client detail. Fixed authored client details preserved. |
| [API/Auth/JwtOptions.cs:18](API/Auth/JwtOptions.cs#L18) / Load | InvalidOperationException | "Configure the non-secret Jwt:Issuer and Jwt:Audience settings." | No submitted values in literal. | Literal or generated diagnostic; auth 401 was Information; unexpected 500 was Error. | Message unchanged. 4xx: Information, type/status/correlation only, no exception; 5xx: full exception internally, generic client detail. Fixed authored client details preserved. |
| [API/Auth/JwtOptions.cs:23](API/Auth/JwtOptions.cs#L23) / Load | InvalidOperationException | "JWT settings require access lifetime 1?15 minutes, ClockSkew 0?60 seconds and refresh lifetime 1?30 days." | No submitted values in literal. | Literal or generated diagnostic; auth 401 was Information; unexpected 500 was Error. | Message unchanged. 4xx: Information, type/status/correlation only, no exception; 5xx: full exception internally, generic client detail. Fixed authored client details preserved. |
| [API/Auth/StokvelAuthorization.cs:21](API/Auth/StokvelAuthorization.cs#L21) / EnsureAsync | NotFoundException | "Stokvel not found." | No submitted values in literal. | Expected domain/validation 4xx was logged with full exception at Error (except authentication). | Message unchanged. 4xx: Information, type/status/correlation only, no exception; 5xx: full exception internally, generic client detail. Fixed authored client details preserved. |
| [API/Auth/StokvelAuthorization.cs:26](API/Auth/StokvelAuthorization.cs#L26) / EnsureAsync | ForbiddenException | "You do not have permission for this operation." | No submitted values in literal. | Expected domain/validation 4xx was logged with full exception at Error (except authentication). | Message unchanged. 4xx: Information, type/status/correlation only, no exception; 5xx: full exception internally, generic client detail. Fixed authored client details preserved. |
| [API/Auth/StokvelAuthorization.cs:31](API/Auth/StokvelAuthorization.cs#L31) / UserId | AuthenticationFailedException | "Authentication is required." | No submitted values in literal. | Literal or generated diagnostic; auth 401 was Information; unexpected 500 was Error. | Message unchanged. 4xx: Information, type/status/correlation only, no exception; 5xx: full exception internally, generic client detail. Fixed authored client details preserved. |
| [API/Auth/StokvelAuthorization.cs:19](API/Auth/StokvelAuthorization.cs#L19) / EnsureAsync | ILogger logger.LogInformation | "Security event {EventType} with {Result}. CorrelationId: {CorrelationId}", "stokvel_refusal", "not_found", SecurityEvents.CorrelationId | Fixed event/state/type/status plus W3C ID; operational correlation is linkable and needs retention/access controls. | No service-owned refusal log; exception handler produced misleading Error. | Information stokvel_refusal + fixed result + correlation; expected access denial, no caller/resource identifiers. |
| [API/Auth/StokvelAuthorization.cs:24](API/Auth/StokvelAuthorization.cs#L24) / EnsureAsync | ILogger logger.LogInformation | "Security event {EventType} with {Result}. CorrelationId: {CorrelationId}", "stokvel_refusal", "forbidden", SecurityEvents.CorrelationId | Fixed event/state/type/status plus W3C ID; operational correlation is linkable and needs retention/access controls. | No service-owned refusal log; exception handler produced misleading Error. | Information stokvel_refusal + fixed result + correlation; expected access denial, no caller/resource identifiers. |
| [API/Controllers/ContributionCyclesController.cs:60](API/Controllers/ContributionCyclesController.cs#L60) / GetAll | NotFoundException | "Stokvel not found." | No submitted values in literal. | Expected domain/validation 4xx was logged with full exception at Error (except authentication). | Message unchanged. 4xx: Information, type/status/correlation only, no exception; 5xx: full exception internally, generic client detail. Fixed authored client details preserved. |
| [API/Controllers/ContributionCyclesController.cs:97](API/Controllers/ContributionCyclesController.cs#L97) / GetById | NotFoundException | "Stokvel not found." | No submitted values in literal. | Expected domain/validation 4xx was logged with full exception at Error (except authentication). | Message unchanged. 4xx: Information, type/status/correlation only, no exception; 5xx: full exception internally, generic client detail. Fixed authored client details preserved. |
| [API/Controllers/ContributionCyclesController.cs:103](API/Controllers/ContributionCyclesController.cs#L103) / GetById | NotFoundException | "Contribution cycle not found." | No submitted values in literal. | Expected domain/validation 4xx was logged with full exception at Error (except authentication). | Message unchanged. 4xx: Information, type/status/correlation only, no exception; 5xx: full exception internally, generic client detail. Fixed authored client details preserved. |
| [API/Controllers/ContributionCyclesController.cs:146](API/Controllers/ContributionCyclesController.cs#L146) / GetContributions | NotFoundException | "Stokvel not found." | No submitted values in literal. | Expected domain/validation 4xx was logged with full exception at Error (except authentication). | Message unchanged. 4xx: Information, type/status/correlation only, no exception; 5xx: full exception internally, generic client detail. Fixed authored client details preserved. |
| [API/Controllers/ContributionCyclesController.cs:152](API/Controllers/ContributionCyclesController.cs#L152) / GetContributions | NotFoundException | "Contribution cycle not found." | No submitted values in literal. | Expected domain/validation 4xx was logged with full exception at Error (except authentication). | Message unchanged. 4xx: Information, type/status/correlation only, no exception; 5xx: full exception internally, generic client detail. Fixed authored client details preserved. |
| [API/Controllers/ContributionCyclesController.cs:157](API/Controllers/ContributionCyclesController.cs#L157) / GetContributions | RequestValidationException | "Page size cannot be negative." | No submitted values in literal. | Expected domain/validation 4xx was logged with full exception at Error (except authentication). | Message unchanged. 4xx: Information, type/status/correlation only, no exception; 5xx: full exception internally, generic client detail. Fixed authored client details preserved. |
| [API/Controllers/ContributionCyclesController.cs:211](API/Controllers/ContributionCyclesController.cs#L211) / Create | NotFoundException | "Stokvel not found." | No submitted values in literal. | Expected domain/validation 4xx was logged with full exception at Error (except authentication). | Message unchanged. 4xx: Information, type/status/correlation only, no exception; 5xx: full exception internally, generic client detail. Fixed authored client details preserved. |
| [API/Controllers/ContributionCyclesController.cs:264](API/Controllers/ContributionCyclesController.cs#L264) / Update | NotFoundException | "Stokvel not found." | No submitted values in literal. | Expected domain/validation 4xx was logged with full exception at Error (except authentication). | Message unchanged. 4xx: Information, type/status/correlation only, no exception; 5xx: full exception internally, generic client detail. Fixed authored client details preserved. |
| [API/Controllers/ContributionCyclesController.cs:270](API/Controllers/ContributionCyclesController.cs#L270) / Update | NotFoundException | "Contribution cycle not found." | No submitted values in literal. | Expected domain/validation 4xx was logged with full exception at Error (except authentication). | Message unchanged. 4xx: Information, type/status/correlation only, no exception; 5xx: full exception internally, generic client detail. Fixed authored client details preserved. |
| [API/Controllers/ContributionCyclesController.cs:275](API/Controllers/ContributionCyclesController.cs#L275) / Update | RequestValidationException | "If-Match header is required." | No submitted values in literal. | Expected domain/validation 4xx was logged with full exception at Error (except authentication). | Message unchanged. 4xx: Information, type/status/correlation only, no exception; 5xx: full exception internally, generic client detail. Fixed authored client details preserved. |
| [API/Controllers/ContributionCyclesController.cs:282](API/Controllers/ContributionCyclesController.cs#L282) / Update | PreconditionFailedException | "The contribution cycle has changed. Refetch it and retry with the current ETag." | No submitted values in literal. | Expected domain/validation 4xx was logged with full exception at Error (except authentication). | Message unchanged. 4xx: Information, type/status/correlation only, no exception; 5xx: full exception internally, generic client detail. Fixed authored client details preserved. |
| [API/Controllers/ContributionCyclesController.cs:319](API/Controllers/ContributionCyclesController.cs#L319) / Delete | NotFoundException | "Stokvel not found." | No submitted values in literal. | Expected domain/validation 4xx was logged with full exception at Error (except authentication). | Message unchanged. 4xx: Information, type/status/correlation only, no exception; 5xx: full exception internally, generic client detail. Fixed authored client details preserved. |
| [API/Controllers/ContributionCyclesController.cs:325](API/Controllers/ContributionCyclesController.cs#L325) / Delete | NotFoundException | "Contribution cycle not found." | No submitted values in literal. | Expected domain/validation 4xx was logged with full exception at Error (except authentication). | Message unchanged. 4xx: Information, type/status/correlation only, no exception; 5xx: full exception internally, generic client detail. Fixed authored client details preserved. |
| [API/Controllers/ContributionCyclesController.cs:356](API/Controllers/ContributionCyclesController.cs#L356) / ParseIfMatch | RequestValidationException | "If-Match header must contain a valid ETag value." | No submitted values in literal. | Expected domain/validation 4xx was logged with full exception at Error (except authentication). | Message unchanged. 4xx: Information, type/status/correlation only, no exception; 5xx: full exception internally, generic client detail. Fixed authored client details preserved. |
| [API/Controllers/StokvelsController.cs:85](API/Controllers/StokvelsController.cs#L85) / GetById | NotFoundException | "Stokvel not found." | No submitted values in literal. | Expected domain/validation 4xx was logged with full exception at Error (except authentication). | Message unchanged. 4xx: Information, type/status/correlation only, no exception; 5xx: full exception internally, generic client detail. Fixed authored client details preserved. |
| [API/Controllers/StokvelsController.cs:116](API/Controllers/StokvelsController.cs#L116) / GetMembers | NotFoundException | "Stokvel not found." | No submitted values in literal. | Expected domain/validation 4xx was logged with full exception at Error (except authentication). | Message unchanged. 4xx: Information, type/status/correlation only, no exception; 5xx: full exception internally, generic client detail. Fixed authored client details preserved. |
| [API/Controllers/StokvelsController.cs:120](API/Controllers/StokvelsController.cs#L120) / GetMembers | RequestValidationException | "Page size cannot be negative." | No submitted values in literal. | Expected domain/validation 4xx was logged with full exception at Error (except authentication). | Message unchanged. 4xx: Information, type/status/correlation only, no exception; 5xx: full exception internally, generic client detail. Fixed authored client details preserved. |
| [API/Controllers/StokvelsController.cs:202](API/Controllers/StokvelsController.cs#L202) / Update | NotFoundException | "Stokvel not found." | No submitted values in literal. | Expected domain/validation 4xx was logged with full exception at Error (except authentication). | Message unchanged. 4xx: Information, type/status/correlation only, no exception; 5xx: full exception internally, generic client detail. Fixed authored client details preserved. |
| [API/Controllers/StokvelsController.cs:206](API/Controllers/StokvelsController.cs#L206) / Update | RequestValidationException | "If-Match header is required." | No submitted values in literal. | Expected domain/validation 4xx was logged with full exception at Error (except authentication). | Message unchanged. 4xx: Information, type/status/correlation only, no exception; 5xx: full exception internally, generic client detail. Fixed authored client details preserved. |
| [API/Controllers/StokvelsController.cs:213](API/Controllers/StokvelsController.cs#L213) / Update | PreconditionFailedException | "The stokvel has changed. Refetch it and retry with the current ETag." | No submitted values in literal. | Expected domain/validation 4xx was logged with full exception at Error (except authentication). | Message unchanged. 4xx: Information, type/status/correlation only, no exception; 5xx: full exception internally, generic client detail. Fixed authored client details preserved. |
| [API/Controllers/StokvelsController.cs:247](API/Controllers/StokvelsController.cs#L247) / Delete | NotFoundException | "Stokvel not found." | No submitted values in literal. | Expected domain/validation 4xx was logged with full exception at Error (except authentication). | Message unchanged. 4xx: Information, type/status/correlation only, no exception; 5xx: full exception internally, generic client detail. Fixed authored client details preserved. |
| [API/Controllers/StokvelsController.cs:384](API/Controllers/StokvelsController.cs#L384) / ParseIfMatch | RequestValidationException | "If-Match header must contain a valid ETag value." | No submitted values in literal. | Expected domain/validation 4xx was logged with full exception at Error (except authentication). | Message unchanged. 4xx: Information, type/status/correlation only, no exception; 5xx: full exception internally, generic client detail. Fixed authored client details preserved. |
| [API/Controllers/UsersController.cs:65](API/Controllers/UsersController.cs#L65) / GetById | NotFoundException | "User not found." | No submitted values in literal. | Expected domain/validation 4xx was logged with full exception at Error (except authentication). | Message unchanged. 4xx: Information, type/status/correlation only, no exception; 5xx: full exception internally, generic client detail. Fixed authored client details preserved. |
| [API/Controllers/UsersController.cs:131](API/Controllers/UsersController.cs#L131) / Update | NotFoundException | "User not found." | No submitted values in literal. | Expected domain/validation 4xx was logged with full exception at Error (except authentication). | Message unchanged. 4xx: Information, type/status/correlation only, no exception; 5xx: full exception internally, generic client detail. Fixed authored client details preserved. |
| [API/Controllers/UsersController.cs:135](API/Controllers/UsersController.cs#L135) / Update | RequestValidationException | "If-Match header is required." | No submitted values in literal. | Expected domain/validation 4xx was logged with full exception at Error (except authentication). | Message unchanged. 4xx: Information, type/status/correlation only, no exception; 5xx: full exception internally, generic client detail. Fixed authored client details preserved. |
| [API/Controllers/UsersController.cs:142](API/Controllers/UsersController.cs#L142) / Update | PreconditionFailedException | "The user has changed. Refetch it and retry with the current ETag." | No submitted values in literal. | Expected domain/validation 4xx was logged with full exception at Error (except authentication). | Message unchanged. 4xx: Information, type/status/correlation only, no exception; 5xx: full exception internally, generic client detail. Fixed authored client details preserved. |
| [API/Controllers/UsersController.cs:174](API/Controllers/UsersController.cs#L174) / Delete | NotFoundException | "User not found." | No submitted values in literal. | Expected domain/validation 4xx was logged with full exception at Error (except authentication). | Message unchanged. 4xx: Information, type/status/correlation only, no exception; 5xx: full exception internally, generic client detail. Fixed authored client details preserved. |
| [API/Controllers/UsersController.cs:202](API/Controllers/UsersController.cs#L202) / ParseIfMatch | RequestValidationException | "If-Match header must contain a valid ETag value." | No submitted values in literal. | Expected domain/validation 4xx was logged with full exception at Error (except authentication). | Message unchanged. 4xx: Information, type/status/correlation only, no exception; 5xx: full exception internally, generic client detail. Fixed authored client details preserved. |
| [API/Filters/ValidationFilter.cs:23](API/Filters/ValidationFilter.cs#L23) / OnActionExecutionAsync | RequestValidationException | "The request body is invalid." | No submitted values in literal. | Expected domain/validation 4xx was logged with full exception at Error (except authentication). | Message unchanged. 4xx: Information, type/status/correlation only, no exception; 5xx: full exception internally, generic client detail. Fixed authored client details preserved. |
| [API/Filters/ValidationFilter.cs:56](API/Filters/ValidationFilter.cs#L56) / OnActionExecutionAsync | RequestValidationException | message | Dynamic diagnostic / possible submitted data; do not retain on 4xx. | Expected domain/validation 4xx was logged with full exception at Error (except authentication). | Message unchanged. 4xx: Information, type/status/correlation only, no exception; 5xx: full exception internally, generic client detail. Fixed authored client details preserved. |
| [API/Handlers/GlobalExceptionHandler.cs:60](API/Handlers/GlobalExceptionHandler.cs#L60) / TryHandleAsync | ILogger _logger.LogError | exception, "Request failed with {StatusCode}. CorrelationId: {CorrelationId}", statusCode, correlationId | Full 5xx exception/inner chain may contain sensitive third-party diagnostics. Residual risk explicitly retained for diagnosis. | Error with full exception for every non-auth failure, including 4xx. | Error only for 5xx, full exception retained internally; generic client detail. No exception text interpolated into template. |
| [API/Handlers/GlobalExceptionHandler.cs:63](API/Handlers/GlobalExceptionHandler.cs#L63) / TryHandleAsync | ILogger _logger.LogInformation | "Request rejected with {StatusCode} ({ExceptionType}). CorrelationId: {CorrelationId}", statusCode, exception.GetType().Name, correlationId | Fixed event/state/type/status plus W3C ID; operational correlation is linkable and needs retention/access controls. | Authentication-only Information event; other 4xx were Error with full exception. | All 4xx Information: exception type/status/W3C correlation only; no message or exception object. |
| [API/Observability/Correlation.cs:14](API/Observability/Correlation.cs#L14) / GetId | InvalidOperationException | "A W3C request activity is required for error correlation." | No submitted values in literal. | Literal or generated diagnostic; auth 401 was Information; unexpected 500 was Error. | Message unchanged. 4xx: Information, type/status/correlation only, no exception; 5xx: full exception internally, generic client detail. Fixed authored client details preserved. |
| [API/Program.cs:106](API/Program.cs#L106) / startup / DbContext configuration | InvalidOperationException | "Connection string 'RondiTrackDb' was not found." | No submitted values in literal. | Literal or generated diagnostic; auth 401 was Information; unexpected 500 was Error. | Message unchanged. 4xx: Information, type/status/correlation only, no exception; 5xx: full exception internally, generic client detail. Fixed authored client details preserved. |
| [API/Program.cs:98](API/Program.cs#L98) / CustomizeProblemDetails callback | ILogger logger.Log | status &gt;= 500 ? LogLevel.Error : LogLevel.Information, "Request failed with {StatusCode}. CorrelationId: {CorrelationId}", status, Correlation.GetId(context.HttpContext) | Fixed event/state/type/status plus W3C ID; operational correlation is linkable and needs retention/access controls. | Existing structured status/correlation event; no submitted values. | Retained safe fields and level; no request/response bodies, identities, amounts or secrets. |
| [API/Program.cs:180](API/Program.cs#L180) / status-code callback | ILogger .LogInformation | "Request rejected with {StatusCode}. CorrelationId: {CorrelationId}", http.Response.StatusCode, Correlation.GetId(http) | Fixed event/state/type/status plus W3C ID; operational correlation is linkable and needs retention/access controls. | Existing structured status/correlation event; no submitted values. | Retained safe fields and level; no request/response bodies, identities, amounts or secrets. |
| [API/Services/ContributionService.cs:48](API/Services/ContributionService.cs#L48) / RecordContributionAsync | RequestValidationException | "Idempotency-Key header is required." | No submitted values in literal. | Expected domain/validation 4xx was logged with full exception at Error (except authentication). | Message unchanged. 4xx: Information, type/status/correlation only, no exception; 5xx: full exception internally, generic client detail. Fixed authored client details preserved. |
| [API/Services/ContributionService.cs:66](API/Services/ContributionService.cs#L66) / RecordContributionAsync | BusinessRuleException | "Idempotency-Key was already used with a different request." | No submitted values in literal. | Expected domain/validation 4xx was logged with full exception at Error (except authentication). | Message unchanged. 4xx: Information, type/status/correlation only, no exception; 5xx: full exception internally, generic client detail. Fixed authored client details preserved. |
| [API/Services/ContributionService.cs:79](API/Services/ContributionService.cs#L79) / RecordContributionAsync | NotFoundException | "Stokvel not found." | No submitted values in literal. | Expected domain/validation 4xx was logged with full exception at Error (except authentication). | Message unchanged. 4xx: Information, type/status/correlation only, no exception; 5xx: full exception internally, generic client detail. Fixed authored client details preserved. |
| [API/Services/ContributionService.cs:89](API/Services/ContributionService.cs#L89) / RecordContributionAsync | NotFoundException | "User not found." | No submitted values in literal. | Expected domain/validation 4xx was logged with full exception at Error (except authentication). | Message unchanged. 4xx: Information, type/status/correlation only, no exception; 5xx: full exception internally, generic client detail. Fixed authored client details preserved. |
| [API/Services/ContributionService.cs:100](API/Services/ContributionService.cs#L100) / RecordContributionAsync | BusinessRuleException | "User is not a member of this stokvel." | No submitted values in literal. | Expected domain/validation 4xx was logged with full exception at Error (except authentication). | Message unchanged. 4xx: Information, type/status/correlation only, no exception; 5xx: full exception internally, generic client detail. Fixed authored client details preserved. |
| [API/Services/ContributionService.cs:110](API/Services/ContributionService.cs#L110) / RecordContributionAsync | NotFoundException | "Contribution cycle not found." | No submitted values in literal. | Expected domain/validation 4xx was logged with full exception at Error (except authentication). | Message unchanged. 4xx: Information, type/status/correlation only, no exception; 5xx: full exception internally, generic client detail. Fixed authored client details preserved. |
| [API/Services/ContributionService.cs:116](API/Services/ContributionService.cs#L116) / RecordContributionAsync | BusinessRuleException | "Contribution cycle does not belong to this stokvel." | No submitted values in literal. | Expected domain/validation 4xx was logged with full exception at Error (except authentication). | Message unchanged. 4xx: Information, type/status/correlation only, no exception; 5xx: full exception internally, generic client detail. Fixed authored client details preserved. |
| [API/Services/ContributionService.cs:129](API/Services/ContributionService.cs#L129) / RecordContributionAsync | BusinessRuleException | "A contribution already exists for this member and cycle." | No submitted values in literal. | Expected domain/validation 4xx was logged with full exception at Error (except authentication). | Message unchanged. 4xx: Information, type/status/correlation only, no exception; 5xx: full exception internally, generic client detail. Fixed authored client details preserved. |
| [API/Services/MembershipService.cs:33](API/Services/MembershipService.cs#L33) / AddMemberAsync | NotFoundException | "Stokvel not found." | No submitted values in literal. | Expected domain/validation 4xx was logged with full exception at Error (except authentication). | Message unchanged. 4xx: Information, type/status/correlation only, no exception; 5xx: full exception internally, generic client detail. Fixed authored client details preserved. |
| [API/Services/MembershipService.cs:43](API/Services/MembershipService.cs#L43) / AddMemberAsync | NotFoundException | "User not found." | No submitted values in literal. | Expected domain/validation 4xx was logged with full exception at Error (except authentication). | Message unchanged. 4xx: Information, type/status/correlation only, no exception; 5xx: full exception internally, generic client detail. Fixed authored client details preserved. |
| [API/Services/MembershipService.cs:54](API/Services/MembershipService.cs#L54) / AddMemberAsync | BusinessRuleException | "User is already a member of this stokvel." | No submitted values in literal. | Expected domain/validation 4xx was logged with full exception at Error (except authentication). | Message unchanged. 4xx: Information, type/status/correlation only, no exception; 5xx: full exception internally, generic client detail. Fixed authored client details preserved. |
| [API/Services/MembershipService.cs:77](API/Services/MembershipService.cs#L77) / RemoveMemberAsync | NotFoundException | "Stokvel not found." | No submitted values in literal. | Expected domain/validation 4xx was logged with full exception at Error (except authentication). | Message unchanged. 4xx: Information, type/status/correlation only, no exception; 5xx: full exception internally, generic client detail. Fixed authored client details preserved. |
| [API/Services/MembershipService.cs:89](API/Services/MembershipService.cs#L89) / RemoveMemberAsync | BusinessRuleException | "User is not a member of this stokvel." | No submitted values in literal. | Expected domain/validation 4xx was logged with full exception at Error (except authentication). | Message unchanged. 4xx: Information, type/status/correlation only, no exception; 5xx: full exception internally, generic client detail. Fixed authored client details preserved. |
| [API/Services/PayoutService.cs:55](API/Services/PayoutService.cs#L55) / CreatePayoutAsync | NotFoundException | "Contribution cycle not found." | No submitted values in literal. | Expected domain/validation 4xx was logged with full exception at Error (except authentication). | Message unchanged. 4xx: Information, type/status/correlation only, no exception; 5xx: full exception internally, generic client detail. Fixed authored client details preserved. |
| [API/Services/PayoutService.cs:61](API/Services/PayoutService.cs#L61) / CreatePayoutAsync | BusinessRuleException | "Contribution cycle is not open for payout." | No submitted values in literal. | Expected domain/validation 4xx was logged with full exception at Error (except authentication). | Message unchanged. 4xx: Information, type/status/correlation only, no exception; 5xx: full exception internally, generic client detail. Fixed authored client details preserved. |
| [API/Services/PayoutService.cs:67](API/Services/PayoutService.cs#L67) / CreatePayoutAsync | BusinessRuleException | "Payout amount must be greater than zero." | No submitted values in literal. | Expected domain/validation 4xx was logged with full exception at Error (except authentication). | Message unchanged. 4xx: Information, type/status/correlation only, no exception; 5xx: full exception internally, generic client detail. Fixed authored client details preserved. |
| [API/Services/PayoutService.cs:97](API/Services/PayoutService.cs#L97) / CreatePayoutAsync | BusinessRuleException | "No eligible member is available for payout." | No submitted values in literal. | Expected domain/validation 4xx was logged with full exception at Error (except authentication). | Message unchanged. 4xx: Information, type/status/correlation only, no exception; 5xx: full exception internally, generic client detail. Fixed authored client details preserved. |
| [API/Services/PayoutService.cs:124](API/Services/PayoutService.cs#L124) / CreatePayoutAsync | rethrow | Original exception / message / inner chain | Dynamic: may inherit personal or database diagnostics. | Propagated original exception. | Rethrow unchanged; handler applies 4xx minimality / 5xx internal diagnostics. Test-only rethrows never deploy. |
| [API/Services/PayoutService.cs:137](API/Services/PayoutService.cs#L137) / CreatePayoutAsync | rethrow | Original exception / message / inner chain | Dynamic: may inherit personal or database diagnostics. | Propagated original exception. | Rethrow unchanged; handler applies 4xx minimality / 5xx internal diagnostics. Test-only rethrows never deploy. |
| [API/Validators/CreateContributionCycleRequestValidator.cs:14](API/Validators/CreateContributionCycleRequestValidator.cs#L14) / CreateContributionCycleRequestValidator | FluentValidation message | "Contribution cycle period is required." | Current literals do not interpolate attempted values. | Combined into RequestValidationException; full 400 exception logged at Error. | Client message unchanged; 400 logs omit message and exception object. |
| [API/Validators/CreateContributionCycleRequestValidator.cs:16](API/Validators/CreateContributionCycleRequestValidator.cs#L16) / CreateContributionCycleRequestValidator | FluentValidation message | "Period must use the YYYY-MM format." | Current literals do not interpolate attempted values. | Combined into RequestValidationException; full 400 exception logged at Error. | Client message unchanged; 400 logs omit message and exception object. |
| [API/Validators/CreateContributionCycleRequestValidator.cs:20](API/Validators/CreateContributionCycleRequestValidator.cs#L20) / CreateContributionCycleRequestValidator | FluentValidation message | "Target amount must be greater than zero." | Current literals do not interpolate attempted values. | Combined into RequestValidationException; full 400 exception logged at Error. | Client message unchanged; 400 logs omit message and exception object. |
| [API/Validators/CreateStokvelRequestValidator.cs:13](API/Validators/CreateStokvelRequestValidator.cs#L13) / CreateStokvelRequestValidator | FluentValidation message | "Stokvel name is required." | Current literals do not interpolate attempted values. | Combined into RequestValidationException; full 400 exception logged at Error. | Client message unchanged; 400 logs omit message and exception object. |
| [API/Validators/CreateStokvelRequestValidator.cs:17](API/Validators/CreateStokvelRequestValidator.cs#L17) / CreateStokvelRequestValidator | FluentValidation message | "Contribution amount must be greater than zero." | Current literals do not interpolate attempted values. | Combined into RequestValidationException; full 400 exception logged at Error. | Client message unchanged; 400 logs omit message and exception object. |
| [API/Validators/CreateUserRequestValidator.cs:13](API/Validators/CreateUserRequestValidator.cs#L13) / CreateUserRequestValidator | FluentValidation message | "Name is required." | Current literals do not interpolate attempted values. | Combined into RequestValidationException; full 400 exception logged at Error. | Client message unchanged; 400 logs omit message and exception object. |
| [API/Validators/CreateUserRequestValidator.cs:17](API/Validators/CreateUserRequestValidator.cs#L17) / CreateUserRequestValidator | FluentValidation message | "Email is required." | Current literals do not interpolate attempted values. | Combined into RequestValidationException; full 400 exception logged at Error. | Client message unchanged; 400 logs omit message and exception object. |
| [API/Validators/CreateUserRequestValidator.cs:19](API/Validators/CreateUserRequestValidator.cs#L19) / CreateUserRequestValidator | FluentValidation message | "A valid email is required." | Current literals do not interpolate attempted values. | Combined into RequestValidationException; full 400 exception logged at Error. | Client message unchanged; 400 logs omit message and exception object. |
| [API/Validators/LoginRequestValidator.cs:10](API/Validators/LoginRequestValidator.cs#L10) / LoginRequestValidator | FluentValidation default NotEmpty message | Email NotEmpty() | Generated field/rule/length text. Future validators could use attempted-value placeholders. | Default message assembled into validation exception and logged at Error. | Rule unchanged; generated validation text is excluded from 4xx logs. This default rule does not echo credentials. |
| [API/Validators/LoginRequestValidator.cs:10](API/Validators/LoginRequestValidator.cs#L10) / LoginRequestValidator | FluentValidation default EmailAddress message | Email EmailAddress() | Generated field/rule/length text. Future validators could use attempted-value placeholders. | Default message assembled into validation exception and logged at Error. | Rule unchanged; generated validation text is excluded from 4xx logs. This default rule does not echo credentials. |
| [API/Validators/LoginRequestValidator.cs:11](API/Validators/LoginRequestValidator.cs#L11) / LoginRequestValidator | FluentValidation default NotEmpty message | Password NotEmpty() | Generated field/rule/length text. Future validators could use attempted-value placeholders. | Default message assembled into validation exception and logged at Error. | Rule unchanged; generated validation text is excluded from 4xx logs. This default rule does not echo credentials. |
| [API/Validators/RecordContributionRequestValidator.cs:13](API/Validators/RecordContributionRequestValidator.cs#L13) / RecordContributionRequestValidator | FluentValidation message | "Contribution amount must be greater than zero." | Current literals do not interpolate attempted values. | Combined into RequestValidationException; full 400 exception logged at Error. | Client message unchanged; 400 logs omit message and exception object. |
| [API/Validators/RecordContributionRequestValidator.cs:18](API/Validators/RecordContributionRequestValidator.cs#L18) / RecordContributionRequestValidator | FluentValidation message | "Contribution cycle ID is required." | Current literals do not interpolate attempted values. | Combined into RequestValidationException; full 400 exception logged at Error. | Client message unchanged; 400 logs omit message and exception object. |
| [API/Validators/RegisterRequestValidator.cs:10](API/Validators/RegisterRequestValidator.cs#L10) / RegisterRequestValidator | FluentValidation default NotEmpty message | Name NotEmpty() | Generated field/rule/length text. Future validators could use attempted-value placeholders. | Default message assembled into validation exception and logged at Error. | Rule unchanged; generated validation text is excluded from 4xx logs. This default rule does not echo credentials. |
| [API/Validators/RegisterRequestValidator.cs:11](API/Validators/RegisterRequestValidator.cs#L11) / RegisterRequestValidator | FluentValidation default NotEmpty message | Email NotEmpty() | Generated field/rule/length text. Future validators could use attempted-value placeholders. | Default message assembled into validation exception and logged at Error. | Rule unchanged; generated validation text is excluded from 4xx logs. This default rule does not echo credentials. |
| [API/Validators/RegisterRequestValidator.cs:11](API/Validators/RegisterRequestValidator.cs#L11) / RegisterRequestValidator | FluentValidation default EmailAddress message | Email EmailAddress() | Generated field/rule/length text. Future validators could use attempted-value placeholders. | Default message assembled into validation exception and logged at Error. | Rule unchanged; generated validation text is excluded from 4xx logs. This default rule does not echo credentials. |
| [API/Validators/RegisterRequestValidator.cs:12](API/Validators/RegisterRequestValidator.cs#L12) / RegisterRequestValidator | FluentValidation default NotEmpty message | Password NotEmpty() | Generated field/rule/length text. Future validators could use attempted-value placeholders. | Default message assembled into validation exception and logged at Error. | Rule unchanged; generated validation text is excluded from 4xx logs. This default rule does not echo credentials. |
| [API/Validators/RegisterRequestValidator.cs:12](API/Validators/RegisterRequestValidator.cs#L12) / RegisterRequestValidator | FluentValidation default MinimumLength message | Password MinimumLength(12) | Generated field/rule/length text. Future validators could use attempted-value placeholders. | Default message assembled into validation exception and logged at Error. | Rule unchanged; generated validation text is excluded from 4xx logs. This default rule does not echo credentials. |
| [API/Validators/UpdateContributionCycleRequestValidator.cs:14](API/Validators/UpdateContributionCycleRequestValidator.cs#L14) / UpdateContributionCycleRequestValidator | FluentValidation message | "Contribution cycle period is required." | Current literals do not interpolate attempted values. | Combined into RequestValidationException; full 400 exception logged at Error. | Client message unchanged; 400 logs omit message and exception object. |
| [API/Validators/UpdateContributionCycleRequestValidator.cs:16](API/Validators/UpdateContributionCycleRequestValidator.cs#L16) / UpdateContributionCycleRequestValidator | FluentValidation message | "Period must use the YYYY-MM format." | Current literals do not interpolate attempted values. | Combined into RequestValidationException; full 400 exception logged at Error. | Client message unchanged; 400 logs omit message and exception object. |
| [API/Validators/UpdateContributionCycleRequestValidator.cs:20](API/Validators/UpdateContributionCycleRequestValidator.cs#L20) / UpdateContributionCycleRequestValidator | FluentValidation message | "Target amount must be greater than zero." | Current literals do not interpolate attempted values. | Combined into RequestValidationException; full 400 exception logged at Error. | Client message unchanged; 400 logs omit message and exception object. |
| [API/Validators/UpdateStokvelRequestValidator.cs:13](API/Validators/UpdateStokvelRequestValidator.cs#L13) / UpdateStokvelRequestValidator | FluentValidation message | "Stokvel name is required." | Current literals do not interpolate attempted values. | Combined into RequestValidationException; full 400 exception logged at Error. | Client message unchanged; 400 logs omit message and exception object. |
| [API/Validators/UpdateStokvelRequestValidator.cs:17](API/Validators/UpdateStokvelRequestValidator.cs#L17) / UpdateStokvelRequestValidator | FluentValidation message | "Contribution amount must be greater than zero." | Current literals do not interpolate attempted values. | Combined into RequestValidationException; full 400 exception logged at Error. | Client message unchanged; 400 logs omit message and exception object. |
| [API/Validators/UpdateUserRequestValidator.cs:13](API/Validators/UpdateUserRequestValidator.cs#L13) / UpdateUserRequestValidator | FluentValidation message | "Name is required." | Current literals do not interpolate attempted values. | Combined into RequestValidationException; full 400 exception logged at Error. | Client message unchanged; 400 logs omit message and exception object. |
| [API/Validators/UpdateUserRequestValidator.cs:17](API/Validators/UpdateUserRequestValidator.cs#L17) / UpdateUserRequestValidator | FluentValidation message | "Email is required." | Current literals do not interpolate attempted values. | Combined into RequestValidationException; full 400 exception logged at Error. | Client message unchanged; 400 logs omit message and exception object. |
| [API/Validators/UpdateUserRequestValidator.cs:19](API/Validators/UpdateUserRequestValidator.cs#L19) / UpdateUserRequestValidator | FluentValidation message | "A valid email is required." | Current literals do not interpolate attempted values. | Combined into RequestValidationException; full 400 exception logged at Error. | Client message unchanged; 400 logs omit message and exception object. |
| [Domain/Entities/Contribution.cs:30](Domain/Entities/Contribution.cs#L30) / Contribution | ArgumentException | "Stokvel ID is required." | No submitted values in literal. | Literal or generated diagnostic; auth 401 was Information; unexpected 500 was Error. | Message unchanged. 4xx: Information, type/status/correlation only, no exception; 5xx: full exception internally, generic client detail. Fixed authored client details preserved. |
| [Domain/Entities/Contribution.cs:34](Domain/Entities/Contribution.cs#L34) / Contribution | ArgumentException | "User ID is required." | No submitted values in literal. | Literal or generated diagnostic; auth 401 was Information; unexpected 500 was Error. | Message unchanged. 4xx: Information, type/status/correlation only, no exception; 5xx: full exception internally, generic client detail. Fixed authored client details preserved. |
| [Domain/Entities/Contribution.cs:38](Domain/Entities/Contribution.cs#L38) / Contribution | ArgumentException | "Contribution cycle ID is required." | No submitted values in literal. | Literal or generated diagnostic; auth 401 was Information; unexpected 500 was Error. | Message unchanged. 4xx: Information, type/status/correlation only, no exception; 5xx: full exception internally, generic client detail. Fixed authored client details preserved. |
| [Domain/Entities/Contribution.cs:42](Domain/Entities/Contribution.cs#L42) / Contribution | ArgumentException | "Contribution amount must be greater than zero." | No submitted values in literal. | Literal or generated diagnostic; auth 401 was Information; unexpected 500 was Error. | Message unchanged. 4xx: Information, type/status/correlation only, no exception; 5xx: full exception internally, generic client detail. Fixed authored client details preserved. |
| [Domain/Entities/ContributionCycle.cs:28](Domain/Entities/ContributionCycle.cs#L28) / ContributionCycle | ArgumentException | "Stokvel ID is required." | No submitted values in literal. | Literal or generated diagnostic; auth 401 was Information; unexpected 500 was Error. | Message unchanged. 4xx: Information, type/status/correlation only, no exception; 5xx: full exception internally, generic client detail. Fixed authored client details preserved. |
| [Domain/Entities/ContributionCycle.cs:32](Domain/Entities/ContributionCycle.cs#L32) / ContributionCycle | ArgumentException | "Contribution cycle period is required." | No submitted values in literal. | Literal or generated diagnostic; auth 401 was Information; unexpected 500 was Error. | Message unchanged. 4xx: Information, type/status/correlation only, no exception; 5xx: full exception internally, generic client detail. Fixed authored client details preserved. |
| [Domain/Entities/ContributionCycle.cs:36](Domain/Entities/ContributionCycle.cs#L36) / ContributionCycle | ArgumentException | "Target amount must be greater than zero." | No submitted values in literal. | Literal or generated diagnostic; auth 401 was Information; unexpected 500 was Error. | Message unchanged. 4xx: Information, type/status/correlation only, no exception; 5xx: full exception internally, generic client detail. Fixed authored client details preserved. |
| [Domain/Entities/ContributionCycle.cs:51](Domain/Entities/ContributionCycle.cs#L51) / UpdateDetails | ArgumentException | "Contribution cycle period is required." | No submitted values in literal. | Literal or generated diagnostic; auth 401 was Information; unexpected 500 was Error. | Message unchanged. 4xx: Information, type/status/correlation only, no exception; 5xx: full exception internally, generic client detail. Fixed authored client details preserved. |
| [Domain/Entities/ContributionCycle.cs:55](Domain/Entities/ContributionCycle.cs#L55) / UpdateDetails | ArgumentException | "Target amount must be greater than zero." | No submitted values in literal. | Literal or generated diagnostic; auth 401 was Information; unexpected 500 was Error. | Message unchanged. 4xx: Information, type/status/correlation only, no exception; 5xx: full exception internally, generic client detail. Fixed authored client details preserved. |
| [Domain/Entities/Payout.cs:30](Domain/Entities/Payout.cs#L30) / Payout | ArgumentException | "Stokvel ID is required." | No submitted values in literal. | Literal or generated diagnostic; auth 401 was Information; unexpected 500 was Error. | Message unchanged. 4xx: Information, type/status/correlation only, no exception; 5xx: full exception internally, generic client detail. Fixed authored client details preserved. |
| [Domain/Entities/Payout.cs:34](Domain/Entities/Payout.cs#L34) / Payout | ArgumentException | "Recipient user ID is required." | No submitted values in literal. | Literal or generated diagnostic; auth 401 was Information; unexpected 500 was Error. | Message unchanged. 4xx: Information, type/status/correlation only, no exception; 5xx: full exception internally, generic client detail. Fixed authored client details preserved. |
| [Domain/Entities/Payout.cs:38](Domain/Entities/Payout.cs#L38) / Payout | ArgumentException | "Contribution cycle ID is required." | No submitted values in literal. | Literal or generated diagnostic; auth 401 was Information; unexpected 500 was Error. | Message unchanged. 4xx: Information, type/status/correlation only, no exception; 5xx: full exception internally, generic client detail. Fixed authored client details preserved. |
| [Domain/Entities/Payout.cs:42](Domain/Entities/Payout.cs#L42) / Payout | ArgumentException | "Payout amount must be greater than zero." | No submitted values in literal. | Literal or generated diagnostic; auth 401 was Information; unexpected 500 was Error. | Message unchanged. 4xx: Information, type/status/correlation only, no exception; 5xx: full exception internally, generic client detail. Fixed authored client details preserved. |
| [Domain/Entities/Payout.cs:57](Domain/Entities/Payout.cs#L57) / UpdateAmount | ArgumentException | "Payout amount must be greater than zero." | No submitted values in literal. | Literal or generated diagnostic; auth 401 was Information; unexpected 500 was Error. | Message unchanged. 4xx: Information, type/status/correlation only, no exception; 5xx: full exception internally, generic client detail. Fixed authored client details preserved. |
| [Domain/Entities/Stokvel.cs:26](Domain/Entities/Stokvel.cs#L26) / Stokvel | ArgumentException | "Stokvel name is required." | No submitted values in literal. | Literal or generated diagnostic; auth 401 was Information; unexpected 500 was Error. | Message unchanged. 4xx: Information, type/status/correlation only, no exception; 5xx: full exception internally, generic client detail. Fixed authored client details preserved. |
| [Domain/Entities/Stokvel.cs:29](Domain/Entities/Stokvel.cs#L29) / Stokvel | ArgumentException | "Contribution amount must be greater than zero." | No submitted values in literal. | Literal or generated diagnostic; auth 401 was Information; unexpected 500 was Error. | Message unchanged. 4xx: Information, type/status/correlation only, no exception; 5xx: full exception internally, generic client detail. Fixed authored client details preserved. |
| [Domain/Entities/Stokvel.cs:42](Domain/Entities/Stokvel.cs#L42) / UpdateDetails | ArgumentException | "Stokvel name is required." | No submitted values in literal. | Literal or generated diagnostic; auth 401 was Information; unexpected 500 was Error. | Message unchanged. 4xx: Information, type/status/correlation only, no exception; 5xx: full exception internally, generic client detail. Fixed authored client details preserved. |
| [Domain/Entities/Stokvel.cs:46](Domain/Entities/Stokvel.cs#L46) / UpdateDetails | ArgumentException | "Contribution amount must be greater than zero." | No submitted values in literal. | Literal or generated diagnostic; auth 401 was Information; unexpected 500 was Error. | Message unchanged. 4xx: Information, type/status/correlation only, no exception; 5xx: full exception internally, generic client detail. Fixed authored client details preserved. |
| [Domain/Entities/Stokvel.cs:57](Domain/Entities/Stokvel.cs#L57) / AddMember | InvalidOperationException | "User is already a member of this stokvel." | No submitted values in literal. | Literal or generated diagnostic; auth 401 was Information; unexpected 500 was Error. | Message unchanged. 4xx: Information, type/status/correlation only, no exception; 5xx: full exception internally, generic client detail. Fixed authored client details preserved. |
| [Domain/Entities/Stokvel.cs:72](Domain/Entities/Stokvel.cs#L72) / RemoveMember | InvalidOperationException | "User is not a member of this stokvel." | No submitted values in literal. | Literal or generated diagnostic; auth 401 was Information; unexpected 500 was Error. | Message unchanged. 4xx: Information, type/status/correlation only, no exception; 5xx: full exception internally, generic client detail. Fixed authored client details preserved. |
| [Domain/Entities/StokvelMember.cs:18](Domain/Entities/StokvelMember.cs#L18) / ChangeRole | ArgumentException | "Unknown stokvel role." | No submitted values in literal. | Literal or generated diagnostic; auth 401 was Information; unexpected 500 was Error. | Message unchanged. 4xx: Information, type/status/correlation only, no exception; 5xx: full exception internally, generic client detail. Fixed authored client details preserved. |
| [Domain/Entities/StokvelMember.cs:38](Domain/Entities/StokvelMember.cs#L38) / StokvelMember | ArgumentException | "Stokvel ID is required." | No submitted values in literal. | Literal or generated diagnostic; auth 401 was Information; unexpected 500 was Error. | Message unchanged. 4xx: Information, type/status/correlation only, no exception; 5xx: full exception internally, generic client detail. Fixed authored client details preserved. |
| [Domain/Entities/StokvelMember.cs:42](Domain/Entities/StokvelMember.cs#L42) / StokvelMember | ArgumentException | "User ID is required." | No submitted values in literal. | Literal or generated diagnostic; auth 401 was Information; unexpected 500 was Error. | Message unchanged. 4xx: Information, type/status/correlation only, no exception; 5xx: full exception internally, generic client detail. Fixed authored client details preserved. |
| [Domain/Entities/StokvelMember.cs:46](Domain/Entities/StokvelMember.cs#L46) / StokvelMember | ArgumentException | "Role is required." | No submitted values in literal. | Literal or generated diagnostic; auth 401 was Information; unexpected 500 was Error. | Message unchanged. 4xx: Information, type/status/correlation only, no exception; 5xx: full exception internally, generic client detail. Fixed authored client details preserved. |
| [Domain/Entities/User.cs:23](Domain/Entities/User.cs#L23) / SetPlatformRole | ArgumentException | "Unknown platform role." | No submitted values in literal. | Literal or generated diagnostic; auth 401 was Information; unexpected 500 was Error. | Message unchanged. 4xx: Information, type/status/correlation only, no exception; 5xx: full exception internally, generic client detail. Fixed authored client details preserved. |
| [Domain/Entities/User.cs:32](Domain/Entities/User.cs#L32) / SetPasswordHash | ArgumentException | "Password hash is required." | No submitted values in literal. | Literal or generated diagnostic; auth 401 was Information; unexpected 500 was Error. | Message unchanged. 4xx: Information, type/status/correlation only, no exception; 5xx: full exception internally, generic client detail. Fixed authored client details preserved. |
| [Domain/Entities/User.cs:44](Domain/Entities/User.cs#L44) / User | ArgumentException | "Name is required." | No submitted values in literal. | Literal or generated diagnostic; auth 401 was Information; unexpected 500 was Error. | Message unchanged. 4xx: Information, type/status/correlation only, no exception; 5xx: full exception internally, generic client detail. Fixed authored client details preserved. |
| [Domain/Entities/User.cs:47](Domain/Entities/User.cs#L47) / User | ArgumentException | "A valid email is required." | No submitted values in literal. | Literal or generated diagnostic; auth 401 was Information; unexpected 500 was Error. | Message unchanged. 4xx: Information, type/status/correlation only, no exception; 5xx: full exception internally, generic client detail. Fixed authored client details preserved. |
| [Domain/Entities/User.cs:58](Domain/Entities/User.cs#L58) / UpdateDetails | ArgumentException | "Name is required." | No submitted values in literal. | Literal or generated diagnostic; auth 401 was Information; unexpected 500 was Error. | Message unchanged. 4xx: Information, type/status/correlation only, no exception; 5xx: full exception internally, generic client detail. Fixed authored client details preserved. |
| [Domain/Entities/User.cs:61](Domain/Entities/User.cs#L61) / UpdateDetails | ArgumentException | "A valid email is required." | No submitted values in literal. | Literal or generated diagnostic; auth 401 was Information; unexpected 500 was Error. | Message unchanged. 4xx: Information, type/status/correlation only, no exception; 5xx: full exception internally, generic client detail. Fixed authored client details preserved. |
| [Domain/Exceptions/AuthenticationFailedException.cs:3](Domain/Exceptions/AuthenticationFailedException.cs#L3) / AuthenticationFailedException.ctor | exception constructor forwarding | Caller-supplied message | Dynamic: wrapper alone cannot guarantee safe contents. | Forwarded unchanged to base exception. | No constructor change; each caller is audited separately; 4xx logger excludes message/object. |
| [Domain/Exceptions/BusinessRuleException.cs:6](Domain/Exceptions/BusinessRuleException.cs#L6) / BusinessRuleException.ctor | exception constructor forwarding | Caller-supplied message | Dynamic: wrapper alone cannot guarantee safe contents. | Forwarded unchanged to base exception. | No constructor change; each caller is audited separately; 4xx logger excludes message/object. |
| [Domain/Exceptions/ForbiddenException.cs:3](Domain/Exceptions/ForbiddenException.cs#L3) / ForbiddenException.ctor | exception constructor forwarding | Caller-supplied message | Dynamic: wrapper alone cannot guarantee safe contents. | Forwarded unchanged to base exception. | No constructor change; each caller is audited separately; 4xx logger excludes message/object. |
| [Domain/Exceptions/NotFoundException.cs:6](Domain/Exceptions/NotFoundException.cs#L6) / NotFoundException.ctor | exception constructor forwarding | Caller-supplied message | Dynamic: wrapper alone cannot guarantee safe contents. | Forwarded unchanged to base exception. | No constructor change; each caller is audited separately; 4xx logger excludes message/object. |
| [Domain/Exceptions/PreconditionFailedException.cs:6](Domain/Exceptions/PreconditionFailedException.cs#L6) / PreconditionFailedException.ctor | exception constructor forwarding | Caller-supplied message | Dynamic: wrapper alone cannot guarantee safe contents. | Forwarded unchanged to base exception. | No constructor change; each caller is audited separately; 4xx logger excludes message/object. |
| [Domain/Exceptions/RequestValidationException.cs:7](Domain/Exceptions/RequestValidationException.cs#L7) / RequestValidationException.ctor | exception constructor forwarding | Caller-supplied message | Dynamic: wrapper alone cannot guarantee safe contents. | Forwarded unchanged to base exception. | No constructor change; each caller is audited separately; 4xx logger excludes message/object. |
| [Domain/Exceptions/RondiTrackException.cs:6](Domain/Exceptions/RondiTrackException.cs#L6) / RondiTrackException.ctor | exception constructor forwarding | Caller-supplied message | Dynamic: wrapper alone cannot guarantee safe contents. | Forwarded unchanged to base exception. | No constructor change; each caller is audited separately; 4xx logger excludes message/object. |
| [Infrastructure/Repositories/ContributionRepository.cs:59](Infrastructure/Repositories/ContributionRepository.cs#L59) / GetByCyclePagedAsync | RequestValidationException | "Sort direction must be 'asc' or 'desc'." | No submitted values in literal. | Expected domain/validation 4xx was logged with full exception at Error (except authentication). | Message unchanged. 4xx: Information, type/status/correlation only, no exception; 5xx: full exception internally, generic client detail. Fixed authored client details preserved. |
| [Infrastructure/Repositories/ContributionRepository.cs:72](Infrastructure/Repositories/ContributionRepository.cs#L72) / GetByCyclePagedAsync | RequestValidationException | "Unsupported sort field for contributions." | No submitted values in literal. | Expected domain/validation 4xx was logged with full exception at Error (except authentication). | Message unchanged. 4xx: Information, type/status/correlation only, no exception; 5xx: full exception internally, generic client detail. Fixed authored client details preserved. |
| [Infrastructure/Repositories/ContributionRepository.cs:212](Infrastructure/Repositories/ContributionRepository.cs#L212) / ApplySort | RequestValidationException | "Unsupported sort field for contributions." | No submitted values in literal. | Expected domain/validation 4xx was logged with full exception at Error (except authentication). | Message unchanged. 4xx: Information, type/status/correlation only, no exception; 5xx: full exception internally, generic client detail. Fixed authored client details preserved. |
| [Infrastructure/Repositories/ContributionRepository.cs:319](Infrastructure/Repositories/ContributionRepository.cs#L319) / ParseToken | RequestValidationException | "Page token is invalid." | No submitted values in literal. | Expected domain/validation 4xx was logged with full exception at Error (except authentication). | Message unchanged. 4xx: Information, type/status/correlation only, no exception; 5xx: full exception internally, generic client detail. Fixed authored client details preserved. |
| [Infrastructure/Repositories/ContributionRepository.cs:329](Infrastructure/Repositories/ContributionRepository.cs#L329) / ParseToken | RequestValidationException | "Page token does not match the current sort or filter." | No submitted values in literal. | Expected domain/validation 4xx was logged with full exception at Error (except authentication). | Message unchanged. 4xx: Information, type/status/correlation only, no exception; 5xx: full exception internally, generic client detail. Fixed authored client details preserved. |
| [Infrastructure/Repositories/ContributionRepository.cs:337](Infrastructure/Repositories/ContributionRepository.cs#L337) / ParseToken | RequestValidationException | "Page token is invalid." | No submitted values in literal. | Expected domain/validation 4xx was logged with full exception at Error (except authentication). | Message unchanged. 4xx: Information, type/status/correlation only, no exception; 5xx: full exception internally, generic client detail. Fixed authored client details preserved. |
| [Infrastructure/Repositories/EfStokvelMemberRepository.cs:45](Infrastructure/Repositories/EfStokvelMemberRepository.cs#L45) / GetByStokvelPagedAsync | RequestValidationException | "Sort direction must be 'asc' or 'desc'." | No submitted values in literal. | Expected domain/validation 4xx was logged with full exception at Error (except authentication). | Message unchanged. 4xx: Information, type/status/correlation only, no exception; 5xx: full exception internally, generic client detail. Fixed authored client details preserved. |
| [Infrastructure/Repositories/EfStokvelMemberRepository.cs:58](Infrastructure/Repositories/EfStokvelMemberRepository.cs#L58) / GetByStokvelPagedAsync | RequestValidationException | "Unsupported sort field for stokvel members." | No submitted values in literal. | Expected domain/validation 4xx was logged with full exception at Error (except authentication). | Message unchanged. 4xx: Information, type/status/correlation only, no exception; 5xx: full exception internally, generic client detail. Fixed authored client details preserved. |
| [Infrastructure/Repositories/EfStokvelMemberRepository.cs:143](Infrastructure/Repositories/EfStokvelMemberRepository.cs#L143) / ApplySort | RequestValidationException | "Unsupported sort field for stokvel members." | No submitted values in literal. | Expected domain/validation 4xx was logged with full exception at Error (except authentication). | Message unchanged. 4xx: Information, type/status/correlation only, no exception; 5xx: full exception internally, generic client detail. Fixed authored client details preserved. |
| [Infrastructure/Repositories/EfStokvelMemberRepository.cs:234](Infrastructure/Repositories/EfStokvelMemberRepository.cs#L234) / ParseToken | RequestValidationException | "Page token is invalid." | No submitted values in literal. | Expected domain/validation 4xx was logged with full exception at Error (except authentication). | Message unchanged. 4xx: Information, type/status/correlation only, no exception; 5xx: full exception internally, generic client detail. Fixed authored client details preserved. |
| [Infrastructure/Repositories/EfStokvelMemberRepository.cs:241](Infrastructure/Repositories/EfStokvelMemberRepository.cs#L241) / ParseToken | RequestValidationException | "Page token does not match the current sort or filter." | No submitted values in literal. | Expected domain/validation 4xx was logged with full exception at Error (except authentication). | Message unchanged. 4xx: Information, type/status/correlation only, no exception; 5xx: full exception internally, generic client detail. Fixed authored client details preserved. |
| [Infrastructure/Repositories/EfStokvelMemberRepository.cs:249](Infrastructure/Repositories/EfStokvelMemberRepository.cs#L249) / ParseToken | RequestValidationException | "Page token is invalid." | No submitted values in literal. | Expected domain/validation 4xx was logged with full exception at Error (except authentication). | Message unchanged. 4xx: Information, type/status/correlation only, no exception; 5xx: full exception internally, generic client detail. Fixed authored client details preserved. |
| [RondiTrack.Tests/BusinessTelemetryTests.cs:156](RondiTrack.Tests/BusinessTelemetryTests.cs#L156) / ReaderExecutingAsync | NpgsqlException | "Synthetic transient test failure.", new TimeoutException() | Dynamic diagnostic / possible submitted data; do not retain on 4xx. | Test-only synthetic diagnostic; not production telemetry. | Unchanged test fixture message; synthetic only. |
| [RondiTrack.Tests/BusinessTelemetryTests.cs:156](RondiTrack.Tests/BusinessTelemetryTests.cs#L156) / ReaderExecutingAsync | TimeoutException | (default message) | No explicit message; library default diagnostic. | Test-only synthetic diagnostic; not production telemetry. | Unchanged test fixture message; synthetic only. |
| [RondiTrack.Tests/BusinessTelemetryTests.cs:166](RondiTrack.Tests/BusinessTelemetryTests.cs#L166) / TransactionCommittingAsync | InvalidOperationException | "Synthetic commit refusal." | No submitted values in literal. | Test-only synthetic diagnostic; not production telemetry. | Unchanged test fixture message; synthetic only. |
| [RondiTrack.Tests/CorrelationTests.cs:176](RondiTrack.Tests/CorrelationTests.cs#L176) / Unexpected500_ReturnsGenericDetailAndMatchingStructuredCorrelation | InvalidOperationException | "private-internal-diagnostic" | No submitted values in literal. | Test-only synthetic diagnostic; not production telemetry. | Unchanged test fixture message; synthetic only. |
| [RondiTrack.Tests/LoggingTests.cs:229](RondiTrack.Tests/LoggingTests.cs#L229) / Unexpected500_KeepsFullExceptionInternally_AndGenericClientDetail | InvalidOperationException | "Synthetic diagnostic failure." | No submitted values in literal. | Test-only synthetic diagnostic; not production telemetry. | Unchanged test fixture message; synthetic only. |
| [RondiTrack.Tests/PayoutTransactionTests.cs:186](RondiTrack.Tests/PayoutTransactionTests.cs#L186) / PayoutTransaction_WhenFailureOccursPartway_RollsBackAllChanges | InvalidOperationException | "Simulated payout failure." | No submitted values in literal. | Test-only synthetic diagnostic; not production telemetry. | Unchanged test fixture message; synthetic only. |
| [RondiTrack.Tests/TestSupport/PostgresApiFactory.cs:73](RondiTrack.Tests/TestSupport/PostgresApiFactory.cs#L73) / InitializeAsync | InvalidOperationException | "Tests must use the disposable PostgreSQL container." | No submitted values in literal. | Test-only synthetic diagnostic; not production telemetry. | Unchanged test fixture message; synthetic only. |
| [RondiTrack.Tests/TestSupport/PostgresApiFactory.cs:86](RondiTrack.Tests/TestSupport/PostgresApiFactory.cs#L86) / InitializeAsync | InvalidOperationException | "The API connected to an unexpected database." | No submitted values in literal. | Test-only synthetic diagnostic; not production telemetry. | Unchanged test fixture message; synthetic only. |
| [RondiTrack.Tests/TestSupport/PostgresApiFactory.cs:93](RondiTrack.Tests/TestSupport/PostgresApiFactory.cs#L93) / InitializeAsync | rethrow | Original exception / message / inner chain | Dynamic: may inherit personal or database diagnostics. | Propagated original exception. | Rethrow unchanged; handler applies 4xx minimality / 5xx internal diagnostics. Test-only rethrows never deploy. |
| [scripts/verify-assignment-5-4-isolation.ps1:12](scripts/verify-assignment-5-4-isolation.ps1#L12) / top-level preflight / proof | PowerShell throw | Development connection string is required for the isolation preflight. | Fixed operational guidance; no credentials/record values. | Existing isolation-script diagnostic. | Unchanged; script was not executed in Stage 5. Development database untouched. |
| [scripts/verify-assignment-5-4-isolation.ps1:21](scripts/verify-assignment-5-4-isolation.ps1#L21) / top-level preflight / proof | PowerShell throw | This proof only permits the local ronditrack development database. | Fixed operational guidance; no credentials/record values. | Existing isolation-script diagnostic. | Unchanged; script was not executed in Stage 5. Development database untouched. |
| [scripts/verify-assignment-5-4-isolation.ps1:23](scripts/verify-assignment-5-4-isolation.ps1#L23) / top-level preflight / proof | PowerShell throw | PostgreSQL psql was not found. | Fixed operational guidance; no credentials/record values. | Existing isolation-script diagnostic. | Unchanged; script was not executed in Stage 5. Development database untouched. |
| [scripts/verify-assignment-5-4-isolation.ps1:36](scripts/verify-assignment-5-4-isolation.ps1#L36) / top-level preflight / proof | PowerShell throw | Development database must initially accept connections. | Fixed operational guidance; no credentials/record values. | Existing isolation-script diagnostic. | Unchanged; script was not executed in Stage 5. Development database untouched. |
| [scripts/verify-assignment-5-4-isolation.ps1:38](scripts/verify-assignment-5-4-isolation.ps1#L38) / top-level preflight / proof | PowerShell throw | Close development API/database sessions before the isolation proof; no sessions will be terminated. | Fixed operational guidance; no credentials/record values. | Existing isolation-script diagnostic. | Unchanged; script was not executed in Stage 5. Development database untouched. |
| [scripts/verify-assignment-5-4-isolation.ps1:41](scripts/verify-assignment-5-4-isolation.ps1#L41) / top-level preflight / proof | PowerShell throw | Could not temporarily disable development database connections. | Fixed operational guidance; no credentials/record values. | Existing isolation-script diagnostic. | Unchanged; script was not executed in Stage 5. Development database untouched. |
| [scripts/verify-assignment-5-4-isolation.ps1:44](scripts/verify-assignment-5-4-isolation.ps1#L44) / top-level preflight / proof | PowerShell throw | A development session appeared during preflight; restore and retry when idle. | Fixed operational guidance; no credentials/record values. | Existing isolation-script diagnostic. | Unchanged; script was not executed in Stage 5. Development database untouched. |
| [scripts/verify-assignment-5-4-isolation.ps1:54](scripts/verify-assignment-5-4-isolation.ps1#L54) / finally / restoration | PowerShell throw | Development database unexpectedly accepted a connection. | Fixed operational guidance; no credentials/record values. | Existing isolation-script diagnostic. | Unchanged; script was not executed in Stage 5. Development database untouched. |
| [scripts/verify-assignment-5-4-isolation.ps1:56](scripts/verify-assignment-5-4-isolation.ps1#L56) / finally / restoration | PowerShell throw | Development probe failed for an unexpected reason; independence is not proven. | Fixed operational guidance; no credentials/record values. | Existing isolation-script diagnostic. | Unchanged; script was not executed in Stage 5. Development database untouched. |
| [scripts/verify-assignment-5-4-isolation.ps1:74](scripts/verify-assignment-5-4-isolation.ps1#L74) / finally / restoration | PowerShell throw | RESTORATION FAILED: restore ALLOW_CONNECTIONS=true for ronditrack before development use. | Fixed operational guidance; no credentials/record values. | Existing isolation-script diagnostic. | Unchanged; script was not executed in Stage 5. Development database untouched. |
| [scripts/verify-assignment-5-4-isolation.ps1:76](scripts/verify-assignment-5-4-isolation.ps1#L76) / finally / restoration | PowerShell throw | Development database restoration probe failed. | Fixed operational guidance; no credentials/record values. | Existing isolation-script diagnostic. | Unchanged; script was not executed in Stage 5. Development database untouched. |
| API/Program.cs / JWT OnTokenValidated | context.Fail message | The authenticated user no longer exists. | Fixed text; no user ID. | JWT rejection and no error details in response. | Unchanged validation; safe fixed diagnostic. |
| API/Program.cs / EF UseNpgsql | Npgsql parsing and connection errors | Library-generated configuration/connection diagnostics | May contain infrastructure or malformed setting values. | Parameter/detail defaults false; configurable connection flags. | Force LogParameters=false and IncludeErrorDetail=false without changing connection target. Never print connection string. |
| API/Program.cs / EF provider diagnostics | EF Database.Command logs | Parameterized SQL, durations, parameter type/size metadata | Values suppressed; literal SQL and metadata can still reveal schema. | Development Information; base Warning; no duplicate output. | Levels retained; no sensitive EF data flag or LogTo. Parameter placeholders verified in focused traffic. |
| EF provider / RelationalEventId.CommandError | EF failed-command log event | SQL template and library exception chain | Raw SQL literals / unexpected exceptions may expose data. | Database.Command Error exists independently of the handler. | No blanket suppression; parameter/detail safeguards preserved; independent database-originated conflict diagnostics remain a limitation. |
| EF provider / CoreEventId.SaveChangesFailed | EF failed-save log event | Context type and library exception / inner PostgreSQL chain | Unexpected diagnostic text may contain data even without parameter logging. | EF Update Error before handler classification. | No blanket suppression; full internal failure retained. Prechecked duplicate-registration 409 tested without Error. |
| EF provider / CoreEventId.QueryIterationFailed | EF failed-query log event | Context type and database exception chain | Unexpected third-party diagnostics may contain data. | EF Query Error independently of handler. | No blanket suppression; clients receive generic 500 detail. Parameter values/row details remain disabled. |
| API/Program.cs / Npgsql activity subscription | Npgsql trace tags/events | Parameterized SQL, operation timing and database metadata | Raw literals/enrichment or exception text could carry data. | AddNpgsql defaults; no parameter enrichment. | Keep command spans and safe defaults; focused listener checks email/password/token/hash absence in tags/events. Literal SQL requires future audit. |
| API/Program.cs / ASP.NET request instrumentation | HTTP trace fields | Method, route template, status and duration; framework URL fields | URLs/query strings can carry identifiers if callers misuse them. | No body/header enrichment. | Unchanged useful instrumentation; sensitive query parameters must not be introduced. External proxy logs remain outside test coverage. |
| API/Program.cs / AddJwtBearer | JWT library diagnostics | Token-validation reason from framework; PII hidden by library defaults | Custom PII/debug logging could expose tokens. | IncludeErrorDetails=false; no PII enablement. | Validation unchanged; credentials never added to application log state. |
| API/Program.cs / UseExceptionHandler | ASP.NET handled-exception diagnostics | Potential independent exception logs/events | A duplicate framework event could retain a 4xx message. | .NET 10 suppresses handled-exception diagnostics by default. | Default suppression retained; captured focused tests assert no Error for expected 4xx. |
| API/Filters/ValidationFilter.cs / OnActionExecutionAsync | dynamic validation aggregation | string.Join of validator errors | Future templates could interpolate submitted values. | Combines messages for client response; full exception previously logged. | Client semantics preserved; 4xx logs contain no aggregation/message. Stage 3 already fixed model-binding detail. |
| API/Handlers/GlobalExceptionHandler.cs / IsUniqueConstraintViolation | database exception inspection | Inner message read to identify 23505/duplicate key | Inner message could contain data. | Read only for classification; handler previously logged complete 409 exception. | Classification unchanged; message not retained on 409; client gets fixed constraint detail. |
| RondiTrack.Tests/TestSupport/CorrelationLogProvider.cs / Log | test provider | Correlation/category metadata only | No message, exception or payload capture. | Existing in-memory correlation assertions. | Unchanged; does not emit a production event. |
| RondiTrack.Tests/TestSupport/CapturedLogProvider.cs / Log | test provider | Formatted message, structured state, exception reference | Test memory can contain diagnostic data; never publish raw capture. | Absent. | Added in-memory capture for automated auditing; no sink or payload serialization. Stage 7 completes the end-to-end leak scenario. |

Sanitized structural examples (illustrations, not captured execution evidence):

```text
Before: Error Request failed ... BusinessRuleException: [business message + stack]
After: Information Request rejected with 409 (BusinessRuleException). CorrelationId: [W3C ID]
Before: Information Login succeeded. UserId: [identifier]
After: Information Security event sign_in with success. CorrelationId: [W3C ID]
Before: Warning Refresh reuse detected ... FamilyId: [identifier]
After: Warning Security event refresh_reuse; session revoked. CorrelationId: [W3C ID]
Before: no dedicated refusal event
After: Information Security event stokvel_refusal with not_found. CorrelationId: [W3C ID]
```

Full 5xx exceptions are intentionally retained. An unexpected third-party exception
may put sensitive values into its message, inner exception or stack/data; this is
not an unconditional guarantee that every possible diagnostic is anonymous.
Npgsql parameter values/row details are disabled, but database errors, raw literal
SQL introduced later and external proxy/debug logs need continuing review. EF
can independently log a failed command or SaveChanges event before the handler
classifies a database conflict; a concurrent database-originated 409 is not the
prechecked duplicate-registration case covered by these tests. Blanket suppression
would hide useful failures, so this residual framework diagnostic risk is recorded.
Correlation IDs are operational, linkable identifiers: access control, secure
transport, retention and incident response are still required for real sinks.
No actual identities, amounts or tokens are included in these examples. Historical
assignment evidence is preserved and predates the current minimization policy.

The complete registration/login/refresh/contribution/duplicate-registration
end-to-end leak scenario remains Stage 7. Dashboard evidence remains Stage 8.
