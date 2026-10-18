# RondiTrack

[![RondiTrack CI](https://github.com/mdvar2/RondiTrack/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/mdvar2/RondiTrack/actions/workflows/ci.yml)

RondiTrack is a backend API I built during my Bitcube software development training. It helps manage stokvels, including members, contributions, savings cycles and payouts.

I started with basic API endpoints and gradually added database relationships, security, testing and monitoring. My latest assignment focused on making the backend ready for a frontend application.

## What the system does

- Manages users, stokvels, memberships and contribution cycles.
- Records contributions and payouts while checking the business rules.
- Uses JWT authentication and membership-based permissions to protect requests.
- Prevents duplicate contribution processing when a request is retried with the same idempotency key.
- Provides a member home endpoint so a logged-in user can see their own stokvels and contribution information.
- Returns consistent error responses and includes tracing, logging and health checks.

The project is **backend only** at the moment. I use Scalar to explore and test the API during development.

## Technologies I used

| Technology | How I used it |
|---|---|
| C# / ASP.NET Core 10 | Built the API endpoints and services |
| EF Core 10 / PostgreSQL 17 | Stored and queried application data |
| FluentValidation | Validated incoming requests |
| JWT authentication | Protected endpoints and identified users |
| xUnit / Testcontainers | Tested the API with disposable PostgreSQL databases |
| OpenAPI / Scalar | Documented and explored the API |
| OpenTelemetry / Aspire | Worked with tracing, metrics and logs |
| TypeScript | Generated frontend-facing types from the API contract |

I separated the solution into **API**, **Domain**, **Infrastructure** and **RondiTrack.Tests** projects. The [architecture guide](docs/architecture.md) explains how they fit together.

## My latest work: Assignment 6.3

For Assignment 6.3, I worked on the parts of the backend that a frontend would depend on:

- **API versioning:** Controller endpoints now use `/api/v1`.
- **CORS:** The API allows the configured frontend origin rather than accepting requests from any origin.
- **Rate limiting:** Login, contribution and payout requests have limits to reduce excessive requests.
- **Contribution retries:** The existing idempotency handling works alongside rate limiting.
- **Member home endpoint:** `GET /api/v1/me/home` returns information for the logged-in member without exposing other members' contribution amounts.
- **Consistent errors:** API errors follow a Problem Details response format.
- **TypeScript contracts:** I generated types from the actual OpenAPI document and checked that a renamed backend property would cause a frontend compile error.

One feature is still incomplete: **`nextPayoutDate` returns `null`** because the system does not yet have a rule for scheduling future payouts. I have not made up a date for it.

For a more detailed explanation of the changes, see the [Assignment 6.3 trainer notes](docs/assignment-6-3-stages.md) and [compliance matrix](docs/assignment-6-3-compliance.md).

## Testing and verification

**On 14 October 2026, all 122 tests passed, with no failures or skipped tests.** The fresh solution build also completed with no warnings or errors.

I also checked that the tests could run without connecting to my development PostgreSQL database. I temporarily disabled new connections to that database, ran the test suite using Testcontainers, and then restored the original connection setting. I confirmed the database was accessible again with `SELECT 1`.

No development data, schemas or migrations were changed during that verification.

The [Assignment 6.3 verification report](docs/assignment-6-3-verification.md#successful-offline-verification) has the full procedure and recorded results. The earlier Assignment 6.2 test result was 101 passing tests; **122 is the latest verified total**.

To run the ordinary tests locally, start Docker Desktop in Linux-container mode and use:

```powershell
dotnet restore RondiTrack.sln
dotnet build RondiTrack.sln --no-restore -m:1
dotnet test RondiTrack.sln --no-restore -m:1
```

These commands run the normal suite. They do **not** disable development database connections. The guarded isolation procedure is explained separately in the verification report.

## Running the API locally

You need the .NET SDK, PostgreSQL and the project's local configuration. From the repository root:

```powershell
dotnet restore RondiTrack.sln
dotnet build RondiTrack.sln
dotnet run --project API
```

The development API documentation is available at `/scalar/v1` and the OpenAPI document at `/openapi/v1.json` on the port used by the running API.

**Important database note:** My existing development database contains duplicate normalized emails that currently block an authentication migration. I am keeping that data intact rather than resetting the database to make the migration pass. See [development migration status](docs/development-migration-status.md) before attempting database updates.

For configuration, local secrets, HTTPS and migration steps, use the [detailed technical notes](docs/README-detailed-technical-notes.md#4-setup-and-running-the-api).

## What I learned and what I still need to improve

This project helped me practise building APIs in stages, keeping business logic separate from controllers, protecting financial operations, and writing integration tests against a real database engine.

There are still limitations I need to address before treating this as a production system. Rate limiting and the idempotency response store currently work **per API process**, not across multiple running instances. I also need a confirmed future payout scheduling rule, and the development migration issue still needs a safe resolution.

## Project documentation

- [API routes and request examples](docs/api-reference.md)
- [Architecture and service lifetimes](docs/architecture.md)
- [Assignment 6.3 implementation notes](docs/assignment-6-3-stages.md)
- [Assignment 6.3 test evidence](docs/assignment-6-3-verification.md)
- [Assignment 6.3 requirement checklist](docs/assignment-6-3-compliance.md)
- [Detailed technical notes and full logging audit](docs/README-detailed-technical-notes.md)
- [Earlier assignment history](README-history.md)

The detailed notes are kept separately so I can still refer to the code, tests and decisions during a trainer walkthrough without making this README difficult to read.

## Assignment 6.4 — Docker, CI, and scheduled processing

From a fresh clone, install Docker Desktop (no host .NET SDK required for Compose), then copy `.env.example` to `.env` and set a strong PostgreSQL password and a random Base64 JWT signing key (at least 32 decoded bytes). Do not commit `.env`.

```powershell
Copy-Item .env.example .env
# Edit .env and replace both placeholders before starting.
docker compose up --build -d api
docker compose ps
docker compose logs migrate --tail 30
curl.exe http://localhost:8080/health/live
curl.exe http://localhost:8080/health/ready
```

The API is available at `http://localhost:8080`, with health checks at `/health/live` and `/health/ready`. Scalar/OpenAPI are mapped only in Development by `Program.cs`; the Compose configuration uses Production, so `/scalar` and `/openapi/v1.json` are deliberately unavailable there. For local interactive API exploration, use a development profile only in a trusted environment.

The PostgreSQL service is internal to the Compose network (no host database port). Its data survives container recreation in `postgres_data`. `migrate` applies EF Core migrations once before `api` starts. The API runs as a non-root user on port 8080. `docker compose down` preserves the named volume; `docker compose down -v` **deletes** local database data.

`CycleClose__IntervalSeconds` sets the cycle-close polling interval (default 60). The worker uses `TimeProvider` and Johannesburg local calendar dates. A cycle ending October 31 remains open at 23:59 SAST and becomes eligible at 00:00 SAST on November 1. It records each member's unpaid balance as integer cents, closes the cycle and creates a successor. A PostgreSQL transaction advisory lock serializes processing of the same stokvel across replicas, and the arrears table has a uniqueness constraint. The worker retries failures on later ticks. Only cycles with a configured `EndDate` are eligible.

`Housekeeping__IntervalHours` (default 24) and `Housekeeping__RefreshTokenGraceDays` (default 7) control database cleanup. Expired refresh-token families are deleted in a single set-based statement after a grace period, cascading to their token hashes. The grace period retains hashes temporarily for replay investigations. `Housekeeping__IdempotencyRetentionHours` is validated but **not enforced**: the current idempotency store is in-memory, not database-backed. Persistent idempotency with retention is a production gap.

CI (`.github/workflows/ci.yml`) runs restore, Release build, Testcontainers integration tests, and Docker build on main pushes and pull requests. Docker image build runs only after tests succeed. For manual verification:

```powershell
dotnet restore RondiTrack.Tests/RondiTrack.Tests.csproj
dotnet build RondiTrack.Tests/RondiTrack.Tests.csproj --configuration Release --no-restore
dotnet test RondiTrack.Tests/RondiTrack.Tests.csproj --configuration Release --no-build
docker build -t ronditrack-api:check .
docker image inspect ronditrack-api:check
docker history ronditrack-api:check
```

For a second API replica, the current Compose file binds host port 8080 and must be adapted to avoid port collisions (for example, remove the fixed host port and use a reverse proxy). After that, `docker compose up --scale api=2` can start two workers; transaction-scoped advisory locks protect each stokvel's close. Migrations must remain a separate one-shot service and never run independently in each replica.

**Before production:** implement persisted idempotency with retention and cleanup, add a unique `(StokvelId, Period)` database constraint and validate cycle period/end-date alignment, secure a public HTTPS endpoint, configure secret management and monitoring, and run a peer fresh-clone acceptance test. The existing tests and README do not substitute for that review. The 125-test suite passed locally on 18 October 2026 and the latest main-branch GitHub Actions run passed; this does not prove the unimplemented acceptance cases.


## Assignment 6.4 — verified CI evidence and outstanding acceptance work

- **Latest green main run:** [commit 4ac8e08, CI passed](https://github.com/mdvar2/RondiTrack/actions/runs/39171975779). The submitted local test output reported **125 passed, 0 failed, 0 skipped**.
- **Intentional red PR run:** [failed test](https://github.com/mdvar2/RondiTrack/actions/runs/39152181436).
- **Fixed green PR run:** [passing after fix](https://github.com/mdvar2/RondiTrack/actions/runs/39152807177).
- **Visible proof PR:** [#3](https://github.com/mdvar2/RondiTrack/pull/3), intentionally left open as requested.
- **Cycle-close instant:** a cycle with an end date of **25 October 2026** remains open at **21:59 UTC** on the 25th (23:59 SAST), and becomes eligible at **22:00 UTC** on the 25th (00:00 SAST on the 26th). Eligibility uses `TimeProvider` and `Africa/Johannesburg`.
- **Catch-up:** each overdue cycle is closed and its next monthly cycle is created, repeating until the first unexpired cycle; the per-stokvel transaction commits the whole catch-up or none of it. Concurrent replicas use a transaction-scoped PostgreSQL advisory lock; arrears are additionally protected by a unique index on `(StokvelId, UserId, ContributionCycleId)`. See `CycleCloseIntegrationTests.ConcurrentProcessors_DoNotDuplicateArrears`.
- **Retention:** expired refresh-token families are retained for **7 additional days** to preserve replay evidence before set-based deletion. The configured **24-hour idempotency retention** is **not currently enforced** because the existing idempotency store is in-memory; a retry after its record is lost could be treated as a new request. A persistent cross-replica store and safe cleanup are required before production.
- **Test coverage still needed:** three-cycle catch-up, a failing-stokvel isolation scenario, refresh-token grace/reuse checks and persisted-idempotency replay checks. The latest 125 passing tests do not establish those cases.
- **Peer fresh-clone:** no independent trainee's questions or fix commits have been recorded yet. This acceptance item remains open; do not claim it happened.
- **Dev DB stopped:** prior 6.3 isolation evidence is in `docs/assignment-6-3-verification.md`; a new 6.4-specific stopped-dev-DB run has not been independently recorded here.

### Docker image and secret-proof commands

Run these on a machine with Docker and record the **actual output**; image size and build times are machine-dependent and have not been fabricated here.

```powershell
git log --all -- .env
docker build --no-cache -t ronditrack-api:proof .
docker image inspect ronditrack-api:proof --format '{{.Size}}'
docker history --no-trunc ronditrack-api:proof
docker image inspect ronditrack-api:proof --format '{{json .Config.Env}}'
Measure-Command { docker build -t ronditrack-api:proof . }
```

`git log --all -- .env` should be empty. The Dockerfile copies published binaries, not the local `.env`; the image's configured environment must not contain passwords or JWT signing keys. `docker history` must not contain secrets either. To measure a *code-only* rebuild, change a harmless source comment before repeating the timed build, then revert that comment. Do not paste live credentials into the README.

### Environment variables used by Compose

| Variable | Required? | Secret? | Purpose |
| --- | --- | --- | --- |
| `POSTGRES_PASSWORD` | Yes | Yes | PostgreSQL container password, sourced from `.env` |
| `JWT_SIGNING_KEY` | Yes | Yes | JWT signing key, sourced from `.env` |
| `ConnectionStrings__RondiTrackDb` | Set by Compose | Yes | Internal connection string with host `db`, not `localhost` |
| `Jwt__SigningKey` | Set by Compose | Yes | ASP.NET Core JWT key |
| `ASPNETCORE_ENVIRONMENT` | Set by Compose | No | Production by default |
| `ASPNETCORE_HTTP_PORTS` | Set by Compose | No | 8080 |
| `CycleClose__IntervalSeconds` | Optional | No | Default 60 seconds |
| `Housekeeping__IntervalHours` | Optional | No | Default 24 hours |
| `Housekeeping__RefreshTokenGraceDays` | Optional | No | Default 7 days |
| `Housekeeping__IdempotencyRetentionHours` | Optional | No | Default 24 hours; not enforced by current in-memory store |
| `OTEL_EXPORTER_OTLP_ENDPOINT` | Optional | No | Tracing exporter endpoint |

The host name `db` resolves via Docker Compose service DNS. `localhost` inside the API container refers to that API container, not PostgreSQL. In Production Compose, `/scalar/v1` and `/openapi/v1.json` are unavailable by design; use the Development environment on a trusted local machine for interactive API documentation.

**Before production:** persist idempotency responses across replicas and implement safe retention; validate and constrain cycle periods; add remaining acceptance tests; use HTTPS and managed secrets; collect image measurements and peer fresh-clone evidence.
