# RondiTrack web (React + Vite)

The original RondiTrack frontend: a React 19, TypeScript and Vite single-page app. It was built across Assignments 7.1 to 7.4 and shows **live, signed-in data from the RondiTrack API**. The browser calls the API directly.

A second frontend, built on Next.js for Assignment 8.1, lives in [`../web-next/`](../web-next/README.md). In that one the browser never calls the API. The [root README](../README.md#current-implementation) compares the two.

> This README describes `web/` as it is now, after Assignment 7.4. The Assignment 7.2 version, from when the dashboard used sample data, is preserved in [docs/history/web-readme-assignment-7.2.md](../docs/history/web-readme-assignment-7.2.md).

## What it does

| Area | Behaviour | Since |
|---|---|---|
| Sign in and out | JWT access token kept **in memory**. The refresh token is the API's `HttpOnly` cookie, which the browser sends and JavaScript can't read. | 7.3 |
| Token refresh | On a 401, concurrent requests share **one** refresh call (single-flight), then retry | 7.3 |
| Live data | `/me/home` for the person's stokvels, plus cycles and paginated contributions, through a typed `openapi-fetch` client and TanStack Query | 7.3 |
| Switching stokvels | Each stokvel's data is cached separately, so one stokvel's contributions are never shown under another | 7.3 |
| Recording a contribution | A form (React Hook Form + Zod) for an Open cycle. Rands are typed in and converted to whole cents. Every retry of one payment sends the same `Idempotency-Key`. A Treasurer or Chairperson can pick another member; the API enforces who may record for whom. | 7.4 |
| Errors | The 7.3 messages, including 429 with `Retry-After`, plus the API's `correlationId` as a reference | 7.3 |
| Presentation | Responsive `StokvelCard` and `ContributionCard`, cycles grouped by status, status and arrears badges, Johannesburg due-date countdown, Light/Dark/System theme | 7.1–7.2 |

## Run it

You need Node.js 24 (see [`.nvmrc`](.nvmrc)) and the RondiTrack API running in **Development**. The [root README](../README.md#running-the-application) explains how to start it.

```powershell
cd web
npm ci
Copy-Item .env.example .env.local   # then set VITE_API_BASE_URL (see below)
npm run dev                          # http://localhost:3000
```

| Setting | Value |
|---|---|
| `VITE_API_BASE_URL` | The API's address. `.env.example` uses `http://localhost:8081`. Use `http://localhost:5101` if you started the API with its default `http` launch profile. `.env.local` is git-ignored. |
| Port | **3000**, with `strictPort: true` in `vite.config.ts`, so Vite refuses to start if port 3000 is taken. `web-next/` also uses port 3000 by default, so **run only one frontend at a time**. |
| CORS | The Development API allows `http://localhost:3000` (`API/appsettings.Development.json`). |

`VITE_` variables are compiled into the browser bundle, so only the API's address goes here, never a secret.

## Scripts

| Command | What it does |
|---|---|
| `npm run dev` | Vite dev server on port 3000 |
| `npm run build` | `tsc -b` and a production build into `dist/` |
| `npm run typecheck` | TypeScript only |
| `npm run lint` | ESLint |
| `npm test -- --run` | Vitest, once (`npm test` alone starts watch mode) |
| `npm run api:types` | Regenerates `src/api/schema.d.ts` from `http://localhost:8081/openapi/v1.json` |
| `npm run generate:api` | Same, from port **8080**. Use it only if a current *Development* API is on 8080; the Docker Compose API runs in Production and doesn't serve the OpenAPI document. |

## Where things are

| Path | Contents |
|---|---|
| `src/auth/session.ts` | Access token in memory, single-flight refresh, sign in and out |
| `src/api/client.ts`, `queries.ts`, `schema.d.ts` | Typed client, TanStack Query hooks, generated contract |
| `src/contributions/useRecordContribution.ts` | Idempotency-key rules for recording a contribution |
| `src/components/` | `StokvelCard`, `ContributionCard`, `ContributionForm`, `ContributionList`, `StatusBadge`, `DueCountdown`, `ErrorView`, shadcn `ui/` |
| `src/lib/` | `money.ts` (cents ↔ rands), `dates.ts` (Johannesburg dates), `errorMessages.ts`, `groupContributions.ts` |

## Verification

| Check | Result | When |
|---|---|---|
| `npm test -- --run` | **63 passed** (9 files) | Re-run 27 October 2026 |
| `npm run lint` | Clean | Re-run 27 October 2026 |
| `npm run build` | Succeeded | Last recorded in [Assignment 7.4](../docs/history/assignment-7.4.md#results) |

CI (`.github/workflows/ci.yml`) builds and tests only the backend and Docker image. These frontend checks are run locally.

Red/green evidence files, each the output of a deliberately broken run and of the restored code:

| Proof | Failing | Passing | Explained in |
|---|---|---|---|
| Single-flight refresh | [single-flight-failing.txt](single-flight-failing.txt) | [single-flight-passing.txt](single-flight-passing.txt) | [Assignment 7.3](../docs/history/assignment-7.3.md#single-flight-proof) |
| Stokvel switch isolation | [stokvel-switch-failing.txt](stokvel-switch-failing.txt) | [stokvel-switch-passing.txt](stokvel-switch-passing.txt) | [Assignment 7.3](../docs/history/assignment-7.3.md#stokvel-switch-proof) |
| Idempotency key reuse | [idempotency-failing.txt](idempotency-failing.txt) | [idempotency-passing.txt](idempotency-passing.txt) | [Assignment 7.4](../docs/history/assignment-7.4.md#idempotency-proof) |

## Known gaps

These are carried over from the 7.3 and 7.4 records and are still open:

- A two-user shared-device browser test and a live HTTP 429 from the running API were not demonstrated manually (7.3).
- Recording a contribution was exercised by automated tests (MSW), not a recorded manual run against the live API (7.4).
- When a Treasurer picks another member, the amount starts empty, because `/me/home` only reports the signed-in person's own amounts (7.4).
- There's no "Already recorded" message, because the API doesn't send a replay header (7.4).
- "Record for myself" reads the user id from the access token's `sub` claim (7.4). `GET /api/v1/me` now exists (8.1) but `web/` doesn't use it yet.
