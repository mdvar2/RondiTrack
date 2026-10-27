# Assignment 7.1 — StokvelCard & ContributionCard

| | |
|---|---|
| Official title | "StokvelCard & ContributionCard", as in the README heading. The brief isn't in the repository. |
| Date | 20 October 2026 |
| Final commit on `main` | [`a5cf3c6`](https://github.com/mdvar2/RondiTrack/commit/a5cf3c6) "Complete Assignment 7.1 StokvelCard and ContributionCard" |
| Branch / PR | Committed directly to `main`; no PR |
| README as submitted | [README at `a5cf3c6`](https://github.com/mdvar2/RondiTrack/blob/a5cf3c6/README.md) |
| Re-tested | **No.** The `web/` app at `a5cf3c6` wasn't re-installed or re-run |

## 1. Purpose and learning objectives
To start RondiTrack's frontend: two typed React components showing a member's stokvels and contributions, with types generated from the real API contract. The brief's learning objectives aren't recorded.

## 2. Original requirements
The brief isn't in the repository. The record below refers to "the assignment rubric" for sample-data provenance and UI edge cases. The sample cases covered:
- Treasurer and Member roles
- an "All paid up" (zero owed) state
- a 10-cent amount
- the Pending, Paid and Confirmed statuses
- Johannesburg-local times

## 3. What I implemented
See [Frontend components and sample cases](#frontend-components-and-sample-cases) below:
- **Frontend:** the new `web/` app (React + Vite + TypeScript) with `StokvelCard` and `ContributionCard`, typed **sample data** (no API calls yet), cents-to-rand formatting and Johannesburg-local time formatting.
- **API:** contributions gained a `status` (migration `AddContributionStatus`), and home amounts became integer cents.

## 4. Technologies, tools and dependencies
Verified against `web/package.json` at `a5cf3c6`:

| Package | Version |
|---|---|
| react, react-dom | 19.3.1 |
| vite | 8.3.4 |
| @vitejs/plugin-react | 6.1.2 |
| typescript | 5.9.3 |
| vitest | 4.1.11 |
| eslint / typescript-eslint | 10.12.0 / 8.71.2 |
| openapi-typescript | 7.13.0 |

## 5. Prerequisites and installation
Node.js 24 (`web/.nvmrc`). The API is only needed to regenerate the types.

## 6. Environment configuration
None for the sample-data dashboard.

## 7. How to run or reproduce
**As submitted:** [Run the frontend](#run-the-frontend) below. That's `npm.cmd ci` and `npm.cmd run dev` in `web/`; types were regenerated against a Development API on port 8081, because 8080 was occupied.

To try that version without disturbing your work, use `git worktree add ..\RondiTrack-7.1 a5cf3c6`. **Not re-tested.**

**In the current codebase:** both components are still in `web/src/components/`, now fed by live data (from [7.3](assignment-7.3.md)). See [web/README.md](../../web/README.md).

## 8. Important files and components
- `web/src/components/StokvelCard.tsx`, `ContributionCard.tsx`
- `web/src/data/sample.ts`
- `web/src/api/schema.d.ts` (generated), `web/src/api/types.ts`
- `web/src/lib/` formatting
- `Infrastructure/Migrations/20261020004041_AddContributionStatus.cs`

## 9. How it works
The components take typed props that match the generated API types, so a contract change becomes a compile error. Money arrives as whole cents and is only formatted for display. Times are UTC and shown in `Africa/Johannesburg`.

## 10. Testing and recorded results
| Check | Recorded result (20 Oct 2026) |
|---|---|
| `web/`: typecheck, ESLint, build | Passed (24 modules) |
| `web/`: Vitest | **14 passed** (2 files) |
| Backend | 130 passed, from the previous run |

## 11. Challenges and solutions
**Port 8080 was taken.** Docker/WSL forwarding occupied 8080 and served an *older* OpenAPI document. Generating types from it would have been wrong, so the up-to-date Development API was run on 8081 instead.

## 12. Learning outcomes
Not recorded as such.

## 13. Evidence, commits and pull requests
- [README at `a5cf3c6`](https://github.com/mdvar2/RondiTrack/blob/a5cf3c6/README.md)
- Commits: [`bbefdc7`](https://github.com/mdvar2/RondiTrack/commit/bbefdc7), [`c23be07`](https://github.com/mdvar2/RondiTrack/commit/c23be07), [`a5cf3c6`](https://github.com/mdvar2/RondiTrack/commit/a5cf3c6)

## 14. Relationship to other assignments
- **Before:** [6.3](assignment-6.3.md)'s `/me/home` contract and TypeScript generation.
- **Next:** [7.2](assignment-7.2.md) styled and grouped these components.
- **Later:**
  - [7.3](assignment-7.3.md) replaced the sample data with live data.
  - [8.1](assignment-8.1.md) copied `StokvelCard` into the Next.js app as a Server Component.

## 15. Original record (as written at the time)

> **Historical record.** This was the Assignment 7.1 section of the root README. The frontend described here used sample data; live data arrived in 7.3. It was moved here unchanged (apart from Markdown formatting) on 27 October 2026. Commands, ports and test counts are as recorded at the time and may be out of date. For the current project, see the [root README](../../README.md).

Assignment 7.1 adds a responsive React 19, TypeScript and Vite frontend in [`web/`](../../web). The dashboard uses typed demonstration data, **not live authenticated API fetching**. See the [frontend README](../../web/README.md) for component-level setup and limitations.

### Backend contract

- The Development API exposes its OpenAPI document at `/openapi/v1.json`.

- `GET /api/v1/me/home` provides member-specific stokvel details, including role, current cycle, amount paid, amount owed and a next payout date when available.

- Contribution response types now include a `status` field. The generated frontend contract in `web/src/api/schema.d.ts` requires this field.

- Home and contribution amounts are represented as **integer cents** in the frontend-facing API contract. Display formatting converts cents to ZAR.

- A missing payout schedule remains `null`; the UI must not imply that a payout date was calculated by the backend.

### Frontend components and sample cases

- **`StokvelCard`** presents the stokvel name, member role, current cycle details, amount paid and owed, and payout scheduling information. The interface distinguishes amounts owed from an **All paid up** state. A Treasurer-only tools area is a nonfunctional UI placeholder, not an active privileged operation.

- **`ContributionCard`** presents the amount, the contribution's `status` (Pending, Paid or Confirmed in the demonstration cases), cycle name, Johannesburg-local recorded timestamp and contributor name when available. Contributions are keyed by their IDs.

- **Formatting:** integer cents are displayed as ZAR amounts; UTC timestamps are displayed in `Africa/Johannesburg`. For example, `2026-10-31T22:30:00Z` is 01 November 2026 at 00:30 SAST.

- **Sample coverage:** the frontend demonstration includes Treasurer and Member roles, a zero-owed state, a small 10-cent amount and contribution status variants. The example records are **illustrative fixtures**, not verified captures of signed-in API responses. This provenance distinction remains a verification item against the assignment rubric.

- **Types:** `web/src/api/schema.d.ts` is generated from the backend OpenAPI document; `web/src/api/types.ts` provides aliases for component use. No live frontend API calls are implemented for this assignment.

### Run the frontend

Install Node.js 24 (see `web/.nvmrc`), then run from the repository root:

```powershell
cd web
npm.cmd ci
npm.cmd run dev
```

Open the local URL printed by Vite. The sample-data dashboard does not require the API to be running.

To **regenerate API types**, start the current Development API on a free port, for example from the repository root:

```powershell
dotnet run --project API --urls http://localhost:8081
```

In a second PowerShell terminal:

```powershell
cd web
npx.cmd openapi-typescript http://localhost:8081/openapi/v1.json -o src/api/schema.d.ts
npm.cmd run typecheck
```

**Port note:** Docker Compose uses host port 8080 for the containerized API. During the Assignment 7.1 verification, Docker Desktop/WSL occupied that port and served an older OpenAPI contract, so the updated development API was run on **8081**. The `npm.cmd run generate:api` script targets 8080; only use it when that port is serving the intended, up-to-date Development API.

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
| Vitest | **14 passed**, across 2 test files |
| Backend automated tests | **130 passed** in the previously reported local run |

These are local verification results, **not** a claim that the latest changes have been committed, pushed or passed CI. The earlier commit history and Assignment 6.4 CI evidence above remain historical evidence for those earlier versions.

### Limitations and handoff

The dashboard does not yet provide login, authenticated API requests, payment submission or working Treasurer controls. The demonstration statuses and records should not be mistaken for independently captured production or signed-in API responses. The final rubric check should confirm the required sample-data provenance and UI edge cases before declaring Assignment 7.1 fully complete. Backend authorization must remain enforced by the API; hiding a frontend control is not authorization.
