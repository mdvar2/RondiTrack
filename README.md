# RondiTrack

A .NET 10 API for managing stokvels, contributions and payouts.

**Latest verification (Assignment 6.3): 122 tests passed, 0 failed, 0 skipped.** The full suite also passed with development database connections temporarily disabled. Earlier assignment results remain below as historical evidence.

**Development migration blocker:** duplicate normalized emails prevent the existing
development database from applying the authentication migration. Do not update, reset or
recreate it to bypass the blocker. Its data is preserved. See [the migration
status](docs/development-migration-status.md).

## Assignment 6.3 — Ready for the Frontend (14 October 2026)

Assignment 6.3 prepares RondiTrack for frontend integration through versioned API routes, CORS, rate limiting, contribution idempotency, a personalized member home endpoint, consistent error responses, and generated TypeScript contracts.

**Verification results**

- **122 tests passed, 0 failed, 0 skipped.**
- Fresh solution build succeeded with **0 warnings and 0 errors**.
- Strict TypeScript compilation and exact-money runtime checks passed.
- Offline isolation verification passed: development database connections were temporarily disabled while the full test suite ran, using disposable Testcontainers databases for database-backed tests.
- The original development connection setting was restored, and `SELECT 1` succeeded afterward.
- No development data, schemas, or migrations were changed during verification.

See the [stage-by-stage trainer notes](docs/assignment-6-3-stages.md), [verification report](docs/assignment-6-3-verification.md), and [requirement compliance matrix](docs/assignment-6-3-compliance.md). The following sections describe the current v1 browser contract; earlier assignment evidence is identified as historical.

### Versioning and the refresh cookie

`API/Common/ApiRoutes.cs` defines `V1 = "api/v1"`. Every controller uses it; the cookie path derives from it as `/api/v1/auth`, covering refresh **and logout**. The moved families are `/api/v1/auth/{register,login,refresh,logout}`, `/api/v1/users`, `/api/v1/stokvels` (including memberships/contributions), nested cycles and payouts. The new screen is `/api/v1/me/home`. Existing JWT, role, resource authorization and refresh rotation/reuse detection remain intact. `VersionedRefreshTests` proves cookie-handler refresh and logout work at the new paths.

Health probes (`/health/live`, `/health/ready`) and Development documentation (`/openapi/v1.json`, `/scalar/v1`) retain their operational paths and contracts. This is an explicit interpretation of ?every route?: every controller API route is versioned, but changing probes/docs would break existing assertions the request requires us to preserve.

### CORS, origins, sites and cookies

`CorsPolicies.Frontend` reads `Cors:AllowedOrigins`. Production's appsettings list is empty. Development allows **exactly `http://localhost:3000`**. Deployment can configure exact entries with `Cors__AllowedOrigins__0`. Wildcards, arbitrary reflection and paths are refused. Credentials are enabled. Allowed methods: GET, POST, PUT, DELETE, OPTIONS. Allowed headers: Authorization, Content-Type, Idempotency-Key, If-Match, traceparent, tracestate. Exposed response headers: Retry-After, ETag, Location. There is no existing idempotency replay response header; replay returns the same 201 body. No tokens or financial information are added to headers.

CORS runs after routing and before authentication; it answers anonymous preflights before JWT fallback authorization. Allowed-origin scoped 403/404 errors retain CORS headers. Five CORS tests cover allowed/rejected preflight, both errors and closed production defaults. CORS controls what a browser can read; it does not replace authentication. See [ASP.NET Core CORS documentation](https://learn.microsoft.com/en-us/aspnet/core/security/cors?view=aspnetcore-10.0).

An **origin** is scheme + host + port. A **site** is scheme + registrable domain (ports do not matter). Cookies remain host-only, HttpOnly, Secure and SameSite=Strict. Browser calls that set/send a refresh cookie need `credentials: "include"`; cross-origin bearer-token calls also require the configured CORS permission. CORS permission cannot override SameSite or Secure. See [MDN's site definition](https://developer.mozilla.org/en-US/docs/Glossary/Site) and [cookie guidance](https://developer.mozilla.org/en-US/docs/Web/HTTP/Guides/Cookies).

| Deployment | Will the current Strict refresh cookie reach the API? |
|---|---|
| `http://localhost:3000` ? `http://localhost:5101` | Same site, different origins. Strict permits it if the API is actually served over HTTP without redirect and the browser accepts Secure cookies on localhost. That localhost exception varies by browser. The app's HTTPS redirection may redirect HTTP preflight, so do not assume this setup works merely from server integration tests. |
| `http://localhost:3000` ? `https://localhost:7125` (existing HTTPS launch profile) | Different scheme means different site. Strict blocks the cookie on cross-site fetch; credentials and CORS cannot fix it. Use a same-scheme frontend/API deployment (with the exact frontend origin explicitly configured), or an intentionally configured same-origin development proxy. Current default allowed origin remains exactly HTTP localhost:3000. |
| `https://app.ronditrack.co.za` ? `https://api.ronditrack.co.za` | Same HTTPS site, different origins. Strict permits a host-only API cookie to reach the API with credentials; configure that exact frontend origin and use trusted HTTPS. It need not be shared with the app subdomain. |
| `https://ronditrack.vercel.app` ? `https://ronditrack-api.azurewebsites.net` | Different sites. The current Strict cookie is not sent; login's bearer response may be readable once the exact frontend origin is allowed, but refresh will fail. Cross-site cookies would need an explicit design change to SameSite=None; Secure, CSRF protection and third-party-cookie restrictions must also be addressed. Prefer a shared-site custom domain. |

These are browser-rule analyses, not claims of a browser deployment test. The cookie-handler integration test proves path/rotation behavior, not SameSite enforcement by a real browser.

### Rate limits and idempotency

All names/rules are central in `RateLimitPolicies`; controllers reference policy names only. Configuration in appsettings:

| Policy | Algorithm | Partition | Default |
|---|---|---|---|
| Login | Fixed window | Direct remote client IP; missing IP shares `unknown` | 5 requests / 60 seconds |
| Contributions | Token bucket | Authenticated JWT `sub` (stable user GUID) | Capacity 30; replenish 30 every 60 seconds |
| Payouts | Separate token bucket | Authenticated JWT `sub` | Capacity 5; replenish 5 every 60 seconds |

All settings must be positive; bucket refill cannot exceed capacity. Queue limits are zero. Failed endpoint attempts and replays count too. Anonymous financial calls reach the existing 401 policy without reserving a user allowance. No untrusted forwarded IP header is accepted; deploying behind a proxy requires explicitly configuring trusted proxies before using forwarded addresses. Token buckets permit a treasurer's approximately twenty post-meeting contribution entries as a burst while bounding later traffic. Shared Wi-Fi users have separate financial quotas because identity, rather than venue IP, partitions money writes. Login has no authenticated identity yet, so its IP limit can affect users sharing Wi-Fi.

Middleware order: exception handler ? status-code pages ? HTTPS redirection ? routing ? **CORS ? authentication ? rate limiting ? authorization** ? controller validation/resource authorization ? **ContributionService idempotency check and write**. CORS stops preflight before fallback authorization. Authentication establishes sub before financial partitioning. Rate limiting stops a rejected request before idempotency can inspect/reserve/store the key or write money. HTTPS preflight redirects are a deployment consideration described above.

Every rejection returns 429 (never rate-limiter 503), application/problem+json, a positive integer-seconds Retry-After rounded upward, title/detail/instance and the existing 6.2 W3C correlationId. Retry-After is exposed to JavaScript. Replays also obey the limiter: a replay can be delayed with 429, but the existing stored response remains available. In the short-window test, two fresh permits replenish, allowing the successful retry with K followed by its replay. The test directly proves K absent and no contribution after rejection, waits the returned Retry-After, then confirms identical success/replay bodies and **one** PostgreSQL row. Separate isolated hosts test login, contribution/payout quotas and two users. Each rate-limit test uses WithWebHostBuilder/UseSetting; the shared fixture receives a generous login allowance.

The built-in limiter is **in memory per API process**. Three balanced replicas can grant up to three times these limits, and a restart resets them. Global enforcement needs shared coordination; it is not provided here. The preserved idempotency store is also per process, so three replicas/restarts do not guarantee stored-response replay. The existing database uniqueness constraint still prevents duplicate member/cycle contribution rows, but those retries may get 409. Distributed idempotency is an existing production gap. See [ASP.NET Core rate limiting](https://learn.microsoft.com/en-us/aspnet/core/performance/rate-limit?view=aspnetcore-10.0).

### Home-screen contract

`MeController` takes the caller from JWT sub, and `MemberHomeService` returns one `HomeResponse`. It projects memberships in one SQL query and current-month cycles with **only that user's** contribution totals in a second query (one query for empty memberships); there is no N+1 fetching. A fixed TimeProvider integration test proves date independence and roles in two stokvels, with an additional no-cycle membership.

```json
{
  "stokvels": [
    {
      "stokvelName": "Example savings group",
      "role": "Member",
      "currentCycle": { "period": "2026-10", "status": "Open" },
      "amountPaid": "125.1",
      "amountOwed": "374.9",
      "nextPayoutDate": null
    }
  ]
}
```

These six fields fill the requested screen. No identifiers, versions, tokens or other members' amounts are returned. Role comes from each membership, not a platform claim. Status is the existing name (Open/PaidOut). Amounts are invariant **decimal strings**, preserving the domain's full precision without rounding to cents or exceeding JavaScript safe integers. They represent currency units, not integer cents. The generated TypeScript function adds them exactly using BigInt with a common decimal scale; it returns a decimal string.

Documented presentation assumptions: current cycle matches `TimeProvider.GetUtcNow()` formatted as `yyyy-MM`, including a PaidOut cycle in the current month; historical duplicate periods prefer Open then stable ID. The member's obligation is `Stokvel.ContributionAmount`, not the cycle's possibly group-level TargetAmount. Owed = max(obligation ? own paid, 0). No current cycle means currentCycle=null and both amounts="0". A user with no memberships receives `{ "stokvels": [] }`, not 404.

**Missing business rule:** entities/services store past payouts but no future payout schedule. Therefore nextPayoutDate is null rather than an invented deadline. The DTO is nullable DateTimeOffset and its OpenAPI format is date-time; any future supplied date must be UTC. A real next payout date requires a confirmed scheduling rule. This requirement is PARTIAL.

### Complete application error contract

Every controller/application error uses `application/problem+json` with this exact field structure (values vary by request/status):

```json
{"type":"about:blank","title":"Not Found","status":404,"detail":"Stokvel not found.","instance":"/api/v1/stokvels/...","correlationId":"<32 lowercase hexadecimal characters>"}
```

FluentValidation adds exactly one field, `errors`, mapping camelCase request names to arrays of messages. Existing detail messages are retained for compatibility:

```json
{"type":"about:blank","title":"Bad Request","status":400,"detail":"<existing joined validation messages>","instance":"/api/v1/stokvels","correlationId":"<32 lowercase hexadecimal characters>","errors":{"name":["Stokvel name is required."],"contributionAmount":["Contribution amount must be greater than zero."]}}
```

| Status/title | Source | Detail and exact shape variation |
|---|---|---|
| 400 Bad Request | FluentValidation | Base structure + errors map; detail is the existing joined safe messages. |
| 400 Bad Request | JSON binding, malformed JSON, quoted numbers | Base only; `The request body is invalid.` No internal type or submitted-value echo. |
| 400 Bad Request | Missing/invalid Idempotency-Key, If-Match, pagination/token/sort input | Base only; safe RequestValidationException message. |
| 401 Unauthorized | Missing/invalid/expired JWT; deleted account; login/refresh authentication failure | Base only; `Authentication is required.` from fallback, or the existing safe authentication-domain message. |
| 403 Forbidden | Platform/resource policy denies a member's action | Base only; `You do not have permission for this operation.` |
| 404 Not Found | Missing resource, non-member scoped resource concealment, unknown route for an authenticated caller | Base only; safe domain not-found message, or `The requested endpoint was not found.` Unknown anonymous routes hit JWT fallback first and may return 401. |
| 405 Method Not Allowed | Unsupported method on a matched route | Base only; `The HTTP method is not allowed for this endpoint.` HTTP Allow header may also be supplied. |
| 409 Conflict | Business rule, duplicate-email/member-cycle/payout uniqueness, concurrency conflict, key reuse with different body | Base only; safe domain message; database uniqueness uses `A database constraint was violated.` Unexpected concurrency details remain generic. |
| 412 Precondition Failed | Stale ETag/If-Match | Base only; existing safe ?refetch and retry? domain detail. Missing If-Match is 400. |
| 415 Unsupported Media Type | MVC input formatter does not support Content-Type | Base only; `The request content type is not supported.` Shared customization normalizes framework type/detail/instance and removes traceId. |
| 429 Too Many Requests | Any named rate-limit policy | Base only; `Too many requests. Try again in N seconds.` Positive Retry-After: N header. |
| 500 Internal Server Error | Unexpected exception, including unexpected database errors | Base only; `An unexpected error occurred.` No exception type/message/stack in the response. |
| 503 Service Unavailable | Preserved `/health/ready` database probe, outside controller contract | `text/plain; charset=utf-8`, exact body `Unhealthy`; preserved existing operational assertions. This is not rate-limit rejection. |

No 422 is defined by this application (the Matric Compass example's status is not copied). Empty additional 4xx/5xx statuses generated inside the application pipeline receive the same base structure with the HTTP reason phrase and generic detail; 5xx always receive the fixed safe detail. Web-server/proxy failures before the pipeline may have their own protocol contract. The health exception and operational URL interpretation are explicit gaps against an unrestricted reading of ?every error/every route?. Existing telemetry privacy/correlation tests pass.

Both MVC and HTTP/OpenAPI JSON options use JsonNumberHandling.Strict. Quoted JSON numbers are 400; integers/decimals in the **actual generated** OpenAPI document are numeric-only. Home money strings are a deliberate separate contract, not a numeric/string union. Eight error tests cover forms, malformed/invalid JSON, routing/method errors, unsupported media type and actual schemas.

### TypeScript contracts and actual rename experiment

Tooling lives outside all .NET projects in `clients/typescript`, with exact versions openapi-typescript 7.13.0 and TypeScript 5.9.3, plus package-lock.json. The actual OpenAPI snapshot, generated schema.d.ts and typed function are committed. `scripts/contracts` reuses the Testcontainers fixture to export the real API document; no new NuGet package or development database is needed.

```powershell
cd clients/typescript
npm ci --ignore-scripts
npm run regenerate
npm run test:money
```

`regenerate` exports ? generates ? runs `tsc --noEmit --strict`. Before rename: exit 0. Temporarily renamed **C# AmountOwed ? OutstandingBalance**, built the API, exported its actual document and regenerated types. The unchanged function failed with actual compiler output, exit 2:

```text
src/home.ts(7,56): error TS2339: Property 'amountOwed' does not exist on type '{ stokvelName: string; role: string; currentCycle: { period: string; status: string; } | null; amountPaid: string; outstandingBalance: string; nextPayoutDate: string | null; }'.
```

Restored AmountOwed, rebuilt, exported and regenerated; strict compilation exit 0. The first saved-file restore retained an old timestamp and reused a renamed DLL; refreshing the timestamp rebuilt it. The final generated document/types include amountOwed and no outstandingBalance. Full reproduction and real recovery details: [client README](clients/typescript/README.md). No test assertion was changed for the experiment.

### Final verification and submission gaps

```powershell
dotnet build RondiTrack.sln --no-restore -m:1
dotnet test RondiTrack.sln --no-restore -m:1 --logger "trx;LogFileName=final.trx" --results-directory obj/assignment-6-3
```

Final actual full-suite result: **122 passed, 0 failed, 0 skipped**, 34 seconds. Actual API contracts regenerated and strictly compiled; exact-money checks passed. Existing 28 test-source files were compared to pre-assignment a733cc6: no differences beyond required URL changes. No tests were silently skipped or assertions weakened. Stage commits and the original baseline Docker failure are recorded in [trainer notes](docs/assignment-6-3-stages.md).

**Offline proof passed on 14 October 2026 at 06:26 SAST.** The approved guarded script freshly built the solution, required zero active development sessions, disabled connections to local `ronditrack`, proved real connection refusal, and ran all 122 tests using Testcontainers with zero failures/skips. It restored the original `ALLOW_CONNECTIONS=true`, checked admission metadata and verified `SELECT 1`. See [the actual run evidence and procedure](docs/assignment-6-3-verification.md#successful-offline-verification).

Remaining gaps: no real payout schedule; current-cycle/obligation assumptions need domain confirmation; health/docs preserved operational contracts; no real-browser deployment verification; process-local limiting and idempotency; existing duplicate-email development migration blocker remains untouched. Assignment 6.3 implementation and offline-verification evidence are published on `main`. Submission repository remains https://github.com/mdvar2/RondiTrack.

## Contents

1. [Project overview](#1-project-overview)
2. [Technologies used](#2-technologies-used)
3. [Project architecture](#3-project-architecture)
4. [Setup and running the API](#4-setup-and-running-the-api)
5. [Features and API functionality](#5-features-and-api-functionality)
6. [Assignments and learning journey](#6-assignments-and-learning-journey)
7. [Authentication and authorization](#7-authentication-and-authorization)
8. [Database and migrations](#8-database-and-migrations)
9. [OpenTelemetry tracing and metrics](#9-opentelemetry-tracing-and-metrics)
10. [POPIA-compliant logging](#10-popia-compliant-logging)
11. [Health checks](#11-health-checks)
12. [Testing and verification](#12-testing-and-verification)
13. [Challenges and future improvements](#13-challenges-and-future-improvements)
14. [Detailed assignment evidence](#14-detailed-assignment-evidence)

The [complete 181-entry audit](#appendix-a-complete-popia-logging-audit) remains in this
README's appendix.

## 1. Project overview

I built RondiTrack through my Bitcube software development assignments. It is a backend API
for a stokvel: a group whose members contribute money and receive payouts according to the
group's rules.

I implemented users, membership, contribution cycles, contributions and payouts. I then added
authentication, authorization and observability while preserving the existing business rules
and tests. There is no frontend; Development provides an interactive API reference.

## 2. Technologies used

| Technology | What I used it for |
|---|---|
| .NET 10 / ASP.NET Core | HTTP API and request handling. |
| EF Core 10 | Maps .NET objects to database tables. |
| Npgsql | Connects .NET to PostgreSQL. |
| PostgreSQL 17 | Persistent application data. |
| FluentValidation | Checking request input. |
| JWT bearer authentication | Authenticating protected requests. |
| OpenTelemetry / Aspire | Traces, metrics, logs and local inspection. |
| xUnit / Testcontainers | Tests with disposable PostgreSQL. |
| OpenAPI / Scalar | Development API documentation. |
| .NET User Secrets | Local configuration outside Git. |

Project files are the source of truth for versions. EF Core and its health-check package are
10.0.4; the Npgsql provider and tracing package are 10.0.3. OpenTelemetry Hosting/OTLP are
1.19.1; ASP.NET instrumentation is 1.19.0. See [the architecture guide](docs/architecture.md)
for remaining versions.

## 3. Project architecture

I separated HTTP concerns, domain rules and database access into clear projects. **Dependency
injection** supplies a class with the services it needs.

- **API:** controllers, request/response models, validation, services and security.
- **Domain:** entities, exceptions, repository interfaces and query records.
- **Infrastructure:** EF context, database repositories and migrations.
- **RondiTrack.Tests:** business, regression, security and integration tests.

Controllers receive requests; services own operations such as payouts; repositories provide
database access for most operations. I retained the payout service's direct context and
explicit **transaction**, which commits or rolls back the payout and cycle update together.

See [architecture and service lifetimes](docs/architecture.md) for dependencies and the
contribution request flow.

## 4. Setup and running the API

Run PowerShell from the repository root. Development uses PostgreSQL 17 as a Windows service.
Tests use Docker Desktop with disposable PostgreSQL containers.

### Step 1: Install prerequisites and build

Install Git, .NET 10 SDK and PostgreSQL 17 for development. Docker Desktop needs
Linux-container mode for tests. EF CLI is needed for migration commands.

```powershell
git clone https://github.com/mdvar2/RondiTrack.git
cd RondiTrack
dotnet --version
dotnet restore RondiTrack.sln
dotnet build RondiTrack.sln --no-restore -m:1
```

Use the existing checkout if already cloned. `-m:1` builds with one MSBuild node, as verified
in the restricted environment. Building does not migrate a database or require PostgreSQL to
be running.

### Step 2: Check PostgreSQL

```powershell
Get-Service -Name 'postgresql*'
& 'C:\Program Files\PostgreSQL\17\bin\psql.exe' -h localhost -U postgres -W -d postgres
```

Adjust the executable path for your installation. The verified service was
`postgresql-x64-17`. The interactive prompt avoids printing credentials. For a **new, empty
environment only**, create the database in `psql`:

```sql
CREATE DATABASE ronditrack;
```

Keep the existing development database intact; do not recreate it to bypass the migration
blocker.

### Step 3: Configure local secrets

The [API project](API/RondiTrack.csproj) already defines `UserSecretsId`. Do not replace it or
overwrite existing secrets. User Secrets keep configuration out of Git, but are not encrypted
production secret storage.

On a new machine, enter a connection string with this placeholder shape:

```text
Host=localhost;Port=5432;Database=ronditrack;Username=postgres;Password=<LOCAL_DB_PASSWORD>
```

This passes JSON through standard input rather than putting the value in shell history:

```powershell
$connectionInput = Read-Host 'Development connection string' -AsSecureString
$connectionValue = [System.Net.NetworkCredential]::new('', $connectionInput).Password
@{ 'ConnectionStrings:RondiTrackDb' = $connectionValue } |
    ConvertTo-Json -Compress |
    dotnet user-secrets --project API set
Remove-Variable connectionInput, connectionValue
```

For a **new signing key only**, generate 32 random bytes without printing them:

```powershell
$jwtBytes = New-Object byte[] 32
$jwtGenerator = [System.Security.Cryptography.RandomNumberGenerator]::Create()
$jwtGenerator.GetBytes($jwtBytes)
$jwtGenerator.Dispose()
@{ 'Jwt:SigningKey' = [Convert]::ToBase64String($jwtBytes) } |
    ConvertTo-Json -Compress |
    dotnet user-secrets --project API set
[Array]::Clear($jwtBytes, 0, $jwtBytes.Length)
Remove-Variable jwtBytes, jwtGenerator
```

Changing an existing signing key invalidates issued access tokens. Never publish secret files,
passwords, tokens, private connection strings or database backups.

### Step 4: Inspect migrations before updating

Follow [Database and migrations](#8-database-and-migrations). The existing development
authentication schema is **not ready**. Resolve its blocker only through a separately approved
plan. A new empty database can apply the full chain.

### Step 5: Run over HTTPS

After secrets and an appropriate schema are ready:

```powershell
dotnet dev-certs https --trust
dotnet run --project API --launch-profile https
```

The [launch settings](API/Properties/launchSettings.json) use `https://localhost:7125` and
`http://localhost:5101` in Development. Use HTTPS for the full session flow: the refresh
cookie is `Secure`. HTTPS redirection is enabled.

Anonymous Development-only documentation is available at:

- `https://localhost:7125/scalar/v1`
- `https://localhost:7125/openapi/v1.json`

These pages do not prove that the database schema is current.

## 5. Features and API functionality

I implemented:

- User and stokvel management, subject to permissions.
- Membership and contribution-cycle management.
- Contribution recording with duplicate and retry protection.
- Payout selection, persistence and cycle status updates.
- Paged member/contribution queries with validated sorting and filtering.

**Idempotency** lets a caller retry the same contribution request with the same
`Idempotency-Key` and receive its original result. A different request with that key returns
409. The store is in memory and is lost on process restart.

An **ETag** represents a record's version. User, stokvel and cycle GET-by-ID responses return
it. PUT requires `If-Match` to avoid overwriting newer data; DELETE does not require it.

There is no standalone contribution CRUD/delete route, payout-list route, public
role-management API or password-reset flow. The [API reference](docs/api-reference.md) lists
all routes, inputs, status codes, pagination and business contracts.

## 6. Assignments and learning journey

My assignments built on each other. The [historical README](README-history.md) preserves the
earlier explanations and experiments.

- **Early API work:** I built the request, validation and business-rule structure.
- **Assignment 5.2:** I worked on relationships and compared loading approaches
  to understand the cost of fetching related rows one at a time.
- **Assignment 5.3:** I added database guards, pagination and concurrency checks,
  and recorded an index/query-plan experiment.
- **Assignment 5.4:** I separated API, Domain and Infrastructure, checked service
  lifetimes and introduced self-contained PostgreSQL integration tests.
- **Assignment 6.1:** I implemented JWT authentication, refresh-token rotation
  and current membership/role checks.
- **Assignment 6.2:** I added correlated errors, traces, bounded counters, safer
  logs and health checks, then verified them through tests and a real dashboard.

The observability work helped me distinguish event logs, aggregate measurements and the timed
steps of a request. It also required treating financial telemetry as data that needs privacy
protection. Earlier test counts belong to their stages, not the current total; see [the
evidence index](#14-detailed-assignment-evidence).

## 7. Authentication and authorization

**Authentication** establishes who the caller is. **Authorization** decides whether that
caller can perform an operation.

I implemented anonymous registration, login, refresh and logout under `/api/v1/auth` (previously `/api/auth` before Assignment 6.3).
Registration requires at least 12 password characters, stores a password hash and does not
accept a privileged role.

A **JWT** is a signed access token. Protected requests send:

```text
Authorization: Bearer <ACCESS_TOKEN>
```

Tokens use HS256. Validation checks signature, algorithm, issuer, audience and expiration, and
rejects deleted users. The configured lifetime is five minutes plus 30 seconds of clock
allowance. JWTs contain no email or role claims; current platform and stokvel roles are read
from the database.

Refresh tokens are separate random values, sent only in the `__Secure-ronditrack_refresh`
cookie with `HttpOnly`, `Secure` and `SameSite=Strict`. Only SHA-256 hashes are stored.
Refresh rotates the token; replay revokes that login family. Logout revokes it and removes the
cookie, but already issued access tokens remain valid until short expiry.

### Membership rules

| Situation | Response |
|---|---|
| Missing or invalid authentication | 401 Unauthorized |
| Authenticated, insufficient permission | 403 Forbidden |
| Not a member of the requested stokvel | 404 Not Found |

The fallback policy requires authentication on unannotated routes. Platform Admin can perform
global administration but **cannot bypass stokvel membership**. Treasurer/Chairperson can
manage their stokvel; Member can view it and record their own contribution. A new stokvel
gives its creator Chairperson membership. Added members default to Member.

I preserved these decisions while adding telemetry. Unknown-account and wrong-password
failures retain the same safe detail. Their existing equality test supplies a shared caller
trace and keeps its byte-for-byte assertion.

See [the security/API reference](docs/api-reference.md) for token claims, cookie settings,
refresh locking, validation limits and route permissions.

## 8. Database and migrations

I preserved all seven migration IDs. Development was last verified with the first four
applied. The final three remain unapplied there; all seven applied to disposable test
databases.

| Migration | Purpose |
|---|---|
| `20261001040953_InitialCreate` | Initial schema. |
| `20261004202213_ConfigureRelationshipsAndQueryModel` | Relationships/query model. |
| `20261005211608_AddDatabaseDefenseConstraints` | Uniqueness guards. |
| `20261006014557_AddContributionPaginationIndex` | Contribution query index. |
| `20261011174110_AddUserAuthentication` | Password hashes and unique email. |
| `20261011174943_AddRefreshTokenSessions` | Tokens and login families. |
| `20261011175640_AddPlatformRole` | Ordinary platform-role default. |

**Do not apply pending migrations to the existing development database.** Duplicate normalized
emails block the unique-email migration. Authentication needs the new schema, even if
connectivity is Healthy. See [the blocker](docs/development-migration-status.md).

Check `dotnet ef --version`. If the CLI is absent, install the matching EF tool:

```powershell
dotnet tool install --global dotnet-ef --version 10.0.4
```

Configure API secrets before using startup-project tools:

```powershell
dotnet ef migrations list --project Infrastructure --startup-project API --no-connect
dotnet ef migrations has-pending-model-changes --project Infrastructure --startup-project API
```

`--no-connect` lists files, not applied database status. Omitting it queries migration history
without applying changes. The model check compares the code model with its snapshot, not the
database.

**Only for a new empty database, or after an approved blocker resolution:**

```powershell
dotnet ef database update --project Infrastructure --startup-project API
```

The API does not auto-migrate. Creating migrations is not routine setup. Private exports are
absent from `main`; their local backup must remain local.

## 9. OpenTelemetry tracing and metrics

I implemented OpenTelemetry with service name **`ronditrack-api`**:

- **Traces:** timed steps in a request; each step is a **span**.
- **Metrics:** numeric measurements, such as operation counts.
- **Logs:** individual events and diagnostic messages.

ASP.NET Core request traces exclude `/health` and its child paths. Npgsql creates
database-command spans. ASP.NET Core and `System.Runtime` supply framework metrics.

### Conditional OTLP export and correlation

**OTLP** is the protocol used to send telemetry to a collector or dashboard. Export is enabled
only when `OTEL_EXPORTER_OTLP_ENDPOINT` is nonblank. No endpoint is hardcoded in
application/launch settings. Tests and CI do not require Aspire.

I added a helper that gives every existing ProblemDetails error response a `correlationId`
equal to the request's 32-character W3C trace ID. ProblemDetails is the standard JSON error
format. A valid caller `traceparent` preserves that trace ID; otherwise ASP.NET Core generates
one.

The corresponding error log carries the same structured `CorrelationId`. This covers
application errors, framework 401/403, non-member 404 and login failures. Binding errors have
fixed details; unexpected 500 responses stay generic.

### Payout tracing and safe tags

I wrapped payout processing in **`payout.process`** from the registered ActivitySource
**`RondiTrack.Business`**. ActivitySource creates named spans. The Meter, which creates
measurements, also uses **`RondiTrack.Business`**.

```text
HTTP request
  -> payout.process
       -> Npgsql SQL command spans
```

The business span surrounds the existing EF retry strategy. Success is counted after the
transaction commits and the strategy returns; retry attempts do not count separately. Terminal
failure sets status `Error` without an exception description. Business rules, transaction
boundaries and rollback stay unchanged.

Every custom payout tag is listed below:

| Tag | Allowed values | Maximum distinct values |
|---|---|---:|
| `outcome` | `success`, `failure` | 2 |

I chose `outcome` to separate completed payouts from failures and find their SQL timeline. It
contains no identity or financial value. There are no custom events, exception messages,
amounts, names or resource/person IDs. SDK resource fields and trace/span IDs are separate
metadata.

### Genuine Aspire evidence

I verified actual traces, structured login logs and the payout success metric in a local
Aspire dashboard. This authorized payout used disposable data; the screenshot is a direct Edge
capture without image or page alterations.

![Genuine HTTP payout trace with nested business and SQL
spans](docs/evidence/payout-trace.png)

The image shows 14 spans, depth 3: HTTP, the business child, nine SQL children beneath it and
three earlier security SQL spans beside it. The business span has only `outcome=success` and
Source `RondiTrack.Business`. These individual development timings are not a benchmark.

See [the safe manifest](docs/evidence/dashboard-verification.json) and [the exact
procedure/image digest](docs/assignment-6-2-stage-8-verification.md).

### Local dashboard setup

1. Start Docker Desktop with Linux containers.
2. Start Aspire with loopback-only published ports:

   ```powershell
   docker run --rm -d --name ronditrack-assignment62-dashboard `
       -p 127.0.0.1:18888:18888 `
       -p 127.0.0.1:4317:18889 `
       -e DOTNET_DASHBOARD_UNSECURED_ALLOW_ANONYMOUS=true `
       -e ASPIRE_DASHBOARD_UNSECURED_ALLOW_ANONYMOUS=true `
       mcr.microsoft.com/dotnet/aspire-dashboard:13
   ```

   **This anonymous dashboard is for a trusted laptop only. Never expose it to    a LAN/public
interface or describe it as deployable.** The UI is    `http://127.0.0.1:18888`; OTLP/gRPC
uses `http://127.0.0.1:4317`.

3. After building the solution, generate isolated evidence with the optional runner:

   ```powershell
   dotnet restore scripts/evidence/EvidenceHost.csproj
   dotnet build scripts/evidence/EvidenceHost.csproj --no-restore -m:1
   Remove-Item -LiteralPath obj/stage8-evidence/stop -ErrorAction SilentlyContinue
   dotnet scripts/evidence/bin/Debug/net10.0/EvidenceHost.dll
   ```

   It configures OTLP and random private credentials only for its child API,    and migrates
only its own disposable PostgreSQL database. It is outside the    solution and reuses
Testcontainers 4.15.0, with no dashboard test dependency.

4. After `REAL_API_EVIDENCE_READY`, inspect Traces, Structured Logs and Metrics.
   With Python Playwright and Windows Edge installed, run the optional capture
   command in another terminal:

   ```powershell
   python scripts/evidence/capture-dashboard.py
   ```

   Inspect generated artifacts before publishing them. New runs have new trace    IDs. The
runner waits up to ten minutes and disposes its API/database; its    separate development
snapshot mode is read-only.

5. Stop the temporary tools:

   ```powershell
   New-Item -ItemType File -Path obj/stage8-evidence/stop -Force | Out-Null
   docker stop ronditrack-assignment62-dashboard
   ```

### Business counters and cardinality

**Cardinality** is the number of distinct tag values or combinations. I kept custom tags in
fixed sets, without identifiers, user-supplied strings or amounts.

| Counter | Purpose |
|---|---|
| `ronditrack.contributions.recorded` | New persisted contributions. |
| `ronditrack.payouts.processed` | Completed payouts by outcome. |
| `ronditrack.authorization.refusals` | Stokvel access refusals. |

| Counter | Tag | Allowed values | Distinct values / series |
|---|---|---|---|
| Contributions | None | None | 0 / 1 |
| Payouts | `outcome` | `success`, `failure` | 2 / 2 |
| Refusals | `result` | `not_found`, `forbidden` | 2 / 2 |

Limits apply per instrument/resource; instances and backend labels can add series.

- `ContributionService` increments after successful persistence, before storing
  its idempotency response. Replay returns earlier without counting again.
- `PayoutService` counts once after retry completion: committed `success` or
  terminal `failure`, never individual transaction attempts.
- `StokvelAuthorization` counts before its 404/403 refusal. Framework/global policy
  refusals are not stokvel measurements. Controllers emit no spans or counters.

A chairperson asking how many payouts **their stokvel** processed this year needs an
authorized database query filtered by stokvel/date. Committed records form the ledger. Metrics
omit stokvel IDs, reset with the process, can lose exports and may have different retention;
they cannot provide that authoritative count.

## 10. POPIA-compliant logging

POPIA is South Africa's Protection of Personal Information Act. Financial history can be
personal information. I treated telemetry as another data store because it can be retained,
copied and joined to other information.

| Event | Level | Application fields |
|---|---|---|
| Expected application 4xx | Information | Type, status and correlation ID. |
| Unexpected 5xx | Error | Full internal exception; generic client detail/ID. |
| Sign-in/session events and refusals | Information | Fixed event/outcome and ID. |
| Refresh-token reuse | Warning | Fixed event and ID; no session identity. |

I removed user/family IDs from security logs. Failed sign-ins do not log the typed email.
Expected 4xx logs omit exception messages/objects. Refresh reuse is Warning because it may
indicate token theft. Framework refusals have no application exception type; they log status
and correlation.

### EF command levels

- **Development: Information**, to inspect queries and identify repetition.
- **Base/Production: Warning**, to reduce routine noise, storage and exposure
  while retaining warning/error diagnostics.

There is one EF `ILogger` command path and no `LogTo`. Console and enabled OTLP can receive
the same event as separate destinations. Sensitive EF logging is not enabled. Npgsql
`LogParameters` and `IncludeErrorDetail` are forced false.

### Genuine sanitized logs

These lines were selected from the actual Stage 8 capture:

```text
Security event sign_in with success. CorrelationId: dedc92c0ec68cfab9a111f982c129e4b
Security event stokvel_refusal with not_found. CorrelationId: bafaae4972b0b8bf5cecc0a82143153d
Request rejected with 404 (NotFoundException). CorrelationId: bafaae4972b0b8bf5cecc0a82143153d
```

The payout excerpt is a genuine EF command from its request window:

```text
Executed DbCommand (3ms) [Parameters=[@stokvelId='?' (DbType = Guid)], CommandType='Text', CommandTimeout='30']
```

```sql
SELECT p."RecipientUserId"
FROM "Payouts" AS p
WHERE p."StokvelId" = @stokvelId
```

The `?` is the original provider redaction. Column names are not actual IDs. No payout-success
log was invented: its span/counter provide the outcome. The excerpt alone does not prove an
SQL correlation property; the trace and listener tests establish that association.

The [full excerpts](docs/evidence/sanitized-log-excerpts.txt) and [capture
procedure](docs/assignment-6-2-stage-8-verification.md) contain no actual names, emails,
passwords, tokens or financial fixture values.

### Complete audit and privacy limits

The [appendix](#appendix-a-complete-popia-logging-audit) retains 181 original audit entries
and all their fields. Later verification-only sites are in the [supplementary
inventory](docs/assignment-6-2-supplementary-diagnostics.md).

Full unexpected 5xx exceptions may contain unforeseen sensitive diagnostics. EF can
independently log failed commands/saves before the handler classifies a concurrent database
conflict. The tested prechecked duplicate 409 does not prove that every database-originated
409 is free of framework Error events.

Future literal SQL, URL fields, scopes, proxy/debug logs and third-party diagnostics need
continuing review. Correlation IDs are linkable operational identifiers; real sinks need
access controls, secure transport, retention and incident response.

## 11. Health checks

I separated **liveness** (can the API respond?) from **readiness** (can it reach PostgreSQL?).
A database outage should remove an instance from traffic, rather than repeatedly restart an
API that cannot repair the database.

| Anonymous endpoint | Healthy | Database unavailable |
|---|---|---|
| `GET /health/live` | 200 `Healthy` | 200 `Healthy` |
| `GET /health/ready` | 200 `Healthy` | 503 `Unhealthy` |

Bodies are exact plain text with no newline or internal diagnostics. Both routes short-circuit
before authentication/authorization; even a bearer token cannot introduce a database query
into liveness. Health HTTP traces remain excluded.

Readiness uses `AddDbContextCheck` and default `Database.CanConnectAsync(cancellationToken)`.
Its **three-second timeout** caps the overall check. Existing EF retries remain **three
retries after the first attempt**, with a **maximum delay of two seconds** per retry delay.
Provider connection/command settings stay unchanged. Cancellation is cooperative, so
scheduling/response overhead can add time beyond the deadline.

### Actual outage measurements

I recorded these single samples on **13 October 2026** using a separate PostgreSQL 17
container stopped/restarted while the same test API host ran:

| Measurement | Response | Duration |
|---|---|---:|
| Readiness before outage | 200 `Healthy` | 10.06 ms |
| Readiness during outage, capped | 503 `Unhealthy` | 3,026.64 ms |
| Liveness during outage | 200 `Healthy` | 2.30 ms |
| Readiness during outage, test-local cap removed | 503 `Unhealthy` | 11,280.96 ms |
| Readiness after restart | 200 `Healthy` | 17.50 ms |

Without the cap, connection attempts/retry delays accumulated. The test restored the
three-second cap before recovery. A stable port kept the endpoint unchanged. Order was
healthy, outage liveness, capped, uncapped, recovery.

TestServer calls exercised real Docker/database connectivity, excluding external network/TLS
and container start/stop time. They are not benchmarks. Development and the shared suite
database were not stopped. Connectivity health does not verify schema upgrades or business
data.

See [the original timing values](docs/assignment-6-2-stage-6-health-timings.json) and [health
verification](docs/assignment-6-2-stage-6-verification.md).

## 12. Testing and verification

**Current Assignment 6.3 verification:** 122 tests passed, 0 failed, 0 skipped, including the guarded development-database-offline run on 14 October 2026. The fresh solution build completed with 0 warnings and 0 errors; development database access was restored and verified. The earlier Assignment 6.2 suite passed 101 tests, as documented in the historical evidence.

### Run the ordinary suite

1. Start Docker Desktop in Linux-container mode.
2. Confirm the engine, restore/build and run:

   ```powershell
   docker info
   dotnet restore RondiTrack.sln
   dotnet build RondiTrack.sln --no-restore -m:1
   dotnet test RondiTrack.Tests/RondiTrack.Tests.csproj --no-build --no-restore -m:1
   ```

The fixture creates disposable PostgreSQL with random credentials and a separate signing key.
It overrides the early-read connection setting, verifies the API's actual target, applies
seven migrations there and disposes the host/container. Development secrets/PostgreSQL are not
prerequisites. Do not redirect tests to development to bypass Docker errors. If Docker is
absent from PATH, use its installed path or reopen the configured terminal.

### What I verified

- Caller trace IDs on anonymous 401/non-member 404 and matching structured logs.
- Real payout/Npgsql activities, safe tags and failed-span Error status.
- Real counter measurements, retry completion and contribution replay.
- Anonymous health routes, isolated outage, bounded failure and recovery.
- Registration, login, refresh, contribution recording and duplicate 409.

The leak test checks messages, nested structured properties, exception text/data and
rendered/structured scopes for seeded email, password, names, both token generations, hashes,
signing-key representations and the distinctive amount. Raw fixture values are not published.

It requires actual security events, EF command logs, nonempty scopes and a persisted
contribution. A positive control verifies the detector; the duplicate 409 window allows no
Error/Critical entry. The payout test checks SDK subscription before adding a listener,
preventing a listener-created false positive.

Original assertions remain intact, including login equality. Framework scopes can carry
route/request IDs; the proof covers seeded values, not universal anonymity.

### Final development isolation run

These were the actual verification commands:

```powershell
dotnet clean RondiTrack.sln -m:1
dotnet build RondiTrack.sln --no-restore -m:1
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/verify-assignment-6-2-isolation.ps1
```

I disabled only development connection admission (`ALLOW_CONNECTIONS=false`), proved a real
connection was rejected, and ran the full disposable suite with OTLP disabled. The fixture
independently overrides the exporter endpoint to empty. **The Windows PostgreSQL service
remained running.** This proves connection isolation, not a service-stop run.

The script refuses active development sessions without terminating them. Its `finally`
restores admission/environment settings, verified by a real `SELECT 1`. The injected
`-FailAfterIsolation` exception also restored admission. Read-only before/after fingerprints
matched public records, migration history and column definitions; no development records or
migrations changed.

This optional script changes admission metadata. It is not ordinary setup or a read-only
check; do not use it while development is in use. Machine/process termination or lost
administrative connectivity cannot be guaranteed recoverable by an in-process `finally`.

See [isolation evidence](docs/assignment-6-2-stage-8-isolation-evidence.txt),
[failure-restoration evidence](docs/assignment-6-2-stage-8-restoration-evidence.txt) and [the
complete report](docs/assignment-6-2-stage-8-verification.md).

## 13. Challenges and future improvements

I kept these limits visible rather than claiming complete production infrastructure:

- **Development migration blocker:** duplicate emails still need an approved
  resolution; legacy passwordless users need a password-setup workflow.
- **Idempotency:** process-local storage is lost on restart.
- **Existing scope:** EF ignores legacy `Stokvel.Members`; cycle navigation,
  payout rotation and rollback coverage retain their documented scope.
- **Deployment/security:** no frontend, public password reset or role assignment.
  Strict cookies assume same-site use; cross-site deployment needs CSRF/CORS
  design. Access JWTs expire rather than being revoked instantly on logout.
- **Telemetry operations:** sampling policy, durable retention, remote sink
  access controls and alerting need deployment decisions outside this local work.
  No explicit sampler override was added; sampling/export loss affects completeness.
- **Privacy:** full unexpected library diagnostics and external logs need review.
  Parameter suppression does not make all telemetry anonymous.
- **Literal PDF evidence:** approved isolated outage/admission tests substituted
  for stopping Windows development PostgreSQL; the matrix marks that distinction.
- **Private local backup:** `backup/assignments-before-safe-push-20261011` retains
  cleanup exports absent from `main` and its ancestors. Keep it local; publishing
  all refs or a `.git` archive would expose them.

Targeted publication scans found no confirmed outgoing credential/token leak, but are not
exhaustive detectors. No backup or earlier evidence was removed. Publication/submission
approval for the current verification changes remains pending; earlier Assignment 6.3 commits are on remote `main`. See [the final
matrix](docs/assignment-6-2-final-compliance-matrix.md) and [safe-publication
guidance](docs/safe-push-verification.md).

## 14. Detailed assignment evidence

<a id="verification-evidence"></a>

`README-history.md` preserves earlier explanations, experiments and recorded results. Its
setup and counts may be superseded. I keep it as a historical archive; use this README for
current instructions. The archive is unchanged by this edit.

### Earlier assignments

- [Historical README](README-history.md)
- [5.3 performance evidence](docs/assignment-5-3-performance-evidence.md)
- [5.3 query-plan script](scripts/assignment-5-3-performance.sql)
- [5.4 initial audit](docs/assignment-5-4-phase-1-audit.md)
- [5.4 phase record](docs/assignment-5-4-phase-verification.md)
- [5.4 completion](docs/assignment-5-4-verification-report.md)
- [5.4 isolation evidence](docs/assignment-5-4-isolation-evidence.txt)
- [6.1 phase record](docs/assignment-6-1-phase-verification.md)
- [6.1 verification](docs/assignment-6-1-verification-report.md)
- [6.1 file manifest](docs/assignment-6-1-file-manifest.txt)
- [6.1 isolation evidence](docs/assignment-6-1-isolation-evidence.txt)

Assignment 5.4 recorded 25 passing tests. Assignment 6.1 recorded 62 (25 original plus 37
additional), also 62 in the safe-publication rerun. The 11 October documentation checks
covered 43 links, 11 PowerShell blocks, seven migrations and 62 discovered tests. These
historical checks are not this edit's results. Discovery lists tests; it does not execute
them.

### Assignment 6.2

| Stage | Recorded result | Report |
|---|---|---|
| 2: Pipeline | See build/test record | [Stage 2](docs/assignment-6-2-stage-2-verification.md) |
| 3: Correlation | 76 passed | [Stage 3](docs/assignment-6-2-stage-3-verification.md) |
| 4: Business telemetry | 83 passed | [Stage 4](docs/assignment-6-2-stage-4-verification.md) |
| 5: Logging/privacy | 95 passed | [Stage 5](docs/assignment-6-2-stage-5-verification.md) |
| 6: Health probes | 99 passed | [Stage 6](docs/assignment-6-2-stage-6-verification.md) |
| 7: Complete verification | 101 passed | [Stage 7](docs/assignment-6-2-stage-7-verification.md) |
| 8: Final evidence | 101 passed | [Stage 8](docs/assignment-6-2-stage-8-verification.md) |

Earlier pending-evidence statements describe their stages, not current status. The final
recorded result remains 101 passed, zero failed/skipped, and a build with zero
warnings/errors.

- [Final compliance matrix](docs/assignment-6-2-final-compliance-matrix.md)
- [Architecture details](docs/architecture.md)
- [API/security reference](docs/api-reference.md)
- [Supplementary diagnostics](docs/assignment-6-2-supplementary-diagnostics.md)
- [Stage 7 isolation evidence](docs/assignment-6-2-stage-7-isolation-evidence.txt)
- [Safe-publication verification](docs/safe-push-verification.md)

## Appendix A. Complete POPIA logging audit

<a id="assignment-62-stage-5-complete-popia-logging-audit"></a>

I kept the complete **181-row index and all 181 full audit entries in this README**. Each
entry preserves its six original fields. The index replaces very wide rows; the full fields
are wrapped below it for easier reading in VS Code.

Line references describe the Stage 5 snapshot. Repeated messages, test-only exceptions,
wrappers, validator defaults and the existing isolation script remain included. That script
was not run in Stage 5. No logger/exception sites exist in migrations. User rules live in
controllers, repositories and the User entity; there is no separate user service.

ContributionService, PayoutService and MembershipService have no ILogger calls; their
exceptions are included. StokvelAccessHandler has no logger/exception-message site; its
refusal helper owns the security event. Later verification-only sites remain in [the
supplementary inventory](docs/assignment-6-2-supplementary-diagnostics.md).

Provider safeguards follow the official [Npgsql
parameters](https://www.npgsql.org/doc/connection-string-parameters.html), [Npgsql
tracing](https://www.npgsql.org/doc/diagnostics/tracing.html) and [.NET 10 handled-exception
policy][handled-exceptions]. Tests inspect captured output instead of treating defaults as
proof of privacy.

Before/after examples below are **structural illustrations, not captured logs**:

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

Audit wording records its stage's before/after behaviour. The ongoing diagnostic risks remain
in [the logging section](#10-popia-compliant-logging). No actual identity, token or financial
value is included in these illustrative examples.

[handled-exceptions]: https://learn.microsoft.com/en-us/aspnet/core/fundamentals/error-handling?view=aspnetcore-10.0

<details>
<summary>Expand the complete 181-row index and full field-by-field audit</summary>

### Audit index

| Entry | Source site | Full record |
|---|---|---|
| 001 | `AuthService.cs:27` | [Six fields](#entry-001) |
| 002 | `AuthService.cs:79` | [Six fields](#entry-002) |
| 003 | `AuthService.cs:74` | [Six fields](#entry-003) |
| 004 | `AuthService.cs:25` | [Six fields](#entry-004) |
| 005 | `AuthService.cs:38` | [Six fields](#entry-005) |
| 006 | `AuthService.cs:53` | [Six fields](#entry-006) |
| 007 | `AuthService.cs:59` | [Six fields](#entry-007) |
| 008 | `AuthService.cs:68` | [Six fields](#entry-008) |
| 009 | `JwtOptions.cs:13` | [Six fields](#entry-009) |
| 010 | `JwtOptions.cs:14` | [Six fields](#entry-010) |
| 011 | `JwtOptions.cs:18` | [Six fields](#entry-011) |
| 012 | `JwtOptions.cs:23` | [Six fields](#entry-012) |
| 013 | `StokvelAuthorization.cs:21` | [Six fields](#entry-013) |
| 014 | `StokvelAuthorization.cs:26` | [Six fields](#entry-014) |
| 015 | `StokvelAuthorization.cs:31` | [Six fields](#entry-015) |
| 016 | `StokvelAuthorization.cs:19` | [Six fields](#entry-016) |
| 017 | `StokvelAuthorization.cs:24` | [Six fields](#entry-017) |
| 018 | `ContributionCyclesController.cs:60` | [Six fields](#entry-018) |
| 019 | `ContributionCyclesController.cs:97` | [Six fields](#entry-019) |
| 020 | `ContributionCyclesController.cs:103` | [Six fields](#entry-020) |
| 021 | `ContributionCyclesController.cs:146` | [Six fields](#entry-021) |
| 022 | `ContributionCyclesController.cs:152` | [Six fields](#entry-022) |
| 023 | `ContributionCyclesController.cs:157` | [Six fields](#entry-023) |
| 024 | `ContributionCyclesController.cs:211` | [Six fields](#entry-024) |
| 025 | `ContributionCyclesController.cs:264` | [Six fields](#entry-025) |
| 026 | `ContributionCyclesController.cs:270` | [Six fields](#entry-026) |
| 027 | `ContributionCyclesController.cs:275` | [Six fields](#entry-027) |
| 028 | `ContributionCyclesController.cs:282` | [Six fields](#entry-028) |
| 029 | `ContributionCyclesController.cs:319` | [Six fields](#entry-029) |
| 030 | `ContributionCyclesController.cs:325` | [Six fields](#entry-030) |
| 031 | `ContributionCyclesController.cs:356` | [Six fields](#entry-031) |
| 032 | `StokvelsController.cs:85` | [Six fields](#entry-032) |
| 033 | `StokvelsController.cs:116` | [Six fields](#entry-033) |
| 034 | `StokvelsController.cs:120` | [Six fields](#entry-034) |
| 035 | `StokvelsController.cs:202` | [Six fields](#entry-035) |
| 036 | `StokvelsController.cs:206` | [Six fields](#entry-036) |
| 037 | `StokvelsController.cs:213` | [Six fields](#entry-037) |
| 038 | `StokvelsController.cs:247` | [Six fields](#entry-038) |
| 039 | `StokvelsController.cs:384` | [Six fields](#entry-039) |
| 040 | `UsersController.cs:65` | [Six fields](#entry-040) |
| 041 | `UsersController.cs:131` | [Six fields](#entry-041) |
| 042 | `UsersController.cs:135` | [Six fields](#entry-042) |
| 043 | `UsersController.cs:142` | [Six fields](#entry-043) |
| 044 | `UsersController.cs:174` | [Six fields](#entry-044) |
| 045 | `UsersController.cs:202` | [Six fields](#entry-045) |
| 046 | `ValidationFilter.cs:23` | [Six fields](#entry-046) |
| 047 | `ValidationFilter.cs:56` | [Six fields](#entry-047) |
| 048 | `GlobalExceptionHandler.cs:60` | [Six fields](#entry-048) |
| 049 | `GlobalExceptionHandler.cs:63` | [Six fields](#entry-049) |
| 050 | `Correlation.cs:14` | [Six fields](#entry-050) |
| 051 | `Program.cs:106` | [Six fields](#entry-051) |
| 052 | `Program.cs:98` | [Six fields](#entry-052) |
| 053 | `Program.cs:180` | [Six fields](#entry-053) |
| 054 | `ContributionService.cs:48` | [Six fields](#entry-054) |
| 055 | `ContributionService.cs:66` | [Six fields](#entry-055) |
| 056 | `ContributionService.cs:79` | [Six fields](#entry-056) |
| 057 | `ContributionService.cs:89` | [Six fields](#entry-057) |
| 058 | `ContributionService.cs:100` | [Six fields](#entry-058) |
| 059 | `ContributionService.cs:110` | [Six fields](#entry-059) |
| 060 | `ContributionService.cs:116` | [Six fields](#entry-060) |
| 061 | `ContributionService.cs:129` | [Six fields](#entry-061) |
| 062 | `MembershipService.cs:33` | [Six fields](#entry-062) |
| 063 | `MembershipService.cs:43` | [Six fields](#entry-063) |
| 064 | `MembershipService.cs:54` | [Six fields](#entry-064) |
| 065 | `MembershipService.cs:77` | [Six fields](#entry-065) |
| 066 | `MembershipService.cs:89` | [Six fields](#entry-066) |
| 067 | `PayoutService.cs:55` | [Six fields](#entry-067) |
| 068 | `PayoutService.cs:61` | [Six fields](#entry-068) |
| 069 | `PayoutService.cs:67` | [Six fields](#entry-069) |
| 070 | `PayoutService.cs:97` | [Six fields](#entry-070) |
| 071 | `PayoutService.cs:124` | [Six fields](#entry-071) |
| 072 | `PayoutService.cs:137` | [Six fields](#entry-072) |
| 073 | `CreateContributionCycleRequestValidator.cs:14` | [Six fields](#entry-073) |
| 074 | `CreateContributionCycleRequestValidator.cs:16` | [Six fields](#entry-074) |
| 075 | `CreateContributionCycleRequestValidator.cs:20` | [Six fields](#entry-075) |
| 076 | `CreateStokvelRequestValidator.cs:13` | [Six fields](#entry-076) |
| 077 | `CreateStokvelRequestValidator.cs:17` | [Six fields](#entry-077) |
| 078 | `CreateUserRequestValidator.cs:13` | [Six fields](#entry-078) |
| 079 | `CreateUserRequestValidator.cs:17` | [Six fields](#entry-079) |
| 080 | `CreateUserRequestValidator.cs:19` | [Six fields](#entry-080) |
| 081 | `LoginRequestValidator.cs:10` | [Six fields](#entry-081) |
| 082 | `LoginRequestValidator.cs:10` | [Six fields](#entry-082) |
| 083 | `LoginRequestValidator.cs:11` | [Six fields](#entry-083) |
| 084 | `RecordContributionRequestValidator.cs:13` | [Six fields](#entry-084) |
| 085 | `RecordContributionRequestValidator.cs:18` | [Six fields](#entry-085) |
| 086 | `RegisterRequestValidator.cs:10` | [Six fields](#entry-086) |
| 087 | `RegisterRequestValidator.cs:11` | [Six fields](#entry-087) |
| 088 | `RegisterRequestValidator.cs:11` | [Six fields](#entry-088) |
| 089 | `RegisterRequestValidator.cs:12` | [Six fields](#entry-089) |
| 090 | `RegisterRequestValidator.cs:12` | [Six fields](#entry-090) |
| 091 | `UpdateContributionCycleRequestValidator.cs:14` | [Six fields](#entry-091) |
| 092 | `UpdateContributionCycleRequestValidator.cs:16` | [Six fields](#entry-092) |
| 093 | `UpdateContributionCycleRequestValidator.cs:20` | [Six fields](#entry-093) |
| 094 | `UpdateStokvelRequestValidator.cs:13` | [Six fields](#entry-094) |
| 095 | `UpdateStokvelRequestValidator.cs:17` | [Six fields](#entry-095) |
| 096 | `UpdateUserRequestValidator.cs:13` | [Six fields](#entry-096) |
| 097 | `UpdateUserRequestValidator.cs:17` | [Six fields](#entry-097) |
| 098 | `UpdateUserRequestValidator.cs:19` | [Six fields](#entry-098) |
| 099 | `Contribution.cs:30` | [Six fields](#entry-099) |
| 100 | `Contribution.cs:34` | [Six fields](#entry-100) |
| 101 | `Contribution.cs:38` | [Six fields](#entry-101) |
| 102 | `Contribution.cs:42` | [Six fields](#entry-102) |
| 103 | `ContributionCycle.cs:28` | [Six fields](#entry-103) |
| 104 | `ContributionCycle.cs:32` | [Six fields](#entry-104) |
| 105 | `ContributionCycle.cs:36` | [Six fields](#entry-105) |
| 106 | `ContributionCycle.cs:51` | [Six fields](#entry-106) |
| 107 | `ContributionCycle.cs:55` | [Six fields](#entry-107) |
| 108 | `Payout.cs:30` | [Six fields](#entry-108) |
| 109 | `Payout.cs:34` | [Six fields](#entry-109) |
| 110 | `Payout.cs:38` | [Six fields](#entry-110) |
| 111 | `Payout.cs:42` | [Six fields](#entry-111) |
| 112 | `Payout.cs:57` | [Six fields](#entry-112) |
| 113 | `Stokvel.cs:26` | [Six fields](#entry-113) |
| 114 | `Stokvel.cs:29` | [Six fields](#entry-114) |
| 115 | `Stokvel.cs:42` | [Six fields](#entry-115) |
| 116 | `Stokvel.cs:46` | [Six fields](#entry-116) |
| 117 | `Stokvel.cs:57` | [Six fields](#entry-117) |
| 118 | `Stokvel.cs:72` | [Six fields](#entry-118) |
| 119 | `StokvelMember.cs:18` | [Six fields](#entry-119) |
| 120 | `StokvelMember.cs:38` | [Six fields](#entry-120) |
| 121 | `StokvelMember.cs:42` | [Six fields](#entry-121) |
| 122 | `StokvelMember.cs:46` | [Six fields](#entry-122) |
| 123 | `User.cs:23` | [Six fields](#entry-123) |
| 124 | `User.cs:32` | [Six fields](#entry-124) |
| 125 | `User.cs:44` | [Six fields](#entry-125) |
| 126 | `User.cs:47` | [Six fields](#entry-126) |
| 127 | `User.cs:58` | [Six fields](#entry-127) |
| 128 | `User.cs:61` | [Six fields](#entry-128) |
| 129 | `AuthenticationFailedException.cs:3` | [Six fields](#entry-129) |
| 130 | `BusinessRuleException.cs:6` | [Six fields](#entry-130) |
| 131 | `ForbiddenException.cs:3` | [Six fields](#entry-131) |
| 132 | `NotFoundException.cs:6` | [Six fields](#entry-132) |
| 133 | `PreconditionFailedException.cs:6` | [Six fields](#entry-133) |
| 134 | `RequestValidationException.cs:7` | [Six fields](#entry-134) |
| 135 | `RondiTrackException.cs:6` | [Six fields](#entry-135) |
| 136 | `ContributionRepository.cs:59` | [Six fields](#entry-136) |
| 137 | `ContributionRepository.cs:72` | [Six fields](#entry-137) |
| 138 | `ContributionRepository.cs:212` | [Six fields](#entry-138) |
| 139 | `ContributionRepository.cs:319` | [Six fields](#entry-139) |
| 140 | `ContributionRepository.cs:329` | [Six fields](#entry-140) |
| 141 | `ContributionRepository.cs:337` | [Six fields](#entry-141) |
| 142 | `EfStokvelMemberRepository.cs:45` | [Six fields](#entry-142) |
| 143 | `EfStokvelMemberRepository.cs:58` | [Six fields](#entry-143) |
| 144 | `EfStokvelMemberRepository.cs:143` | [Six fields](#entry-144) |
| 145 | `EfStokvelMemberRepository.cs:234` | [Six fields](#entry-145) |
| 146 | `EfStokvelMemberRepository.cs:241` | [Six fields](#entry-146) |
| 147 | `EfStokvelMemberRepository.cs:249` | [Six fields](#entry-147) |
| 148 | `BusinessTelemetryTests.cs:156` | [Six fields](#entry-148) |
| 149 | `BusinessTelemetryTests.cs:156` | [Six fields](#entry-149) |
| 150 | `BusinessTelemetryTests.cs:166` | [Six fields](#entry-150) |
| 151 | `CorrelationTests.cs:176` | [Six fields](#entry-151) |
| 152 | `LoggingTests.cs:229` | [Six fields](#entry-152) |
| 153 | `PayoutTransactionTests.cs:186` | [Six fields](#entry-153) |
| 154 | `PostgresApiFactory.cs:73` | [Six fields](#entry-154) |
| 155 | `PostgresApiFactory.cs:86` | [Six fields](#entry-155) |
| 156 | `PostgresApiFactory.cs:93` | [Six fields](#entry-156) |
| 157 | `verify-assignment-5-4-isolation.ps1:12` | [Six fields](#entry-157) |
| 158 | `verify-assignment-5-4-isolation.ps1:21` | [Six fields](#entry-158) |
| 159 | `verify-assignment-5-4-isolation.ps1:23` | [Six fields](#entry-159) |
| 160 | `verify-assignment-5-4-isolation.ps1:36` | [Six fields](#entry-160) |
| 161 | `verify-assignment-5-4-isolation.ps1:38` | [Six fields](#entry-161) |
| 162 | `verify-assignment-5-4-isolation.ps1:41` | [Six fields](#entry-162) |
| 163 | `verify-assignment-5-4-isolation.ps1:44` | [Six fields](#entry-163) |
| 164 | `verify-assignment-5-4-isolation.ps1:54` | [Six fields](#entry-164) |
| 165 | `verify-assignment-5-4-isolation.ps1:56` | [Six fields](#entry-165) |
| 166 | `verify-assignment-5-4-isolation.ps1:74` | [Six fields](#entry-166) |
| 167 | `verify-assignment-5-4-isolation.ps1:76` | [Six fields](#entry-167) |
| 168 | `Program.cs` | [Six fields](#entry-168) |
| 169 | `Program.cs` | [Six fields](#entry-169) |
| 170 | `Program.cs` | [Six fields](#entry-170) |
| 171 | `EF provider` | [Six fields](#entry-171) |
| 172 | `EF provider` | [Six fields](#entry-172) |
| 173 | `EF provider` | [Six fields](#entry-173) |
| 174 | `Program.cs` | [Six fields](#entry-174) |
| 175 | `Program.cs` | [Six fields](#entry-175) |
| 176 | `Program.cs` | [Six fields](#entry-176) |
| 177 | `Program.cs` | [Six fields](#entry-177) |
| 178 | `ValidationFilter.cs` | [Six fields](#entry-178) |
| 179 | `GlobalExceptionHandler.cs` | [Six fields](#entry-179) |
| 180 | `CorrelationLogProvider.cs` | [Six fields](#entry-180) |
| 181 | `CapturedLogProvider.cs` | [Six fields](#entry-181) |

### Full audit records

### Entry 001

- **Site and method:** [API/Auth/AuthService.cs:27](API/Auth/AuthService.cs#L27) / LoginAsync
- **Diagnostic type:** AuthenticationFailedException
- **Potential contents:** "Email or password is incorrect."
- **Personal/financial risk:** No submitted values in literal.
- **Before Stage 5:** Literal or generated diagnostic; auth 401 was Information; unexpected
  500 was Error.
- **After / minimization justification:** Message unchanged. 4xx: Information,
  type/status/correlation only, no exception; 5xx: full exception internally, generic client
  detail. Fixed authored client details preserved.

### Entry 002

- **Site and method:** [API/Auth/AuthService.cs:79](API/Auth/AuthService.cs#L79) /
  RegisterAsync
- **Diagnostic type:** BusinessRuleException
- **Potential contents:** "An account with this email already exists."
- **Personal/financial risk:** No submitted values in literal.
- **Before Stage 5:** Expected domain/validation 4xx was logged with full exception at Error
  (except authentication).
- **After / minimization justification:** Message unchanged. 4xx: Information,
  type/status/correlation only, no exception; 5xx: full exception internally, generic client
  detail. Fixed authored client details preserved.

### Entry 003

- **Site and method:** [API/Auth/AuthService.cs:74](API/Auth/AuthService.cs#L74) /
  InvalidRefresh
- **Diagnostic type:** AuthenticationFailedException (target-typed)
- **Potential contents:** "Refresh session is invalid. Please sign in again."
- **Personal/financial risk:** Fixed text; no raw/hash/session value.
- **Before Stage 5:** Information authentication rejection without type/status.
- **After / minimization justification:** Message unchanged; handler now includes
  type/status/correlation only.

### Entry 004

- **Site and method:** [API/Auth/AuthService.cs:25](API/Auth/AuthService.cs#L25) / LoginAsync
- **Diagnostic type:** ILogger logger.LogInformation
- **Potential contents:** "Security event {EventType} with {Outcome}. CorrelationId:
  {CorrelationId}", "sign_in", "failure", SecurityEvents.CorrelationId
- **Personal/financial risk:** Fixed event/state/type/status plus W3C ID; operational
  correlation is linkable and needs retention/access controls.
- **Before Stage 5:** No service-owned failed-sign-in event.
- **After / minimization justification:** Information sign_in/failure + correlation; no typed
  email or account identity. Expected credential rejection.

### Entry 005

- **Site and method:** [API/Auth/AuthService.cs:38](API/Auth/AuthService.cs#L38) / LoginAsync
- **Diagnostic type:** ILogger logger.LogInformation
- **Potential contents:** "Security event {EventType} with {Outcome}. CorrelationId:
  {CorrelationId}", "sign_in", "success", SecurityEvents.CorrelationId
- **Personal/financial risk:** Fixed event/state/type/status plus W3C ID; operational
  correlation is linkable and needs retention/access controls.
- **Before Stage 5:** Information: Login succeeded. UserId: [identifier].
- **After / minimization justification:** Information sign_in/success + correlation; no
  identity. Normal security event.

### Entry 006

- **Site and method:** [API/Auth/AuthService.cs:53](API/Auth/AuthService.cs#L53) /
  RefreshAsync
- **Diagnostic type:** ILogger logger.LogWarning
- **Potential contents:** "Security event {EventType}; session revoked. CorrelationId:
  {CorrelationId}", "refresh_reuse", SecurityEvents.CorrelationId
- **Personal/financial risk:** Fixed event/state/type/status plus W3C ID; operational
  correlation is linkable and needs retention/access controls.
- **Before Stage 5:** Warning reuse with FamilyId.
- **After / minimization justification:** Warning refresh_reuse + correlation; session
  identifiers omitted. Reuse warrants investigation.

### Entry 007

- **Site and method:** [API/Auth/AuthService.cs:59](API/Auth/AuthService.cs#L59) /
  RefreshAsync
- **Diagnostic type:** ILogger logger.LogInformation
- **Potential contents:** "Security event {EventType} with {Outcome}. CorrelationId:
  {CorrelationId}", "refresh", "success", SecurityEvents.CorrelationId
- **Personal/financial risk:** Fixed event/state/type/status plus W3C ID; operational
  correlation is linkable and needs retention/access controls.
- **Before Stage 5:** Information refresh success with UserId and FamilyId.
- **After / minimization justification:** Information refresh/success + correlation; omit
  account and family identifiers. Normal session event.

### Entry 008

- **Site and method:** [API/Auth/AuthService.cs:68](API/Auth/AuthService.cs#L68) / LogoutAsync
- **Diagnostic type:** ILogger logger.LogInformation
- **Potential contents:** "Security event {EventType} completed. CorrelationId:
  {CorrelationId}", "logout", SecurityEvents.CorrelationId
- **Personal/financial risk:** Fixed event/state/type/status plus W3C ID; operational
  correlation is linkable and needs retention/access controls.
- **Before Stage 5:** Information: Logout completed.
- **After / minimization justification:** Information logout event + correlation only; no
  token or identity.

### Entry 009

- **Site and method:** [API/Auth/JwtOptions.cs:13](API/Auth/JwtOptions.cs#L13) / Load
- **Diagnostic type:** InvalidOperationException
- **Potential contents:** guidance = "Configure Jwt:SigningKey in User Secrets as a
  base64-encoded random key of at least 32 bytes: dotnet user-secrets --project API set
  Jwt:SigningKey &lt;BASE64_KEY&gt;."
- **Personal/financial risk:** Constant configuration guidance and placeholder; never the
  signing key.
- **Before Stage 5:** Literal or generated diagnostic; auth 401 was Information; unexpected
  500 was Error.
- **After / minimization justification:** Message unchanged. 4xx: Information,
  type/status/correlation only, no exception; 5xx: full exception internally, generic client
  detail. Fixed authored client details preserved.

### Entry 010

- **Site and method:** [API/Auth/JwtOptions.cs:14](API/Auth/JwtOptions.cs#L14) / Load
- **Diagnostic type:** InvalidOperationException
- **Potential contents:** guidance = "Configure Jwt:SigningKey in User Secrets as a
  base64-encoded random key of at least 32 bytes: dotnet user-secrets --project API set
  Jwt:SigningKey &lt;BASE64_KEY&gt;."
- **Personal/financial risk:** Constant configuration guidance and placeholder; never the
  signing key.
- **Before Stage 5:** Literal or generated diagnostic; auth 401 was Information; unexpected
  500 was Error.
- **After / minimization justification:** Message unchanged. 4xx: Information,
  type/status/correlation only, no exception; 5xx: full exception internally, generic client
  detail. Fixed authored client details preserved.

### Entry 011

- **Site and method:** [API/Auth/JwtOptions.cs:18](API/Auth/JwtOptions.cs#L18) / Load
- **Diagnostic type:** InvalidOperationException
- **Potential contents:** "Configure the non-secret Jwt:Issuer and Jwt:Audience settings."
- **Personal/financial risk:** No submitted values in literal.
- **Before Stage 5:** Literal or generated diagnostic; auth 401 was Information; unexpected
  500 was Error.
- **After / minimization justification:** Message unchanged. 4xx: Information,
  type/status/correlation only, no exception; 5xx: full exception internally, generic client
  detail. Fixed authored client details preserved.

### Entry 012

- **Site and method:** [API/Auth/JwtOptions.cs:23](API/Auth/JwtOptions.cs#L23) / Load
- **Diagnostic type:** InvalidOperationException
- **Potential contents:** "JWT settings require access lifetime 1?15 minutes, ClockSkew 0?60
  seconds and refresh lifetime 1?30 days."
- **Personal/financial risk:** No submitted values in literal.
- **Before Stage 5:** Literal or generated diagnostic; auth 401 was Information; unexpected
  500 was Error.
- **After / minimization justification:** Message unchanged. 4xx: Information,
  type/status/correlation only, no exception; 5xx: full exception internally, generic client
  detail. Fixed authored client details preserved.

### Entry 013

- **Site and method:**
  [API/Auth/StokvelAuthorization.cs:21](API/Auth/StokvelAuthorization.cs#L21) / EnsureAsync
- **Diagnostic type:** NotFoundException
- **Potential contents:** "Stokvel not found."
- **Personal/financial risk:** No submitted values in literal.
- **Before Stage 5:** Expected domain/validation 4xx was logged with full exception at Error
  (except authentication).
- **After / minimization justification:** Message unchanged. 4xx: Information,
  type/status/correlation only, no exception; 5xx: full exception internally, generic client
  detail. Fixed authored client details preserved.

### Entry 014

- **Site and method:**
  [API/Auth/StokvelAuthorization.cs:26](API/Auth/StokvelAuthorization.cs#L26) / EnsureAsync
- **Diagnostic type:** ForbiddenException
- **Potential contents:** "You do not have permission for this operation."
- **Personal/financial risk:** No submitted values in literal.
- **Before Stage 5:** Expected domain/validation 4xx was logged with full exception at Error
  (except authentication).
- **After / minimization justification:** Message unchanged. 4xx: Information,
  type/status/correlation only, no exception; 5xx: full exception internally, generic client
  detail. Fixed authored client details preserved.

### Entry 015

- **Site and method:**
  [API/Auth/StokvelAuthorization.cs:31](API/Auth/StokvelAuthorization.cs#L31) / UserId
- **Diagnostic type:** AuthenticationFailedException
- **Potential contents:** "Authentication is required."
- **Personal/financial risk:** No submitted values in literal.
- **Before Stage 5:** Literal or generated diagnostic; auth 401 was Information; unexpected
  500 was Error.
- **After / minimization justification:** Message unchanged. 4xx: Information,
  type/status/correlation only, no exception; 5xx: full exception internally, generic client
  detail. Fixed authored client details preserved.

### Entry 016

- **Site and method:**
  [API/Auth/StokvelAuthorization.cs:19](API/Auth/StokvelAuthorization.cs#L19) / EnsureAsync
- **Diagnostic type:** ILogger logger.LogInformation
- **Potential contents:** "Security event {EventType} with {Result}. CorrelationId:
  {CorrelationId}", "stokvel_refusal", "not_found", SecurityEvents.CorrelationId
- **Personal/financial risk:** Fixed event/state/type/status plus W3C ID; operational
  correlation is linkable and needs retention/access controls.
- **Before Stage 5:** No service-owned refusal log; exception handler produced misleading
  Error.
- **After / minimization justification:** Information stokvel_refusal + fixed result +
  correlation; expected access denial, no caller/resource identifiers.

### Entry 017

- **Site and method:**
  [API/Auth/StokvelAuthorization.cs:24](API/Auth/StokvelAuthorization.cs#L24) / EnsureAsync
- **Diagnostic type:** ILogger logger.LogInformation
- **Potential contents:** "Security event {EventType} with {Result}. CorrelationId:
  {CorrelationId}", "stokvel_refusal", "forbidden", SecurityEvents.CorrelationId
- **Personal/financial risk:** Fixed event/state/type/status plus W3C ID; operational
  correlation is linkable and needs retention/access controls.
- **Before Stage 5:** No service-owned refusal log; exception handler produced misleading
  Error.
- **After / minimization justification:** Information stokvel_refusal + fixed result +
  correlation; expected access denial, no caller/resource identifiers.

### Entry 018

- **Site and method:**
  [API/Controllers/ContributionCyclesController.cs:60](API/Controllers/ContributionCyclesController.cs#L60)
  / GetAll
- **Diagnostic type:** NotFoundException
- **Potential contents:** "Stokvel not found."
- **Personal/financial risk:** No submitted values in literal.
- **Before Stage 5:** Expected domain/validation 4xx was logged with full exception at Error
  (except authentication).
- **After / minimization justification:** Message unchanged. 4xx: Information,
  type/status/correlation only, no exception; 5xx: full exception internally, generic client
  detail. Fixed authored client details preserved.

### Entry 019

- **Site and method:**
  [API/Controllers/ContributionCyclesController.cs:97](API/Controllers/ContributionCyclesController.cs#L97)
  / GetById
- **Diagnostic type:** NotFoundException
- **Potential contents:** "Stokvel not found."
- **Personal/financial risk:** No submitted values in literal.
- **Before Stage 5:** Expected domain/validation 4xx was logged with full exception at Error
  (except authentication).
- **After / minimization justification:** Message unchanged. 4xx: Information,
  type/status/correlation only, no exception; 5xx: full exception internally, generic client
  detail. Fixed authored client details preserved.

### Entry 020

- **Site and method:**
  [API/Controllers/ContributionCyclesController.cs:103](API/Controllers/ContributionCyclesController.cs#L103)
  / GetById
- **Diagnostic type:** NotFoundException
- **Potential contents:** "Contribution cycle not found."
- **Personal/financial risk:** No submitted values in literal.
- **Before Stage 5:** Expected domain/validation 4xx was logged with full exception at Error
  (except authentication).
- **After / minimization justification:** Message unchanged. 4xx: Information,
  type/status/correlation only, no exception; 5xx: full exception internally, generic client
  detail. Fixed authored client details preserved.

### Entry 021

- **Site and method:**
  [API/Controllers/ContributionCyclesController.cs:146](API/Controllers/ContributionCyclesController.cs#L146)
  / GetContributions
- **Diagnostic type:** NotFoundException
- **Potential contents:** "Stokvel not found."
- **Personal/financial risk:** No submitted values in literal.
- **Before Stage 5:** Expected domain/validation 4xx was logged with full exception at Error
  (except authentication).
- **After / minimization justification:** Message unchanged. 4xx: Information,
  type/status/correlation only, no exception; 5xx: full exception internally, generic client
  detail. Fixed authored client details preserved.

### Entry 022

- **Site and method:**
  [API/Controllers/ContributionCyclesController.cs:152](API/Controllers/ContributionCyclesController.cs#L152)
  / GetContributions
- **Diagnostic type:** NotFoundException
- **Potential contents:** "Contribution cycle not found."
- **Personal/financial risk:** No submitted values in literal.
- **Before Stage 5:** Expected domain/validation 4xx was logged with full exception at Error
  (except authentication).
- **After / minimization justification:** Message unchanged. 4xx: Information,
  type/status/correlation only, no exception; 5xx: full exception internally, generic client
  detail. Fixed authored client details preserved.

### Entry 023

- **Site and method:**
  [API/Controllers/ContributionCyclesController.cs:157](API/Controllers/ContributionCyclesController.cs#L157)
  / GetContributions
- **Diagnostic type:** RequestValidationException
- **Potential contents:** "Page size cannot be negative."
- **Personal/financial risk:** No submitted values in literal.
- **Before Stage 5:** Expected domain/validation 4xx was logged with full exception at Error
  (except authentication).
- **After / minimization justification:** Message unchanged. 4xx: Information,
  type/status/correlation only, no exception; 5xx: full exception internally, generic client
  detail. Fixed authored client details preserved.

### Entry 024

- **Site and method:**
  [API/Controllers/ContributionCyclesController.cs:211](API/Controllers/ContributionCyclesController.cs#L211)
  / Create
- **Diagnostic type:** NotFoundException
- **Potential contents:** "Stokvel not found."
- **Personal/financial risk:** No submitted values in literal.
- **Before Stage 5:** Expected domain/validation 4xx was logged with full exception at Error
  (except authentication).
- **After / minimization justification:** Message unchanged. 4xx: Information,
  type/status/correlation only, no exception; 5xx: full exception internally, generic client
  detail. Fixed authored client details preserved.

### Entry 025

- **Site and method:**
  [API/Controllers/ContributionCyclesController.cs:264](API/Controllers/ContributionCyclesController.cs#L264)
  / Update
- **Diagnostic type:** NotFoundException
- **Potential contents:** "Stokvel not found."
- **Personal/financial risk:** No submitted values in literal.
- **Before Stage 5:** Expected domain/validation 4xx was logged with full exception at Error
  (except authentication).
- **After / minimization justification:** Message unchanged. 4xx: Information,
  type/status/correlation only, no exception; 5xx: full exception internally, generic client
  detail. Fixed authored client details preserved.

### Entry 026

- **Site and method:**
  [API/Controllers/ContributionCyclesController.cs:270](API/Controllers/ContributionCyclesController.cs#L270)
  / Update
- **Diagnostic type:** NotFoundException
- **Potential contents:** "Contribution cycle not found."
- **Personal/financial risk:** No submitted values in literal.
- **Before Stage 5:** Expected domain/validation 4xx was logged with full exception at Error
  (except authentication).
- **After / minimization justification:** Message unchanged. 4xx: Information,
  type/status/correlation only, no exception; 5xx: full exception internally, generic client
  detail. Fixed authored client details preserved.

### Entry 027

- **Site and method:**
  [API/Controllers/ContributionCyclesController.cs:275](API/Controllers/ContributionCyclesController.cs#L275)
  / Update
- **Diagnostic type:** RequestValidationException
- **Potential contents:** "If-Match header is required."
- **Personal/financial risk:** No submitted values in literal.
- **Before Stage 5:** Expected domain/validation 4xx was logged with full exception at Error
  (except authentication).
- **After / minimization justification:** Message unchanged. 4xx: Information,
  type/status/correlation only, no exception; 5xx: full exception internally, generic client
  detail. Fixed authored client details preserved.

### Entry 028

- **Site and method:**
  [API/Controllers/ContributionCyclesController.cs:282](API/Controllers/ContributionCyclesController.cs#L282)
  / Update
- **Diagnostic type:** PreconditionFailedException
- **Potential contents:** "The contribution cycle has changed. Refetch it and retry with the
  current ETag."
- **Personal/financial risk:** No submitted values in literal.
- **Before Stage 5:** Expected domain/validation 4xx was logged with full exception at Error
  (except authentication).
- **After / minimization justification:** Message unchanged. 4xx: Information,
  type/status/correlation only, no exception; 5xx: full exception internally, generic client
  detail. Fixed authored client details preserved.

### Entry 029

- **Site and method:**
  [API/Controllers/ContributionCyclesController.cs:319](API/Controllers/ContributionCyclesController.cs#L319)
  / Delete
- **Diagnostic type:** NotFoundException
- **Potential contents:** "Stokvel not found."
- **Personal/financial risk:** No submitted values in literal.
- **Before Stage 5:** Expected domain/validation 4xx was logged with full exception at Error
  (except authentication).
- **After / minimization justification:** Message unchanged. 4xx: Information,
  type/status/correlation only, no exception; 5xx: full exception internally, generic client
  detail. Fixed authored client details preserved.

### Entry 030

- **Site and method:**
  [API/Controllers/ContributionCyclesController.cs:325](API/Controllers/ContributionCyclesController.cs#L325)
  / Delete
- **Diagnostic type:** NotFoundException
- **Potential contents:** "Contribution cycle not found."
- **Personal/financial risk:** No submitted values in literal.
- **Before Stage 5:** Expected domain/validation 4xx was logged with full exception at Error
  (except authentication).
- **After / minimization justification:** Message unchanged. 4xx: Information,
  type/status/correlation only, no exception; 5xx: full exception internally, generic client
  detail. Fixed authored client details preserved.

### Entry 031

- **Site and method:**
  [API/Controllers/ContributionCyclesController.cs:356](API/Controllers/ContributionCyclesController.cs#L356)
  / ParseIfMatch
- **Diagnostic type:** RequestValidationException
- **Potential contents:** "If-Match header must contain a valid ETag value."
- **Personal/financial risk:** No submitted values in literal.
- **Before Stage 5:** Expected domain/validation 4xx was logged with full exception at Error
  (except authentication).
- **After / minimization justification:** Message unchanged. 4xx: Information,
  type/status/correlation only, no exception; 5xx: full exception internally, generic client
  detail. Fixed authored client details preserved.

### Entry 032

- **Site and method:**
  [API/Controllers/StokvelsController.cs:85](API/Controllers/StokvelsController.cs#L85) /
  GetById
- **Diagnostic type:** NotFoundException
- **Potential contents:** "Stokvel not found."
- **Personal/financial risk:** No submitted values in literal.
- **Before Stage 5:** Expected domain/validation 4xx was logged with full exception at Error
  (except authentication).
- **After / minimization justification:** Message unchanged. 4xx: Information,
  type/status/correlation only, no exception; 5xx: full exception internally, generic client
  detail. Fixed authored client details preserved.

### Entry 033

- **Site and method:**
  [API/Controllers/StokvelsController.cs:116](API/Controllers/StokvelsController.cs#L116) /
  GetMembers
- **Diagnostic type:** NotFoundException
- **Potential contents:** "Stokvel not found."
- **Personal/financial risk:** No submitted values in literal.
- **Before Stage 5:** Expected domain/validation 4xx was logged with full exception at Error
  (except authentication).
- **After / minimization justification:** Message unchanged. 4xx: Information,
  type/status/correlation only, no exception; 5xx: full exception internally, generic client
  detail. Fixed authored client details preserved.

### Entry 034

- **Site and method:**
  [API/Controllers/StokvelsController.cs:120](API/Controllers/StokvelsController.cs#L120) /
  GetMembers
- **Diagnostic type:** RequestValidationException
- **Potential contents:** "Page size cannot be negative."
- **Personal/financial risk:** No submitted values in literal.
- **Before Stage 5:** Expected domain/validation 4xx was logged with full exception at Error
  (except authentication).
- **After / minimization justification:** Message unchanged. 4xx: Information,
  type/status/correlation only, no exception; 5xx: full exception internally, generic client
  detail. Fixed authored client details preserved.

### Entry 035

- **Site and method:**
  [API/Controllers/StokvelsController.cs:202](API/Controllers/StokvelsController.cs#L202) /
  Update
- **Diagnostic type:** NotFoundException
- **Potential contents:** "Stokvel not found."
- **Personal/financial risk:** No submitted values in literal.
- **Before Stage 5:** Expected domain/validation 4xx was logged with full exception at Error
  (except authentication).
- **After / minimization justification:** Message unchanged. 4xx: Information,
  type/status/correlation only, no exception; 5xx: full exception internally, generic client
  detail. Fixed authored client details preserved.

### Entry 036

- **Site and method:**
  [API/Controllers/StokvelsController.cs:206](API/Controllers/StokvelsController.cs#L206) /
  Update
- **Diagnostic type:** RequestValidationException
- **Potential contents:** "If-Match header is required."
- **Personal/financial risk:** No submitted values in literal.
- **Before Stage 5:** Expected domain/validation 4xx was logged with full exception at Error
  (except authentication).
- **After / minimization justification:** Message unchanged. 4xx: Information,
  type/status/correlation only, no exception; 5xx: full exception internally, generic client
  detail. Fixed authored client details preserved.

### Entry 037

- **Site and method:**
  [API/Controllers/StokvelsController.cs:213](API/Controllers/StokvelsController.cs#L213) /
  Update
- **Diagnostic type:** PreconditionFailedException
- **Potential contents:** "The stokvel has changed. Refetch it and retry with the current
  ETag."
- **Personal/financial risk:** No submitted values in literal.
- **Before Stage 5:** Expected domain/validation 4xx was logged with full exception at Error
  (except authentication).
- **After / minimization justification:** Message unchanged. 4xx: Information,
  type/status/correlation only, no exception; 5xx: full exception internally, generic client
  detail. Fixed authored client details preserved.

### Entry 038

- **Site and method:**
  [API/Controllers/StokvelsController.cs:247](API/Controllers/StokvelsController.cs#L247) /
  Delete
- **Diagnostic type:** NotFoundException
- **Potential contents:** "Stokvel not found."
- **Personal/financial risk:** No submitted values in literal.
- **Before Stage 5:** Expected domain/validation 4xx was logged with full exception at Error
  (except authentication).
- **After / minimization justification:** Message unchanged. 4xx: Information,
  type/status/correlation only, no exception; 5xx: full exception internally, generic client
  detail. Fixed authored client details preserved.

### Entry 039

- **Site and method:**
  [API/Controllers/StokvelsController.cs:384](API/Controllers/StokvelsController.cs#L384) /
  ParseIfMatch
- **Diagnostic type:** RequestValidationException
- **Potential contents:** "If-Match header must contain a valid ETag value."
- **Personal/financial risk:** No submitted values in literal.
- **Before Stage 5:** Expected domain/validation 4xx was logged with full exception at Error
  (except authentication).
- **After / minimization justification:** Message unchanged. 4xx: Information,
  type/status/correlation only, no exception; 5xx: full exception internally, generic client
  detail. Fixed authored client details preserved.

### Entry 040

- **Site and method:**
  [API/Controllers/UsersController.cs:65](API/Controllers/UsersController.cs#L65) / GetById
- **Diagnostic type:** NotFoundException
- **Potential contents:** "User not found."
- **Personal/financial risk:** No submitted values in literal.
- **Before Stage 5:** Expected domain/validation 4xx was logged with full exception at Error
  (except authentication).
- **After / minimization justification:** Message unchanged. 4xx: Information,
  type/status/correlation only, no exception; 5xx: full exception internally, generic client
  detail. Fixed authored client details preserved.

### Entry 041

- **Site and method:**
  [API/Controllers/UsersController.cs:131](API/Controllers/UsersController.cs#L131) / Update
- **Diagnostic type:** NotFoundException
- **Potential contents:** "User not found."
- **Personal/financial risk:** No submitted values in literal.
- **Before Stage 5:** Expected domain/validation 4xx was logged with full exception at Error
  (except authentication).
- **After / minimization justification:** Message unchanged. 4xx: Information,
  type/status/correlation only, no exception; 5xx: full exception internally, generic client
  detail. Fixed authored client details preserved.

### Entry 042

- **Site and method:**
  [API/Controllers/UsersController.cs:135](API/Controllers/UsersController.cs#L135) / Update
- **Diagnostic type:** RequestValidationException
- **Potential contents:** "If-Match header is required."
- **Personal/financial risk:** No submitted values in literal.
- **Before Stage 5:** Expected domain/validation 4xx was logged with full exception at Error
  (except authentication).
- **After / minimization justification:** Message unchanged. 4xx: Information,
  type/status/correlation only, no exception; 5xx: full exception internally, generic client
  detail. Fixed authored client details preserved.

### Entry 043

- **Site and method:**
  [API/Controllers/UsersController.cs:142](API/Controllers/UsersController.cs#L142) / Update
- **Diagnostic type:** PreconditionFailedException
- **Potential contents:** "The user has changed. Refetch it and retry with the current ETag."
- **Personal/financial risk:** No submitted values in literal.
- **Before Stage 5:** Expected domain/validation 4xx was logged with full exception at Error
  (except authentication).
- **After / minimization justification:** Message unchanged. 4xx: Information,
  type/status/correlation only, no exception; 5xx: full exception internally, generic client
  detail. Fixed authored client details preserved.

### Entry 044

- **Site and method:**
  [API/Controllers/UsersController.cs:174](API/Controllers/UsersController.cs#L174) / Delete
- **Diagnostic type:** NotFoundException
- **Potential contents:** "User not found."
- **Personal/financial risk:** No submitted values in literal.
- **Before Stage 5:** Expected domain/validation 4xx was logged with full exception at Error
  (except authentication).
- **After / minimization justification:** Message unchanged. 4xx: Information,
  type/status/correlation only, no exception; 5xx: full exception internally, generic client
  detail. Fixed authored client details preserved.

### Entry 045

- **Site and method:**
  [API/Controllers/UsersController.cs:202](API/Controllers/UsersController.cs#L202) /
  ParseIfMatch
- **Diagnostic type:** RequestValidationException
- **Potential contents:** "If-Match header must contain a valid ETag value."
- **Personal/financial risk:** No submitted values in literal.
- **Before Stage 5:** Expected domain/validation 4xx was logged with full exception at Error
  (except authentication).
- **After / minimization justification:** Message unchanged. 4xx: Information,
  type/status/correlation only, no exception; 5xx: full exception internally, generic client
  detail. Fixed authored client details preserved.

### Entry 046

- **Site and method:**
  [API/Filters/ValidationFilter.cs:23](API/Filters/ValidationFilter.cs#L23) /
  OnActionExecutionAsync
- **Diagnostic type:** RequestValidationException
- **Potential contents:** "The request body is invalid."
- **Personal/financial risk:** No submitted values in literal.
- **Before Stage 5:** Expected domain/validation 4xx was logged with full exception at Error
  (except authentication).
- **After / minimization justification:** Message unchanged. 4xx: Information,
  type/status/correlation only, no exception; 5xx: full exception internally, generic client
  detail. Fixed authored client details preserved.

### Entry 047

- **Site and method:**
  [API/Filters/ValidationFilter.cs:56](API/Filters/ValidationFilter.cs#L56) /
  OnActionExecutionAsync
- **Diagnostic type:** RequestValidationException
- **Potential contents:** message
- **Personal/financial risk:** Dynamic diagnostic / possible submitted data; do not retain on
  4xx.
- **Before Stage 5:** Expected domain/validation 4xx was logged with full exception at Error
  (except authentication).
- **After / minimization justification:** Message unchanged. 4xx: Information,
  type/status/correlation only, no exception; 5xx: full exception internally, generic client
  detail. Fixed authored client details preserved.

### Entry 048

- **Site and method:**
  [API/Handlers/GlobalExceptionHandler.cs:60](API/Handlers/GlobalExceptionHandler.cs#L60) /
  TryHandleAsync
- **Diagnostic type:** ILogger _logger.LogError
- **Potential contents:** exception, "Request failed with {StatusCode}. CorrelationId:
  {CorrelationId}", statusCode, correlationId
- **Personal/financial risk:** Full 5xx exception/inner chain may contain sensitive
  third-party diagnostics. Residual risk explicitly retained for diagnosis.
- **Before Stage 5:** Error with full exception for every non-auth failure, including 4xx.
- **After / minimization justification:** Error only for 5xx, full exception retained
  internally; generic client detail. No exception text interpolated into template.

### Entry 049

- **Site and method:**
  [API/Handlers/GlobalExceptionHandler.cs:63](API/Handlers/GlobalExceptionHandler.cs#L63) /
  TryHandleAsync
- **Diagnostic type:** ILogger _logger.LogInformation
- **Potential contents:** "Request rejected with {StatusCode} ({ExceptionType}).
  CorrelationId: {CorrelationId}", statusCode, exception.GetType().Name, correlationId
- **Personal/financial risk:** Fixed event/state/type/status plus W3C ID; operational
  correlation is linkable and needs retention/access controls.
- **Before Stage 5:** Authentication-only Information event; other 4xx were Error with full
  exception.
- **After / minimization justification:** All 4xx Information: exception type/status/W3C
  correlation only; no message or exception object.

### Entry 050

- **Site and method:**
  [API/Observability/Correlation.cs:14](API/Observability/Correlation.cs#L14) / GetId
- **Diagnostic type:** InvalidOperationException
- **Potential contents:** "A W3C request activity is required for error correlation."
- **Personal/financial risk:** No submitted values in literal.
- **Before Stage 5:** Literal or generated diagnostic; auth 401 was Information; unexpected
  500 was Error.
- **After / minimization justification:** Message unchanged. 4xx: Information,
  type/status/correlation only, no exception; 5xx: full exception internally, generic client
  detail. Fixed authored client details preserved.

### Entry 051

- **Site and method:** [API/Program.cs:106](API/Program.cs#L106) / startup / DbContext
  configuration
- **Diagnostic type:** InvalidOperationException
- **Potential contents:** "Connection string 'RondiTrackDb' was not found."
- **Personal/financial risk:** No submitted values in literal.
- **Before Stage 5:** Literal or generated diagnostic; auth 401 was Information; unexpected
  500 was Error.
- **After / minimization justification:** Message unchanged. 4xx: Information,
  type/status/correlation only, no exception; 5xx: full exception internally, generic client
  detail. Fixed authored client details preserved.

### Entry 052

- **Site and method:** [API/Program.cs:98](API/Program.cs#L98) / CustomizeProblemDetails
  callback
- **Diagnostic type:** ILogger logger.Log
- **Potential contents:** status &gt;= 500 ? LogLevel.Error : LogLevel.Information, "Request
  failed with {StatusCode}. CorrelationId: {CorrelationId}", status,
  Correlation.GetId(context.HttpContext)
- **Personal/financial risk:** Fixed event/state/type/status plus W3C ID; operational
  correlation is linkable and needs retention/access controls.
- **Before Stage 5:** Existing structured status/correlation event; no submitted values.
- **After / minimization justification:** Retained safe fields and level; no request/response
  bodies, identities, amounts or secrets.

### Entry 053

- **Site and method:** [API/Program.cs:180](API/Program.cs#L180) / status-code callback
- **Diagnostic type:** ILogger .LogInformation
- **Potential contents:** "Request rejected with {StatusCode}. CorrelationId:
  {CorrelationId}", http.Response.StatusCode, Correlation.GetId(http)
- **Personal/financial risk:** Fixed event/state/type/status plus W3C ID; operational
  correlation is linkable and needs retention/access controls.
- **Before Stage 5:** Existing structured status/correlation event; no submitted values.
- **After / minimization justification:** Retained safe fields and level; no request/response
  bodies, identities, amounts or secrets.

### Entry 054

- **Site and method:**
  [API/Services/ContributionService.cs:48](API/Services/ContributionService.cs#L48) /
  RecordContributionAsync
- **Diagnostic type:** RequestValidationException
- **Potential contents:** "Idempotency-Key header is required."
- **Personal/financial risk:** No submitted values in literal.
- **Before Stage 5:** Expected domain/validation 4xx was logged with full exception at Error
  (except authentication).
- **After / minimization justification:** Message unchanged. 4xx: Information,
  type/status/correlation only, no exception; 5xx: full exception internally, generic client
  detail. Fixed authored client details preserved.

### Entry 055

- **Site and method:**
  [API/Services/ContributionService.cs:66](API/Services/ContributionService.cs#L66) /
  RecordContributionAsync
- **Diagnostic type:** BusinessRuleException
- **Potential contents:** "Idempotency-Key was already used with a different request."
- **Personal/financial risk:** No submitted values in literal.
- **Before Stage 5:** Expected domain/validation 4xx was logged with full exception at Error
  (except authentication).
- **After / minimization justification:** Message unchanged. 4xx: Information,
  type/status/correlation only, no exception; 5xx: full exception internally, generic client
  detail. Fixed authored client details preserved.

### Entry 056

- **Site and method:**
  [API/Services/ContributionService.cs:79](API/Services/ContributionService.cs#L79) /
  RecordContributionAsync
- **Diagnostic type:** NotFoundException
- **Potential contents:** "Stokvel not found."
- **Personal/financial risk:** No submitted values in literal.
- **Before Stage 5:** Expected domain/validation 4xx was logged with full exception at Error
  (except authentication).
- **After / minimization justification:** Message unchanged. 4xx: Information,
  type/status/correlation only, no exception; 5xx: full exception internally, generic client
  detail. Fixed authored client details preserved.

### Entry 057

- **Site and method:**
  [API/Services/ContributionService.cs:89](API/Services/ContributionService.cs#L89) /
  RecordContributionAsync
- **Diagnostic type:** NotFoundException
- **Potential contents:** "User not found."
- **Personal/financial risk:** No submitted values in literal.
- **Before Stage 5:** Expected domain/validation 4xx was logged with full exception at Error
  (except authentication).
- **After / minimization justification:** Message unchanged. 4xx: Information,
  type/status/correlation only, no exception; 5xx: full exception internally, generic client
  detail. Fixed authored client details preserved.

### Entry 058

- **Site and method:**
  [API/Services/ContributionService.cs:100](API/Services/ContributionService.cs#L100) /
  RecordContributionAsync
- **Diagnostic type:** BusinessRuleException
- **Potential contents:** "User is not a member of this stokvel."
- **Personal/financial risk:** No submitted values in literal.
- **Before Stage 5:** Expected domain/validation 4xx was logged with full exception at Error
  (except authentication).
- **After / minimization justification:** Message unchanged. 4xx: Information,
  type/status/correlation only, no exception; 5xx: full exception internally, generic client
  detail. Fixed authored client details preserved.

### Entry 059

- **Site and method:**
  [API/Services/ContributionService.cs:110](API/Services/ContributionService.cs#L110) /
  RecordContributionAsync
- **Diagnostic type:** NotFoundException
- **Potential contents:** "Contribution cycle not found."
- **Personal/financial risk:** No submitted values in literal.
- **Before Stage 5:** Expected domain/validation 4xx was logged with full exception at Error
  (except authentication).
- **After / minimization justification:** Message unchanged. 4xx: Information,
  type/status/correlation only, no exception; 5xx: full exception internally, generic client
  detail. Fixed authored client details preserved.

### Entry 060

- **Site and method:**
  [API/Services/ContributionService.cs:116](API/Services/ContributionService.cs#L116) /
  RecordContributionAsync
- **Diagnostic type:** BusinessRuleException
- **Potential contents:** "Contribution cycle does not belong to this stokvel."
- **Personal/financial risk:** No submitted values in literal.
- **Before Stage 5:** Expected domain/validation 4xx was logged with full exception at Error
  (except authentication).
- **After / minimization justification:** Message unchanged. 4xx: Information,
  type/status/correlation only, no exception; 5xx: full exception internally, generic client
  detail. Fixed authored client details preserved.

### Entry 061

- **Site and method:**
  [API/Services/ContributionService.cs:129](API/Services/ContributionService.cs#L129) /
  RecordContributionAsync
- **Diagnostic type:** BusinessRuleException
- **Potential contents:** "A contribution already exists for this member and cycle."
- **Personal/financial risk:** No submitted values in literal.
- **Before Stage 5:** Expected domain/validation 4xx was logged with full exception at Error
  (except authentication).
- **After / minimization justification:** Message unchanged. 4xx: Information,
  type/status/correlation only, no exception; 5xx: full exception internally, generic client
  detail. Fixed authored client details preserved.

### Entry 062

- **Site and method:**
  [API/Services/MembershipService.cs:33](API/Services/MembershipService.cs#L33) /
  AddMemberAsync
- **Diagnostic type:** NotFoundException
- **Potential contents:** "Stokvel not found."
- **Personal/financial risk:** No submitted values in literal.
- **Before Stage 5:** Expected domain/validation 4xx was logged with full exception at Error
  (except authentication).
- **After / minimization justification:** Message unchanged. 4xx: Information,
  type/status/correlation only, no exception; 5xx: full exception internally, generic client
  detail. Fixed authored client details preserved.

### Entry 063

- **Site and method:**
  [API/Services/MembershipService.cs:43](API/Services/MembershipService.cs#L43) /
  AddMemberAsync
- **Diagnostic type:** NotFoundException
- **Potential contents:** "User not found."
- **Personal/financial risk:** No submitted values in literal.
- **Before Stage 5:** Expected domain/validation 4xx was logged with full exception at Error
  (except authentication).
- **After / minimization justification:** Message unchanged. 4xx: Information,
  type/status/correlation only, no exception; 5xx: full exception internally, generic client
  detail. Fixed authored client details preserved.

### Entry 064

- **Site and method:**
  [API/Services/MembershipService.cs:54](API/Services/MembershipService.cs#L54) /
  AddMemberAsync
- **Diagnostic type:** BusinessRuleException
- **Potential contents:** "User is already a member of this stokvel."
- **Personal/financial risk:** No submitted values in literal.
- **Before Stage 5:** Expected domain/validation 4xx was logged with full exception at Error
  (except authentication).
- **After / minimization justification:** Message unchanged. 4xx: Information,
  type/status/correlation only, no exception; 5xx: full exception internally, generic client
  detail. Fixed authored client details preserved.

### Entry 065

- **Site and method:**
  [API/Services/MembershipService.cs:77](API/Services/MembershipService.cs#L77) /
  RemoveMemberAsync
- **Diagnostic type:** NotFoundException
- **Potential contents:** "Stokvel not found."
- **Personal/financial risk:** No submitted values in literal.
- **Before Stage 5:** Expected domain/validation 4xx was logged with full exception at Error
  (except authentication).
- **After / minimization justification:** Message unchanged. 4xx: Information,
  type/status/correlation only, no exception; 5xx: full exception internally, generic client
  detail. Fixed authored client details preserved.

### Entry 066

- **Site and method:**
  [API/Services/MembershipService.cs:89](API/Services/MembershipService.cs#L89) /
  RemoveMemberAsync
- **Diagnostic type:** BusinessRuleException
- **Potential contents:** "User is not a member of this stokvel."
- **Personal/financial risk:** No submitted values in literal.
- **Before Stage 5:** Expected domain/validation 4xx was logged with full exception at Error
  (except authentication).
- **After / minimization justification:** Message unchanged. 4xx: Information,
  type/status/correlation only, no exception; 5xx: full exception internally, generic client
  detail. Fixed authored client details preserved.

### Entry 067

- **Site and method:** [API/Services/PayoutService.cs:55](API/Services/PayoutService.cs#L55) /
  CreatePayoutAsync
- **Diagnostic type:** NotFoundException
- **Potential contents:** "Contribution cycle not found."
- **Personal/financial risk:** No submitted values in literal.
- **Before Stage 5:** Expected domain/validation 4xx was logged with full exception at Error
  (except authentication).
- **After / minimization justification:** Message unchanged. 4xx: Information,
  type/status/correlation only, no exception; 5xx: full exception internally, generic client
  detail. Fixed authored client details preserved.

### Entry 068

- **Site and method:** [API/Services/PayoutService.cs:61](API/Services/PayoutService.cs#L61) /
  CreatePayoutAsync
- **Diagnostic type:** BusinessRuleException
- **Potential contents:** "Contribution cycle is not open for payout."
- **Personal/financial risk:** No submitted values in literal.
- **Before Stage 5:** Expected domain/validation 4xx was logged with full exception at Error
  (except authentication).
- **After / minimization justification:** Message unchanged. 4xx: Information,
  type/status/correlation only, no exception; 5xx: full exception internally, generic client
  detail. Fixed authored client details preserved.

### Entry 069

- **Site and method:** [API/Services/PayoutService.cs:67](API/Services/PayoutService.cs#L67) /
  CreatePayoutAsync
- **Diagnostic type:** BusinessRuleException
- **Potential contents:** "Payout amount must be greater than zero."
- **Personal/financial risk:** No submitted values in literal.
- **Before Stage 5:** Expected domain/validation 4xx was logged with full exception at Error
  (except authentication).
- **After / minimization justification:** Message unchanged. 4xx: Information,
  type/status/correlation only, no exception; 5xx: full exception internally, generic client
  detail. Fixed authored client details preserved.

### Entry 070

- **Site and method:** [API/Services/PayoutService.cs:97](API/Services/PayoutService.cs#L97) /
  CreatePayoutAsync
- **Diagnostic type:** BusinessRuleException
- **Potential contents:** "No eligible member is available for payout."
- **Personal/financial risk:** No submitted values in literal.
- **Before Stage 5:** Expected domain/validation 4xx was logged with full exception at Error
  (except authentication).
- **After / minimization justification:** Message unchanged. 4xx: Information,
  type/status/correlation only, no exception; 5xx: full exception internally, generic client
  detail. Fixed authored client details preserved.

### Entry 071

- **Site and method:** [API/Services/PayoutService.cs:124](API/Services/PayoutService.cs#L124)
  / CreatePayoutAsync
- **Diagnostic type:** rethrow
- **Potential contents:** Original exception / message / inner chain
- **Personal/financial risk:** Dynamic: may inherit personal or database diagnostics.
- **Before Stage 5:** Propagated original exception.
- **After / minimization justification:** Rethrow unchanged; handler applies 4xx minimality /
  5xx internal diagnostics. Test-only rethrows never deploy.

### Entry 072

- **Site and method:** [API/Services/PayoutService.cs:137](API/Services/PayoutService.cs#L137)
  / CreatePayoutAsync
- **Diagnostic type:** rethrow
- **Potential contents:** Original exception / message / inner chain
- **Personal/financial risk:** Dynamic: may inherit personal or database diagnostics.
- **Before Stage 5:** Propagated original exception.
- **After / minimization justification:** Rethrow unchanged; handler applies 4xx minimality /
  5xx internal diagnostics. Test-only rethrows never deploy.

### Entry 073

- **Site and method:**
  [API/Validators/CreateContributionCycleRequestValidator.cs:14](API/Validators/CreateContributionCycleRequestValidator.cs#L14)
  / CreateContributionCycleRequestValidator
- **Diagnostic type:** FluentValidation message
- **Potential contents:** "Contribution cycle period is required."
- **Personal/financial risk:** Current literals do not interpolate attempted values.
- **Before Stage 5:** Combined into RequestValidationException; full 400 exception logged at
  Error.
- **After / minimization justification:** Client message unchanged; 400 logs omit message and
  exception object.

### Entry 074

- **Site and method:**
  [API/Validators/CreateContributionCycleRequestValidator.cs:16](API/Validators/CreateContributionCycleRequestValidator.cs#L16)
  / CreateContributionCycleRequestValidator
- **Diagnostic type:** FluentValidation message
- **Potential contents:** "Period must use the YYYY-MM format."
- **Personal/financial risk:** Current literals do not interpolate attempted values.
- **Before Stage 5:** Combined into RequestValidationException; full 400 exception logged at
  Error.
- **After / minimization justification:** Client message unchanged; 400 logs omit message and
  exception object.

### Entry 075

- **Site and method:**
  [API/Validators/CreateContributionCycleRequestValidator.cs:20](API/Validators/CreateContributionCycleRequestValidator.cs#L20)
  / CreateContributionCycleRequestValidator
- **Diagnostic type:** FluentValidation message
- **Potential contents:** "Target amount must be greater than zero."
- **Personal/financial risk:** Current literals do not interpolate attempted values.
- **Before Stage 5:** Combined into RequestValidationException; full 400 exception logged at
  Error.
- **After / minimization justification:** Client message unchanged; 400 logs omit message and
  exception object.

### Entry 076

- **Site and method:**
  [API/Validators/CreateStokvelRequestValidator.cs:13](API/Validators/CreateStokvelRequestValidator.cs#L13)
  / CreateStokvelRequestValidator
- **Diagnostic type:** FluentValidation message
- **Potential contents:** "Stokvel name is required."
- **Personal/financial risk:** Current literals do not interpolate attempted values.
- **Before Stage 5:** Combined into RequestValidationException; full 400 exception logged at
  Error.
- **After / minimization justification:** Client message unchanged; 400 logs omit message and
  exception object.

### Entry 077

- **Site and method:**
  [API/Validators/CreateStokvelRequestValidator.cs:17](API/Validators/CreateStokvelRequestValidator.cs#L17)
  / CreateStokvelRequestValidator
- **Diagnostic type:** FluentValidation message
- **Potential contents:** "Contribution amount must be greater than zero."
- **Personal/financial risk:** Current literals do not interpolate attempted values.
- **Before Stage 5:** Combined into RequestValidationException; full 400 exception logged at
  Error.
- **After / minimization justification:** Client message unchanged; 400 logs omit message and
  exception object.

### Entry 078

- **Site and method:**
  [API/Validators/CreateUserRequestValidator.cs:13](API/Validators/CreateUserRequestValidator.cs#L13)
  / CreateUserRequestValidator
- **Diagnostic type:** FluentValidation message
- **Potential contents:** "Name is required."
- **Personal/financial risk:** Current literals do not interpolate attempted values.
- **Before Stage 5:** Combined into RequestValidationException; full 400 exception logged at
  Error.
- **After / minimization justification:** Client message unchanged; 400 logs omit message and
  exception object.

### Entry 079

- **Site and method:**
  [API/Validators/CreateUserRequestValidator.cs:17](API/Validators/CreateUserRequestValidator.cs#L17)
  / CreateUserRequestValidator
- **Diagnostic type:** FluentValidation message
- **Potential contents:** "Email is required."
- **Personal/financial risk:** Current literals do not interpolate attempted values.
- **Before Stage 5:** Combined into RequestValidationException; full 400 exception logged at
  Error.
- **After / minimization justification:** Client message unchanged; 400 logs omit message and
  exception object.

### Entry 080

- **Site and method:**
  [API/Validators/CreateUserRequestValidator.cs:19](API/Validators/CreateUserRequestValidator.cs#L19)
  / CreateUserRequestValidator
- **Diagnostic type:** FluentValidation message
- **Potential contents:** "A valid email is required."
- **Personal/financial risk:** Current literals do not interpolate attempted values.
- **Before Stage 5:** Combined into RequestValidationException; full 400 exception logged at
  Error.
- **After / minimization justification:** Client message unchanged; 400 logs omit message and
  exception object.

### Entry 081

- **Site and method:**
  [API/Validators/LoginRequestValidator.cs:10](API/Validators/LoginRequestValidator.cs#L10) /
  LoginRequestValidator
- **Diagnostic type:** FluentValidation default NotEmpty message
- **Potential contents:** Email NotEmpty()
- **Personal/financial risk:** Generated field/rule/length text. Future validators could use
  attempted-value placeholders.
- **Before Stage 5:** Default message assembled into validation exception and logged at Error.
- **After / minimization justification:** Rule unchanged; generated validation text is
  excluded from 4xx logs. This default rule does not echo credentials.

### Entry 082

- **Site and method:**
  [API/Validators/LoginRequestValidator.cs:10](API/Validators/LoginRequestValidator.cs#L10) /
  LoginRequestValidator
- **Diagnostic type:** FluentValidation default EmailAddress message
- **Potential contents:** Email EmailAddress()
- **Personal/financial risk:** Generated field/rule/length text. Future validators could use
  attempted-value placeholders.
- **Before Stage 5:** Default message assembled into validation exception and logged at Error.
- **After / minimization justification:** Rule unchanged; generated validation text is
  excluded from 4xx logs. This default rule does not echo credentials.

### Entry 083

- **Site and method:**
  [API/Validators/LoginRequestValidator.cs:11](API/Validators/LoginRequestValidator.cs#L11) /
  LoginRequestValidator
- **Diagnostic type:** FluentValidation default NotEmpty message
- **Potential contents:** Password NotEmpty()
- **Personal/financial risk:** Generated field/rule/length text. Future validators could use
  attempted-value placeholders.
- **Before Stage 5:** Default message assembled into validation exception and logged at Error.
- **After / minimization justification:** Rule unchanged; generated validation text is
  excluded from 4xx logs. This default rule does not echo credentials.

### Entry 084

- **Site and method:**
  [API/Validators/RecordContributionRequestValidator.cs:13](API/Validators/RecordContributionRequestValidator.cs#L13)
  / RecordContributionRequestValidator
- **Diagnostic type:** FluentValidation message
- **Potential contents:** "Contribution amount must be greater than zero."
- **Personal/financial risk:** Current literals do not interpolate attempted values.
- **Before Stage 5:** Combined into RequestValidationException; full 400 exception logged at
  Error.
- **After / minimization justification:** Client message unchanged; 400 logs omit message and
  exception object.

### Entry 085

- **Site and method:**
  [API/Validators/RecordContributionRequestValidator.cs:18](API/Validators/RecordContributionRequestValidator.cs#L18)
  / RecordContributionRequestValidator
- **Diagnostic type:** FluentValidation message
- **Potential contents:** "Contribution cycle ID is required."
- **Personal/financial risk:** Current literals do not interpolate attempted values.
- **Before Stage 5:** Combined into RequestValidationException; full 400 exception logged at
  Error.
- **After / minimization justification:** Client message unchanged; 400 logs omit message and
  exception object.

### Entry 086

- **Site and method:**
  [API/Validators/RegisterRequestValidator.cs:10](API/Validators/RegisterRequestValidator.cs#L10)
  / RegisterRequestValidator
- **Diagnostic type:** FluentValidation default NotEmpty message
- **Potential contents:** Name NotEmpty()
- **Personal/financial risk:** Generated field/rule/length text. Future validators could use
  attempted-value placeholders.
- **Before Stage 5:** Default message assembled into validation exception and logged at Error.
- **After / minimization justification:** Rule unchanged; generated validation text is
  excluded from 4xx logs. This default rule does not echo credentials.

### Entry 087

- **Site and method:**
  [API/Validators/RegisterRequestValidator.cs:11](API/Validators/RegisterRequestValidator.cs#L11)
  / RegisterRequestValidator
- **Diagnostic type:** FluentValidation default NotEmpty message
- **Potential contents:** Email NotEmpty()
- **Personal/financial risk:** Generated field/rule/length text. Future validators could use
  attempted-value placeholders.
- **Before Stage 5:** Default message assembled into validation exception and logged at Error.
- **After / minimization justification:** Rule unchanged; generated validation text is
  excluded from 4xx logs. This default rule does not echo credentials.

### Entry 088

- **Site and method:**
  [API/Validators/RegisterRequestValidator.cs:11](API/Validators/RegisterRequestValidator.cs#L11)
  / RegisterRequestValidator
- **Diagnostic type:** FluentValidation default EmailAddress message
- **Potential contents:** Email EmailAddress()
- **Personal/financial risk:** Generated field/rule/length text. Future validators could use
  attempted-value placeholders.
- **Before Stage 5:** Default message assembled into validation exception and logged at Error.
- **After / minimization justification:** Rule unchanged; generated validation text is
  excluded from 4xx logs. This default rule does not echo credentials.

### Entry 089

- **Site and method:**
  [API/Validators/RegisterRequestValidator.cs:12](API/Validators/RegisterRequestValidator.cs#L12)
  / RegisterRequestValidator
- **Diagnostic type:** FluentValidation default NotEmpty message
- **Potential contents:** Password NotEmpty()
- **Personal/financial risk:** Generated field/rule/length text. Future validators could use
  attempted-value placeholders.
- **Before Stage 5:** Default message assembled into validation exception and logged at Error.
- **After / minimization justification:** Rule unchanged; generated validation text is
  excluded from 4xx logs. This default rule does not echo credentials.

### Entry 090

- **Site and method:**
  [API/Validators/RegisterRequestValidator.cs:12](API/Validators/RegisterRequestValidator.cs#L12)
  / RegisterRequestValidator
- **Diagnostic type:** FluentValidation default MinimumLength message
- **Potential contents:** Password MinimumLength(12)
- **Personal/financial risk:** Generated field/rule/length text. Future validators could use
  attempted-value placeholders.
- **Before Stage 5:** Default message assembled into validation exception and logged at Error.
- **After / minimization justification:** Rule unchanged; generated validation text is
  excluded from 4xx logs. This default rule does not echo credentials.

### Entry 091

- **Site and method:**
  [API/Validators/UpdateContributionCycleRequestValidator.cs:14](API/Validators/UpdateContributionCycleRequestValidator.cs#L14)
  / UpdateContributionCycleRequestValidator
- **Diagnostic type:** FluentValidation message
- **Potential contents:** "Contribution cycle period is required."
- **Personal/financial risk:** Current literals do not interpolate attempted values.
- **Before Stage 5:** Combined into RequestValidationException; full 400 exception logged at
  Error.
- **After / minimization justification:** Client message unchanged; 400 logs omit message and
  exception object.

### Entry 092

- **Site and method:**
  [API/Validators/UpdateContributionCycleRequestValidator.cs:16](API/Validators/UpdateContributionCycleRequestValidator.cs#L16)
  / UpdateContributionCycleRequestValidator
- **Diagnostic type:** FluentValidation message
- **Potential contents:** "Period must use the YYYY-MM format."
- **Personal/financial risk:** Current literals do not interpolate attempted values.
- **Before Stage 5:** Combined into RequestValidationException; full 400 exception logged at
  Error.
- **After / minimization justification:** Client message unchanged; 400 logs omit message and
  exception object.

### Entry 093

- **Site and method:**
  [API/Validators/UpdateContributionCycleRequestValidator.cs:20](API/Validators/UpdateContributionCycleRequestValidator.cs#L20)
  / UpdateContributionCycleRequestValidator
- **Diagnostic type:** FluentValidation message
- **Potential contents:** "Target amount must be greater than zero."
- **Personal/financial risk:** Current literals do not interpolate attempted values.
- **Before Stage 5:** Combined into RequestValidationException; full 400 exception logged at
  Error.
- **After / minimization justification:** Client message unchanged; 400 logs omit message and
  exception object.

### Entry 094

- **Site and method:**
  [API/Validators/UpdateStokvelRequestValidator.cs:13](API/Validators/UpdateStokvelRequestValidator.cs#L13)
  / UpdateStokvelRequestValidator
- **Diagnostic type:** FluentValidation message
- **Potential contents:** "Stokvel name is required."
- **Personal/financial risk:** Current literals do not interpolate attempted values.
- **Before Stage 5:** Combined into RequestValidationException; full 400 exception logged at
  Error.
- **After / minimization justification:** Client message unchanged; 400 logs omit message and
  exception object.

### Entry 095

- **Site and method:**
  [API/Validators/UpdateStokvelRequestValidator.cs:17](API/Validators/UpdateStokvelRequestValidator.cs#L17)
  / UpdateStokvelRequestValidator
- **Diagnostic type:** FluentValidation message
- **Potential contents:** "Contribution amount must be greater than zero."
- **Personal/financial risk:** Current literals do not interpolate attempted values.
- **Before Stage 5:** Combined into RequestValidationException; full 400 exception logged at
  Error.
- **After / minimization justification:** Client message unchanged; 400 logs omit message and
  exception object.

### Entry 096

- **Site and method:**
  [API/Validators/UpdateUserRequestValidator.cs:13](API/Validators/UpdateUserRequestValidator.cs#L13)
  / UpdateUserRequestValidator
- **Diagnostic type:** FluentValidation message
- **Potential contents:** "Name is required."
- **Personal/financial risk:** Current literals do not interpolate attempted values.
- **Before Stage 5:** Combined into RequestValidationException; full 400 exception logged at
  Error.
- **After / minimization justification:** Client message unchanged; 400 logs omit message and
  exception object.

### Entry 097

- **Site and method:**
  [API/Validators/UpdateUserRequestValidator.cs:17](API/Validators/UpdateUserRequestValidator.cs#L17)
  / UpdateUserRequestValidator
- **Diagnostic type:** FluentValidation message
- **Potential contents:** "Email is required."
- **Personal/financial risk:** Current literals do not interpolate attempted values.
- **Before Stage 5:** Combined into RequestValidationException; full 400 exception logged at
  Error.
- **After / minimization justification:** Client message unchanged; 400 logs omit message and
  exception object.

### Entry 098

- **Site and method:**
  [API/Validators/UpdateUserRequestValidator.cs:19](API/Validators/UpdateUserRequestValidator.cs#L19)
  / UpdateUserRequestValidator
- **Diagnostic type:** FluentValidation message
- **Potential contents:** "A valid email is required."
- **Personal/financial risk:** Current literals do not interpolate attempted values.
- **Before Stage 5:** Combined into RequestValidationException; full 400 exception logged at
  Error.
- **After / minimization justification:** Client message unchanged; 400 logs omit message and
  exception object.

### Entry 099

- **Site and method:**
  [Domain/Entities/Contribution.cs:30](Domain/Entities/Contribution.cs#L30) / Contribution
- **Diagnostic type:** ArgumentException
- **Potential contents:** "Stokvel ID is required."
- **Personal/financial risk:** No submitted values in literal.
- **Before Stage 5:** Literal or generated diagnostic; auth 401 was Information; unexpected
  500 was Error.
- **After / minimization justification:** Message unchanged. 4xx: Information,
  type/status/correlation only, no exception; 5xx: full exception internally, generic client
  detail. Fixed authored client details preserved.

### Entry 100

- **Site and method:**
  [Domain/Entities/Contribution.cs:34](Domain/Entities/Contribution.cs#L34) / Contribution
- **Diagnostic type:** ArgumentException
- **Potential contents:** "User ID is required."
- **Personal/financial risk:** No submitted values in literal.
- **Before Stage 5:** Literal or generated diagnostic; auth 401 was Information; unexpected
  500 was Error.
- **After / minimization justification:** Message unchanged. 4xx: Information,
  type/status/correlation only, no exception; 5xx: full exception internally, generic client
  detail. Fixed authored client details preserved.

### Entry 101

- **Site and method:**
  [Domain/Entities/Contribution.cs:38](Domain/Entities/Contribution.cs#L38) / Contribution
- **Diagnostic type:** ArgumentException
- **Potential contents:** "Contribution cycle ID is required."
- **Personal/financial risk:** No submitted values in literal.
- **Before Stage 5:** Literal or generated diagnostic; auth 401 was Information; unexpected
  500 was Error.
- **After / minimization justification:** Message unchanged. 4xx: Information,
  type/status/correlation only, no exception; 5xx: full exception internally, generic client
  detail. Fixed authored client details preserved.

### Entry 102

- **Site and method:**
  [Domain/Entities/Contribution.cs:42](Domain/Entities/Contribution.cs#L42) / Contribution
- **Diagnostic type:** ArgumentException
- **Potential contents:** "Contribution amount must be greater than zero."
- **Personal/financial risk:** No submitted values in literal.
- **Before Stage 5:** Literal or generated diagnostic; auth 401 was Information; unexpected
  500 was Error.
- **After / minimization justification:** Message unchanged. 4xx: Information,
  type/status/correlation only, no exception; 5xx: full exception internally, generic client
  detail. Fixed authored client details preserved.

### Entry 103

- **Site and method:**
  [Domain/Entities/ContributionCycle.cs:28](Domain/Entities/ContributionCycle.cs#L28) /
  ContributionCycle
- **Diagnostic type:** ArgumentException
- **Potential contents:** "Stokvel ID is required."
- **Personal/financial risk:** No submitted values in literal.
- **Before Stage 5:** Literal or generated diagnostic; auth 401 was Information; unexpected
  500 was Error.
- **After / minimization justification:** Message unchanged. 4xx: Information,
  type/status/correlation only, no exception; 5xx: full exception internally, generic client
  detail. Fixed authored client details preserved.

### Entry 104

- **Site and method:**
  [Domain/Entities/ContributionCycle.cs:32](Domain/Entities/ContributionCycle.cs#L32) /
  ContributionCycle
- **Diagnostic type:** ArgumentException
- **Potential contents:** "Contribution cycle period is required."
- **Personal/financial risk:** No submitted values in literal.
- **Before Stage 5:** Literal or generated diagnostic; auth 401 was Information; unexpected
  500 was Error.
- **After / minimization justification:** Message unchanged. 4xx: Information,
  type/status/correlation only, no exception; 5xx: full exception internally, generic client
  detail. Fixed authored client details preserved.

### Entry 105

- **Site and method:**
  [Domain/Entities/ContributionCycle.cs:36](Domain/Entities/ContributionCycle.cs#L36) /
  ContributionCycle
- **Diagnostic type:** ArgumentException
- **Potential contents:** "Target amount must be greater than zero."
- **Personal/financial risk:** No submitted values in literal.
- **Before Stage 5:** Literal or generated diagnostic; auth 401 was Information; unexpected
  500 was Error.
- **After / minimization justification:** Message unchanged. 4xx: Information,
  type/status/correlation only, no exception; 5xx: full exception internally, generic client
  detail. Fixed authored client details preserved.

### Entry 106

- **Site and method:**
  [Domain/Entities/ContributionCycle.cs:51](Domain/Entities/ContributionCycle.cs#L51) /
  UpdateDetails
- **Diagnostic type:** ArgumentException
- **Potential contents:** "Contribution cycle period is required."
- **Personal/financial risk:** No submitted values in literal.
- **Before Stage 5:** Literal or generated diagnostic; auth 401 was Information; unexpected
  500 was Error.
- **After / minimization justification:** Message unchanged. 4xx: Information,
  type/status/correlation only, no exception; 5xx: full exception internally, generic client
  detail. Fixed authored client details preserved.

### Entry 107

- **Site and method:**
  [Domain/Entities/ContributionCycle.cs:55](Domain/Entities/ContributionCycle.cs#L55) /
  UpdateDetails
- **Diagnostic type:** ArgumentException
- **Potential contents:** "Target amount must be greater than zero."
- **Personal/financial risk:** No submitted values in literal.
- **Before Stage 5:** Literal or generated diagnostic; auth 401 was Information; unexpected
  500 was Error.
- **After / minimization justification:** Message unchanged. 4xx: Information,
  type/status/correlation only, no exception; 5xx: full exception internally, generic client
  detail. Fixed authored client details preserved.

### Entry 108

- **Site and method:** [Domain/Entities/Payout.cs:30](Domain/Entities/Payout.cs#L30) / Payout
- **Diagnostic type:** ArgumentException
- **Potential contents:** "Stokvel ID is required."
- **Personal/financial risk:** No submitted values in literal.
- **Before Stage 5:** Literal or generated diagnostic; auth 401 was Information; unexpected
  500 was Error.
- **After / minimization justification:** Message unchanged. 4xx: Information,
  type/status/correlation only, no exception; 5xx: full exception internally, generic client
  detail. Fixed authored client details preserved.

### Entry 109

- **Site and method:** [Domain/Entities/Payout.cs:34](Domain/Entities/Payout.cs#L34) / Payout
- **Diagnostic type:** ArgumentException
- **Potential contents:** "Recipient user ID is required."
- **Personal/financial risk:** No submitted values in literal.
- **Before Stage 5:** Literal or generated diagnostic; auth 401 was Information; unexpected
  500 was Error.
- **After / minimization justification:** Message unchanged. 4xx: Information,
  type/status/correlation only, no exception; 5xx: full exception internally, generic client
  detail. Fixed authored client details preserved.

### Entry 110

- **Site and method:** [Domain/Entities/Payout.cs:38](Domain/Entities/Payout.cs#L38) / Payout
- **Diagnostic type:** ArgumentException
- **Potential contents:** "Contribution cycle ID is required."
- **Personal/financial risk:** No submitted values in literal.
- **Before Stage 5:** Literal or generated diagnostic; auth 401 was Information; unexpected
  500 was Error.
- **After / minimization justification:** Message unchanged. 4xx: Information,
  type/status/correlation only, no exception; 5xx: full exception internally, generic client
  detail. Fixed authored client details preserved.

### Entry 111

- **Site and method:** [Domain/Entities/Payout.cs:42](Domain/Entities/Payout.cs#L42) / Payout
- **Diagnostic type:** ArgumentException
- **Potential contents:** "Payout amount must be greater than zero."
- **Personal/financial risk:** No submitted values in literal.
- **Before Stage 5:** Literal or generated diagnostic; auth 401 was Information; unexpected
  500 was Error.
- **After / minimization justification:** Message unchanged. 4xx: Information,
  type/status/correlation only, no exception; 5xx: full exception internally, generic client
  detail. Fixed authored client details preserved.

### Entry 112

- **Site and method:** [Domain/Entities/Payout.cs:57](Domain/Entities/Payout.cs#L57) /
  UpdateAmount
- **Diagnostic type:** ArgumentException
- **Potential contents:** "Payout amount must be greater than zero."
- **Personal/financial risk:** No submitted values in literal.
- **Before Stage 5:** Literal or generated diagnostic; auth 401 was Information; unexpected
  500 was Error.
- **After / minimization justification:** Message unchanged. 4xx: Information,
  type/status/correlation only, no exception; 5xx: full exception internally, generic client
  detail. Fixed authored client details preserved.

### Entry 113

- **Site and method:** [Domain/Entities/Stokvel.cs:26](Domain/Entities/Stokvel.cs#L26) /
  Stokvel
- **Diagnostic type:** ArgumentException
- **Potential contents:** "Stokvel name is required."
- **Personal/financial risk:** No submitted values in literal.
- **Before Stage 5:** Literal or generated diagnostic; auth 401 was Information; unexpected
  500 was Error.
- **After / minimization justification:** Message unchanged. 4xx: Information,
  type/status/correlation only, no exception; 5xx: full exception internally, generic client
  detail. Fixed authored client details preserved.

### Entry 114

- **Site and method:** [Domain/Entities/Stokvel.cs:29](Domain/Entities/Stokvel.cs#L29) /
  Stokvel
- **Diagnostic type:** ArgumentException
- **Potential contents:** "Contribution amount must be greater than zero."
- **Personal/financial risk:** No submitted values in literal.
- **Before Stage 5:** Literal or generated diagnostic; auth 401 was Information; unexpected
  500 was Error.
- **After / minimization justification:** Message unchanged. 4xx: Information,
  type/status/correlation only, no exception; 5xx: full exception internally, generic client
  detail. Fixed authored client details preserved.

### Entry 115

- **Site and method:** [Domain/Entities/Stokvel.cs:42](Domain/Entities/Stokvel.cs#L42) /
  UpdateDetails
- **Diagnostic type:** ArgumentException
- **Potential contents:** "Stokvel name is required."
- **Personal/financial risk:** No submitted values in literal.
- **Before Stage 5:** Literal or generated diagnostic; auth 401 was Information; unexpected
  500 was Error.
- **After / minimization justification:** Message unchanged. 4xx: Information,
  type/status/correlation only, no exception; 5xx: full exception internally, generic client
  detail. Fixed authored client details preserved.

### Entry 116

- **Site and method:** [Domain/Entities/Stokvel.cs:46](Domain/Entities/Stokvel.cs#L46) /
  UpdateDetails
- **Diagnostic type:** ArgumentException
- **Potential contents:** "Contribution amount must be greater than zero."
- **Personal/financial risk:** No submitted values in literal.
- **Before Stage 5:** Literal or generated diagnostic; auth 401 was Information; unexpected
  500 was Error.
- **After / minimization justification:** Message unchanged. 4xx: Information,
  type/status/correlation only, no exception; 5xx: full exception internally, generic client
  detail. Fixed authored client details preserved.

### Entry 117

- **Site and method:** [Domain/Entities/Stokvel.cs:57](Domain/Entities/Stokvel.cs#L57) /
  AddMember
- **Diagnostic type:** InvalidOperationException
- **Potential contents:** "User is already a member of this stokvel."
- **Personal/financial risk:** No submitted values in literal.
- **Before Stage 5:** Literal or generated diagnostic; auth 401 was Information; unexpected
  500 was Error.
- **After / minimization justification:** Message unchanged. 4xx: Information,
  type/status/correlation only, no exception; 5xx: full exception internally, generic client
  detail. Fixed authored client details preserved.

### Entry 118

- **Site and method:** [Domain/Entities/Stokvel.cs:72](Domain/Entities/Stokvel.cs#L72) /
  RemoveMember
- **Diagnostic type:** InvalidOperationException
- **Potential contents:** "User is not a member of this stokvel."
- **Personal/financial risk:** No submitted values in literal.
- **Before Stage 5:** Literal or generated diagnostic; auth 401 was Information; unexpected
  500 was Error.
- **After / minimization justification:** Message unchanged. 4xx: Information,
  type/status/correlation only, no exception; 5xx: full exception internally, generic client
  detail. Fixed authored client details preserved.

### Entry 119

- **Site and method:**
  [Domain/Entities/StokvelMember.cs:18](Domain/Entities/StokvelMember.cs#L18) / ChangeRole
- **Diagnostic type:** ArgumentException
- **Potential contents:** "Unknown stokvel role."
- **Personal/financial risk:** No submitted values in literal.
- **Before Stage 5:** Literal or generated diagnostic; auth 401 was Information; unexpected
  500 was Error.
- **After / minimization justification:** Message unchanged. 4xx: Information,
  type/status/correlation only, no exception; 5xx: full exception internally, generic client
  detail. Fixed authored client details preserved.

### Entry 120

- **Site and method:**
  [Domain/Entities/StokvelMember.cs:38](Domain/Entities/StokvelMember.cs#L38) / StokvelMember
- **Diagnostic type:** ArgumentException
- **Potential contents:** "Stokvel ID is required."
- **Personal/financial risk:** No submitted values in literal.
- **Before Stage 5:** Literal or generated diagnostic; auth 401 was Information; unexpected
  500 was Error.
- **After / minimization justification:** Message unchanged. 4xx: Information,
  type/status/correlation only, no exception; 5xx: full exception internally, generic client
  detail. Fixed authored client details preserved.

### Entry 121

- **Site and method:**
  [Domain/Entities/StokvelMember.cs:42](Domain/Entities/StokvelMember.cs#L42) / StokvelMember
- **Diagnostic type:** ArgumentException
- **Potential contents:** "User ID is required."
- **Personal/financial risk:** No submitted values in literal.
- **Before Stage 5:** Literal or generated diagnostic; auth 401 was Information; unexpected
  500 was Error.
- **After / minimization justification:** Message unchanged. 4xx: Information,
  type/status/correlation only, no exception; 5xx: full exception internally, generic client
  detail. Fixed authored client details preserved.

### Entry 122

- **Site and method:**
  [Domain/Entities/StokvelMember.cs:46](Domain/Entities/StokvelMember.cs#L46) / StokvelMember
- **Diagnostic type:** ArgumentException
- **Potential contents:** "Role is required."
- **Personal/financial risk:** No submitted values in literal.
- **Before Stage 5:** Literal or generated diagnostic; auth 401 was Information; unexpected
  500 was Error.
- **After / minimization justification:** Message unchanged. 4xx: Information,
  type/status/correlation only, no exception; 5xx: full exception internally, generic client
  detail. Fixed authored client details preserved.

### Entry 123

- **Site and method:** [Domain/Entities/User.cs:23](Domain/Entities/User.cs#L23) /
  SetPlatformRole
- **Diagnostic type:** ArgumentException
- **Potential contents:** "Unknown platform role."
- **Personal/financial risk:** No submitted values in literal.
- **Before Stage 5:** Literal or generated diagnostic; auth 401 was Information; unexpected
  500 was Error.
- **After / minimization justification:** Message unchanged. 4xx: Information,
  type/status/correlation only, no exception; 5xx: full exception internally, generic client
  detail. Fixed authored client details preserved.

### Entry 124

- **Site and method:** [Domain/Entities/User.cs:32](Domain/Entities/User.cs#L32) /
  SetPasswordHash
- **Diagnostic type:** ArgumentException
- **Potential contents:** "Password hash is required."
- **Personal/financial risk:** No submitted values in literal.
- **Before Stage 5:** Literal or generated diagnostic; auth 401 was Information; unexpected
  500 was Error.
- **After / minimization justification:** Message unchanged. 4xx: Information,
  type/status/correlation only, no exception; 5xx: full exception internally, generic client
  detail. Fixed authored client details preserved.

### Entry 125

- **Site and method:** [Domain/Entities/User.cs:44](Domain/Entities/User.cs#L44) / User
- **Diagnostic type:** ArgumentException
- **Potential contents:** "Name is required."
- **Personal/financial risk:** No submitted values in literal.
- **Before Stage 5:** Literal or generated diagnostic; auth 401 was Information; unexpected
  500 was Error.
- **After / minimization justification:** Message unchanged. 4xx: Information,
  type/status/correlation only, no exception; 5xx: full exception internally, generic client
  detail. Fixed authored client details preserved.

### Entry 126

- **Site and method:** [Domain/Entities/User.cs:47](Domain/Entities/User.cs#L47) / User
- **Diagnostic type:** ArgumentException
- **Potential contents:** "A valid email is required."
- **Personal/financial risk:** No submitted values in literal.
- **Before Stage 5:** Literal or generated diagnostic; auth 401 was Information; unexpected
  500 was Error.
- **After / minimization justification:** Message unchanged. 4xx: Information,
  type/status/correlation only, no exception; 5xx: full exception internally, generic client
  detail. Fixed authored client details preserved.

### Entry 127

- **Site and method:** [Domain/Entities/User.cs:58](Domain/Entities/User.cs#L58) /
  UpdateDetails
- **Diagnostic type:** ArgumentException
- **Potential contents:** "Name is required."
- **Personal/financial risk:** No submitted values in literal.
- **Before Stage 5:** Literal or generated diagnostic; auth 401 was Information; unexpected
  500 was Error.
- **After / minimization justification:** Message unchanged. 4xx: Information,
  type/status/correlation only, no exception; 5xx: full exception internally, generic client
  detail. Fixed authored client details preserved.

### Entry 128

- **Site and method:** [Domain/Entities/User.cs:61](Domain/Entities/User.cs#L61) /
  UpdateDetails
- **Diagnostic type:** ArgumentException
- **Potential contents:** "A valid email is required."
- **Personal/financial risk:** No submitted values in literal.
- **Before Stage 5:** Literal or generated diagnostic; auth 401 was Information; unexpected
  500 was Error.
- **After / minimization justification:** Message unchanged. 4xx: Information,
  type/status/correlation only, no exception; 5xx: full exception internally, generic client
  detail. Fixed authored client details preserved.

### Entry 129

- **Site and method:**
  [Domain/Exceptions/AuthenticationFailedException.cs:3](Domain/Exceptions/AuthenticationFailedException.cs#L3)
  / AuthenticationFailedException.ctor
- **Diagnostic type:** exception constructor forwarding
- **Potential contents:** Caller-supplied message
- **Personal/financial risk:** Dynamic: wrapper alone cannot guarantee safe contents.
- **Before Stage 5:** Forwarded unchanged to base exception.
- **After / minimization justification:** No constructor change; each caller is audited
  separately; 4xx logger excludes message/object.

### Entry 130

- **Site and method:**
  [Domain/Exceptions/BusinessRuleException.cs:6](Domain/Exceptions/BusinessRuleException.cs#L6)
  / BusinessRuleException.ctor
- **Diagnostic type:** exception constructor forwarding
- **Potential contents:** Caller-supplied message
- **Personal/financial risk:** Dynamic: wrapper alone cannot guarantee safe contents.
- **Before Stage 5:** Forwarded unchanged to base exception.
- **After / minimization justification:** No constructor change; each caller is audited
  separately; 4xx logger excludes message/object.

### Entry 131

- **Site and method:**
  [Domain/Exceptions/ForbiddenException.cs:3](Domain/Exceptions/ForbiddenException.cs#L3) /
  ForbiddenException.ctor
- **Diagnostic type:** exception constructor forwarding
- **Potential contents:** Caller-supplied message
- **Personal/financial risk:** Dynamic: wrapper alone cannot guarantee safe contents.
- **Before Stage 5:** Forwarded unchanged to base exception.
- **After / minimization justification:** No constructor change; each caller is audited
  separately; 4xx logger excludes message/object.

### Entry 132

- **Site and method:**
  [Domain/Exceptions/NotFoundException.cs:6](Domain/Exceptions/NotFoundException.cs#L6) /
  NotFoundException.ctor
- **Diagnostic type:** exception constructor forwarding
- **Potential contents:** Caller-supplied message
- **Personal/financial risk:** Dynamic: wrapper alone cannot guarantee safe contents.
- **Before Stage 5:** Forwarded unchanged to base exception.
- **After / minimization justification:** No constructor change; each caller is audited
  separately; 4xx logger excludes message/object.

### Entry 133

- **Site and method:**
  [Domain/Exceptions/PreconditionFailedException.cs:6](Domain/Exceptions/PreconditionFailedException.cs#L6)
  / PreconditionFailedException.ctor
- **Diagnostic type:** exception constructor forwarding
- **Potential contents:** Caller-supplied message
- **Personal/financial risk:** Dynamic: wrapper alone cannot guarantee safe contents.
- **Before Stage 5:** Forwarded unchanged to base exception.
- **After / minimization justification:** No constructor change; each caller is audited
  separately; 4xx logger excludes message/object.

### Entry 134

- **Site and method:**
  [Domain/Exceptions/RequestValidationException.cs:7](Domain/Exceptions/RequestValidationException.cs#L7)
  / RequestValidationException.ctor
- **Diagnostic type:** exception constructor forwarding
- **Potential contents:** Caller-supplied message
- **Personal/financial risk:** Dynamic: wrapper alone cannot guarantee safe contents.
- **Before Stage 5:** Forwarded unchanged to base exception.
- **After / minimization justification:** No constructor change; each caller is audited
  separately; 4xx logger excludes message/object.

### Entry 135

- **Site and method:**
  [Domain/Exceptions/RondiTrackException.cs:6](Domain/Exceptions/RondiTrackException.cs#L6) /
  RondiTrackException.ctor
- **Diagnostic type:** exception constructor forwarding
- **Potential contents:** Caller-supplied message
- **Personal/financial risk:** Dynamic: wrapper alone cannot guarantee safe contents.
- **Before Stage 5:** Forwarded unchanged to base exception.
- **After / minimization justification:** No constructor change; each caller is audited
  separately; 4xx logger excludes message/object.

### Entry 136

- **Site and method:**
  [Infrastructure/Repositories/ContributionRepository.cs:59](Infrastructure/Repositories/ContributionRepository.cs#L59)
  / GetByCyclePagedAsync
- **Diagnostic type:** RequestValidationException
- **Potential contents:** "Sort direction must be 'asc' or 'desc'."
- **Personal/financial risk:** No submitted values in literal.
- **Before Stage 5:** Expected domain/validation 4xx was logged with full exception at Error
  (except authentication).
- **After / minimization justification:** Message unchanged. 4xx: Information,
  type/status/correlation only, no exception; 5xx: full exception internally, generic client
  detail. Fixed authored client details preserved.

### Entry 137

- **Site and method:**
  [Infrastructure/Repositories/ContributionRepository.cs:72](Infrastructure/Repositories/ContributionRepository.cs#L72)
  / GetByCyclePagedAsync
- **Diagnostic type:** RequestValidationException
- **Potential contents:** "Unsupported sort field for contributions."
- **Personal/financial risk:** No submitted values in literal.
- **Before Stage 5:** Expected domain/validation 4xx was logged with full exception at Error
  (except authentication).
- **After / minimization justification:** Message unchanged. 4xx: Information,
  type/status/correlation only, no exception; 5xx: full exception internally, generic client
  detail. Fixed authored client details preserved.

### Entry 138

- **Site and method:**
  [Infrastructure/Repositories/ContributionRepository.cs:212](Infrastructure/Repositories/ContributionRepository.cs#L212)
  / ApplySort
- **Diagnostic type:** RequestValidationException
- **Potential contents:** "Unsupported sort field for contributions."
- **Personal/financial risk:** No submitted values in literal.
- **Before Stage 5:** Expected domain/validation 4xx was logged with full exception at Error
  (except authentication).
- **After / minimization justification:** Message unchanged. 4xx: Information,
  type/status/correlation only, no exception; 5xx: full exception internally, generic client
  detail. Fixed authored client details preserved.

### Entry 139

- **Site and method:**
  [Infrastructure/Repositories/ContributionRepository.cs:319](Infrastructure/Repositories/ContributionRepository.cs#L319)
  / ParseToken
- **Diagnostic type:** RequestValidationException
- **Potential contents:** "Page token is invalid."
- **Personal/financial risk:** No submitted values in literal.
- **Before Stage 5:** Expected domain/validation 4xx was logged with full exception at Error
  (except authentication).
- **After / minimization justification:** Message unchanged. 4xx: Information,
  type/status/correlation only, no exception; 5xx: full exception internally, generic client
  detail. Fixed authored client details preserved.

### Entry 140

- **Site and method:**
  [Infrastructure/Repositories/ContributionRepository.cs:329](Infrastructure/Repositories/ContributionRepository.cs#L329)
  / ParseToken
- **Diagnostic type:** RequestValidationException
- **Potential contents:** "Page token does not match the current sort or filter."
- **Personal/financial risk:** No submitted values in literal.
- **Before Stage 5:** Expected domain/validation 4xx was logged with full exception at Error
  (except authentication).
- **After / minimization justification:** Message unchanged. 4xx: Information,
  type/status/correlation only, no exception; 5xx: full exception internally, generic client
  detail. Fixed authored client details preserved.

### Entry 141

- **Site and method:**
  [Infrastructure/Repositories/ContributionRepository.cs:337](Infrastructure/Repositories/ContributionRepository.cs#L337)
  / ParseToken
- **Diagnostic type:** RequestValidationException
- **Potential contents:** "Page token is invalid."
- **Personal/financial risk:** No submitted values in literal.
- **Before Stage 5:** Expected domain/validation 4xx was logged with full exception at Error
  (except authentication).
- **After / minimization justification:** Message unchanged. 4xx: Information,
  type/status/correlation only, no exception; 5xx: full exception internally, generic client
  detail. Fixed authored client details preserved.

### Entry 142

- **Site and method:**
  [Infrastructure/Repositories/EfStokvelMemberRepository.cs:45](Infrastructure/Repositories/EfStokvelMemberRepository.cs#L45)
  / GetByStokvelPagedAsync
- **Diagnostic type:** RequestValidationException
- **Potential contents:** "Sort direction must be 'asc' or 'desc'."
- **Personal/financial risk:** No submitted values in literal.
- **Before Stage 5:** Expected domain/validation 4xx was logged with full exception at Error
  (except authentication).
- **After / minimization justification:** Message unchanged. 4xx: Information,
  type/status/correlation only, no exception; 5xx: full exception internally, generic client
  detail. Fixed authored client details preserved.

### Entry 143

- **Site and method:**
  [Infrastructure/Repositories/EfStokvelMemberRepository.cs:58](Infrastructure/Repositories/EfStokvelMemberRepository.cs#L58)
  / GetByStokvelPagedAsync
- **Diagnostic type:** RequestValidationException
- **Potential contents:** "Unsupported sort field for stokvel members."
- **Personal/financial risk:** No submitted values in literal.
- **Before Stage 5:** Expected domain/validation 4xx was logged with full exception at Error
  (except authentication).
- **After / minimization justification:** Message unchanged. 4xx: Information,
  type/status/correlation only, no exception; 5xx: full exception internally, generic client
  detail. Fixed authored client details preserved.

### Entry 144

- **Site and method:**
  [Infrastructure/Repositories/EfStokvelMemberRepository.cs:143](Infrastructure/Repositories/EfStokvelMemberRepository.cs#L143)
  / ApplySort
- **Diagnostic type:** RequestValidationException
- **Potential contents:** "Unsupported sort field for stokvel members."
- **Personal/financial risk:** No submitted values in literal.
- **Before Stage 5:** Expected domain/validation 4xx was logged with full exception at Error
  (except authentication).
- **After / minimization justification:** Message unchanged. 4xx: Information,
  type/status/correlation only, no exception; 5xx: full exception internally, generic client
  detail. Fixed authored client details preserved.

### Entry 145

- **Site and method:**
  [Infrastructure/Repositories/EfStokvelMemberRepository.cs:234](Infrastructure/Repositories/EfStokvelMemberRepository.cs#L234)
  / ParseToken
- **Diagnostic type:** RequestValidationException
- **Potential contents:** "Page token is invalid."
- **Personal/financial risk:** No submitted values in literal.
- **Before Stage 5:** Expected domain/validation 4xx was logged with full exception at Error
  (except authentication).
- **After / minimization justification:** Message unchanged. 4xx: Information,
  type/status/correlation only, no exception; 5xx: full exception internally, generic client
  detail. Fixed authored client details preserved.

### Entry 146

- **Site and method:**
  [Infrastructure/Repositories/EfStokvelMemberRepository.cs:241](Infrastructure/Repositories/EfStokvelMemberRepository.cs#L241)
  / ParseToken
- **Diagnostic type:** RequestValidationException
- **Potential contents:** "Page token does not match the current sort or filter."
- **Personal/financial risk:** No submitted values in literal.
- **Before Stage 5:** Expected domain/validation 4xx was logged with full exception at Error
  (except authentication).
- **After / minimization justification:** Message unchanged. 4xx: Information,
  type/status/correlation only, no exception; 5xx: full exception internally, generic client
  detail. Fixed authored client details preserved.

### Entry 147

- **Site and method:**
  [Infrastructure/Repositories/EfStokvelMemberRepository.cs:249](Infrastructure/Repositories/EfStokvelMemberRepository.cs#L249)
  / ParseToken
- **Diagnostic type:** RequestValidationException
- **Potential contents:** "Page token is invalid."
- **Personal/financial risk:** No submitted values in literal.
- **Before Stage 5:** Expected domain/validation 4xx was logged with full exception at Error
  (except authentication).
- **After / minimization justification:** Message unchanged. 4xx: Information,
  type/status/correlation only, no exception; 5xx: full exception internally, generic client
  detail. Fixed authored client details preserved.

### Entry 148

- **Site and method:**
  [RondiTrack.Tests/BusinessTelemetryTests.cs:156](RondiTrack.Tests/BusinessTelemetryTests.cs#L156)
  / ReaderExecutingAsync
- **Diagnostic type:** NpgsqlException
- **Potential contents:** "Synthetic transient test failure.", new TimeoutException()
- **Personal/financial risk:** Dynamic diagnostic / possible submitted data; do not retain on
  4xx.
- **Before Stage 5:** Test-only synthetic diagnostic; not production telemetry.
- **After / minimization justification:** Unchanged test fixture message; synthetic only.

### Entry 149

- **Site and method:**
  [RondiTrack.Tests/BusinessTelemetryTests.cs:156](RondiTrack.Tests/BusinessTelemetryTests.cs#L156)
  / ReaderExecutingAsync
- **Diagnostic type:** TimeoutException
- **Potential contents:** (default message)
- **Personal/financial risk:** No explicit message; library default diagnostic.
- **Before Stage 5:** Test-only synthetic diagnostic; not production telemetry.
- **After / minimization justification:** Unchanged test fixture message; synthetic only.

### Entry 150

- **Site and method:**
  [RondiTrack.Tests/BusinessTelemetryTests.cs:166](RondiTrack.Tests/BusinessTelemetryTests.cs#L166)
  / TransactionCommittingAsync
- **Diagnostic type:** InvalidOperationException
- **Potential contents:** "Synthetic commit refusal."
- **Personal/financial risk:** No submitted values in literal.
- **Before Stage 5:** Test-only synthetic diagnostic; not production telemetry.
- **After / minimization justification:** Unchanged test fixture message; synthetic only.

### Entry 151

- **Site and method:**
  [RondiTrack.Tests/CorrelationTests.cs:176](RondiTrack.Tests/CorrelationTests.cs#L176) /
  Unexpected500_ReturnsGenericDetailAndMatchingStructuredCorrelation
- **Diagnostic type:** InvalidOperationException
- **Potential contents:** "private-internal-diagnostic"
- **Personal/financial risk:** No submitted values in literal.
- **Before Stage 5:** Test-only synthetic diagnostic; not production telemetry.
- **After / minimization justification:** Unchanged test fixture message; synthetic only.

### Entry 152

- **Site and method:**
  [RondiTrack.Tests/LoggingTests.cs:229](RondiTrack.Tests/LoggingTests.cs#L229) /
  Unexpected500_KeepsFullExceptionInternally_AndGenericClientDetail
- **Diagnostic type:** InvalidOperationException
- **Potential contents:** "Synthetic diagnostic failure."
- **Personal/financial risk:** No submitted values in literal.
- **Before Stage 5:** Test-only synthetic diagnostic; not production telemetry.
- **After / minimization justification:** Unchanged test fixture message; synthetic only.

### Entry 153

- **Site and method:**
  [RondiTrack.Tests/PayoutTransactionTests.cs:186](RondiTrack.Tests/PayoutTransactionTests.cs#L186)
  / PayoutTransaction_WhenFailureOccursPartway_RollsBackAllChanges
- **Diagnostic type:** InvalidOperationException
- **Potential contents:** "Simulated payout failure."
- **Personal/financial risk:** No submitted values in literal.
- **Before Stage 5:** Test-only synthetic diagnostic; not production telemetry.
- **After / minimization justification:** Unchanged test fixture message; synthetic only.

### Entry 154

- **Site and method:**
  [RondiTrack.Tests/TestSupport/PostgresApiFactory.cs:73](RondiTrack.Tests/TestSupport/PostgresApiFactory.cs#L73)
  / InitializeAsync
- **Diagnostic type:** InvalidOperationException
- **Potential contents:** "Tests must use the disposable PostgreSQL container."
- **Personal/financial risk:** No submitted values in literal.
- **Before Stage 5:** Test-only synthetic diagnostic; not production telemetry.
- **After / minimization justification:** Unchanged test fixture message; synthetic only.

### Entry 155

- **Site and method:**
  [RondiTrack.Tests/TestSupport/PostgresApiFactory.cs:86](RondiTrack.Tests/TestSupport/PostgresApiFactory.cs#L86)
  / InitializeAsync
- **Diagnostic type:** InvalidOperationException
- **Potential contents:** "The API connected to an unexpected database."
- **Personal/financial risk:** No submitted values in literal.
- **Before Stage 5:** Test-only synthetic diagnostic; not production telemetry.
- **After / minimization justification:** Unchanged test fixture message; synthetic only.

### Entry 156

- **Site and method:**
  [RondiTrack.Tests/TestSupport/PostgresApiFactory.cs:93](RondiTrack.Tests/TestSupport/PostgresApiFactory.cs#L93)
  / InitializeAsync
- **Diagnostic type:** rethrow
- **Potential contents:** Original exception / message / inner chain
- **Personal/financial risk:** Dynamic: may inherit personal or database diagnostics.
- **Before Stage 5:** Propagated original exception.
- **After / minimization justification:** Rethrow unchanged; handler applies 4xx minimality /
  5xx internal diagnostics. Test-only rethrows never deploy.

### Entry 157

- **Site and method:**
  [scripts/verify-assignment-5-4-isolation.ps1:12](scripts/verify-assignment-5-4-isolation.ps1#L12)
  / top-level preflight / proof
- **Diagnostic type:** PowerShell throw
- **Potential contents:** Development connection string is required for the isolation
  preflight.
- **Personal/financial risk:** Fixed operational guidance; no credentials/record values.
- **Before Stage 5:** Existing isolation-script diagnostic.
- **After / minimization justification:** Unchanged; script was not executed in Stage 5.
  Development database untouched.

### Entry 158

- **Site and method:**
  [scripts/verify-assignment-5-4-isolation.ps1:21](scripts/verify-assignment-5-4-isolation.ps1#L21)
  / top-level preflight / proof
- **Diagnostic type:** PowerShell throw
- **Potential contents:** This proof only permits the local ronditrack development database.
- **Personal/financial risk:** Fixed operational guidance; no credentials/record values.
- **Before Stage 5:** Existing isolation-script diagnostic.
- **After / minimization justification:** Unchanged; script was not executed in Stage 5.
  Development database untouched.

### Entry 159

- **Site and method:**
  [scripts/verify-assignment-5-4-isolation.ps1:23](scripts/verify-assignment-5-4-isolation.ps1#L23)
  / top-level preflight / proof
- **Diagnostic type:** PowerShell throw
- **Potential contents:** PostgreSQL psql was not found.
- **Personal/financial risk:** Fixed operational guidance; no credentials/record values.
- **Before Stage 5:** Existing isolation-script diagnostic.
- **After / minimization justification:** Unchanged; script was not executed in Stage 5.
  Development database untouched.

### Entry 160

- **Site and method:**
  [scripts/verify-assignment-5-4-isolation.ps1:36](scripts/verify-assignment-5-4-isolation.ps1#L36)
  / top-level preflight / proof
- **Diagnostic type:** PowerShell throw
- **Potential contents:** Development database must initially accept connections.
- **Personal/financial risk:** Fixed operational guidance; no credentials/record values.
- **Before Stage 5:** Existing isolation-script diagnostic.
- **After / minimization justification:** Unchanged; script was not executed in Stage 5.
  Development database untouched.

### Entry 161

- **Site and method:**
  [scripts/verify-assignment-5-4-isolation.ps1:38](scripts/verify-assignment-5-4-isolation.ps1#L38)
  / top-level preflight / proof
- **Diagnostic type:** PowerShell throw
- **Potential contents:** Close development API/database sessions before the isolation proof;
  no sessions will be terminated.
- **Personal/financial risk:** Fixed operational guidance; no credentials/record values.
- **Before Stage 5:** Existing isolation-script diagnostic.
- **After / minimization justification:** Unchanged; script was not executed in Stage 5.
  Development database untouched.

### Entry 162

- **Site and method:**
  [scripts/verify-assignment-5-4-isolation.ps1:41](scripts/verify-assignment-5-4-isolation.ps1#L41)
  / top-level preflight / proof
- **Diagnostic type:** PowerShell throw
- **Potential contents:** Could not temporarily disable development database connections.
- **Personal/financial risk:** Fixed operational guidance; no credentials/record values.
- **Before Stage 5:** Existing isolation-script diagnostic.
- **After / minimization justification:** Unchanged; script was not executed in Stage 5.
  Development database untouched.

### Entry 163

- **Site and method:**
  [scripts/verify-assignment-5-4-isolation.ps1:44](scripts/verify-assignment-5-4-isolation.ps1#L44)
  / top-level preflight / proof
- **Diagnostic type:** PowerShell throw
- **Potential contents:** A development session appeared during preflight; restore and retry
  when idle.
- **Personal/financial risk:** Fixed operational guidance; no credentials/record values.
- **Before Stage 5:** Existing isolation-script diagnostic.
- **After / minimization justification:** Unchanged; script was not executed in Stage 5.
  Development database untouched.

### Entry 164

- **Site and method:**
  [scripts/verify-assignment-5-4-isolation.ps1:54](scripts/verify-assignment-5-4-isolation.ps1#L54)
  / finally / restoration
- **Diagnostic type:** PowerShell throw
- **Potential contents:** Development database unexpectedly accepted a connection.
- **Personal/financial risk:** Fixed operational guidance; no credentials/record values.
- **Before Stage 5:** Existing isolation-script diagnostic.
- **After / minimization justification:** Unchanged; script was not executed in Stage 5.
  Development database untouched.

### Entry 165

- **Site and method:**
  [scripts/verify-assignment-5-4-isolation.ps1:56](scripts/verify-assignment-5-4-isolation.ps1#L56)
  / finally / restoration
- **Diagnostic type:** PowerShell throw
- **Potential contents:** Development probe failed for an unexpected reason; independence is
  not proven.
- **Personal/financial risk:** Fixed operational guidance; no credentials/record values.
- **Before Stage 5:** Existing isolation-script diagnostic.
- **After / minimization justification:** Unchanged; script was not executed in Stage 5.
  Development database untouched.

### Entry 166

- **Site and method:**
  [scripts/verify-assignment-5-4-isolation.ps1:74](scripts/verify-assignment-5-4-isolation.ps1#L74)
  / finally / restoration
- **Diagnostic type:** PowerShell throw
- **Potential contents:** RESTORATION FAILED: restore ALLOW_CONNECTIONS=true for ronditrack
  before development use.
- **Personal/financial risk:** Fixed operational guidance; no credentials/record values.
- **Before Stage 5:** Existing isolation-script diagnostic.
- **After / minimization justification:** Unchanged; script was not executed in Stage 5.
  Development database untouched.

### Entry 167

- **Site and method:**
  [scripts/verify-assignment-5-4-isolation.ps1:76](scripts/verify-assignment-5-4-isolation.ps1#L76)
  / finally / restoration
- **Diagnostic type:** PowerShell throw
- **Potential contents:** Development database restoration probe failed.
- **Personal/financial risk:** Fixed operational guidance; no credentials/record values.
- **Before Stage 5:** Existing isolation-script diagnostic.
- **After / minimization justification:** Unchanged; script was not executed in Stage 5.
  Development database untouched.

### Entry 168

- **Site and method:** API/Program.cs / JWT OnTokenValidated
- **Diagnostic type:** context.Fail message
- **Potential contents:** The authenticated user no longer exists.
- **Personal/financial risk:** Fixed text; no user ID.
- **Before Stage 5:** JWT rejection and no error details in response.
- **After / minimization justification:** Unchanged validation; safe fixed diagnostic.

### Entry 169

- **Site and method:** API/Program.cs / EF UseNpgsql
- **Diagnostic type:** Npgsql parsing and connection errors
- **Potential contents:** Library-generated configuration/connection diagnostics
- **Personal/financial risk:** May contain infrastructure or malformed setting values.
- **Before Stage 5:** Parameter/detail defaults false; configurable connection flags.
- **After / minimization justification:** Force LogParameters=false and
  IncludeErrorDetail=false without changing connection target. Never print connection string.

### Entry 170

- **Site and method:** API/Program.cs / EF provider diagnostics
- **Diagnostic type:** EF Database.Command logs
- **Potential contents:** Parameterized SQL, durations, parameter type/size metadata
- **Personal/financial risk:** Values suppressed; literal SQL and metadata can still reveal
  schema.
- **Before Stage 5:** Development Information; base Warning; no duplicate output.
- **After / minimization justification:** Levels retained; no sensitive EF data flag or LogTo.
  Parameter placeholders verified in focused traffic.

### Entry 171

- **Site and method:** EF provider / RelationalEventId.CommandError
- **Diagnostic type:** EF failed-command log event
- **Potential contents:** SQL template and library exception chain
- **Personal/financial risk:** Raw SQL literals / unexpected exceptions may expose data.
- **Before Stage 5:** Database.Command Error exists independently of the handler.
- **After / minimization justification:** No blanket suppression; parameter/detail safeguards
  preserved; independent database-originated conflict diagnostics remain a limitation.

### Entry 172

- **Site and method:** EF provider / CoreEventId.SaveChangesFailed
- **Diagnostic type:** EF failed-save log event
- **Potential contents:** Context type and library exception / inner PostgreSQL chain
- **Personal/financial risk:** Unexpected diagnostic text may contain data even without
  parameter logging.
- **Before Stage 5:** EF Update Error before handler classification.
- **After / minimization justification:** No blanket suppression; full internal failure
  retained. Prechecked duplicate-registration 409 tested without Error.

### Entry 173

- **Site and method:** EF provider / CoreEventId.QueryIterationFailed
- **Diagnostic type:** EF failed-query log event
- **Potential contents:** Context type and database exception chain
- **Personal/financial risk:** Unexpected third-party diagnostics may contain data.
- **Before Stage 5:** EF Query Error independently of handler.
- **After / minimization justification:** No blanket suppression; clients receive generic 500
  detail. Parameter values/row details remain disabled.

### Entry 174

- **Site and method:** API/Program.cs / Npgsql activity subscription
- **Diagnostic type:** Npgsql trace tags/events
- **Potential contents:** Parameterized SQL, operation timing and database metadata
- **Personal/financial risk:** Raw literals/enrichment or exception text could carry data.
- **Before Stage 5:** AddNpgsql defaults; no parameter enrichment.
- **After / minimization justification:** Keep command spans and safe defaults; focused
  listener checks email/password/token/hash absence in tags/events. Literal SQL requires
  future audit.

### Entry 175

- **Site and method:** API/Program.cs / ASP.NET request instrumentation
- **Diagnostic type:** HTTP trace fields
- **Potential contents:** Method, route template, status and duration; framework URL fields
- **Personal/financial risk:** URLs/query strings can carry identifiers if callers misuse
  them.
- **Before Stage 5:** No body/header enrichment.
- **After / minimization justification:** Unchanged useful instrumentation; sensitive query
  parameters must not be introduced. External proxy logs remain outside test coverage.

### Entry 176

- **Site and method:** API/Program.cs / AddJwtBearer
- **Diagnostic type:** JWT library diagnostics
- **Potential contents:** Token-validation reason from framework; PII hidden by library
  defaults
- **Personal/financial risk:** Custom PII/debug logging could expose tokens.
- **Before Stage 5:** IncludeErrorDetails=false; no PII enablement.
- **After / minimization justification:** Validation unchanged; credentials never added to
  application log state.

### Entry 177

- **Site and method:** API/Program.cs / UseExceptionHandler
- **Diagnostic type:** ASP.NET handled-exception diagnostics
- **Potential contents:** Potential independent exception logs/events
- **Personal/financial risk:** A duplicate framework event could retain a 4xx message.
- **Before Stage 5:** .NET 10 suppresses handled-exception diagnostics by default.
- **After / minimization justification:** Default suppression retained; captured focused tests
  assert no Error for expected 4xx.

### Entry 178

- **Site and method:** API/Filters/ValidationFilter.cs / OnActionExecutionAsync
- **Diagnostic type:** dynamic validation aggregation
- **Potential contents:** string.Join of validator errors
- **Personal/financial risk:** Future templates could interpolate submitted values.
- **Before Stage 5:** Combines messages for client response; full exception previously logged.
- **After / minimization justification:** Client semantics preserved; 4xx logs contain no
  aggregation/message. Stage 3 already fixed model-binding detail.

### Entry 179

- **Site and method:** API/Handlers/GlobalExceptionHandler.cs / IsUniqueConstraintViolation
- **Diagnostic type:** database exception inspection
- **Potential contents:** Inner message read to identify 23505/duplicate key
- **Personal/financial risk:** Inner message could contain data.
- **Before Stage 5:** Read only for classification; handler previously logged complete 409
  exception.
- **After / minimization justification:** Classification unchanged; message not retained on
  409; client gets fixed constraint detail.

### Entry 180

- **Site and method:** RondiTrack.Tests/TestSupport/CorrelationLogProvider.cs / Log
- **Diagnostic type:** test provider
- **Potential contents:** Correlation/category metadata only
- **Personal/financial risk:** No message, exception or payload capture.
- **Before Stage 5:** Existing in-memory correlation assertions.
- **After / minimization justification:** Unchanged; does not emit a production event.

### Entry 181

- **Site and method:** RondiTrack.Tests/TestSupport/CapturedLogProvider.cs / Log
- **Diagnostic type:** test provider
- **Potential contents:** Formatted message, structured state, exception reference
- **Personal/financial risk:** Test memory can contain diagnostic data; never publish raw
  capture.
- **Before Stage 5:** Absent.
- **After / minimization justification:** Added in-memory capture for automated auditing; no
  sink or payload serialization. Stage 7 completes the end-to-end leak scenario.

</details>
