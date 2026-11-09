# Assignment 9.2: Tests, performance and accessibility

Tests for Parts 1–4 and 6, the Vitest and Playwright setup, two new CI jobs, and the accessibility, performance and metadata fixes those tests and measurements led to. All of it builds on the 9.1 branch. The full write-up is in `web-next/README.md`, under "Assignment 9.2".

## Why each non-obvious change was made

**Rules pulled out of components so they could be tested**
- `lib/contribution-feedback.ts` holds `amountError` and `feedbackFor`. Amount validation and the API-answer → message mapping used to live inside the Server Action and the form.
- `lib/cycles.ts` holds `pickCurrentCycle`. The page used `cycles.at(-1)`, but `GET /cycles` is newest first, so the page used the **oldest** cycle.
- `lib/invite-metadata.ts` takes only the invite's status, so it cannot leak a name.
- `lib/dates.ts` gains `johannesburgPeriod` and `johannesburgDayRangeUtc`. The activity filters used UTC day boundaries.

**The contribution form**
- It takes **rands** (`R 1 500,50`), not cents. The old action refused anything that wasn't whole rands.
- Its only edge is the Server Action, passed in as a prop.
- The Idempotency-Key is made **in the form**: on the first submit, kept for retries, replaced only after a success. The old key came from `useId()` and never changed.
- API 400s map to fields (`amountInCents` → Amount, `userId` → Member). 409 and 422 are shown at the top with their reference.
- A second click while pending sends nothing. The button uses `aria-disabled` rather than `disabled`, which drops focus.

**The stokvel page**
- Split into a summary boundary and a nested contribution-list boundary. INP attribution showed a click forced the whole page's content to hydrate (473 ms in `pointerdown`).
- The list sits at the bottom, so nothing moves when it streams in. Totals use tabular figures.
- Amounts are no longer multiplied by 100: the API already sends cents. The proof link points at the real BFF route; it was a 404.

**The home screen**
- The first page of "Recent activity" is rendered on the server. It was the LCP element, waiting on a client fetch after hydration.
- The heading is in the static shell.
- The sidebar placeholder reserves two lines, which was the 0.018 CLS on phones.

**Metadata and privacy**
- Title template; `robots: noindex` once in `(app)/layout.tsx`.
- `robots.ts` allows everything, so crawlers can see each page's `noindex`.
- `X-Robots-Tag` on `/bff/*` and `/api/*`.
- OG card on `/login`.
- `/join?code=…` signed out is now a 200 page, not a redirect. Its `generateMetadata` uses a new **anonymous** API endpoint, `GET /api/v1/invites/{code}/status`, which returns only `valid` or `expired`, is rate-limited per address, and has its own test.

**Accessibility**
- **Toast region:** two always-present live regions (status and alert). While a toast is visible, a focused control is scrolled clear of it (2.4.11). `scroll-padding` alone failed on a 360 px screen.
- **Close Cycle:** no longer steals focus on page load. The loading state is a `role="status"`.
- **Join:** per-step titles, focused step headings, and Back keeps the code (3.3.7).
- **Sign in:** the email field is `autocomplete="username"` (3.3.8).
- **Labels and targets:** labels on the activity filters, names for each proof control, larger targets, reduced motion.

**API:** contributions are capped at R1 000 000 (`RecordContributionRequestValidator`). The web form refuses the same number first (`MAX_CONTRIBUTION_CENTS`).

**Compose:**
- The API's host port is `${API_PORT:-8080}`, so a throwaway e2e project can run beside the dev stack.
- **`1ca4dcf`:** the `migrate` service is now built from a `migrate` stage in the Dockerfile, not run from a read-only mount of the checkout. Without this, the first CI run's e2e job couldn't start. See [CI](#ci).

**Dependencies:** all pinned and at least two weeks old:

| Package | Version |
|---|---|
| jsdom | 30.1.3 |
| @testing-library/react | 16.3.3 |
| @testing-library/user-event | 14.6.8 |
| @testing-library/jest-dom | 7.0.1 |
| @testing-library/dom | 10.4.2 |
| msw | 3.0.3 |
| @playwright/test | 1.64.1 |
| @axe-core/playwright | 4.14.0 |
| web-vitals | 6.2.3 |

`@axe-core/playwright` pulled in `playwright-core@1.65.0` (3 days old) as a peer, so `overrides` pins it to 1.64.1.

## Commands and results (2026-11-08)

| Command | Result |
|---|---|
| `npm run lint` | clean |
| `npm run typecheck` (`next typegen && tsc --noEmit`) | clean |
| `npm run build` | clean |
| `npm audit --omit=dev` | found 0 vulnerabilities |
| `npm audit` | found 0 vulnerabilities |
| `npm run test:utc` / `npm run test:jhb` | 15 files, 170 tests passed in each |
| `dotnet test RondiTrack.Tests -c Release` | Passed: 159, Failed: 0 |
| `CI=1 npx playwright test` ×3 | 25 passed, 25 passed, 25 passed |
| Compose `/health/live` and `/health/ready` (dev :8080, e2e :5147) | 200 |

### Ten Vitest runs in a row (alternating time zones)

```
===== Vitest run 1 of 10 (TZ=UTC)
exit=0
 Test Files  15 passed (15)
      Tests  170 passed (170)
===== Vitest run 2 of 10 (TZ=Africa/Johannesburg)
exit=0
 Test Files  15 passed (15)
      Tests  170 passed (170)
===== Vitest run 3 of 10 (TZ=UTC)
exit=0
 Test Files  15 passed (15)
      Tests  170 passed (170)
===== Vitest run 4 of 10 (TZ=Africa/Johannesburg)
exit=0
 Test Files  15 passed (15)
      Tests  170 passed (170)
===== Vitest run 5 of 10 (TZ=UTC)
exit=0
 Test Files  15 passed (15)
      Tests  170 passed (170)
===== Vitest run 6 of 10 (TZ=Africa/Johannesburg)
exit=0
 Test Files  15 passed (15)
      Tests  170 passed (170)
===== Vitest run 7 of 10 (TZ=UTC)
exit=0
 Test Files  15 passed (15)
      Tests  170 passed (170)
===== Vitest run 8 of 10 (TZ=Africa/Johannesburg)
exit=0
 Test Files  15 passed (15)
      Tests  170 passed (170)
===== Vitest run 9 of 10 (TZ=UTC)
exit=0
 Test Files  15 passed (15)
      Tests  170 passed (170)
===== Vitest run 10 of 10 (TZ=Africa/Johannesburg)
exit=0
 Test Files  15 passed (15)
      Tests  170 passed (170)
```

### Three Playwright runs in a row (production build, fresh Compose database, no 429)

```
===== Playwright run 1 of 3 (18:19:55Z)
  25 passed (37.9s)
===== Playwright run 2 of 3 (18:20:36Z)
  25 passed (36.2s)
===== Playwright run 3 of 3 (18:21:17Z)
  25 passed (36.9s)
```

Run 1, test by test:

```
ok  1 [setup] › e2e\auth.setup.ts › sign in as Thandi Dlamini
ok  2 [setup] › e2e\auth.setup.ts › sign in as Sipho Nkosi
ok  3 [chromium] › accessibility.spec.ts › light mode › signed out › sign in, with and without its error
ok  4 [chromium] › accessibility.spec.ts › light mode › signed in › the home screen
ok  5 [chromium] › accessibility.spec.ts › light mode › signed in › a stokvel, the contribution form showing its errors, and the open Close Cycle dialog
ok  6 [chromium] › accessibility.spec.ts › light mode › signed in › the join flow, step by step
ok  7 [chromium] › accessibility.spec.ts › dark mode › signed out › sign in, with and without its error
ok  8 [chromium] › accessibility.spec.ts › dark mode › signed in › the home screen
ok  9 [chromium] › accessibility.spec.ts › dark mode › signed in › a stokvel, the contribution form showing its errors, and the open Close Cycle dialog
ok 10 [chromium] › accessibility.spec.ts › dark mode › signed in › the join flow, step by step
ok 11 [chromium] › journey.spec.ts › record R 150,50 and the stokvel total goes up by exactly that, without a reload
ok 12 [chromium] › keyboard.spec.ts › signed out › 3.3.8: sign-in takes paste and a password manager, with no puzzle
ok 13 [chromium] › keyboard.spec.ts › signed in › join a stokvel with the keyboard only: headings take focus, and Back never asks for the code again
ok 14 [chromium] › keyboard.spec.ts › signed in › record a contribution with the keyboard only; a refusal keeps what was typed and focuses the field
ok 15 [chromium] › keyboard.spec.ts › signed in › the Close Cycle dialog: focus starts on Cancel, Escape closes it and focus returns
ok 16 [chromium] › keyboard.spec.ts › signed in › on a phone › 2.4.11: a toast never hides the control that has focus
ok 17 [chromium] › roles.spec.ts › the UI isn't the rule: Sipho, a Member here, sends the treasurer's close and the API says 403
ok 18 [chromium] › roles.spec.ts › in his own stokvel the same person has the treasurer actions
ok 19 [chromium] › seo.spec.ts › signed in › every signed-in page is noindex
ok 20 [chromium] › seo.spec.ts › signed out › a real invite, previewed signed out, names no stokvel and no amount
ok 21 [chromium] › seo.spec.ts › signed out › an unknown invite says so, still without indexing
ok 22 [chromium] › seo.spec.ts › signed out › the sign-in page may be indexed and previewed: what RondiTrack is
ok 23 [chromium] › seo.spec.ts › signed out › robots.txt allows crawling, so crawlers can see each page's noindex
ok 24 [chromium] › signed-out.spec.ts › a signed-in page sends you to sign in, with the way back
ok 25 [chromium] › signed-out.spec.ts › a /bff/ fetch with no session gets a JSON 401, not HTML, and is never indexed
  25 passed (37.9s)
```

## Lighthouse medians

**Setup:**
- Lighthouse 13.5.0, mobile, `npm run start`, headless Chromium with no extensions.
- Three runs per page, median reported.
- The seeded stokvel has 40 members, 12 cycles and 436 contributions.
- "sim" is the default simulated slow 4G with 4× CPU. "applied" is `--throttling-method=devtools`.

| Page | LCP sim | CLS sim | LCP applied | CLS applied | TBT sim | Perf | A11y |
|---|---|---|---|---|---|---|---|
| Home, before | 2.98 s | 0 | 6.85 s | 0.018 | 221 ms | 91 | 86 |
| Home, after | **2.44 s** | 0 | **2.30 s** | **0** | 266 ms | 92 | 100 |
| Stokvel, before | 3.19 s | 0 | 7.60 s | 0.018 | 325 ms | 86 | 96 |
| Stokvel, after | 3.10 s | 0 | 3.20 s | **0** | 268 ms | 88 | 100 |
| Sign in, before | 2.49 s | 0 | 2.11 s | 0 | 199 ms | 94 | 95 |
| Sign in, after | 2.69 s | 0 | 2.47 s | 0 | 217 ms | 93 | 100 |

**INP** was measured with `web-vitals` `onINP` (attribution build) while opening Close Cycle and recording a contribution, at 4× CPU on slow 4G, as the median of three runs:
- **Before:** 1,080 ms.
- **After, clicking straight after load:** 768 ms.
- **After, once the page is idle:** 280 ms.

**The 200 ms INP target is not met.** The time is spent in React during the click (`pointerdown` and `click` handlers). The sign-in page change is within run-to-run variation; that page didn't change.

## The invite's `<head>`, as a link-preview bot gets it, signed out

`curl -s -A "WhatsApp/2.23.20.0" "http://localhost:3000/join?code=85017ecf58c53940bdbfddbf9db7e346"`. This is a real invite to "Masakhane Savings Club", R 500,00 a month. Response: `HTTP/1.1 200 OK`.

```html
<title>You’re invited to join a stokvel · RondiTrack</title>
<meta name="description" content="Someone has invited you to join their stokvel on RondiTrack. Sign in to see the details and join."/>
<meta name="application-name" content="RondiTrack"/>
<meta name="robots" content="noindex, nofollow"/>
<meta property="og:title" content="You’re invited to join a stokvel · RondiTrack"/>
<meta property="og:description" content="Someone has invited you to join their stokvel on RondiTrack. Sign in to see the details and join."/>
<meta property="og:site_name" content="RondiTrack"/>
<meta property="og:type" content="website"/>
<meta name="twitter:card" content="summary"/>
<meta name="twitter:title" content="You’re invited to join a stokvel · RondiTrack"/>
<meta name="twitter:description" content="Someone has invited you to join their stokvel on RondiTrack. Sign in to see the details and join."/>
```

Checked against the whole HTML: "Masakhane" appears 0 times, and "500,00" appears 0 times.

```
$ curl -si -A "WhatsApp/2.23.20.0" http://localhost:3000/bff/contributions/00000000-0000-4000-8000-000000000000/proof
HTTP/1.1 401 Unauthorized
X-Robots-Tag: noindex, nofollow, noarchive
content-type: application/json
{"title":"Authentication required."}

$ curl -s http://localhost:3000/robots.txt
User-Agent: *
Allow: /

$ curl -si -A "WhatsApp/2.23.20.0" http://localhost:3000/dashboard/member
HTTP/1.1 307 Temporary Redirect
location: /login?next=%2Fdashboard%2Fmember
```

## CI

Each result below was read from the GitHub API for that run.

- **Red:** [PR #10](https://github.com/mdvar2/RondiTrack/pull/10) (`assignment-9.2-ci-red` → `assignment-9.2`, commit `b3d0688`, not to be merged) renames the *Record contribution* button to *Save contribution*.
  - [Run 42517356298](https://github.com/mdvar2/RondiTrack/actions/runs/42517356298): **failure**.
  - [`frontend`](https://github.com/mdvar2/RondiTrack/actions/runs/42517356298/job/128843183727) failed at "Vitest, TZ=UTC". GitHub reports 10 failures at `src/components/ContributionForm.test.tsx:44`: `Unable to find an accessible element with the role "button" and name "Record contribution"`.
  - [`e2e`](https://github.com/mdvar2/RondiTrack/actions/runs/42517356298/job/128843572437) was **skipped** (`needs: [backend, frontend]`).
  - [`backend`](https://github.com/mdvar2/RondiTrack/actions/runs/42517356298/job/128843183779) passed.
- **Green:** [PR #11](https://github.com/mdvar2/RondiTrack/pull/11) (this PR), commit `1ca4dcf`.
  - [Run 42518612947](https://github.com/mdvar2/RondiTrack/actions/runs/42518612947): **success**. All three jobs passed.
  - [`backend`](https://github.com/mdvar2/RondiTrack/actions/runs/42518612947/job/128846984321) and [`frontend`](https://github.com/mdvar2/RondiTrack/actions/runs/42518612947/job/128846984446) passed.
  - [`e2e`](https://github.com/mdvar2/RondiTrack/actions/runs/42518612947/job/128847350234) passed: Compose startup, `npm run build`, and Playwright.

**The first green attempt failed, and why.** On `39f4ed4`, `backend` and `frontend` passed. Then `e2e` failed while starting Compose, before Playwright ran:

```text
Container ronditrack-e2e-migrate-1 Starting
error mounting ".../src/Infrastructure/bin": mkdir .../src/Infrastructure/bin: read-only file system
```

- **The cause:** `migrate` bind-mounted the checkout read-only (`./:/src:ro`) and mounted named volumes over each project's `bin/` and `obj/`.
  - Docker must create those mount points inside the read-only mount.
  - That only works when a local build has already left the git-ignored folders there. A fresh CI checkout doesn't have them.
  - The mount was in the base `compose.yaml`, not a development override.
- **The fix (`1ca4dcf`):** a `migrate` stage in the Dockerfile (restored source plus `dotnet-ef` 10.0.1). Compose builds it with `target: migrate`.
  - Nothing is mounted from the host, and the six `bin`/`obj` volumes are removed. `postgres_data` is unchanged.
  - The runtime image is unchanged, and `docker build .` doesn't build the new stage.
- **Checked locally before pushing:**
  - The failure reproduced from a fresh clone of `39f4ed4`.
  - From a fresh clone of `1ca4dcf`, migrations applied to an empty database and a second run changed nothing.
  - Playwright passed 25/25 against that stack.
  - The dev stack kept its data and its 14 migrations.

**Later runs:** the docs commit `15e2e6f` ran green too: [run 42527732827](https://github.com/mdvar2/RondiTrack/actions/runs/42527732827).

## Follow-up on 2026-11-09: INP investigation

Full write-up: "INP" under Performance in `web-next/README.md`. In short:

**Root cause, right after load.**
- A click during hydration made React discard its in-progress (transition) hydration and redo it synchronously: about 470 ms of Blocking Render before the click handler ran, at 4× CPU.
- Inside that, `DueCountdown`, which renders a single `<span>`, took 107–143 ms. The reason: `lib/dates.ts` built two `Africa/Johannesburg` `Intl.DateTimeFormat`s at import time, and a browser's first time-zone formatter loads ICU data. Alone, it took 102–122 ms at 4× CPU.
- `lib/money.ts` did the same with `Intl.NumberFormat('en-ZA')`.

**Root cause, once idle.** It's opening the dialog: React's render and commit (10–17 ms), then style and layout forced by focusing *Cancel*. The same 27 layout objects took 18–94 ms depending on CPU-throttling slices.

**Changes:**
- Formatters made on first use.
- The dialog no longer formats "R 0,00" while its preview loads. Its confirm button now says "Waiting for the preview", tested in the new `CloseCycleDialog.test.tsx`.
- TanStack Query and nuqs moved from the root layout to `DataProviders`, on the two pages that use them.
- New diagnostic scripts, `scripts/trace-click.mjs` and `scripts/profile-click.mjs`, and a `PAGES` option for `scripts/lighthouse.mjs`.

**Disproved:**
- Hiding the 40 proof-upload forms didn't help.
- No hydration mismatch.

**INP, measured alternately.** Both builds were measured in four rounds, each the usual three-run `inp.mjs` median:

| Click | Before (12 runs) | After (12 runs) |
|---|---|---|
| Right after load | median 1,216 ms | **712 ms** |
| Once idle | median 272 ms | 280 ms (5 of 12 runs under 200 ms, against 1 of 12) |

**The 200 ms target is not met.**

**Home LCP and CLS, measured alternately:**
- **Applied throttling** (10 runs each): 1.91 s before and 1.93 s after. All 20 runs are under 2.5 s, and CLS is 0.
- **Simulated** (6 runs each): 2.61 s for both builds, above the target today. It was 2.44 s on 2026-11-08, so this is not caused by these changes.

**Checks on the final code (2026-11-09):**

| Command | Result |
|---|---|
| `npm run lint`, `npm run typecheck`, `npm run build` | clean |
| `npm audit`, `npm audit --omit=dev` | 0 vulnerabilities |
| Vitest, 10 runs in a row, alternating `TZ=UTC` and `TZ=Africa/Johannesburg` | 16 files, 171 tests passed in every run |
| `CI=1 npx playwright test` ×3 on the production build | 25 passed, 25 passed, 25 passed |
| `dotnet test RondiTrack.Tests -c Release` | Passed: 159, Failed: 0 |

**Accessibility.**
- axe found no serious or critical violations in light and dark mode, and the keyboard spec passed.
- Lighthouse accessibility was 100 on all three pages.
- **NVDA was not run:** it isn't installed on this machine. The steps, with the exact expected text, are in `docs/history/assignment-9.2-screen-reader-checklist.md`.
- The contribution form has no date field, so there's no date validation error to test.

## Screenshots to attach

1. The red CI run, showing which job failed and the failing test's message.
2. The green CI run, with all three jobs (`backend`, `frontend`, `e2e`) passing.
3. Lighthouse on the home screen before (2.98 s) and after (2.44 s).
4. The Playwright HTML report with the journey's trace open.

## Not done in this PR

- **INP under 200 ms: not met.** After the follow-up it is 712 ms right after load (from 1,216 ms) and 280 ms once the page is idle, against the 200 ms target.
- **Home LCP, simulated: 2.61 s today** for both the old and new builds. Applied throttling is 1.93 s.
- **The NVDA pass: outstanding.** The checklist is ready, and the amount and date utterances aren't recorded yet.
- **The five-minute demo recording: outstanding.**
- **The screenshots listed above: outstanding.**
- Lighthouse CI budgets (optional).
- 400% zoom.

See "Known gaps" in `web-next/README.md`.
