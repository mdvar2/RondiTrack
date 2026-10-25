# RondiTrack

[![RondiTrack CI](https://github.com/mdvar2/RondiTrack/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/mdvar2/RondiTrack/actions/workflows/ci.yml)

RondiTrack is a stokvel management application I built during my Bitcube software development training. It includes an ASP.NET Core backend for managing members, contributions, savings cycles and payouts, alongside a React and TypeScript frontend for displaying stokvel and contribution information.

I started with basic API endpoints and gradually added database relationships, security, testing and monitoring. The project progressed through frontend-facing API contracts, containerized deployment, scheduled processing, durable idempotency and automated CI verification. Assignment 7.1 introduced a responsive sample-data React dashboard typed against the backend OpenAPI contract; Assignment 7.3 subsequently connected it to live authenticated data.

## What the system does

- Manages users, stokvels, memberships and contribution cycles.

- Records contributions and payouts while checking the business rules.

- Uses JWT authentication and membership-based permissions to protect requests.

- Prevents duplicate contribution processing when a request is retried with the same idempotency key.

- Provides a member home endpoint so a logged-in user can see their own stokvels and contribution information.

- Returns consistent error responses and includes tracing, logging and health checks.

The project now includes both a backend API and a React frontend. I use Scalar to explore and test the API during development. As of Assignment 7.3, the React dashboard loads live authenticated data from the API. Earlier assignment sections below describe the previous sample-data versions.

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

| React 19 / Vite 8 | Built the responsive StokvelCard and ContributionCard frontend |

| TypeScript 5 / OpenAPI TypeScript | Generated typed frontend contracts from the API OpenAPI document |

| Vitest / ESLint | Tested frontend formatting, session refresh, API errors, and query isolation; checked code quality |

I separated the solution into ****API****, ****Domain****, ****Infrastructure**** and ****RondiTrack.Tests**** projects. The [architecture guide](docs/architecture.md) explains how they fit together.

## Assignment 6.3 — Frontend-Ready Backend

For Assignment 6.3, I worked on the parts of the backend that a frontend would depend on:

- ****API versioning:**** Controller endpoints now use `/api/v1`.

- ****CORS:**** The API allows the configured frontend origin rather than accepting requests from any origin.

- ****Rate limiting:**** Login, contribution and payout requests have limits to reduce excessive requests.

- ****Contribution retries:**** The existing idempotency handling works alongside rate limiting.

- ****Member home endpoint:**** `GET /api/v1/me/home` returns information for the logged-in member without exposing other members' contribution amounts.

- ****Consistent errors:**** API errors follow a Problem Details response format.

- ****TypeScript contracts:**** I generated types from the actual OpenAPI document and checked that a renamed backend property would cause a frontend compile error.

One feature is still incomplete: ****`nextPayoutDate` returns `null`**** because the system does not yet have a rule for scheduling future payouts. I have not made up a date for it.

For a more detailed explanation of the changes, see the [Assignment 6.3 trainer notes](docs/assignment-6-3-stages.md) and [compliance matrix](docs/assignment-6-3-compliance.md).

## Testing and verification

****On 14 October 2026, all 122 tests passed, with no failures or skipped tests.**** The fresh solution build also completed with no warnings or errors.

I also checked that the tests could run without connecting to my development PostgreSQL database. I temporarily disabled new connections to that database, ran the test suite using Testcontainers, and then restored the original connection setting. I confirmed the database was accessible again with `SELECT 1`.

No development data, schemas or migrations were changed during that verification.

The [Assignment 6.3 verification report](docs/assignment-6-3-verification.md#successful-offline-verification) has the full procedure and recorded results. The earlier Assignment 6.2 test result was 101 passing tests; Assignment 6.3 verified 122, and Assignment 6.4 later verified 130 passing tests.

To run the ordinary tests locally, start Docker Desktop in Linux-container mode and use:

```powershell

dotnet restore RondiTrack.sln

dotnet build RondiTrack.sln --no-restore -m:1

dotnet test RondiTrack.sln --no-restore -m:1

```

These commands run the normal suite. They do ****not**** disable development database connections. The guarded isolation procedure is explained separately in the verification report.

## Running the API locally

You need the .NET SDK, PostgreSQL and the project's local configuration. From the repository root:

```powershell

dotnet restore RondiTrack.sln

dotnet build RondiTrack.sln

dotnet run --project API

```

The development API documentation is available at `/scalar/v1` and the OpenAPI document at `/openapi/v1.json` on the port used by the running API.

****Important database note:**** My existing development database contains duplicate normalized emails that currently block an authentication migration. I am keeping that data intact rather than resetting the database to make the migration pass. See [development migration status](docs/development-migration-status.md) before attempting database updates.

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

- ****Containerized deployment:**** a root-level, multi-stage `Dockerfile` publishes the API using the .NET 10 SDK and runs it using the ASP.NET Core 10 runtime. `compose.yaml` defines PostgreSQL, a one-shot EF Core migration service and the API, with a persistent PostgreSQL volume. The API listens on port 8080.

- ****Scheduled cycle closing:**** a background processor uses `TimeProvider` and `Africa/Johannesburg` calendar dates to identify ended cycles. It records unpaid member contributions as arrears in integer cents, closes eligible cycles and creates successor cycles. Catch-up processing handles multiple overdue cycles. Per-stokvel transactions, PostgreSQL advisory locks and an arrears uniqueness constraint protect concurrent execution. Errors include the affected stokvel ID and are retried on later runs.

- ****Housekeeping:**** expired refresh-token families are deleted after a configurable grace period, with related token rows removed by cascade. Persisted idempotency records older than the configured retention cutoff are also removed using set-based database operations.

- ****Durable idempotency:**** idempotency records are persisted in PostgreSQL instead of relying solely on an in-memory process-local response store.

- ****Continuous integration:**** `.github/workflows/ci.yml` restores and builds the solution, runs automated tests including PostgreSQL Testcontainers integration tests, and builds the Docker image.

### Prerequisites and configuration

For Docker Compose, install Docker Desktop (Linux containers enabled) and Git. The host .NET SDK is not required for the Compose build. To run the .NET test suite directly, install the .NET 10 SDK and start Docker Desktop for Testcontainers.

From the repository root, create a local environment file:

```powershell

Copy-Item .env.example .env

```

Replace the example values in `.env` with a strong PostgreSQL password and a random Base64 JWT signing key containing at least 32 decoded bytes. ****Never commit `.env` or publish its secrets.****

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

****Warning:**** `docker compose down -v` deletes named volumes, including PostgreSQL data.

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

Cycle eligibility is based on Johannesburg local dates. For example, a cycle ending on ****25 October 2026**** remains open at 23:59 SAST that day and becomes eligible at ****00:00 SAST on 26 October**** (22:00 UTC on 25 October). Only cycles with a configured end date are eligible.

When several cycles are overdue, the processor catches up by closing each eligible cycle and creating the next until it reaches a cycle that has not yet ended. Processing is transactional per stokvel, and concurrent workers coordinate using PostgreSQL transaction-scoped advisory locks. The arrears table also has a unique index on `(StokvelId, UserId, ContributionCycleId)`.

Housekeeping retains expired refresh-token families for seven additional days by default, supporting replay investigations before cleanup. Persisted idempotency records are removed when their creation timestamp is ****older than**** the configured 24-hour retention cutoff.

### Verification evidence — 19 October 2026

| Check | Observed result |

| --- | --- |

| Local automated tests | ****130 passed, 0 failed, 0 skipped**** |

| Main-branch GitHub Actions workflow #24 (commit `451b786`) | ****Passed**** |

| `docker compose config --quiet` | Passed |

| Standalone Docker image build | Passed, 19/19 steps |

| Compose API image build | Passed |

| PostgreSQL service | Healthy |

| API service | Running on host port 8080 |

| `GET /health/live` | `HTTP/1.1 200 OK` — `Healthy` |

| `GET /health/ready` | `HTTP/1.1 200 OK` — `Healthy` |

The initial health-check requests returned empty replies immediately after startup; repeating them once the API was running returned HTTP 200 for both endpoints. This is evidence of successful ****local container startup and readiness****, not proof of a public production deployment.

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

The frontend should use the API v1 contract, respect configured CORS origins and rate limits, and send an `Idempotency-Key` for contribution requests that might be retried. Authentication uses JWT access tokens and a refresh-token cookie; keep cookies in the supported browser flow rather than reading their contents from JavaScript. Consult [API reference](docs/api-reference.md) and [detailed technical notes](docs/README-detailed-technical-notes.md) for existing route and configuration details. For Assignment 7.1, the TypeScript contract was successfully regenerated from a locally running Development API on port ****8081****, because port 8080 was already occupied by Docker/WSL forwarding. See the frontend README for contract-generation instructions.

****Unverified handoff items:**** seeded demo accounts (including one member in two stokvels), a step-by-step refresh replay walkthrough, and a frontend error-status table have not been independently verified in this README. Do not assume these are available. A peer fresh-clone test and its findings/fix commits have not yet been recorded.

### Assignment 6.4 evidence still to collect

The assignment also requires numeric image size, initial-versus-code-only rebuild timing, `docker history` and `docker inspect` environment output, and evidence that `.env` has never been committed. The successful Docker builds establish that the image builds, ****not**** these measurements or secret-history proofs. Collect and record actual outputs before marking these items complete.

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

## Assignment 7.1 — StokvelCard & ContributionCard

Assignment 7.1 adds a responsive React 19, TypeScript and Vite frontend in [`web/`](web/). The dashboard uses typed demonstration data, ****not live authenticated API fetching****. See the [frontend README](web/README.md) for component-level setup and limitations.

### Backend contract

- The Development API exposes its OpenAPI document at `/openapi/v1.json`.

- `GET /api/v1/me/home` provides member-specific stokvel details, including role, current cycle, amount paid, amount owed and a next payout date when available.

- Contribution response types now include a `status` field. The generated frontend contract in `web/src/api/schema.d.ts` requires this field.

- Home and contribution amounts are represented as ****integer cents**** in the frontend-facing API contract. Display formatting converts cents to ZAR.

- A missing payout schedule remains `null`; the UI must not imply that a payout date was calculated by the backend.

### Frontend components and sample cases

- ****`StokvelCard`**** presents the stokvel name, member role, current cycle details, amount paid and owed, and payout scheduling information. The interface distinguishes amounts owed from an ****All paid up**** state. A Treasurer-only tools area is a nonfunctional UI placeholder, not an active privileged operation.

- ****`ContributionCard`**** presents the amount, the contribution's `status` (Pending, Paid or Confirmed in the demonstration cases), cycle name, Johannesburg-local recorded timestamp and contributor name when available. Contributions are keyed by their IDs.

- ****Formatting:**** integer cents are displayed as ZAR amounts; UTC timestamps are displayed in `Africa/Johannesburg`. For example, `2026-10-31T22:30:00Z` is 01 November 2026 at 00:30 SAST.

- ****Sample coverage:**** the frontend demonstration includes Treasurer and Member roles, a zero-owed state, a small 10-cent amount and contribution status variants. The example records are ****illustrative fixtures****, not verified captures of signed-in API responses. This provenance distinction remains a verification item against the assignment rubric.

- ****Types:**** `web/src/api/schema.d.ts` is generated from the backend OpenAPI document; `web/src/api/types.ts` provides aliases for component use. No live frontend API calls are implemented for this assignment.

### Run the frontend

Install Node.js 24 (see `web/.nvmrc`), then run from the repository root:

```powershell

cd web

npm.cmd ci

npm.cmd run dev

```

Open the local URL printed by Vite. The sample-data dashboard does not require the API to be running.

To ****regenerate API types****, start the current Development API on a free port, for example from the repository root:

```powershell

dotnet run --project API --urls http://localhost:8081

```

In a second PowerShell terminal:

```powershell

cd web

npx.cmd openapi-typescript http://localhost:8081/openapi/v1.json -o src/api/schema.d.ts

npm.cmd run typecheck

```

****Port note:**** Docker Compose uses host port 8080 for the containerized API. During the Assignment 7.1 verification, Docker Desktop/WSL occupied that port and served an older OpenAPI contract, so the updated development API was run on ****8081****. The `npm.cmd run generate:api` script targets 8080; only use it when that port is serving the intended, up-to-date Development API.

### Verification — 20 October 2026

From `web/`:

```powershell

npm.cmd run typecheck

npm.cmd run build

npm.cmd run lint

npm.cmd run test -- --run

```

| Check | Observed result |

| --- | --- |

| TypeScript typecheck | Passed |

| Vite production build | Passed (24 modules transformed) |

| ESLint | Passed |

| Vitest | ****14 passed****, across 2 test files |

| Backend automated tests | ****130 passed**** in the previously reported local run |

These are local verification results, ****not**** a claim that the latest changes have been committed, pushed or passed CI. The earlier commit history and Assignment 6.4 CI evidence above remain historical evidence for those earlier versions.

### Limitations and handoff

The dashboard does not yet provide login, authenticated API requests, payment submission or working Treasurer controls. The demonstration statuses and records should not be mistaken for independently captured production or signed-in API responses. The final rubric check should confirm the required sample-data provenance and UI edge cases before declaring Assignment 7.1 fully complete. Backend authorization must remain enforced by the API; hiding a frontend control is not authorization.

## Assignment 7.2 — A Styled, Responsive Stokvel View

Assignment 7.2 extends the RondiTrack React frontend with responsive styling, contribution-cycle grouping, status badges, South African due-date countdowns, and theme selection.

### Implemented features

- Responsive StokvelCard and ContributionCard components.

- Light, Dark and System theme options.

- Contribution cycles grouped and ordered, including Open, Closed and PaidOut statuses.

- Expandable contribution-cycle sections.

- Contribution status badges for Pending, Paid and Confirmed.

- Arrears indicators for members with outstanding contributions.

- South African currency and Johannesburg-local date formatting.

- Due-date countdowns based on Johannesburg calendar days.

- Additional backend support for retrieving the authenticated member's persisted arrears.

- Automated tests for contribution grouping, date boundaries and arrears behaviour.

### Running the frontend

From the repository root:

```powershell

cd web

npm ci

npm run dev

```

Open the local URL displayed by Vite, normally `http://localhost:3000/`.

### Verification

The latest local verification completed successfully:

- Backend: 132 tests passed.

- Frontend: 23 tests passed.

- TypeScript, ESLint and production build passed.

- Production dependency audit: 0 vulnerabilities.

### Current limitations

The dashboard uses illustrative demonstration data rather than live authenticated API requests. The interface and backend implementation should be reviewed separately when verifying database-backed scenarios.

The contribution-group controls and the required mobile screenshots, contrast measurements and other assignment evidence have not all been verified as fully compliant.

### Further documentation

See [Assignment 7.2 frontend README](web/README.md) and [Assignment 7.2 backend/UI notes](docs/assignment-7-2-stage2.md).

## Assignment 7.3 — Live RondiTrack Data

Assignment 7.3 replaces the previous demonstration-data dashboard with authenticated requests to the ASP.NET Core API. The Assignment 7.1 and 7.2 sections above describe the implementation and test results **at those earlier stages**, not the current frontend.

### Delivered functionality

- Sign in and sign out, with JWT access tokens kept in memory and refresh-token cookies handled by the browser.
- A single-flight refresh mechanism: concurrent requests receiving HTTP 401 share one refresh attempt rather than issuing separate refresh requests.
- Typed API client and queries using the generated OpenAPI contract (`web/src/api/schema.d.ts`).
- Live stokvel, contribution-cycle, and contribution data, including paginated contribution responses.
- Switching between stokvels with query isolation, plus loading, empty, and error states.
- Error handling for API failures, including HTTP 429, while retaining Light, Dark, and System theme options.

### API contract notes

The generated TypeScript contract comes from the running **Development** API's `/openapi/v1.json`. The contribution pagination response annotation was aligned with the paginated contribution-with-member response shape; the typed query layer uses that generated response type and its continuation token.

During local verification, the Development API ran at `http://localhost:8081`; Docker Compose exposed a separate API at `http://localhost:8080`. Do not assume the two instances connect to the same database or expose identical development-only endpoints.

### Run and verify

Start the appropriate backend API with its required configuration. From the repository root, run:

```powershell
cd web
npm.cmd ci
npm.cmd run dev
```

Open `http://localhost:3000` and sign in with an existing valid account. To regenerate the contract from the Development API running on port 8081:

```powershell
cd web
npx.cmd openapi-typescript http://localhost:8081/openapi/v1.json -o src/api/schema.d.ts
npm.cmd run typecheck
```

For local verification:

```powershell
# From the repository root:
dotnet test

# From web/:
npm.cmd run typecheck
npm.cmd run lint
npm.cmd run build
npm.cmd test -- --run
```

| Check | Recorded result |
| --- | --- |
| Backend automated tests | 132 passed |
| Frontend automated tests | 35 passed across 7 test files |
| TypeScript typecheck | Passed |
| ESLint | Passed |
| Vite production build | Passed |
| Pull-request CI | Passed in the previously observed GitHub checks; verify again after documentation changes |

### Single-flight proof

The session tests verify that concurrent HTTP 401 responses share one token-refresh request. In a deliberate red/green test, temporarily disabling the guard made the test fail (multiple refresh requests); restoring the guard made it pass.

Evidence: `web/src/auth/session.test.ts`, `web/single-flight-failing.txt`, and `web/single-flight-passing.txt`.

### Stokvel switch proof

The query tests cover pagination, an empty page, and a slow-loading stokvel switch. The isolation test failed when `keepPreviousData` was temporarily enabled and passed after it was removed, demonstrating protection against showing the previous stokvel's contributions under the newly selected stokvel.

Evidence: `web/src/api/queries.pagination.test.tsx`, `web/stokvel-switch-failing.txt`, and `web/stokvel-switch-passing.txt`.

Manual browser checks also showed the selected stokvel and its displayed amount changing between **RondiTrack Demo Emergency Fund (R250)** and **RondiTrack Demo Savings (R500)**, and switching back successfully.

### Shared phone check

The browser sign-out flow was exercised, and an attempted sign-in with invalid credentials was rejected. A complete shared-device demonstration with **two different successfully authenticated users** was **not completed**, because a second account with known credentials was unavailable. This remains a manual verification gap; automated session tests are not a substitute for that scenario.

### API errors and HTTP 429

The frontend includes API error handling and automated tests for error messages, including HTTP 429. A **real HTTP 429 response from the running API was not manually demonstrated**. Do not treat the automated error-handling test as evidence of a live rate-limit event.

### Known gaps

- Two-user shared-phone browser test not completed.
- Unauthorized-stokvel access was not independently demonstrated in a manual browser/API check.
- Live HTTP 429 response was not independently demonstrated.
- No public production deployment is claimed; local Development and Docker Compose API instances may use different databases.

The red/green test evidence is included in the `web/` directory. The historical results and limitations in earlier assignment sections remain intentionally preserved.


## Assignment 7.4 — Record a Contribution

A member can now record what they paid into the open cycle from a form on the dashboard, and a Treasurer or Chairperson can record it for any member of their own stokvel. The amount is typed in rands, becomes whole cents in the browser, and every retry of one payment carries the same `Idempotency-Key`.

### What I built

- **API:** `RecordContributionRequest` now takes `amountInCents` (a whole number) instead of a decimal `amount`. A contribution to a cycle that is not `Open` is refused with a 409. I updated the existing tests that posted the old field.
- **Money:** `parseRandsToCents` and `centsToInput` in `web/src/lib/money.ts`. Only whole-number arithmetic is used. `"150.50"`, `"150.5"`, `"150,50"` and `"1 500,50"` are accepted. `"150.505"` (needs rounding) and `"1,500.50"` (is the comma a thousands separator or a decimal point?) are refused. A round-trip test checks every value from 0 to 100 000 cents.
- **Form:** `web/src/components/ContributionForm.tsx` uses React Hook Form, Zod and `useMutation`. It is only shown for an Open cycle. For Closed or PaidOut it shows a sentence. The member picker only appears for a Treasurer or Chairperson, read from `/me/home` for this stokvel.
- **Key logic:** `web/src/contributions/useRecordContribution.ts`.
- **Tests:** `web/src/contributions/recordContribution.test.ts` (six MSW tests) and `RondiTrack.Tests/ContributionIdempotencyContractTests.cs`.

The assignment names the route `/cycles/{cycleId}/contributions`. My API's real route is `POST /api/v1/stokvels/{stokvelId}/members/{userId}/contributions`, with the cycle in the body. I kept my route and used the generated client for it.

### Idempotency contract

I checked each answer against the real API with automated tests (Testcontainers PostgreSQL). All of these pass.

| # | Question | Answer | Test |
|---|---|---|---|
| 1 | Same key, same request | The second call returns the same `201` and the identical body. Only one contribution is stored. There is **no** replay header, so the form cannot say "Already recorded". | `SameKeySameRequest_ReplaysTheSame201_WithoutAReplayHeader` |
| 2 | Same key, different amount | `409 Conflict`, detail "Idempotency-Key was already used with a different request." The old response is not replayed. So a changed amount **must** get a new key. | `SameKeyDifferentAmount_Is409_AndRecordsNothingNew` |
| 3 | New key, member already contributed | `409`, detail "A contribution already exists for this member and cycle." | `NewKeyForAMemberWhoAlreadyContributed_Is409WithAReason` |
| 4 | Rate limit, then retry with the same key | The `429` does not store the key. After `Retry-After` the same key records once, and a further replay still gives one contribution. | `Contribution429_DoesNotStoreKey_ThenSucceedsAndReplaysExactlyOnce` (already existed) |
| 5 | No key | `400`. The OpenAPI document lists `Idempotency-Key` as a header parameter, so the generated client has it. An operation transformer in `Program.cs` marks it **required** in the document (the C# parameter is nullable so the service can give its own 400 message), so the generated client now fails to compile without it. | `MissingKey_Is400_AndOpenApiDescribesTheHeader` |
| 6 | Preflight from `http://localhost:3000` asking for `Idempotency-Key` | `204`, the origin is allowed, `Access-Control-Allow-Credentials: true`, and `idempotency-key` is in the allowed headers. | `Preflight_AllowsOnlyConfiguredOriginWithoutToken` (already existed) |

The answers led to two API fixes, both with tests: the request body now uses `amountInCents`, and a closed cycle is refused (`ClosedCycle_Is409WithAReason`). Before that, the API accepted a contribution into a Closed cycle.

Who may record for whom is enforced by the API, not by hiding the picker. `Member_CanRecordOwnContribution_ButCannotRecordForAnotherMember` shows a Member gets `403` for another member.

### The key, and the decision table

The key is made with `crypto.randomUUID()` inside the submit handler, and kept in a `useRef` inside the hook. Each form has its own, and it is not in a module variable or the TanStack cache. It is sent as the `Idempotency-Key` header through the generated client.

- A new key is made when there is none, or when the member, cycle or amount differs from the last attempt. That covers the first submit and the first submit after a success.
- The same key is reused after a network error, a 5xx, a 429 and a 401 that `authFetch` refreshes and retries (`authFetch` copies the request headers).
- After a 201 the key is dropped, so the next contribution gets a new one.
- **400, 409 or 422, then resubmit with nothing changed: I keep the same key.** None of those answers is stored by the API, so nothing was recorded. Resending the same intent gets the same answer again, and if the problem was fixed on the server meanwhile, it records once. Changing anything gives a new key, which question 2 shows is required.

**StrictMode:** StrictMode renders twice, but the key is never made during rendering, only in the submit handler. The six tests run inside `<StrictMode>`.

**Reload during a request:** the key is lost and the next attempt gets a new one. This is still safe, because the API allows one contribution per member per cycle (question 3). If the first request landed, the new attempt gets a `409` saying the contribution already exists, and nothing is recorded twice. If it did not land, it records once. Persisting the key (the stretch challenge) is not done.

### Idempotency proof

I made the deliberate mistake in `useRecordContribution.ts`: a new `crypto.randomUUID()` for every request instead of using the intent's key. Output is in `web/idempotency-failing.txt`:

```
 ❯ src/contributions/recordContribution.test.ts (6 tests | 2 failed) 141ms
     × sends the same key when retrying after a network error 62ms
     × keeps the key on a 429 and exposes the wait from Retry-After 21ms
 ...
 Test Files  1 failed (1)
      Tests  2 failed | 4 passed (6)
```

After restoring the code (`web/idempotency-passing.txt`):

```
 Test Files  1 passed (1)
      Tests  6 passed (6)
```

The 401 test still passed with the bug. This is because `authFetch` resends the same `Request` object, so the key made for it is kept. The bug shows in the two tests where my own code makes a second request.

What this bug would cost a real member: after a dropped connection, the retry would carry a new key, so the API would treat it as a new payment. The member would be told "already recorded" (409) at best, and where the amount changed or the first request had not landed, the money could be recorded twice or wrongly.

### Optimistic or not

I did not make recording optimistic. The server owns the contribution's id, its status, the cycle's new total and any arrears, so the browser could only guess them. A 409 or 422 is a normal answer here (already paid, cycle closed), so an optimistic "Recorded" would be shown and then taken away, and in between a member or Treasurer could act on a wrong total. "Recorded" in front of a treasurer at a meeting has to mean the API saved it. So the button says "Recording…" until the API has answered and the home, cycle and contribution queries have refetched, then the form says what was recorded.

### Known gaps

- I did not do the screen recording or a manual browser run against the live API. The form was exercised by automated tests only (MSW), plus a throwaway component smoke test that I deleted.
- Treasurer prefill: the home endpoint only says what the signed-in person owes, so when a Treasurer picks another member the amount starts empty.
- The member picker shows for a Chairperson too, since the API allows both roles to record for others.
- The user id for "record for myself" is read from the access token's `sub` claim (`currentUserId` in `session.ts`), because `/me/home` does not return it.
- No replay header, so there is no "Already recorded" message (stretch 3). Stretch 1, 2 and 4 are not done.
- `clients/typescript/` (the Assignment 6.3 contract check) was not regenerated: it had already drifted from the API, and regenerating it breaks its own `check` step for reasons unrelated to this assignment. Only `web/src/api/schema.d.ts` was regenerated.

### Results

- Backend: `dotnet test RondiTrack.Tests` — 137 passed, 0 failed.
- Frontend: `npm run lint` clean, `npm run build` succeeded, `npm test -- --run` — 8 files, 62 tests passed.
