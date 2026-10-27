# RondiTrack

[![RondiTrack CI](https://github.com/mdvar2/RondiTrack/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/mdvar2/RondiTrack/actions/workflows/ci.yml)

## Project Overview

RondiTrack manages **stokvels**: South African savings groups whose members pay into a shared pool each cycle and take turns receiving payouts. It records members, contribution cycles, contributions and payouts, and enforces who may see and do what in each stokvel.

I'm building RondiTrack as part of the **Bitcube Software Development Trainee programme**. Each assignment extends this same repository, so the project is both a working application and a record of what I've learned. This README describes the project **as it is now**. Every assignment has its own record, with its requirements, implementation, how to run and test it, and its evidence, in the [assignment history](docs/history/README.md).

### How the assignments built RondiTrack

| Stage | Assignments | What it added |
|---|---|---|
| A working API | 4.1–4.4 | Endpoints, validation, business rules, RFC 9457 errors, idempotent contributions (in memory) |
| Real persistence | 5.1–5.4 | PostgreSQL with EF Core, relationships, database constraints, concurrency, pagination, the API / Domain / Infrastructure split, Testcontainers |
| Security and operations | 6.1–6.4 | JWT and refresh tokens, per-stokvel roles, OpenTelemetry, POPIA-safe logs, `/api/v1`, CORS, rate limits, Docker, CI, scheduled jobs |
| A browser frontend | 7.1–7.4 | `web/` (React + Vite): cards, a responsive dashboard, live signed-in data, recording contributions |
| Server-rendered frontend | **8.1** | `web-next/` (Next.js): tokens kept on the server, member and treasurer dashboards |

Each row links to its records in the [assignment history](docs/history/README.md#assignments-oldest-first).

## Current Implementation

| Part | Folder | State | Latest assignment |
|---|---|---|---|
| REST API | [`API/`](API/), [`Domain/`](Domain/), [`Infrastructure/`](Infrastructure/) | ASP.NET Core 10 + EF Core 10 + PostgreSQL 17, versioned under `/api/v1` | 8.1 (stokvel detail, `GET /me`) |
| Backend tests | [`RondiTrack.Tests/`](RondiTrack.Tests/) | xUnit integration tests against disposable PostgreSQL (Testcontainers) | 8.1 |
| **Next.js frontend** | [`web-next/`](web-next/README.md) | Server-rendered member and treasurer pages; the browser never calls the API | **8.1 (current)** |
| React/Vite frontend | [`web/`](web/README.md) | The original single-page app; live data and contribution recording; the browser calls the API | 7.4 |
| Contract check | [`clients/typescript/`](clients/typescript/README.md) | Assignment 6.3 breaking-change experiment (out of date, see [Known Limitations](#known-limitations)) | 6.3 |

### The two frontends

Both frontends show the same API data, but they're built differently. The newer one, `web-next/`, keeps every token on the server.

| | `web/`: React + Vite | `web-next/`: Next.js 16 |
|---|---|---|
| Built in | Assignments 7.1–7.4 | Assignment 8.1 |
| Rendering | Client-side single-page app | Server Components (App Router) |
| Who calls the API | The browser | Only the Next.js server |
| Access token | Browser memory | Encrypted `HttpOnly` `session` cookie on the Next.js site, unreadable by JavaScript |
| Refresh | Single-flight refresh on 401 | Not yet (Day 3): after 5 minutes, the next page goes to `/login?expired=1` |
| Pages | One dashboard with a stokvel switcher and a contribution form | `/login`, `/dashboard/member`, `/dashboard/treasurer`, `/stokvels`, `/stokvels/[id]` |
| Configuration | `VITE_API_BASE_URL` (compiled into the browser bundle) | `API_BASE_URL`, `SESSION_SECRET` (server-only) |
| Port | 3000 | 3000 |
| Details | [web/README.md](web/README.md) | [web-next/README.md](web-next/README.md) |

**Both frontends use port 3000 by default, so don't run them at the same time.** `web/` sets `strictPort: true` and won't start if port 3000 is taken.

## Features

**Stokvels and money**
- Users, stokvels, memberships and contribution cycles. Creating a stokvel makes its creator Chairperson. New members join as Member.
- Contributions recorded with an **`Idempotency-Key`**: a retried request records once, and the same key with a different body gets a `409`. Keys are stored in PostgreSQL.
- Payouts in a single database transaction. Amounts sent to frontends are **integer cents**.
- A background job closes cycles whose end date has passed (Johannesburg calendar days), records **arrears** and opens the next cycle. A housekeeping job removes expired refresh-token families and old idempotency records.

**Security**
- JWT access tokens (5 minutes) and rotating refresh tokens in an `HttpOnly`, `Secure`, `SameSite=Strict` cookie. A reused refresh token revokes the whole session.
- **Roles belong to each stokvel** (Member, Treasurer, Chairperson), checked live on every request. A non-member gets `404`, so they can't tell whether a stokvel exists. A separate PlatformAdmin role covers user administration.
- Rate limits: 5 logins per minute per IP, and per-user limits on contributions and payouts.
- CORS restricted to configured origins.

**Frontend-facing contract**
- `GET /api/v1/me` (who is signed in), `GET /api/v1/me/home` (my stokvels, my role, what I've paid and owe), and `GET /api/v1/stokvels/{id}` (member count, my role, current cycle summary).
- Errors are ProblemDetails, each with a `correlationId` the frontends show as a reference.
- An OpenAPI document that both frontends generate their TypeScript types from.

**Operations**
- Health checks (`/health/live`, `/health/ready`), OpenTelemetry traces and metrics (OTLP), and logging that avoids personal data.
- A Dockerfile, Docker Compose (PostgreSQL, a one-shot migration and the API), and GitHub Actions CI.

The full endpoint list is in the [API reference](docs/api-reference.md).

## Technology Stack

| Layer | Technology |
|---|---|
| API | C# / ASP.NET Core 10, FluentValidation 12, JWT bearer authentication, OpenAPI + Scalar |
| Data | EF Core 10, Npgsql, PostgreSQL 17 |
| Observability | OpenTelemetry (OTLP exporter), health checks |
| Backend tests | xUnit, Testcontainers (PostgreSQL) |
| Next.js frontend | Next.js 16.4.1 (App Router, React Compiler), React 19, TypeScript 5, Tailwind CSS 4, jose, openapi-fetch, Vitest |
| React frontend | React 19, Vite 8, TypeScript 5, Tailwind CSS 4, shadcn/ui, TanStack Query, React Hook Form + Zod, openapi-fetch, Vitest + MSW |
| Contracts | openapi-typescript, generated from the API's `/openapi/v1.json` |
| Delivery | Docker, Docker Compose, GitHub Actions |

Exact dependency versions are in the manifests:
- [`API/RondiTrack.csproj`](API/RondiTrack.csproj), [`Infrastructure/Infrastructure.csproj`](Infrastructure/Infrastructure.csproj), [`RondiTrack.Tests/RondiTrack.Tests.csproj`](RondiTrack.Tests/RondiTrack.Tests.csproj)
- [`web/package.json`](web/package.json), and [`web-next/package.json`](web-next/package.json), where every version is pinned exactly. It's also explained in [web-next/README.md](web-next/README.md#dependencies)

## System Architecture

```
            Browser                                   Browser
               │                                         │  only cookie: session (encrypted, HttpOnly)
               ▼                                         ▼
     web/  (React + Vite, :3000)               web-next/  (Next.js server, :3000)
     calls the API from the browser            Server Components call the API with the
     (Bearer token in memory + refresh cookie)  token from the session; browser never does
               │                                         │
               └───────────────┬─────────────────────────┘
                               ▼
              API  (ASP.NET Core, /api/v1)   ── Controllers, validation, auth, services
                               │
              Domain  (entities, rules, repository interfaces)
                               │
              Infrastructure  (EF Core DbContext, repositories, migrations)
                               │
                               ▼
                         PostgreSQL 17
```

- **Backend layers:** `API` depends on `Domain` and `Infrastructure`. `Domain` depends on nothing. Two hosted workers (cycle close, housekeeping) run inside the API process.
- **Authorization:** the API reads the caller's membership of the requested stokvel on every request. Frontends only *display* roles; hiding a link is a convenience, never a security check.
- **`web-next/` boundary:** `src/lib/session.ts` and `src/lib/api.ts` start with `import 'server-only'`, so importing them into a Client Component fails the build.

More detail is in [docs/architecture.md](docs/architecture.md): projects, request flow, dependency-injection lifetimes and both frontends.

## Repository Structure

```
API/                   ASP.NET Core API: controllers, DTOs, auth, services, background jobs
Domain/                Entities, domain exceptions, repository interfaces
Infrastructure/        EF Core DbContext, repositories, migrations
RondiTrack.Tests/      Backend tests (xUnit + Testcontainers)
web/                   React + Vite frontend (Assignments 7.1–7.4)
web-next/              Next.js frontend (Assignment 8.1)
clients/typescript/    Assignment 6.3 TypeScript contract experiment
scripts/               SQL and PowerShell scripts used by earlier assignments' evidence
docs/                  Current technical references
docs/history/          Assignment-by-assignment records and their evidence
Dockerfile, compose.yaml, .github/workflows/ci.yml
```

## Getting Started

### Prerequisites

| Tool | Used for |
|---|---|
| .NET 10 SDK | API and backend tests |
| PostgreSQL 17 | The local development database |
| `dotnet-ef` 10.x (`dotnet tool install --global dotnet-ef`) | Applying migrations |
| Node.js 24 | Both frontends (`web/.nvmrc`) |
| Docker Desktop (Linux containers) | Backend tests (Testcontainers) and Docker Compose |

### Configure the API

The API reads its secrets from **.NET user secrets**, never from files in the repository:

```powershell
# A PostgreSQL database for RondiTrack, e.g. "ronditrack" on localhost:5432
dotnet user-secrets --project API set ConnectionStrings:RondiTrackDb "Host=localhost;Port=5432;Database=ronditrack;Username=<user>;Password=<password>"

# A random key of at least 32 bytes, base64-encoded
dotnet user-secrets --project API set Jwt:SigningKey "<base64 key>"

# Create or update the database schema
dotnet ef database update --project Infrastructure --startup-project API
```

Non-secret settings (JWT issuer, token lifetimes, rate limits, CORS) are in [`API/appsettings.json`](API/appsettings.json) and [`appsettings.Development.json`](API/appsettings.Development.json). The current migration state of my development database is recorded in [docs/development-migration-status.md](docs/development-migration-status.md).

There's **no seed data**. Create an account with `POST /api/v1/auth/register` (Scalar works for this), then a stokvel with `POST /api/v1/stokvels`. The account that creates it becomes its Chairperson.

## Running the Application

### Ports

| Port | What |
|---|---|
| **5101** | Development API, `http` launch profile (`https` profile: 7125) |
| 8081 | Development API when started with `--urls http://localhost:8081`, which `web/.env.example` expects |
| 8080 | Docker Compose API (Production mode) |
| **3000** | `web/` **or** `web-next/`, one at a time |
| 5432 | Local PostgreSQL. The Compose database isn't published to the host |

### 1. The API (Development)

```powershell
dotnet run --project API --launch-profile http
```

It listens on `http://localhost:5101`. In Development you can browse the API in Scalar at `/scalar/v1` and fetch the OpenAPI document at `/openapi/v1.json`. The health endpoints are `/health/live` and `/health/ready`. The cycle-close and housekeeping workers start with the API.

### 2a. The Next.js frontend (Assignment 8.1)

```powershell
cd web-next
npm ci
Copy-Item .env.example .env.local
# In .env.local: API_BASE_URL=http://localhost:5101 and a SESSION_SECRET from
#   node -e "console.log(require('crypto').randomBytes(32).toString('base64'))"
npm run dev
```

Open `http://localhost:3000`. More in [web-next/README.md](web-next/README.md#running-it).

### 2b. The React/Vite frontend (Assignments 7.1–7.4)

Stop `web-next` first: both use port 3000.

```powershell
cd web
npm ci
Copy-Item .env.example .env.local   # set VITE_API_BASE_URL, e.g. http://localhost:5101
npm run dev
```

Open `http://localhost:3000`. More in [web/README.md](web/README.md#run-it).

### Docker Compose (API only)

```powershell
Copy-Item .env.example .env          # set POSTGRES_PASSWORD and JWT_SIGNING_KEY; never commit .env
docker compose up --build -d api
curl.exe -i http://localhost:8080/health/ready
```

Compose runs PostgreSQL, applies the migrations once, then starts the API on port 8080 in **Production**. That means no Scalar and no OpenAPI document. Its database is a separate Docker volume, not your local development database. `docker compose down -v` **deletes that volume**. The full Compose guide, with all its settings, is in the [Assignment 6.4 record](docs/history/assignment-6.4.md#build-and-run-with-docker).

## Testing and Verification

| Project | Command | Latest result |
|---|---|---|
| Backend | `dotnet test RondiTrack.Tests` (needs Docker running) | **145 passed**, 0 failed, 27 Oct 2026 |
| `web-next/` | `npm run lint`, `npm test`, `npm run build`, `npm audit` | Lint clean, **58 passed**, build succeeded, 0 vulnerabilities, 27 Oct 2026 |
| `web/` | `npm run lint`, `npm test -- --run` | Lint clean, **63 passed**, 27 Oct 2026. Build last recorded in 7.4 |

**What CI checks:** on pushes and pull requests to `main`, [GitHub Actions](.github/workflows/ci.yml) restores, builds and tests the **backend** in Release and builds the Docker image. It doesn't run either frontend's checks; those are run locally, as above.

**Signed-in check for `web-next/` (27 Oct 2026):** a local script signed in three demonstration accounts, built real session cookies and fetched every page from the production build against the running API. 98 checks passed, 0 failed. The checks covered roles, the treasurer filter, not-found pages, and no tokens or API port in the HTML. The script and the demonstration accounts are local, not part of the repository; the result is recorded in the Assignment 8.1 pull request.

Older test counts (101, 122, 130, 132, 137) belong to earlier assignments and are kept in their [history records](docs/history/README.md).

## Recent Development

### Assignment 8.1: RondiTrack routes (Next.js)
- A new [`web-next/`](web-next/README.md) app on **Next.js 16.4.1**, with four signed-in routes rendered by Server Components.
- Sessions are an encrypted `HttpOnly` cookie (jose `dir` + `A256GCM`). Sign-in and sign-out are Server Actions.
- **The treasurer dashboard is for everyone.** It shows only the stokvels where *this* person is Treasurer, with a helpful empty state otherwise. It filters each stokvel's role as the API reports it; no role is kept in the session.
- API fixes found while checking the contract:
  - `GET /stokvels/{id}` always reported `memberCount: 0`. It now returns the correct count, the caller's role and a current-cycle summary.
  - `GET /me` is new.
  - The login response type is now declared, so generated clients get the right type.
- `server-only` turns a leak of session code into the browser into a build error.

### Assignment 7.4: Record a contribution
Members, and Treasurers or Chairpersons on a member's behalf, record a payment from `web/`. Retries keep one `Idempotency-Key`. The API now takes `amountInCents` and refuses contributions to closed cycles. [Record →](docs/history/assignment-7.4.md)

### Assignment 7.3: Live RondiTrack data
`web/` switched from sample data to live, signed-in API data, with single-flight token refresh, stokvel switching that never mixes data, and paginated contributions. [Record →](docs/history/assignment-7.3.md)

Earlier assignments are listed in the [assignment history](docs/history/README.md).

## Known Limitations

- **Login rate limit and `web-next/`.** The limit is 5 per minute **per IP**. Through `web-next/`, every sign-in comes from the Next.js server's IP, so all users share those 5 attempts.
- **No token refresh in `web-next/` yet.** The access token lasts 5 minutes; after that the next page asks the person to sign in again. Planned for the next stage (Day 3).
- **`nextPayoutDate` is always `null`.** No payout-scheduling rule exists, so the frontends show "No payout scheduled" rather than invent a date.
- **`memberCount` in two older responses.** The admin-only `GET /api/v1/stokvels` and the `POST /api/v1/stokvels` response still report `0`. Only `GET /stokvels/{id}` was fixed.
- **Frontend checks aren't in CI.** Lint, tests and builds for `web/` and `web-next/` are run locally only.
- **`clients/typescript/`** has drifted from the API and wasn't regenerated (noted in [7.4](docs/history/assignment-7.4.md#known-gaps)).
- **No deployment.** Everything here runs locally (directly or with Docker Compose). HTTPS, managed secrets, central monitoring and load testing aren't set up. Some Assignment 6.4 measurements (image size, rebuild timings) were never collected; see the [6.4 record](docs/history/assignment-6.4.md#assignment-64-evidence-still-to-collect).
- **`web/`'s open manual checks** (two-user shared device, live 429) are listed in [web/README.md](web/README.md#known-gaps).

## Documentation and Assignment History

| Document | What's in it |
|---|---|
| [docs/README.md](docs/README.md) | Index of all documentation |
| [docs/api-reference.md](docs/api-reference.md) | Endpoints, authentication, cookies, permissions |
| [docs/architecture.md](docs/architecture.md) | Projects, request flow, DI lifetimes, the two frontends |
| [docs/development-migration-status.md](docs/development-migration-status.md) | Migration state of the development database |
| [web-next/README.md](web-next/README.md) | Assignment 8.1 in full: API check, routes, treasurer dashboard, client boundary, server-only proof |
| [web/README.md](web/README.md) | The React/Vite frontend |
| [docs/history/README.md](docs/history/README.md) | **Assignment history**: every assignment from 4.4 to 8.1 in order. Each record covers requirements, implementation, how to run and test it (as submitted and today), challenges and evidence. The backend of 4.4, 5.4 and 6.1–6.4 was re-run from its original commit on 27 Oct 2026 |
