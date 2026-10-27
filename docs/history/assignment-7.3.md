# Assignment 7.3 — Live RondiTrack Data

| | |
|---|---|
| Official title | "Live RondiTrack Data", as in the README heading. The brief isn't in the repository. |
| Date | 22 October 2026 |
| Final commit on `main` | [`2a4db93`](https://github.com/mdvar2/RondiTrack/commit/2a4db93) (code: [`b849494`](https://github.com/mdvar2/RondiTrack/commit/b849494); docs: [`9b9c130`](https://github.com/mdvar2/RondiTrack/commit/9b9c130)) |
| Branch / PR | `assignment-7.3`, [PR #4](https://github.com/mdvar2/RondiTrack/pull/4) (merged 26 Oct 2026) |
| README as submitted | [README at `2a4db93`](https://github.com/mdvar2/RondiTrack/blob/2a4db93/README.md) |
| Re-tested | **No** for this commit. `web/` today is the 7.4 code (see [7.4](assignment-7.4.md)) |

## 1. Purpose and learning objectives
To replace the sample data with **live, signed-in API data**, safely: tokens handled correctly, concurrent refreshes coordinated, and one stokvel's data never shown under another. The brief's learning objectives aren't recorded.

## 2. Original requirements
The brief isn't in the repository. The record below and the [working notes](assignment-7.3-working-notes.md) name these deliverables:
- a **single-flight proof** and a **stokvel-switch proof**, each failing then passing
- a **shared-phone check** (two users on one device)
- a real **HTTP 429**
- **API contract notes**

The notes also record that "the assignment" named a `GET /me` route and a stokvel-level contribution route that the API didn't have.

## 3. What I implemented
See [Delivered functionality](#delivered-functionality) below:
- Sign in and out, with the **access token in memory** and the refresh token in the API's HttpOnly cookie.
- **Single-flight refresh.**
- A typed `openapi-fetch` client with **TanStack Query**.
- Stokvel switching with query isolation, and paginated contributions.
- Loading, empty and error states, including 429.
- **API:** the contribution-pagination OpenAPI annotation was corrected to the real `PagedResult` shape.

## 4. Technologies, tools and dependencies
New in `web/package.json` at `2a4db93`:
- @tanstack/react-query (+ devtools) 5.104.5
- openapi-fetch 0.17.0
- react-hook-form 7.90.0, @hookform/resolvers 5.9.2, zod 4.6.7 (used by the 7.4 form)

## 5. Prerequisites and installation
Node.js 24, and the API running in **Development** with a database and secrets (see [6.1](assignment-6.1.md#6-environment-configuration)).

## 6. Environment configuration
`web/.env.example` → `VITE_API_BASE_URL=http://localhost:8081`. The Development API's CORS allows `http://localhost:3000`.

## 7. How to run or reproduce
**As submitted:** [Run and verify](#run-and-verify) below. Run the API on 8081 (`dotnet run --project API --urls http://localhost:8081`), then in `web/`: `npm.cmd ci` and `npm.cmd run dev`. **Not re-tested** for this commit.

**In the current codebase:** everything here is in `web/` today. See [web/README.md](../../web/README.md#run-it).

## 8. Important files and components
- `web/src/auth/session.ts` (+ `session.test.ts`)
- `web/src/api/client.ts`, `queries.ts` (+ `queries.pagination.test.tsx`)
- `web/src/lib/errorMessages.ts` (+ test), `web/src/components/ErrorView.tsx`, `web/.env.example`

## 9. How it works
The first `401` starts one refresh request. Any other request that gets a `401` meanwhile waits for that same refresh instead of starting its own; otherwise the API's reuse detection would revoke the session. Queries are keyed by stokvel, so switching never shows the previous stokvel's rows while the new ones load.

## 10. Testing and recorded results
| Check | Recorded result |
|---|---|
| Backend | 132 passed |
| `web/` | **35 passed** (7 files). Typecheck, ESLint and build passed |
| Red/green proofs | Single-flight and stokvel-switch: each failed when its guard was removed and passed when restored ([web/README.md](../../web/README.md#verification)) |

## 11. Challenges and solutions
- The OpenAPI document described contribution pagination as an array; the annotation was fixed in the API, not worked around in the frontend.
- **Not completed**, as recorded in [Known gaps](#known-gaps):
  - a shared-phone test with two users (no second account with known credentials at the time)
  - a manual demonstration of unauthorized access
  - a live 429

## 12. Learning outcomes
Not recorded as such.

## 13. Evidence, commits and pull requests
- [PR #4](https://github.com/mdvar2/RondiTrack/pull/4)
- [Working notes](assignment-7.3-working-notes.md)
- The red/green output files in [`web/`](../../web/README.md#verification)

## 14. Relationship to other assignments
- **Before:** [6.1](assignment-6.1.md)'s refresh-cookie rotation (the reason single-flight is needed) and [6.3](assignment-6.3.md)'s CORS.
- **Next:** [7.4](assignment-7.4.md) added the first write: recording a contribution.
- **Later:**
  - [8.1](assignment-8.1.md) moved token handling to the server.
  - 8.1 also added the `GET /me` endpoint this assignment noted was missing.

## 15. Original record (as written at the time)

> **Historical record.** This was the Assignment 7.3 section of the root README. It was moved here unchanged (apart from Markdown formatting) on 27 October 2026. Commands, ports and test counts are as recorded at the time and may be out of date. For the current project, see the [root README](../../README.md).

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
