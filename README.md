# RondiTrack

[![RondiTrack CI](https://github.com/mdvar2/RondiTrack/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/mdvar2/RondiTrack/actions/workflows/ci.yml)

RondiTrack is a backend API I built during my Bitcube software development training. It helps manage stokvels, including members, contributions, savings cycles and payouts.

I started with basic API endpoints and gradually added database relationships, security, testing and monitoring. The project has progressed from frontend-facing API contracts to containerized deployment, scheduled processing, durable idempotency and automated CI verification.

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

The [Assignment 6.3 verification report](docs/assignment-6-3-verification.md#successful-offline-verification) has the full procedure and recorded results. The earlier Assignment 6.2 test result was 101 passing tests; Assignment 6.3 verified 122, and Assignment 6.4 later verified 130 passing tests.

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

There are still limitations I need to address before treating this as a production system. Rate limiting requires review before distributed deployment. Assignment 6.4 introduced PostgreSQL-backed durable idempotency records, replacing the earlier per-process limitation. I also need a confirmed future payout scheduling rule, and the development migration issue still needs a safe resolution.

## Project documentation

- [API routes and request examples](docs/api-reference.md)

- [Architecture and service lifetimes](docs/architecture.md)

- [Assignment 6.3 implementation notes](docs/assignment-6-3-stages.md)

- [Assignment 6.3 test evidence](docs/assignment-6-3-verification.md)

- [Assignment 6.3 requirement checklist](docs/assignment-6-3-compliance.md)

- [Detailed technical notes and full logging audit](docs/README-detailed-technical-notes.md)

- [Earlier assignment history](README-history.md)

The detailed notes are kept separately so I can still refer to the code, tests and decisions during a trainer walkthrough without making this README difficult to read.

## Assignment 6.4 — Shipped, Scheduled and Handed Over

Assignment 6.4 focused on repeatable Docker deployment, continuous integration, scheduled contribution-cycle processing, housekeeping, durable idempotency and technical handover. The implementation builds on ASP.NET Core 10, EF Core 10, PostgreSQL 17 and the existing API, Domain, Infrastructure and test projects.

### Delivered functionality

- **Containerized deployment:** a root-level, multi-stage `Dockerfile` publishes the API using the .NET 10 SDK and runs it using the ASP.NET Core 10 runtime. `compose.yaml` defines PostgreSQL, a one-shot EF Core migration service and the API, with a persistent PostgreSQL volume. The API listens on port 8080.
- **Scheduled cycle closing:** a background processor uses `TimeProvider` and `Africa/Johannesburg` calendar dates to identify ended cycles. It records unpaid member contributions as arrears in integer cents, closes eligible cycles and creates successor cycles. Catch-up processing handles multiple overdue cycles. Per-stokvel transactions, PostgreSQL advisory locks and an arrears uniqueness constraint protect concurrent execution. Errors include the affected stokvel ID and are retried on later runs.
- **Housekeeping:** expired refresh-token families are deleted after a configurable grace period, with related token rows removed by cascade. Persisted idempotency records older than the configured retention cutoff are also removed using set-based database operations.
- **Durable idempotency:** idempotency records are persisted in PostgreSQL instead of relying solely on an in-memory process-local response store.
- **Continuous integration:** `.github/workflows/ci.yml` restores and builds the solution, runs automated tests including PostgreSQL Testcontainers integration tests, and builds the Docker image.

### Prerequisites and configuration

For Docker Compose, install Docker Desktop (Linux containers enabled) and Git. The host .NET SDK is not required for the Compose build. To run the .NET test suite directly, install the .NET 10 SDK and start Docker Desktop for Testcontainers.

From the repository root, create a local environment file:

```powershell
Copy-Item .env.example .env
```

Replace the example values in `.env` with a strong PostgreSQL password and a random Base64 JWT signing key containing at least 32 decoded bytes. **Never commit `.env` or publish its secrets.**

| Variable or setting | Purpose | Default / source |
| --- | --- | --- |
| `POSTGRES_PASSWORD` | PostgreSQL password | Required in `.env` |
| `JWT_SIGNING_KEY` | JWT signing key | Required in `.env` |
| `ConnectionStrings__RondiTrackDb` | Internal PostgreSQL connection | Set by Compose, host `db` |
| `Jwt__SigningKey` | API JWT configuration | Set by Compose |
| `ASPNETCORE_ENVIRONMENT` | API runtime environment | `Production` in Compose |
| `ASPNETCORE_HTTP_PORTS` | API HTTP listener | `8080` in Compose |
| `CycleClose__IntervalSeconds` | Cycle-close polling interval | 60 seconds |
| `Housekeeping__IntervalHours` | Housekeeping polling interval | 24 hours |
| `Housekeeping__RefreshTokenGraceDays` | Expired token-family grace period | 7 days |
| `Housekeeping__IdempotencyRetentionHours` | Persisted idempotency record retention | 24 hours |

The `db` hostname is resolved on the Compose network; `localhost` inside the API container does not refer to PostgreSQL. The database is not published on a host port. Scalar and OpenAPI endpoints are mapped only in Development, not in the default Production Compose environment.

### Build and run with Docker

Run the following from the repository root:

```powershell
docker compose config --quiet
docker compose up --build -d api
docker compose ps
```

The `db` service becomes healthy before the `migrate` service applies EF Core migrations. The `api` service starts after the migration service completes successfully.

Verify both health endpoints:

```powershell
curl.exe -i http://localhost:8080/health/live
curl.exe -i http://localhost:8080/health/ready
```

Both should return `HTTP/1.1 200 OK` and `Healthy` after startup. A request made immediately as the container starts may need to be retried.

For troubleshooting:

```powershell
docker compose logs api --tail 80
docker compose logs migrate --tail 50
```

To stop containers while preserving the database volume:

```powershell
docker compose down
```

**Warning:** `docker compose down -v` deletes named volumes, including PostgreSQL data.

To build the image independently, use the Dockerfile at the repository root:

```powershell
docker build -t ronditrack:assignment-6-4 -f Dockerfile .
```

### Run the automated tests

```powershell
dotnet restore RondiTrack.sln
dotnet build RondiTrack.sln --configuration Release
dotnet test RondiTrack.Tests/RondiTrack.Tests.csproj --configuration Release
```

Integration tests use disposable PostgreSQL instances through Testcontainers, so Docker Desktop must be available.

### Scheduled processing behaviour

Cycle eligibility is based on Johannesburg local dates. For example, a cycle ending on **25 October 2026** remains open at 23:59 SAST that day and becomes eligible at **00:00 SAST on 26 October** (22:00 UTC on 25 October). Only cycles with a configured end date are eligible.

When several cycles are overdue, the processor catches up by closing each eligible cycle and creating the next until it reaches a cycle that has not yet ended. Processing is transactional per stokvel, and concurrent workers coordinate using PostgreSQL transaction-scoped advisory locks. The arrears table also has a unique index on `(StokvelId, UserId, ContributionCycleId)`.

Housekeeping retains expired refresh-token families for seven additional days by default, supporting replay investigations before cleanup. Persisted idempotency records are removed when their creation timestamp is **older than** the configured 24-hour retention cutoff.

### Verification evidence — 19 October 2026

| Check | Observed result |
| --- | --- |
| Local automated tests | **130 passed, 0 failed, 0 skipped** |
| Main-branch GitHub Actions workflow #24 (commit `451b786`) | **Passed** |
| `docker compose config --quiet` | Passed |
| Standalone Docker image build | Passed, 19/19 steps |
| Compose API image build | Passed |
| PostgreSQL service | Healthy |
| API service | Running on host port 8080 |
| `GET /health/live` | `HTTP/1.1 200 OK` — `Healthy` |
| `GET /health/ready` | `HTTP/1.1 200 OK` — `Healthy` |

The initial health-check requests returned empty replies immediately after startup; repeating them once the API was running returned HTTP 200 for both endpoints. This is evidence of successful **local container startup and readiness**, not proof of a public production deployment.

The following repository resources support review:

- [Final passing CI run #24](https://github.com/mdvar2/RondiTrack/actions/runs/39176699045)
- [GitHub Actions](https://github.com/mdvar2/RondiTrack/actions)
- [CI workflow](.github/workflows/ci.yml)
- [Cycle-close integration tests](RondiTrack.Tests/CycleCloseIntegrationTests.cs)
- [Housekeeping retention tests](RondiTrack.Tests/HousekeepingRetentionTests.cs)
- [Dockerfile](Dockerfile)
- [Compose configuration](compose.yaml)
- [Intentional failing CI run](https://github.com/mdvar2/RondiTrack/actions/runs/39152181436)
- [Passing CI run after fix](https://github.com/mdvar2/RondiTrack/actions/runs/39152807177)
- [Proof pull request #3](https://github.com/mdvar2/RondiTrack/pull/3)

### Run it and build against it

This section is for a frontend developer who has cloned the repository with Docker Desktop available. Use the Docker Compose startup and health-check commands above; the API base URL is `http://localhost:8080`.

| Endpoint | URL | Availability |
| --- | --- | --- |
| API base | `http://localhost:8080/api/v1` | Production Compose |
| Liveness | `http://localhost:8080/health/live` | Production Compose |
| Readiness | `http://localhost:8080/health/ready` | Production Compose |
| Scalar explorer | `/scalar/v1` on the development API host/port | Development only |
| OpenAPI JSON | `/openapi/v1.json` on the development API host/port | Development only |

The frontend should use the API v1 contract, respect configured CORS origins and rate limits, and send an `Idempotency-Key` for contribution requests that might be retried. Authentication uses JWT access tokens and a refresh-token cookie; keep cookies in the supported browser flow rather than reading their contents from JavaScript. Consult [API reference](docs/api-reference.md) and [detailed technical notes](docs/README-detailed-technical-notes.md) for existing route and configuration details. TypeScript contracts are generated from the OpenAPI contract; confirm the exact generation command in the repository before running it.

**Unverified handoff items:** seeded demo accounts (including one member in two stokvels), a step-by-step refresh replay walkthrough, a frontend error-status table, and the exact TypeScript regeneration command have not been independently verified in this README. Do not assume these are available. A peer fresh-clone test and its findings/fix commits have not yet been recorded.

### Assignment 6.4 evidence still to collect

The assignment also requires numeric image size, initial-versus-code-only rebuild timing, `docker history` and `docker inspect` environment output, and evidence that `.env` has never been committed. The successful Docker builds establish that the image builds, **not** these measurements or secret-history proofs. Collect and record actual outputs before marking these items complete.

```powershell
git log --all -- .env
docker image inspect ronditrack:assignment-6-4 --format '{{.Size}}'
docker image inspect ronditrack:assignment-6-4 --format '{{json .Config.Env}}'
docker history --no-trunc ronditrack:assignment-6-4
```

The image contains only published application output in its runtime stage; the test project is not copied into that runtime output because testing is performed separately in CI. The contents of `docker history` and `docker inspect` must still be inspected for accidental secret exposure. For timing comparisons, measure a fresh uncached build and a rebuild after changing only source code; report the actual durations rather than estimates.

The refresh-token grace period is seven days so expiry does not immediately remove spent-token evidence that supports reuse investigation. Idempotency records are retained for 24 hours, accommodating delayed client retries within that window; a retry after expiry can be processed as a new request and must be treated accordingly by clients and operators.

For `docker compose up --scale api=2`, the fixed host mapping `8080:8080` cannot be shared by two replicas. A deployment should move port exposure to a reverse proxy, keep migrations as one one-shot service, and use the database transaction/advisory lock to serialize close processing. The automated concurrent-processors test exercises the locking behaviour; this is not a claim of a separate two-container manual demonstration.

### Handover and remaining work

RondiTrack is currently a **backend-only application**. The repository includes source code, automated tests, Compose configuration, an example environment file and technical documentation. The successful checks demonstrate that it can be built and started locally in containers and that its API health endpoints respond successfully.

Before a production rollout, the project still needs HTTPS and managed secrets, centralized operational monitoring, load and resilience testing, review of distributed rate limiting and cycle-period business constraints, and an independent fresh-clone peer acceptance review. A separate development database has a documented migration/data-quality issue; the successful Compose startup does not mean that database was repaired. `nextPayoutDate` remains `null` until a future-payout scheduling rule is defined.

No independent peer handover or public deployment is claimed here.
