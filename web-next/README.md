# RondiTrack on Next.js

RondiTrack's member pages, rebuilt on **Next.js 16.4.1** with the App Router. Every page is a Server Component that fetches from the ASP.NET Core API **on the server**. The browser only ever talks to this Next.js app: it holds one encrypted, `HttpOnly` cookie called `session` and never sees an access or refresh token.

## Assignment 9.2: Tests, performance, accessibility and privacy

Assignment 9.2 covers Week 9 Days 2 and 3 together. There is no separate 9.3 specification. Everything below was run on 2026-11-08 against a throwaway Compose project (`ronditrack-e2e`, API on port 5147, fresh volume). The development database was not touched.

### Plan

**1. Which money, date and validation rules are pure functions, and which were buried?**
Pure, and now tested one line per case:
- `lib/money.ts`: `parseRandsToCents`, `formatCents`, `centsToInput`, `MAX_CONTRIBUTION_CENTS`
- `lib/dates.ts`: `johannesburgPeriod`, `johannesburgDayRangeUtc`, `getDueCountdown`, `getJohannesburgDeadlineUtc`
- `lib/cycles.ts`: `pickCurrentCycle`
- `lib/contribution-permissions.ts`: `canManageStokvel`, `canSeeProof`, `getEligibleMembersForContribution`
- `lib/contribution-filters.ts`
- `lib/invite-metadata.ts`

Rules that were buried and have been pulled out:
- **Amount validation** lived inside the `recordContribution` Server Action. It used `parseInt` on a *cents* field and refused anything that wasn't whole rands. It is now `amountError` / `parseRandsToCents`.
- **The answer → message mapping** for the contribution form is now `feedbackFor` in `lib/contribution-feedback.ts`.
- **"Which cycle is current"** was `cycles.at(-1)` inline in two places. It is now `pickCurrentCycle`.
- **Invite preview metadata** is now `inviteMetadata`.

**2. Which tool for each part?**

| Part | Tool | Why |
|---|---|---|
| 1 Money and dates | Vitest (node) | Pure functions. Milliseconds per case, run under two time zones. |
| 2 Contribution form | Vitest + React Testing Library + user-event | A Client Component with one edge, the Server Action, which is faked. |
| 3 Roles in the UI | Vitest + RTL | A synchronous view fed with fixtures typed from the generated API types. |
| 3 Roles at the API, redirects, the journey | Playwright | Needs a page, a cookie, a redirect and the real API. Vitest can't render async Server Components. |
| 4 Accessibility | Playwright + axe, a keyboard spec, and a manual screen-reader pass | axe finds markup failures. The keyboard spec checks focus. Only a person can judge what a screen reader says. |
| 5 Performance | Lighthouse 13.5.0 (mobile) and `web-vitals` `onINP` driven by Playwright | Measurements, not tests. |
| 6 Metadata | Vitest (`inviteMetadata`) + Playwright (`seo.spec.ts`) + `curl` with a bot user agent | Next.js puts metadata in `<head>` only for known bots. |

**3. The edges of the contribution form, and what stands in for each.**
- **The Server Action** (`recordContribution`, passed in as the `action` prop) is the only network edge. In tests a fake stands in. It resolves only when the test says so, or rejects like a dropped connection.
- **The router** isn't used by the form. After a success the Server Action's `revalidatePath` refreshes the page, and Playwright checks that.
- **The URL:** the form reads nothing from it.
- **The Idempotency-Key** is made *in the form*. A retry is the same form submitting again, so only the form can hold the key across it. An action that made a key per call would turn every retry into a second contribution. The key is created on the first submit, reused after a failure (dropped connection, 4xx or 5xx), and replaced only after a success.

**4. Login rate limit, Playwright sign-in, and CI data.**
- **The limit:** the API allows 5 logins a minute per address (`RateLimiting:Login`).
- **Signing in:** Playwright's `setup` project signs Sipho and Thandi in **once each** through the real form and saves the sessions to `e2e/.auth/` (git-ignored). A saved session is reused while it is under two minutes old, because the access token lives five minutes and the app rotates the refresh token in its last minute.
- **Test data:** tests make their own stokvels, cycles, members and invites through the API. The bearer token comes from the saved, encrypted session cookie, which the test run can decrypt because it generated `SESSION_SECRET` itself. So data setup costs no extra logins.
- **CI:** a fresh `docker compose -p ronditrack-e2e` database. The two accounts are registered by the setup project (registration isn't rate-limited). `POSTGRES_PASSWORD`, `JWT_SIGNING_KEY`, `SESSION_SECRET` and `E2E_PASSWORD` are generated in each run and masked.

**5. Routes.** See [Metadata and indexing](#metadata-and-indexing). Only `/login` and `/register` should be indexed.

**6. The largest thing on the home screen, and what it waited for.**
- **Before:** an item in the "Recent activity" feed. The browser fetched it after the JavaScript loaded and hydrated (`/api/demo-activity`), and that endpoint then made several API calls.
- **After the first change:** the `<h1>` became the LCP element. It still waited for `/me/home`, because the whole page body streamed.
- **Now:** the heading is in the static shell, and the data streams in below it.

**7. Dragging and typing from memory.**
- **Dragging:** nothing in RondiTrack drags. There is no payout-rotation reordering UI in this app, and no rotation order in the API (`PayoutService` picks the next member). SC 2.5.7 has nothing to apply to yet. Any future reordering needs Move up / Move down buttons and an announcement.
- **Typing from memory:** the password and the invite code.
  - Sign-in has `autocomplete="username"` and `"current-password"`, allows paste, and has no puzzle (SC 3.3.8).
  - The invite code can be pasted. It is carried through sign-in in `?next=`, and Back from step 2 refills it (SC 3.3.7).

### Running the tests

```powershell
cd web-next
npm ci
npm test                    # Vitest, every project
npm run test:utc            # same, TZ=UTC
npm run test:jhb            # same, TZ=Africa/Johannesburg
npm run lint
npm run typecheck           # next typegen && tsc --noEmit
```

End-to-end tests need the API, a database and a production build. Run them from the repository root, in a shell with throwaway values, and never your own `.env`:

```bash
export POSTGRES_PASSWORD=$(openssl rand -hex 24) JWT_SIGNING_KEY=$(openssl rand -base64 48)
export SESSION_SECRET=$(openssl rand -base64 32) E2E_PASSWORD=e2e-$(openssl rand -hex 12)
export API_PORT=5147 API_BASE_URL=http://localhost:5147
docker compose -p ronditrack-e2e up -d --build --wait api   # wait for /health/ready
cd web-next
npx playwright install chromium
npm run build
CI=1 npx playwright test        # CI=1: Playwright starts its own `npm run start`
npx playwright show-report      # the HTML report, with traces of failures
```

Some things must be running first, and some state can trip you up:
- **The Compose API must be ready:** `curl $API_BASE_URL/health/ready` returns 200.
- **Port 3000 must be free.** Without `CI=1`, Playwright reuses whatever is on port 3000. That must never be `next dev`.
- **A recreated database needs fresh sessions.** If you recreate the e2e database, delete `e2e/.auth/`. Those sessions belong to the old database.

Performance measurement uses the same stack:
- `npm run seed:perf` seeds one stokvel with 40 members, 12 monthly cycles and 436 contributions.
- `node scripts/lighthouse.mjs <label>` runs Lighthouse three times per page and takes the median. Add `THROTTLING=devtools` for applied throttling, `RUNS=5` for more runs, or `PAGES=home` for one page.
- `node scripts/inp.mjs <label>` measures INP: three runs, median. Add `SETTLE_MS=5000` to click only once the page is idle.
- Diagnosis only, measuring nothing for the report:
  - `node scripts/trace-click.mjs <label>` records a Chrome performance trace of the same clicks. It splits each interaction into input delay, handlers and waiting for the frame, and into script, style and layout, and paint.
  - `node scripts/profile-click.mjs ["Record contribution"]` takes a V8 CPU profile of one click.

### What's checked where

| Behaviour | Where | Tool |
|---|---|---|
| Parsing accepts `1500`, `1 500`, `1 500,50`, `R1500`, `R 1 500,50`, NBSP, `,5` | `src/lib/money.test.ts` | Vitest |
| Parsing refuses 3 decimals, `1,500.50`, letters, empty, negative, above max; never rounds | `src/lib/money.test.ts` | Vitest |
| `19,99` is 1999 cents; values where `x * 100` truncates wrongly | `src/lib/money.test.ts` | Vitest |
| Format → parse round-trips 0, 1, 99, 100, 150050 and the maximum | `src/lib/money.test.ts` | Vitest |
| 23:30 SAST on the last day of a month belongs to that month; due dates; SAST day filters | `src/lib/dates.test.ts`, `contribution-filters.test.ts` | Vitest, TZ=UTC and TZ=Africa/Johannesburg |
| The page picks the current cycle, not the oldest | `src/lib/cycles.test.ts` | Vitest |
| Form fields by label and buttons by role; API 400s under the right field (`amountInCents` → Amount, `userId` → Member); 409/422 at the top with a reference; success; typed value kept | `src/components/ContributionForm.test.tsx` | Vitest + RTL |
| One click, one request; retry reuses the key; the next contribution gets a new one | `src/components/ContributionForm.test.tsx` | Vitest + RTL |
| Treasurer actions shown in the family stokvel and absent (`queryByRole`) in the work one | `src/components/StokvelView.test.tsx` | Vitest + RTL, typed fixtures |
| "Load more" sends page 2 with the filter; failure keeps the list and offers Try again | `src/components/ActivityFeed.test.tsx` | Vitest + RTL + MSW 3 |
| Close Cycle dialog: focus on Cancel; while the preview loads, the confirm button can't be used and names no amount; then it names the real total | `src/components/CloseCycleDialog.test.tsx` | Vitest + RTL + MSW 3 |
| Invite metadata can't carry a name or an amount | `src/lib/invite-metadata.test.ts` | Vitest |
| Invite status endpoint is anonymous and returns only `valid` or `expired`; contribution maximum is a field error | `RondiTrack.Tests/Assignment92ApiTests.cs` | xUnit + Testcontainers |
| Journey: record R 150,50, total goes up by exactly 15 050 cents without a reload | `e2e/journey.spec.ts` | Playwright |
| Sipho, a Member, sends the treasurer's close through `/bff/`: 403, still signed in | `e2e/roles.spec.ts` | Playwright |
| Signed out: sign-in redirect with `?next=`; `/bff/` gives a JSON 401 with `X-Robots-Tag` | `e2e/signed-out.spec.ts` | Playwright |
| Signed-in pages `noindex`; signed-out invite `<head>` has no name or amount; robots.txt allows | `e2e/seo.spec.ts` | Playwright |
| axe WCAG 2.2 AA (light and dark): sign-in, home, stokvel, form with errors, open Close Cycle dialog, join steps | `e2e/accessibility.spec.ts` | Playwright + @axe-core/playwright |
| Keyboard-only join with focused step headings; Back keeps the code; keyboard-only record; dialog focus; toast never covers focus on a phone; sign-in paste and autocomplete | `e2e/keyboard.spec.ts` | Playwright |
| What the screen reader says | Manual, **not yet done**: [checklist](../docs/history/assignment-9.2-screen-reader-checklist.md) | NVDA |

No test uses `data-testid`. Every element is found by role, label or visible text. The journey reads the total with `dt:text-is("Collected") + dd`, which is the visible term and its value.

### Accessibility (WCAG 2.2 AA)

"Found by" says whether a tool or a person found it:
- **axe** means Lighthouse's or Playwright's axe.
- **Keyboard spec** means `e2e/keyboard.spec.ts` failed first.
- **RTL** means a component test failed first.
- **Review** means I found it while reading the code.

| Criterion | Flow | What failed | Fix | How checked | Found by |
|---|---|---|---|---|---|
| 4.1.2 Name, role, value | Every page | The toast container had `aria-label` on a role-less `div` (`aria-prohibited-attr`) | A `section` landmark holding two always-present live regions: `status`, and `alert` for errors | axe spec | axe (Lighthouse) |
| 1.3.1 / 3.3.2 Labels | Home | The activity search, status `select` and both date inputs had no label (`label`, `select-name`) | Visible labels | axe spec | axe (Lighthouse) |
| 4.1.2 | Close cycle | The loading placeholder had `aria-label` on a plain `div`, so nothing was read | `role="status"` with visually hidden text | axe spec | Review |
| 2.4.3 Focus order | Stokvel page | Focus jumped to *Close cycle* on every page load: the dialog's effect ran its "closed" branch on mount | Return focus only after the dialog has been open | `keyboard.spec.ts` | Review |
| 2.4.11 Focus not obscured | Proofs, on a 360 px phone | After an upload, Tab reached a link underneath the success toast. `scroll-padding-bottom` alone didn't help, because Chrome doesn't scroll an element already inside the viewport | While a toast shows: scroll any newly focused control clear of it, and pad the page bottom (plus `scroll-padding-bottom`) | `keyboard.spec.ts` | Keyboard spec |
| 3.3.7 Redundant entry | Join | Browser Back from step 2 showed an empty invite-code field | The code is kept for the tab and refilled; Back links on steps 2 and 3 | `keyboard.spec.ts` | Keyboard spec |
| 2.4.3 / 4.1.3 | Join | Step headings had `tabIndex={-1}` but nothing focused them. After the fix, step 2 → 3 still didn't refocus | `FocusOnMount`, keyed by step | `keyboard.spec.ts` | Keyboard spec (second half) |
| 2.4.2 Page titled | Join, every page | Every join step had the same title. Titles were hand-written | Title template `%s · RondiTrack`; per-step titles with no names | `keyboard.spec.ts` (title check) | Review |
| 3.3.1 / 3.3.3 Error identification | Record a contribution | One generic alert, not tied to any field. The field asked for **cents** | Field errors with `aria-invalid` and `aria-describedby`, focus moved to the field; amounts in rands with a hint | `ContributionForm.test.tsx`, axe | Review |
| 2.4.6 / 2.5.3 Labels and names | Proofs | 40 identical "Proof of payment" labels and "Download proof" links. My first fix produced the name "Uploadyour proof" | Names that say whose proof; `aria-label` starting with the visible word | `StokvelView.test.tsx` | RTL |
| 3.2 / 4.1.2 | Upload, record, close | `disabled` while pending dropped keyboard focus to `<body>` | `aria-disabled` plus a guard | `ContributionForm.test.tsx` | Review |
| 2.5.8 Target size | Toasts, back links, nav | The toast *Close* was about 16 px tall | `min-h-6` or larger on standalone controls | axe `target-size` | Review |
| 3.3.8 Accessible authentication | Sign in | The email field was `autocomplete="email"` | `autocomplete="username"` (paste was already allowed, and there's no puzzle) | `keyboard.spec.ts` | Review |
| 2.3.3 / motion | Skeletons | Pulsing placeholders ignored reduced motion | `motion-reduce:animate-none` and a global reduced-motion rule | Manual | Review |
| 2.5.7 Dragging | Payout rotation | Nothing drags. There is no rotation UI | None needed | Review | — |
| 2.4.6 / 4.1.2 Labels, name | Close cycle | While the preview loaded, the confirm button was named "Close cycle and record R 0,00 in arrears", an amount nobody had calculated | "Waiting for the preview" until the preview arrives (still `aria-disabled`), then the real total | `CloseCycleDialog.test.tsx` | Review (found during the INP work) |

**Re-checked on 2026-11-09, after the INP changes:**
- axe passed with no serious or critical violations, in light and dark: 8 axe tests, inside 3 consecutive Playwright runs of 25/25.
- The keyboard spec passed: focus on Cancel, Escape returns focus, the join headings take focus, and toasts never cover focus.
- Lighthouse accessibility was 100 on all three pages.

**Screen reader: not done.** NVDA isn't installed on the machine this work was done on, and nothing here can listen to it, so no NVDA result is claimed. What it relies on is checked automatically: accessible names, the live regions, focus on step headings, and titles. The step-by-step check, with the exact text the app produces for each step, is in [the screen-reader checklist](../docs/history/assignment-9.2-screen-reader-checklist.md).

One correction to the task as given: the contribution form has **no date field** (the API dates each contribution). The only date inputs are the activity feed's From and To filters, which have no validation message to hear.

The two utterances the assignment asks for (one amount and one date, as NVDA reads them) go here once recorded:

- Amount: _to be recorded_
- Date: _to be recorded_

### Performance

**Method:**
- `npm run start`, never `next dev`.
- Lighthouse 13.5.0, mobile profile, headless Chromium, a fresh profile with no extensions.
- Three runs per page; the median is reported.
- The stokvel is "Masakhane Savings Club": 40 members, 12 cycles, 436 contributions.
- Pages were measured signed in with a session cookie built by `scripts/session-cookie.mjs`.
- Simulated throttling is Lighthouse's default (slow 4G, 4× CPU). Applied throttling (`--throttling-method=devtools`) is reported too, because the simulation doesn't replay streamed HTML.

| Page | Metric | Before | After | What made the difference |
|---|---|---|---|---|
| Home (`/dashboard/member`) | LCP, simulated | 2.98 s | **2.44 s** | First activity page rendered on the server (no client fetch waterfall); heading in the static shell |
| Home | LCP, applied | 6.85 s | **2.30 s** | Same |
| Home | CLS, simulated / applied | 0 / 0.018 | **0 / 0** | Sidebar placeholder reserves the two lines that replace it; no client "Loading recent activity…" swap |
| Home | Accessibility score | 86 | **100** | Labels; toast region |
| Stokvel (40 members) | LCP, simulated | 3.19 s | 3.10 s | No real change: the `<h1>` is the stokvel's name and needs the API |
| Stokvel | LCP, applied | 7.60 s | 3.20 s | Split Suspense boundaries; less to hydrate before the reveal |
| Stokvel | CLS while contributions stream in, simulated / applied | 0 / 0.018 | **0 / 0** | Nested list boundary at the bottom; tabular figures; skeletons |
| Stokvel | TBT, simulated | 325 ms | 268 ms | |
| Stokvel | INP: open Close Cycle, record a contribution (4× CPU, slow 4G) | 1,080 ms | 768 ms after load; 280 ms once the page is idle (2026-11-08). After the follow-up on 2026-11-09: **712 ms** and **280 ms** (12 runs each; see [INP](#inp)) | Contribution list in its own boundary; formatters made on first use; TanStack Query and nuqs only where used. **Still above 200 ms.** |
| Sign in | LCP, simulated | 2.49 s | 2.69 s | Unchanged page. Within run-to-run variation (runs 2.64–2.84 s) |

LCP elements:
- **Home:** an activity item before; `h1` "Member dashboard" after.
- **Stokvel:** `h1` (the stokvel's name) before and after.

The SEO score on signed-in pages falls to 63 on purpose: they are `noindex`.

**Not changed, and why:**
- **Font:** it was already `next/font` (Geist), self-hosted and preloaded. The font-display insight passed and CLS was 0, so nothing justified changing it.
- **`next/image`:** none. The app has no content images. Proofs of payment are opened as plain links through `/bff/contributions/{id}/proof` (`Cache-Control: private, no-store`). They never go through the `next/image` optimiser, whose cache is shared by everyone.
- **The OG card:** a build-time `ImageResponse` with no user data.
- **`next/dynamic`:** not used. The only large client-only part, the Close Cycle dialog, is about 5 KB. The INP cost is hydration, not download.

#### INP

**Where the time went (2026-11-09).** The worst interaction is always the **Close cycle** click, sometimes the **Record contribution** click. The diagnosis used:
- Chrome performance traces (`scripts/trace-click.mjs`);
- V8 CPU profiles (`scripts/profile-click.mjs`);
- React 19.2's Scheduler tracks in a `next build --profile` build;
- for one experiment only, temporary React `<Profiler>` probes around each part of the page. They were removed afterwards and never committed.

1. **Clicking right after load: React redoing hydration.**
   - The trace splits the 821 ms interaction into 13 ms input delay, **647 ms in the `pointerdown` handler** and 161 ms waiting for the frame. That is 742 ms of script, 33 ms of style and layout, and 16 ms of paint.
   - React's tracks show why. The page's first hydration pass (a transition) was still running, so the click made React **discard it and redo it synchronously**: about 470 ms of Blocking Render, then 103 ms of Commit. Only then did it run the click, at +626 ms. The click itself (handler, render, commit, effects) took about 165 ms.
2. **What made that hydration slow: two `Intl` formatters built at import time.**
   - The `<Profiler>` probes timed the summary boundary's hydration at 213–228 ms. Of that, **`DueCountdown` took 107–143 ms**, although it renders one `<span>` ("Due in 21 days") and only does date arithmetic.
   - The cause: `lib/dates.ts` created two `Intl.DateTimeFormat`s for `Africa/Johannesburg` **when the module was imported**. A browser's first time-zone formatter loads ICU data. Timed alone in a blank page at 4× CPU, it took **102–122 ms**; the first `Intl.NumberFormat('en-ZA')` took 12–30 ms.
   - The form paid for `lib/money.ts`'s formatter the same way: 47–57 ms of hydration, though it only parses amounts.
3. **Once idle: the dialog itself.**
   - The click handler takes about 60–110 ms: the handler, React's render and commit (10–17 ms by the probes), and the effect that focuses *Cancel*.
   - That `focus()` forces style (14 elements) and layout (27 of about 1,080 objects). The same work took 18 ms in some runs and 94 ms in others.
   - Fonts and late network responses didn't line up with the slow runs. It is the variance of CPU throttling, which pauses the renderer in slices.

**Changes.** Each change is targeted at one of the costs above. Nothing was removed, and the measurement scripts are unchanged:
- `lib/dates.ts` and `lib/money.ts` make their formatters **on first use**. The first money formatting now happens when the close preview or a success message arrives, outside the click.
- The Close Cycle dialog no longer formats "R 0,00" into its button while the preview loads. That was also a wrong label (see the accessibility table).
- `Providers` in the root layout now holds only the toasts. TanStack Query and nuqs moved to `DataProviders`, around the activity feed on the two pages that show it. The stokvel, sign-in, register and join pages no longer download or hydrate them: 13 KB less JavaScript on the stokvel and sign-in pages. One `QueryClient` per tab keeps the cache across those two pages.

**Disproved on the way.**
- Hiding the 40 proof-upload forms, as a temporary experiment, didn't help. React hydrates that nested boundary separately, at the lowest priority.
- No hydration mismatch: no console errors, and the countdown text matched the server's.

**Results.** Run-to-run spread is ±150–200 ms, and the machine drifted over the day: the unchanged code measured 224 ms idle in the morning and 344 ms in the afternoon. So both builds were kept on disk and **measured alternately**, four rounds of the usual three runs, with the same `inp.mjs` and the same conditions (4× CPU, slow 4G, 412 px).

| Click | Before: median per round | After: median per round | All 12 runs: median before → after | Runs under 200 ms |
|---|---|---|---|---|
| Right after load | 1,056 / 984 / 1,360 / 1,216 | 888 / 704 / 752 / 504 | **1,216 → 712 ms (−41%)** | 0 → 0 of 12 |
| Once idle (`SETTLE_MS=5000`) | 224 / 280 / 264 / 272 | 280 / 304 / 288 / 192 | 272 → 280 ms (no change beyond noise) | 1 → 5 of 12 |

**The 200 ms target is still not met.** See [Known gaps](#known-gaps).

**LCP and CLS after these changes.** The same alternating method was used (the before build, then the after build):

| Home screen | Before | After |
|---|---|---|
| LCP, applied throttling, 10 runs | median **1.91 s**, all 10 under 2.5 s | median **1.93 s**, all 10 under 2.5 s |
| LCP, simulated, 6 runs | median 2.61 s | median 2.61 s |
| CLS | 0 (worst run 0.013) | **0** in every run |
| LCP element | `h1` "Member dashboard" | the same |

The **simulated** home LCP is above 2.5 s today for **both** builds; it was 2.44 s on 2026-11-08. The heading paints at 185–296 ms unthrottled, together with first paint. Lantern's estimate flips between about 1.5 s and 2.6 s depending on which requests started before that early paint. The applied measurement is under 2.5 s in all 20 runs.

On the stokvel page, simulated LCP went from 3.09 to 2.89 s (6 runs each), with CLS 0 in both.

### Metadata and indexing

| Route | Access | Indexed? | What a preview may show | Mechanism |
|---|---|---|---|---|
| `/login` | public | yes | Product title, description, image | `metadata` + `openGraph` + `login/opengraph-image.tsx` |
| `/register` | public | yes | Product description | `metadata` |
| `/join` (no code) | public | no | "Join a stokvel" | `generateMetadata` → `noindex` |
| `/join?code=…` signed out | public (sensitive URL) | no | "You're invited to join a stokvel", or "expired" / "not found". No names, no amounts | `generateMetadata` using only `GET /api/v1/invites/{code}/status` |
| `/join?code=…` signed in | session | no | Step title only | same |
| `/`, `/dashboard/member`, `/dashboard/treasurer`, `/stokvels`, `/stokvels/[id]` | session | no | Nothing: signed out they 307 to `/login?next=…` | `robots` in `(app)/layout.tsx`, set once; `proxy.ts` redirect |
| `/bff/contributions/[id]/proof`, `/bff/stokvels/[id]/cycles/[id]/close`, `/api/demo-activity` | session (JSON 401 otherwise) | no | Never fetched without a session | `X-Robots-Tag: noindex, nofollow, noarchive` (`next.config.ts`) |
| `/robots.txt` | public | n/a | `Allow: /` | `app/robots.ts`. Nothing is disallowed, so crawlers can see each page's `noindex` |

**How the invite page handles a signed-out bot.** It used to redirect to sign-in, so a preview showed the sign-in card and couldn't say whether the invite was valid. Now a signed-out request with a code gets a **200** page.
- **The decision:** the API's full preview needs a signed-in person, so a new anonymous endpoint, `GET /api/v1/invites/{code}/status`, returns only `{"status":"valid"|"expired"}` or 404. It is rate-limited per address.
- **What the bot sees:** `generateMetadata` builds the title, description and `og:` tags from that status alone. `inviteMetadata` takes nothing else, so it *can't* name the stokvel.
- **What a person sees:** Sign in / Create an account, each carrying `?next=/join?code=…&step=rules`, so the code is never retyped. The stokvel's name and amount appear only after sign-in.

The `<head>` a WhatsApp preview bot receives is in the PR description, and `seo.spec.ts` checks it on every run. Repeat the check with a link-preview checker after deploying (Day 4).

### CI

`.github/workflows/ci.yml`, on every pull request:
- **`backend`** (existing): builds, runs the API tests (Testcontainers), and builds the Docker image.
- **`frontend`** (new, beside it): `npm ci`, lint, `next typegen` + `tsc`, then Vitest with `TZ=UTC` and with `TZ=Africa/Johannesburg`.
- **`e2e`** (new; `needs: [backend, frontend]`):
  1. Starts a fresh `ronditrack-e2e` Compose project with secrets generated and masked in the run.
  2. Waits for `/health/ready`.
  3. Installs Chromium, runs `npm run build`, then `npx playwright test`. Playwright starts `npm run start` itself.
  4. Keeps `playwright-report/` and `test-results/` as an artifact when the run fails, and always removes the stack.

Saved sessions (`e2e/.auth/`), reports and `.env*` are git-ignored.

#### CI evidence

Both runs are real GitHub Actions runs of this workflow. Each result below was read from the GitHub API for that run.

| | Red | Green |
|---|---|---|
| Pull request | [#10](https://github.com/mdvar2/RondiTrack/pull/10): `assignment-9.2-ci-red` → `assignment-9.2`, open, not to be merged | [#11](https://github.com/mdvar2/RondiTrack/pull/11): `assignment-9.2` → `assignment-9.1` |
| Commit | `b3d0688`: renames the *Record contribution* button to *Save contribution* | `1ca4dcf` |
| Run | [42517356298](https://github.com/mdvar2/RondiTrack/actions/runs/42517356298): **failure** | [42518612947](https://github.com/mdvar2/RondiTrack/actions/runs/42518612947): **success** |
| `backend` | [success](https://github.com/mdvar2/RondiTrack/actions/runs/42517356298/job/128843183779) | [success](https://github.com/mdvar2/RondiTrack/actions/runs/42518612947/job/128846984321) |
| `frontend` | [**failure**](https://github.com/mdvar2/RondiTrack/actions/runs/42517356298/job/128843183727) at "Vitest, TZ=UTC". The Johannesburg step was skipped | [success](https://github.com/mdvar2/RondiTrack/actions/runs/42518612947/job/128846984446) |
| `e2e` | [**skipped**](https://github.com/mdvar2/RondiTrack/actions/runs/42517356298/job/128843572437): `needs: [backend, frontend]` | [success](https://github.com/mdvar2/RondiTrack/actions/runs/42518612947/job/128847350234): Compose startup, build, Playwright |

**Why the red run failed.** GitHub reports 10 failures, all at `src/components/ContributionForm.test.tsx:44`, with this error:

```text
TestingLibraryElementError: Unable to find an accessible element with the role "button" and name "Record contribution"
```

The tests ask for the button by its accessible name, so renaming it broke them, as intended.

**Later green run:** the CI-evidence documentation commit `15e2e6f` ran green as well, in [run 42527732827](https://github.com/mdvar2/RondiTrack/actions/runs/42527732827).

**Green, on the second attempt.** The first green attempt on PR #11 (commit `39f4ed4`) passed `backend` and `frontend`. Its `e2e` job then failed while Compose was starting the stack, before Playwright ran:

```text
error mounting ".../src/Infrastructure/bin": mkdir .../src/Infrastructure/bin: read-only file system
```

- **The cause:** the `migrate` service bind-mounted the whole checkout read-only (`./:/src:ro`), then mounted named volumes over `API`, `Domain` and `Infrastructure` `bin/` and `obj/`.
  - Docker has to create those mount points inside the read-only mount.
  - That only worked where the checkout already had `bin/` and `obj/` folders from a local build. A fresh checkout doesn't have them, because they are git-ignored.
  - The mount was in the base `compose.yaml`, not a development override, so every environment used it.
- **The fix (commit `1ca4dcf`):** `migrate` is now built from a `migrate` stage in the `Dockerfile`, with the source and `dotnet-ef` 10.0.1 inside the image.
  - Nothing is mounted from the host, and the six `bin`/`obj` volumes are gone.
  - The runtime stage is unchanged, and `docker build .` never builds the new stage.
- **Checked before pushing:**
  - The error reproduced locally from a fresh clone of `39f4ed4`.
  - From a fresh clone of `1ca4dcf`, migrations ran on an empty database, a second run changed nothing, and Playwright passed 25/25.
  - The development stack kept its data: the same user count and the same 14 migrations.

### Bugs the tests found

Each of these was in the app before this assignment. None was a test mistake.

| Since | What a person saw | Cause | Fix |
|---|---|---|---|
| 7.4 | The form asked for **cents** and refused R 150,50: `amountInCents % 100 !== 0` allowed only whole rands | Parsing was buried in the Server Action | Rands input parsed by `parseRandsToCents` |
| 7.4 | After one success, every later contribution from the same form reused the same Idempotency-Key, so the API replayed the first response or refused with 409 | The key came from `useId()`, which never changes | Key held in a ref, replaced only after success (`ContributionForm.test.tsx`) |
| 8.x | With more than one cycle, the stokvel page recorded against, listed and offered to close the **oldest** cycle | `GET /cycles` is newest first, and the page took `cycles.at(-1)` | `pickCurrentCycle` (found by the 12-cycle performance seed) |
| 9.1 | Contribution amounts showed 100× too large (R 50 000,00 for R 500,00), in the proofs list and the activity feed | The API sends int64 cents; the page multiplied by 100 again | Use the cents as they are |
| 9.1 | "Download proof" was a 404 | It linked to `/bff/…/proof/download`; the route is `/bff/…/proof` | Correct link |
| 8.3 | Filtering activity "from 1 November" hid a contribution made at 00:30 SAST on 1 November | UTC day boundaries | `johannesburgDayRangeUtc` |
| 8.3 | A failed "Load more" replaced the whole list with an error, with no retry | `isError` was treated as "nothing loaded" | Keep the list; "Try again" (`ActivityFeed.test.tsx`) |

### Known gaps

- **INP is not under 200 ms on the 40-member stokvel.** After the 2026-11-09 follow-up it is 712 ms when you click straight after load (down from 1,216 ms, measured alternately), and 280 ms once the page is idle (no change beyond noise). Both are at 4× CPU slowdown. See [INP](#inp).
  - **Right after load,** the remaining cost is React redoing the interrupted hydration of the root and the summary: mostly Next.js and React themselves.
  - **Once idle,** it's opening the dialog: its render, plus the style and layout that focusing *Cancel* forces. Those vary 5× from run to run under CPU throttling.
  - **Paginating the proofs list won't help:** it was tested and doesn't.
  - **What's left to try:** server-render more of the summary as plain HTML, so there's less for a click to hydrate, and check on a real entry-level phone rather than emulation.
- **Home LCP, simulated, is 2.61 s today for both the old and new builds.** Applied throttling measures 1.91–1.93 s (all 20 runs under 2.5 s).
- **Stokvel page LCP (simulated) is 3.1 s.** The 2.5 s target is for the home screen, which meets it (2.44 s). The stokvel `<h1>` is its name, which needs the API.
- **The screen-reader pass (NVDA) hasn't been done.** NVDA isn't installed where this work was done. The steps, with the exact text to compare against, are in the [screen-reader checklist](../docs/history/assignment-9.2-screen-reader-checklist.md), and the two utterances above are still to be recorded.
- **The five-minute demo recording hasn't been made yet.**
- **The contribution form needs JavaScript.** The old one worked without JS through `useActionState`. The new one calls the action directly, to hold the key across retries.
- **The invite-status and login rate limits are per address.** All traffic comes through the Next.js server, so those limits are effectively shared by everyone.
- **Not done (optional stretch):** Lighthouse CI budgets.
- **Not checked:** 400% zoom (the reduced-motion rule is in place).
- **Lighthouse ran in headless Chromium** with Lighthouse's mobile emulation, not on a physical entry-level phone.
- **jsdom 30.1.3 declares Node `^24.15`.** Locally Node 24.14 gives an engine warning, and every test still passes. CI uses the latest Node 24.

## Assignment 9.1

### API check

The API check is now covered as follows:

1. Invites: `POST /api/v1/stokvels/{stokvelId}/invites`, `GET /api/v1/invites/{code}` and `POST /api/v1/invites/{code}/join`; unknown is 404, expired is 410 and an existing membership is 409.
2. Preview fields: only stokvel ID, name, contribution amount, schedule, rules and member count are serialized.
3. Proofs: `POST/GET/DELETE /api/v1/contributions/{contributionId}/proof` and `GET .../proof/download`; files are limited to 5 MB and checked by PDF/JPEG/PNG signatures.
4. Proof authorization: the uploader and a treasurer/chairperson of the contribution's stokvel may download; other users receive 404.
5. Close preview: `GET /api/v1/stokvels/{stokvelId}/cycles/{cycleId}/close/preview` returns member arrears, integer cents, South African date and a contribution-derived version.
6. Close mutation: `POST .../close` requires the preview version and `Idempotency-Key`; a changed contribution returns 409 and no arrears are written, while a repeated key returns the original result.

### Join flow

The flow lives at `/join?code=...&step=...`. The URL is the state machine, so reload, Back and the invite link preserve the code without asking for it twice.

| Step | Shows | Moves on | Guard and URL behavior |
|---|---|---|---|
| Code | Invite code and validation result | Successful preview | `code`; invalid/expired/429 remain inline |
| Rules | Name, amount, schedule, rules and member count | Continue | `step=rules`; preview is required |
| Confirm | The same preview facts and a clear join button | Server Action posts one join | `step=confirm`; no second code/details prompt |

`src/lib/join-state.ts` contains the discriminated union and transition function. Signed-out invite links use a validated local `next` path and return to the same invite URL after sign-in.

### Uploads

Proofs accept PDF, JPEG and PNG up to 5 MB. The browser checks type and size; the Next.js BFF streams the multipart body without exposing the API token; the API checks magic bytes and stores bytes in a private database table with a server-generated ID. Downloads use `nosniff` and `private, no-store`. The frontend hides controls for unauthorized users, but the API enforces the uploader/treasurer matrix and returns 404 for unauthorized or missing proofs.

| Hop | Limit | Over-limit behavior |
|---|---:|---|
| Browser | 5 MB | Inline file-input validation |
| Next.js BFF | Streamed request body | Backend response is passed through |
| API | 5 MB file, 6 MB request envelope | Field validation problem response |

### Closing a cycle

The close dialog consumes the API preview, formats server-provided integer cents using `formatCents`, starts focus on Cancel and keeps the dialog open while closing. The API locks the stokvel transaction, compares the contribution-derived preview version, writes arrears once and creates the next cycle. Stale previews return 409 and are refreshed without an automatic retry.

### Feedback

Invalid invite and file states are inline messages. Join uses a Server Action. Upload success uses a dismissible toast; unexpected upload/close HTTP 500 and network failures use persistent toasts with a correlation ID when supplied, or “Reference unavailable” otherwise. Authorization failures remain inline and do not sign the user out.

### Known gaps

The dedicated 9.1 integration tests are present but require Docker Desktop's PostgreSQL container. The active Docker context is `desktop-linux`, but its daemon is unavailable in this environment. The scheduled processor and manual close now share `CycleArrearsCalculator`; the custom dialog includes a keyboard focus trap and the app has persistent error toasts.

### Submission checklist

- Run the API and Next.js commands in the “Running it” section.
- Record: signed-out invite sign-in and Back navigation; 6 MB and renamed-executable rejection followed by a valid proof; uploader/treasurer/ordinary-member download authorization; stale close preview after a contribution; refreshed confirmation and exactly-once arrears.
- PR title: `Assignment 9.1: Join flow, proof of payment and safe confirmations`.

The Vite app in [`../web`](../web/README.md) is unchanged. For the project as a whole, both frontends and the API, see the [root README](../README.md).

## Assignment 8.3 additions

The Next.js app includes the Assignment 8.3 frontend work for live activity views and session-aware state:

- URL-based contribution-history filters are bound to the browser URL with `nuqs`, including search, status, and date-range controls.
- Recent activity is sourced from the ASP.NET Core contribution records for each member's current cycle rather than generated demo data. If a member or stokvel has no recorded contributions, the feed intentionally shows an empty state instead of inventing payments.
- Pinned stokvels are stored client-side in a shared Zustand store and surfaced at the top of the My Stokvels screen.
- South African date formatting remains centralised in the existing `src/lib/dates.ts` helpers, so cycle dates and activity timestamps stay in SAST.

## Assignment 8.2 status (verified)

The current Next.js app includes the remaining Assignment 8.2 work that was being completed in the previous pass:

- Parallel data loading on the stokvel detail route uses `Promise.all` and a `Suspense` boundary.
- Contribution recording is handled via a server action backed by `useActionState`, with validation, idempotency-key handling, and safe API error messages.
- Treasurer-only actions are gated against the role reported by the API for the specific stokvel.
- Silent refreshes are performed by the server only, with an in-flight guard that prevents concurrent refresh storms and a fresh `session` cookie written back after each refresh.

### Evidence run on 2026-11-01

| Check | Command | Result |
|---|---|---|
| Lint | `npm run lint` | Passed |
| Production build | `npm run build` | Passed |
| Automated tests | `npx vitest run` | 58 passed, 0 failed |
| Dependency audit | `npm audit --audit-level=low` | 0 vulnerabilities after the compatible `minimatch` override |

The current app is therefore functionally verified for lint/build/tests, but the dependency audit still reports upstream advisory issues in the dev-tooling chain rather than in the application runtime itself.

## Running it

You need Node 24 and the RondiTrack API running locally. In development it uses the `http` launch profile on **port 5101**.

```powershell
# 1. The API (from the repository root)
dotnet run --project API --launch-profile http

# 2. This app
cd web-next
npm ci
Copy-Item .env.example .env.local
# Put a fresh secret in .env.local:
node -e "console.log(require('crypto').randomBytes(32).toString('base64'))"
npm run dev        # http://localhost:3000
```

| Variable | What it is |
|---|---|---|---|
| `API_BASE_URL` | The API's address, e.g. `http://localhost:5101`. Only the Next.js server uses it. |
| `SESSION_SECRET` | 32 random bytes, base64-encoded. It encrypts the `session` cookie. |

**Why neither starts with `NEXT_PUBLIC_`:** Next.js copies every `NEXT_PUBLIC_` variable into the JavaScript it sends to the browser. `SESSION_SECRET` would then let anyone decrypt every session cookie and read the tokens inside. `API_BASE_URL` would tell the browser where the API is, but the point of this design is that the browser never calls the API. Both are read only by server code (`src/lib/session.ts` and `src/lib/api.ts`).

`.env.local` is git-ignored. Only `.env.example` is committed.

| Command | What it does |
|---|---|
| `npm run dev` | Development server on port 3000 |
| `npm run build` / `npm start` | Production build and server |
| `npm run lint` | ESLint (`eslint-config-next`) |
| `npm test` | Vitest unit tests (`src/**/*.test.ts`) |
| `npm run generate:api` | Regenerates `src/lib/schema.d.ts` from `http://localhost:5101/openapi/v1.json` |

Every version in `package.json` is pinned exactly, with no `^` or `~`. The project uses a safe `overrides` entry for `minimatch` to remove the transitive moderate advisories without forcing a breaking ESLint upgrade. `npm audit --audit-level=low` now reports 0 vulnerabilities. Vitest is 4.1.12 because 4.1.11 (the version `web/` uses) has two moderate advisories in `@vitest/mocker`.

**Port 3000 is shared** with the React/Vite app in `../web`. Run one frontend at a time.

### Dependencies

From `package.json`. The app was created with `create-next-app@16.4.1` (TypeScript, Tailwind, ESLint, App Router, `src/`, React Compiler, no AGENTS.md). `next.config.ts` sets `reactCompiler: true`, `cacheComponents: false` and `agentRules: false`.

| Package | Version | Used for |
|---|---|---|
| `next` | 16.4.1 | App Router, Server Components, Server Actions |
| `react`, `react-dom` | 19.3.0 | |
| `jose` | 6.2.13 | Encrypting the session cookie (`dir` + `A256GCM`) |
| `server-only` | 0.0.1 | Turns importing session or API code into a Client Component into a build error |
| `openapi-fetch` | 0.17.0 | Typed API client, generated from the API's OpenAPI document |
| `class-variance-authority`, `clsx`, `tailwind-merge` | 0.7.1, 2.1.1, 3.7.0 | Badge and button variants, copied from `web/` |
| `lucide-react` | 1.60.0 | Status-badge icons |
| `tailwindcss`, `@tailwindcss/turbopack` | 4.3.3 | Styling |
| `typescript` | 5.9.3 | |
| `eslint`, `eslint-config-next` | 9.39.5, 16.4.1 | `npm run lint` |
| `babel-plugin-react-compiler` | 1.0.0 | React Compiler |
| `openapi-typescript` | 7.13.0 | `npm run generate:api` |
| `vitest` | 4.1.12 | `npm test` |
| `@types/node`, `@types/react`, `@types/react-dom` | 20.19.43, 19.3.0, 19.3.0 | |

### Testing

| Command | Result on 27 Oct 2026 |
|---|---|
| `npm run lint` | No problems |
| `npm test` | **58 passed** in 7 files |
| `npm run build` | Succeeded; 7 routes |
| `npm audit` | 0 vulnerabilities |
| `dotnet test RondiTrack.Tests` (repository root, Docker running) | **145 passed**, including the 8 tests added for the API fixes below |

What the unit tests cover:

| File | Checks |
|---|---|
| `src/lib/treasurer.test.ts` | The treasurer filter: a mixed list, Treasurer nowhere, an empty list, exact role spelling |
| `src/lib/session.test.ts` | Encrypt/decrypt round trip; tampered, wrong-secret, wrong-shape and expired cookies all count as no session; no role is stored |
| `src/lib/api.test.ts` | `readOrFail`: data, 401 → `/login?expired=1`, 404 → `notFound()`, other errors → `ApiError` with the reference |
| `src/lib/errors.test.ts` | The 7.3 messages and the error digest |
| `src/lib/refresh-cookie.test.ts` | Reading the refresh token from the login response's `Set-Cookie` headers |
| `src/lib/money.test.ts`, `dates.test.ts` | Copied unchanged from `web/` |

On 27 Oct 2026 I also checked the signed-in pages with a local script, which isn't part of the repository. It signed three accounts in against the real API, built real session cookies and fetched every page from the production build. **98 checks passed, 0 failed.** The checks covered roles, the treasurer filter, the not-found pages, and that no token, nothing JWT-shaped and no mention of the API's port appears in any page's HTML.

## Security architecture

```
Browser ──(cookie: session, encrypted, HttpOnly, Secure, SameSite=Lax)──▶ Next.js server ──(Authorization: Bearer)──▶ API
```

| Concern | How it's handled | Where |
|---|---|---|
| Where tokens live | Only inside the encrypted `session` cookie. Browser JavaScript can't read it, and neither token appears in the page | `src/lib/session.ts` |
| Tampering | `jose` `dir` + `A256GCM` with `SESSION_SECRET`. A cookie that fails to decrypt, has expired or has the wrong shape counts as no session | `decrypt()` in `session.ts` |
| Who calls the API | Only server code. `api()` adds the bearer token from the session. No `NEXT_PUBLIC_` variable exists, so the browser doesn't know the API's address | `src/lib/api.ts` |
| Server code leaking to the browser | `import 'server-only'` in `session.ts` and `api.ts` makes that a build error (see [Server-only proof](#server-only-proof)) | |
| Client Component props | Only safe values cross the boundary: the login form's message, reference and email | `src/components/LoginForm.tsx` |
| Error details | In production only a digest reaches the browser. `ApiError` puts just the status and `correlationId` in it, never a stack trace | `src/lib/errors.ts`, `(app)/error.tsx` |
| Roles | Never stored in the session or read from the token. Each page uses the role the API returns for each stokvel, and the API still authorizes every request | `src/lib/treasurer.ts` |
| Hiding that a stokvel exists | A non-member, an unknown id and a malformed id all get the same 404 page, which shows nothing from the URL | `stokvels/[id]/not-found.tsx` |
| Signing out | Revokes the API's refresh session (the refresh token is sent as a `Cookie` header), then deletes the local session | `src/lib/auth-actions.ts` |

## API check

These were checked against the running API on port 5101 on 27 October 2026, using a Node script that calls the API the way this server does. Passwords and tokens were typed in or read from a file, and never printed.

| # | Question | Answer |
|---|---|---|
| 1 | Does the list give the caller's role in each stokvel? What about a user in no stokvels? | **`GET /api/v1/stokvels` is for platform admins only.** A normal user gets **403**, so it can't feed these pages. **`GET /api/v1/me/home`** returns each of the caller's stokvels with `role` (`Member`, `Treasurer` or `Chairperson`), plus the current cycle, `amountPaid`, `amountOwed`, `nextPayoutDate` and `arrearsCents`. A user in no stokvels gets **200 with `{"stokvels": []}`** (covered by `MemberHomeTests.NoMemberships_ReturnsEmptyList`). All three list pages use `/me/home`. |
| 2 | `GET /stokvels/{id}` as a non-member, for an unknown id and for `not-a-guid`: is the status the same? | **Yes, all three are 404.** A non-member and an unknown id get identical bodies (`"detail": "Stokvel not found."`). `not-a-guid` doesn't match the `{id:guid}` route, so it's a 404 with `"detail": "The requested endpoint was not found."`. **Decision:** the page ignores the body and treats every 404 the same way, `notFound()`, so all three show the same `not-found.tsx`. Signed out, all three are 401, because authentication runs before routing. |
| 3 | Which header carries the refresh token after login? | `Set-Cookie: __Secure-ronditrack_refresh=…; expires=<+7 days>; path=/api/v1/auth; secure; samesite=strict; httponly`. Node's `response.headers.getSetCookie()` returns it. A browser's `fetch` can't. The body is only `{ accessToken, expiresAtUtc }`. The server keeps the refresh token in the encrypted session and sends it back as a `Cookie` header on logout. |
| 4 | How long does the access token live? What happens after it expires? | **300 seconds (5 minutes)**, with 30 seconds of allowed clock skew (`Jwt:AccessTokenMinutes`, `Jwt:ClockSkewSeconds`). An expired token gets **401** with `WWW-Authenticate: Bearer` and a ProblemDetails body that has a `correlationId`. A missing or malformed token gets the same 401. Until Day 3 adds refresh, **about 5 minutes after signing in** the next page load sends the person to `/login?expired=1`. |
| 5 | Which IP does the login rate limit see? | The limit is 5 attempts per 60 seconds **per remote IP**. The 6th attempt got `429` with `Retry-After: 60`. Every sign-in now comes from the Next.js server, so the API sees one IP for everyone. See [Known gaps](#known-gaps). |
| 6 | Does `GET /stokvels/{id}` return the member count and a summary of the current cycle? | **No, so the API was fixed.** `memberCount` was **always 0**: it counted `Stokvel.Members`, a navigation that EF Core ignores (`RondiTrackDbContext`). Demo Savings returned 0 with 2 members, and Emergency Fund returned 0 with 1. There was also no cycle summary and no caller role. |

### API fixes in this PR
- **`GET /api/v1/stokvels/{id}`** now returns `StokvelDetailResponse` (`API/Services/StokvelDetailService.cs`). It keeps the original fields (`id, name, contributionAmount, memberCount, version`) and adds `contributionAmountCents`, `role` (the **caller's** role in this stokvel) and `currentCycle { period, status, dueDate, membersPaid, collectedCents, targetCents }`. The current cycle follows the same rule as `/me/home`: this Johannesburg month, preferring an Open cycle. `memberCount` is now counted from `StokvelMembers`. Authorization is unchanged: the `StokvelView` policy, with 404 for non-members.
- **`GET /api/v1/me`** (new) returns `{ id, name, email, version }` for the caller. Sign-in uses it to store who the person is. It has no role.
- **`POST /api/v1/auth/login`** OpenAPI metadata: it declared only its 429, which stopped ASP.NET from inferring the 200, so the generated type for a successful login body was `undefined`. It now declares `200 AccessTokenResponse`, `401` and `429`. Login's behaviour hasn't changed.
- Tests: `RondiTrack.Tests/StokvelDetailTests.cs` (7) and `LoginContractTests.cs` (1). The backend suite passes: **145 passed, 0 failed**.

## Routes

```
src/app
├── page.tsx                      /   → /dashboard/member if signed in, else /login
├── (auth)/login/page.tsx         /login   (Server Component) + LoginForm (client)
└── (app)/                        signed in
    ├── layout.tsx                sidebar: 4 links, who's signed in (<Suspense>), Sign out
    ├── loading.tsx               skeleton while a page awaits the API
    ├── error.tsx                 7.3 message + reference + Try again (retry)
    ├── dashboard/member/page.tsx
    ├── dashboard/treasurer/page.tsx
    ├── stokvels/page.tsx
    └── stokvels/[id]/page.tsx + not-found.tsx
```

| Route | API call (server only) | Shows | When there's nothing, or it isn't theirs |
|---|---|---|---|
| `/` | none; reads the session cookie | Redirect | — |
| `/login` | `POST /auth/login`, then `GET /me` (Server Action) | Email and password form. A wrong password shows "Email or password is incorrect." with the `correlationId`. `?expired=1` shows "Your session has expired." | — |
| `/dashboard/member` | `GET /me/home` | Totals paid and owed this cycle, then each stokvel with cycle, paid, still owed and next payout ("No payout scheduled" while the API returns `null`) | Empty state: "You're not in any stokvels yet" |
| `/dashboard/treasurer` | `GET /me/home`, filtered | Only the stokvels where `role === 'Treasurer'`, each linking to `/stokvels/{id}` | Empty state linking to the member dashboard. Never a 403 |
| `/stokvels` | `GET /me/home` | 7.1 `StokvelCard`s with "You're the {role}", each linking to its detail page | Empty state |
| `/stokvels/[id]` | `GET /stokvels/{id}` | Name, contribution, the person's role in this stokvel, member count, and the current cycle (status, due date, members paid of total, collected of target) | `not-found.tsx`: "We couldn't find that stokvel" |

**How data flows.** Each page calls `api()` from `src/lib/api.ts`. It reads the session (or redirects to `/login`) and returns a typed `openapi-fetch` client that sends `Authorization: Bearer …`. `readOrFail(result)` then turns the result into data or the right response:

| API status | What happens |
|---|---|
| 2xx | The page gets typed data |
| 401 | `redirect('/login?expired=1')` |
| 404 | `notFound()`, so the nearest `not-found.tsx` |
| anything else | throws `ApiError`, shown by `(app)/error.tsx` |

**Errors keep their reference in production.** In a production build, Next.js replaces the message of an error thrown in a Server Component before it reaches the browser, and keeps only `error.digest`. If an error already has a digest, Next.js keeps it. So `ApiError` sets `digest = "RONDITRACK_API;<status>;<correlationId>"`. Those two values are safe to show, and `error.tsx` reads them back to show the 7.3 message and its reference. A 403 shows "You don't have permission to do that."; a 500 shows "Something went wrong. Please try again." Both come with the reference and a **Try again** button that calls `retry()`. Neither shows a stack trace.

**Status codes while streaming.** Because `(app)/loading.tsx` exists, a page starts streaming before its fetch finishes. So a `notFound()` or `redirect()` arrives inside an HTTP **200** response: a `<meta name="robots" content="noindex">` tag and the not-found UI, or a `NEXT_REDIRECT` instruction. That's expected in Next.js 16.

**Signed out.** `/` answers with a real `307` to `/login`. The (app) routes stream a redirect to `/login`. A cookie that fails to decrypt counts as no session.

## The treasurer dashboard

RondiTrack has no "treasurer users". A person is **Treasurer of some stokvels** and Member or Chairperson of others, all with one login. So `/dashboard/treasurer` isn't a door with a role check on it. **Everyone may open it.** It shows the stokvels where *this* person is Treasurer, and for most people that list is empty.

- **The filter is a pure function:** `stokvelsWhereTreasurer(list)` in [`src/lib/treasurer.ts`](src/lib/treasurer.ts), with Vitest tests in [`treasurer.test.ts`](src/lib/treasurer.test.ts) for a mixed list, a list where the person is Treasurer nowhere, an empty list, and exact role spelling.
- **The role comes from the API's answer for each stokvel** (`/me/home` → `stokvels[].role`), never from the session or the token.
- **The session doesn't store a role at all, and can't.** A single role per person doesn't exist: Lerato is Treasurer of her family stokvel and Member of her work stokvel at the same moment. A role cached in the cookie would also go stale as soon as a Chairperson changes it, and it would stay wrong for up to the cookie's 7-day life. The API's access token has no role claim either (`sub, jti, iat, nbf, exp, iss, aud`). The API checks membership and role per stokvel on every request, which is why the page is safe whatever the browser sends. A unit test (`session.test.ts`) checks that the session holds only `accessToken`, `refreshToken` and `user`.
- **The empty state is useful, not an error.** It says "You don't run any stokvels yet" and links to the member dashboard. It isn't a 403, a redirect or an alert.
- **The sidebar shows all four links to everyone** (Home, Member dashboard, Treasurer dashboard, My stokvels). It doesn't hide "Treasurer". If a later version hid it for people who are Treasurer nowhere, that would only be a **convenience**: the page would still be safe, because the API decides what each person may see.

## The client boundary

Only these files have `'use client'`. Everything else, including `StokvelCard`, `StatusBadge`, the layout and all the pages, runs on the server and sends no JavaScript of its own.

| File | Why it needs `'use client'` |
|---|---|
| `src/components/LoginForm.tsx` | `useActionState` keeps the sign-in Server Action's result (message and reference) and the pending state between submissions |
| `src/components/DueCountdown.tsx` | `useState` + `useEffect` set a timer to re-render the countdown at Johannesburg midnight |
| `src/app/(app)/error.tsx` | Error boundaries must be Client Components. Its **Try again** and **Copy** buttons use `onClick` |

What else changed from `web/`:
- **Badge and Button.** The shadcn `Badge` in `web/` renders through Base UI's `useRender` hook, which can't run in a Server Component. The web-next copies keep the same variant classes on a plain `<span>` and `<button>`. That lets `StatusBadge`, `StokvelCard` and the Sign out form stay on the server.
- **Sign out** is a `<form action={signOut}>` in the Server Component layout, so it needs no client code.

**Only names cross the boundary.** No Client Component receives the session. `LoginForm` gets no props, and its action state holds only `{ message, correlationId?, retryAfter?, email }`. The layout renders `session.user.name` and `.email` as plain text on the server. Neither `session.ts` nor `api.ts` is imported by any file with `'use client'`. `auth-actions.ts` is a `'use server'` module, so a Client Component that imports it gets only a reference to the action, never its code.

## Server-only proof

`src/lib/session.ts` and `src/lib/api.ts` both start with `import 'server-only'`. To prove the guard works, `getSession` was imported into the `DueCountdown` Client Component:

```tsx
// src/components/DueCountdown.tsx (temporary)
'use client'
import { useEffect, useState } from 'react'
import { getSession } from '@/lib/session'
void getSession
```

`npm run build` **failed**:

```
> Build error occurred
Error: Turbopack build failed with 3 errors:
...
./src/lib/session.ts:1:1
Error: 'server-only' cannot be imported from a Client Component module
> 1 | import 'server-only'
    | ^^^^^^^^^^^^^^^^^^^^
  2 | import { cookies } from 'next/headers'
  3 | import { EncryptJWT, jwtDecrypt } from 'jose'
  4 |

It should only be used from a Server Component.

Import traces:
  Server Component:
    ./src/lib/session.ts
    ./src/app/(app)/layout.tsx

  Client Component Browser:
    ./src/lib/session.ts [Client Component Browser]
    ./src/components/DueCountdown.tsx [Client Component Browser]
    ./src/components/DueCountdown.tsx [Server Component]
    ./src/app/(app)/stokvels/[id]/page.tsx [Server Component]

  Client Component SSR:
    ./src/lib/session.ts [Client Component SSR]
    ./src/components/DueCountdown.tsx [Client Component SSR]
    ./src/components/DueCountdown.tsx [Server Component]
    ./src/app/(app)/stokvels/[id]/page.tsx [Server Component]
```

The other two errors point at the same import from `next/headers` and `server-only` ("This API is only available in Server Components in the App Router").

With the import removed, `npm run build` **passed**:

```
▲ Next.js 16.4.1 (Turbopack)
✓ Compiled successfully
  Finished TypeScript ...
✓ Generating static pages using 7 workers (8/8)

Route (app)
┌ ƒ /
├ ○ /_not-found
├ ƒ /dashboard/member
├ ƒ /dashboard/treasurer
├ ƒ /login
├ ƒ /stokvels
└ ƒ /stokvels/[id]
```

**Without the guard,** `session.ts` and everything it imports would have been bundled into the browser JavaScript for `DueCountdown`. That includes the code that decrypts the cookie, and with it whatever `SESSION_SECRET` handling the bundler could inline. That moves secret-handling code onto every visitor's device and puts the server's session logic in public view. It would also only fail at runtime (`cookies()` can't run in a browser) instead of failing at build time.

## Known gaps

- **One IP for every sign-in.** The API limits login to 5 attempts per 60 seconds per remote IP. Every sign-in now comes from the Next.js server, so the API sees that one address for all users. Five people signing in within the same minute, or one person mistyping five times, locks **everyone** out for a minute (`429`, "Too many requests"). The fix, which is a stretch challenge and not done here, is to forward the person's IP from the Next.js server and have the API trust that header only from the Next.js server, as a known proxy.
- **No refresh until Day 3.** The access token lives 5 minutes. After that, the next page load goes to `/login?expired=1`, even though the session cookie still holds a valid 7-day refresh token. The old cookie stays until the next sign-in replaces it (a Server Component can't delete cookies).
- **Next payout date.** `/me/home` always returns `nextPayoutDate: null` because the API has no payout-scheduling rule yet. The pages show "No payout scheduled" rather than invent a date.
- **`memberCount` elsewhere.** The admin-only `GET /api/v1/stokvels` and the `POST /api/v1/stokvels` response still use `StokvelResponse.FromEntity`, so their `memberCount` is still 0, for the same EF root cause as above. This frontend doesn't use either.
- **No route protection layer yet.** Pages redirect to `/login` when there's no session. A `proxy.ts` check comes on Day 3. The API is still the real gate.
- **Theme.** Dark mode follows the operating system. The 7.2 theme switcher wasn't ported.
