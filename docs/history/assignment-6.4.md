# Assignment 6.4 — Shipped, Scheduled and Handed Over

| | |
|---|---|
| Official title | "Shipped, Scheduled and Handed Over", as in the README heading. The brief isn't in the repository. |
| Dates | 18–19 October 2026 |
| Final commit on `main` | [`16d1a4c`](https://github.com/mdvar2/RondiTrack/commit/16d1a4c) "docs: fix README formatting and clarify handover requirements" |
| Branch / PR | Committed directly to `main`. [PR #3](https://github.com/mdvar2/RondiTrack/pull/3) (`test/ci-failure-check`, still open) was the deliberate CI-failure proof |
| README as submitted | [README at `16d1a4c`](https://github.com/mdvar2/RondiTrack/blob/16d1a4c/README.md) |
| Re-tested | **Yes, 27 Oct 2026**: the `16d1a4c` code, exported unchanged, passed **130/130** on disposable PostgreSQL. Docker Compose and CI weren't re-run |

## 1. Purpose and learning objectives
To make RondiTrack shippable and self-maintaining: a container image, one-command startup, CI, scheduled cycle closing, housekeeping, durable idempotency, and a handover a frontend developer could follow. The brief's learning objectives aren't recorded.

## 2. Original requirements
The brief isn't in the repository. The record below quotes several requirements:
- **Evidence the assignment required:** "numeric image size, initial-versus-code-only rebuild timing, `docker history` and `docker inspect` environment output, and evidence that `.env` has never been committed". **These were never collected.**
- **Discussion questions:** scaling to `--scale api=2`, the refresh-token grace period, and idempotency retention.
- **Handover items, recorded as unverified:** seeded demo accounts, a refresh-replay walkthrough, a frontend error-status table, and a peer fresh-clone test.

The [handover checklist](evidence/assignment-6-4-handover.md) lists what was delivered and what was outstanding.

## 3. What I implemented
See [Delivered functionality](#delivered-functionality) below:
- A multi-stage, non-root `Dockerfile`, and `compose.yaml` (PostgreSQL 17, a one-shot migration service, the API).
- GitHub Actions CI.
- **Cycle closing** with arrears in integer cents, catch-up processing and advisory locks.
- **Housekeeping** of expired refresh families and old idempotency records.
- **Durable idempotency:** `PostgresIdempotencyStore`, replacing the in-memory store used since 4.4.

## 4. Technologies, tools and dependencies
- **Docker images:** `mcr.microsoft.com/dotnet/sdk:10.0` and `aspnet:10.0`, and `postgres:17`.
- **Compose:** the migration container installs `dotnet-ef` 10.0.1.
- **CI:** GitHub Actions.
- **.NET building blocks:** `BackgroundService` and `TimeProvider`, plus PostgreSQL transaction-scoped advisory locks.
- **No new NuGet packages** compared with 6.3, judging by the current `API/RondiTrack.csproj`.

## 5. Prerequisites and installation
Docker Desktop (Linux containers) and Git for Compose. The .NET 10 SDK only to run the tests directly.

## 6. Environment configuration
- `.env` from `.env.example`: `POSTGRES_PASSWORD` and `JWT_SIGNING_KEY`. Never commit it.
- Optional settings: `CycleClose__IntervalSeconds`, and the `Housekeeping__*` interval, grace and retention settings. All of them are listed in the [original record](#prerequisites-and-configuration).

## 7. How to run or reproduce
**As submitted:** [Build and run with Docker](#build-and-run-with-docker) and [Run the automated tests](#run-the-automated-tests) below. Re-run on 27 Oct 2026: the tests passed **130 of 130**. I didn't re-run the Compose stack.

**In the current codebase:** `docker compose up --build -d api` still uses the same files. See the [root README](../../README.md#docker-compose-api-only).

## 8. Important files and components
- `Dockerfile`, `.dockerignore`, `compose.yaml`, `.env.example`, `.github/workflows/ci.yml`
- `API/BackgroundJobs/`: `CycleCloseProcessor`, `CycleCloseWorker`, `HousekeepingProcessor`, `HousekeepingWorker` and their options
- `API/Idempotency/PostgresIdempotencyStore.cs`, `Domain/Entities/ContributionArrear.cs`, `PersistedIdempotencyRecord.cs`
- Migrations: `AddContributionCycleEndDate`, `AddContributionArrears`, `AddPersistedIdempotency`
- Tests: `CycleCloseIntegrationTests.cs`, `CycleCloseCatchUpTests.cs`, `HousekeepingRetentionTests.cs`, `PersistedIdempotencyTests.cs`

## 9. How it works
See [Scheduled processing behaviour](#scheduled-processing-behaviour) below. A cycle becomes eligible at Johannesburg midnight after its end date. Each stokvel is processed in its own transaction under an advisory lock, so two API instances can't close the same cycle twice. Unpaid members get arrears rows, and the next cycle is created.

## 10. Testing and recorded results
| Point | Result |
|---|---|
| Local tests (19 Oct 2026) | **130 passed**, 0 failed, 0 skipped |
| Main-branch CI run #24 (`451b786`) | Passed |
| Re-run 27 Oct 2026 of `16d1a4c` | **130 passed, 0 failed** |

## 11. Challenges and solutions
- **Health checks at startup.** The first `/health` requests right after startup got empty replies; repeating them once the API was up returned 200.
- **Evidence never collected.** The handover was written in an environment without the .NET SDK or Docker, so the full suite had to be run afterwards. The image-size and timing evidence was never collected (see [evidence still to collect](#assignment-64-evidence-still-to-collect)).

## 12. Learning outcomes
Not recorded as such. The [handover section](#handover-and-remaining-work) lists what production would still need.

## 13. Evidence, commits and pull requests
- [Handover checklist](evidence/assignment-6-4-handover.md)
- [PR #3](https://github.com/mdvar2/RondiTrack/pull/3), the deliberate CI-failure proof
- The CI run links are in [verification evidence](#verification-evidence--19-october-2026) below

## 14. Relationship to other assignments
- **Before:** [6.3](assignment-6.3.md)'s frontend-ready API, and [4.4](assignment-4.4.md)'s idempotency, now persisted.
- **Next:** [7.1](assignment-7.1.md) started the frontend.
- **Later:**
  - The cycle-close and housekeeping workers run whenever the API runs, including under [8.1](assignment-8.1.md).
  - `nextPayoutDate` is still `null`.

## 15. Original record (as written at the time)

> **Historical record.** This was the Assignment 6.4 section of the root README. It was moved here unchanged (apart from Markdown formatting) on 27 October 2026. Commands, ports and test counts are as recorded at the time and may be out of date. For the current project, see the [root README](../../README.md).

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

- [CI workflow](../../.github/workflows/ci.yml)

- [Cycle-close integration tests](../../RondiTrack.Tests/CycleCloseIntegrationTests.cs)

- [Housekeeping retention tests](../../RondiTrack.Tests/HousekeepingRetentionTests.cs)

- [Dockerfile](../../Dockerfile)

- [Compose configuration](../../compose.yaml)

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

The frontend should use the API v1 contract, respect configured CORS origins and rate limits, and send an `Idempotency-Key` for contribution requests that might be retried. Authentication uses JWT access tokens and a refresh-token cookie; keep cookies in the supported browser flow rather than reading their contents from JavaScript. Consult [API reference](../api-reference.md) and [detailed technical notes](technical-notes-to-6.3.md) for existing route and configuration details. For Assignment 7.1, the TypeScript contract was successfully regenerated from a locally running Development API on port **8081**, because port 8080 was already occupied by Docker/WSL forwarding. See the frontend README for contract-generation instructions.

**Unverified handoff items:** seeded demo accounts (including one member in two stokvels), a step-by-step refresh replay walkthrough, and a frontend error-status table have not been independently verified in this README. Do not assume these are available. A peer fresh-clone test and its findings/fix commits have not yet been recorded.

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

At the time of Assignment 6.4, RondiTrack was a backend-only application. Assignment 7.1 subsequently added a React frontend using typed sample data. The repository includes backend and frontend source code, automated tests, Compose configuration, an example environment file and technical documentation. The recorded checks demonstrate local container startup and API readiness, not a public production deployment.

Before a production rollout, the project still needs HTTPS and managed secrets, centralized operational monitoring, load and resilience testing, review of distributed rate limiting and cycle-period business constraints, and an independent fresh-clone peer acceptance review. A separate development database has a documented migration/data-quality issue; the successful Compose startup does not mean that database was repaired. `nextPayoutDate` remains `null` until a future-payout scheduling rule is defined.

No independent peer handover or public deployment is claimed here.
