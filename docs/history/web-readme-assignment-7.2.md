# Original frontend README, as of Assignment 7.2

> **Historical record.** This is `web/README.md` as it stood after Assignment 7.2 (sample data, before live API integration), preserved here on 27 October 2026 when `web/README.md` was updated for Assignments 7.3 and 7.4. For the current frontend, see [`web/README.md`](../../web/README.md).

## Original title: RondiTrack — Assignment 7.2 frontend

RondiTrack's responsive React/TypeScript stokvel dashboard uses Tailwind CSS v4 and shadcn/ui. The repository's `web/` folder is the frontend; the ASP.NET Core API and PostgreSQL infrastructure live in the repository root.

## Start and verify

```powershell
cd web
npm.cmd ci
npm.cmd run lint
npm.cmd run typecheck
npm.cmd run build
npx.cmd vitest run
npm.cmd audit --omit=dev
npm.cmd run dev
```

The development server uses `http://localhost:3000/`. The generated API contract is `src/api/schema.d.ts`; regenerate it with `npm.cmd run generate:api` only when the updated backend's OpenAPI document is accessible at `http://localhost:8080/openapi/v1.json`. The production Docker API can return 401 for this development-only document.

**Data provenance:** The dashboard currently renders typed, illustrative fixtures in `src/data/sample.ts` and `src/data/sampleCycles.ts`, not live authenticated API responses. The cycle totals are supplied by the cycle response objects and are never recomputed from contribution rows in the browser. For a complete API demonstration, capture authorized, anonymized data in Scalar and replace the fixtures; do not claim these fixtures are database seed records.

## Assignment 7.2 design decisions

### Grouping and expand/collapse state

`groupContributionsByCycle` is a pure function in `src/lib/`. It sorts cycles newest-first and contribution entries newest-first, retains empty cycles and puts unmatched contributions in a final `Unknown cycle` group. The group list derives from props at render time, not from React state. `ContributionList` owns the expanded-state dictionary because both individual group buttons and the list-level Expand all / Collapse all buttons need access to it. Keys are cycle IDs rather than array positions, so adding a new cycle cannot transfer another cycle's expanded state to the wrong group. Open cycles default to expanded; others default to collapsed.

### Johannesburg due dates and timer

The API's `dueDate` is a South African calendar date (`YYYY-MM-DD`), not an instant. `getJohannesburgDeadlineUtc` interprets `2026-10-31` as the exclusive deadline `2026-10-31T22:00:00Z` (the beginning of 1 November SAST), not UTC midnight on 31 October. `getDueCountdown` compares calendar dates in Johannesburg, preventing a premature overdue message on the due date. The effect in `DueCountdown` depends on `[dueDate, status]`: it schedules only for Open cycles, clears the timeout on cleanup and schedules a new timeout after each Johannesburg midnight. A per-second interval would waste battery without changing a day-based display. The user's device clock drives the informational countdown, but the API's server-side cycle-close and arrears decisions are authoritative. South Africa's fixed UTC+02:00 offset makes the next-midnight helper deterministic without DST adjustments.

### Badge meaning, roles and privacy

`StatusBadge` uses status text and an icon, not colour alone. `Paid` denotes a contribution claim; `Confirmed` denotes a treasurer-verified payment; `Paid out` denotes the entire cycle's payout. Unknown statuses are displayed neutrally with their raw label and an icon. The arrears badge accepts integer cents and is absent for zero. Role is not a status: it must be displayed as contextual text such as `You're the Treasurer`. Personal arrears must come from the authenticated caller's API response, never from another member's contribution or a browser-side sum. The present frontend does **not** yet demonstrate authenticated caller arrears because that backend contract has not been verified.

### Theme and storage

Light, Dark and System use CSS theme tokens. The inline `index.html` script applies the stored preference before the first React paint, and the React effect follows operating-system theme changes while the page is open. Storage access is wrapped in `try/catch`. A theme preference is device-wide and not personal; future per-user pinned stokvels must be scoped to the authenticated account and cleared on logout.

### Dependency audit

The initial production audit reported seven high-severity transitive findings associated with the shadcn CLI when `shadcn` was installed as a production dependency. Moving `shadcn` to pinned `devDependencies` resulted in `npm audit --omit=dev` reporting **0 production vulnerabilities** on the user's last run. Development-tool findings are distinct from runtime production dependency exposure.

## Evidence still required before full sign-off

Do not present unmeasured results as complete:

1. **StrictMode evidence:** Temporarily add `console.count` to the timer scheduling and cleanup paths; capture console output with cleanup enabled and temporarily disabled, compare the duplicate StrictMode setup/cleanup behaviour, then remove the logging. This evidence has not been captured.
2. **Contrast:** In DevTools measure foreground/background ratios for every badge (Open, Closed, Paid out, Pending, Paid, Confirmed, arrears, unknown) in Light and Dark; each must be at least 4.5:1. Add the measured table here. Measurements have not been captured.
3. **360px screenshots:** In DevTools set width 360px, expand every group, capture Light and Dark screenshots, verify no horizontal scrolling, names wrap, amounts do not break, buttons are at least 24×24px and grayscale meaning survives. Screenshots have not been captured or embedded.
4. **API handoff:** Existing backend cycle DTO/repository changes expose status, due date, collected and due totals and were previously reported to pass backend tests. Dedicated new tests and actual seeded Open/Closed/PaidOut and caller-arrears API evidence have not been supplied. Do not infer caller arrears from the cycle outstanding amount.
5. **Real API data:** Replace illustrative fixtures with captured anonymized authenticated API responses or implement authenticated fetching, and confirm the caller's own arrears are supplied in cents by `/api/v1/me/home`.

## Current scope

This package strengthens frontend boundary tests, theme startup and status semantics, but does not itself establish production authentication, seed the PostgreSQL database, or complete the API arrears contract. Run the full verification suite and inspect the remaining evidence before submitting as fully compliant.
