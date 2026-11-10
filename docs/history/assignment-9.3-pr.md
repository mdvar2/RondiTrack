# Assignment 9.3: Deploy & Demo Day

Evidence for each part, recorded as it lands. Implementation PR: [#13](https://github.com/mdvar2/RondiTrack/pull/13) (`assignment-9.3` → `assignment-9.2`). Red CI evidence PR: [#12](https://github.com/mdvar2/RondiTrack/pull/12) (`assignment-9.3-contract-red` → `assignment-9.3`, **do not merge**).

## Part 1: The contract, checked in CI

### What was built

| Piece | File | What it does |
|---|---|---|
| Committed contract | [`openapi/v1.json`](../../openapi/v1.json) | The document the API serves, minus `servers` (which echoes the request host). |
| API contract test | [`RondiTrack.Tests/OpenApiContractTests.cs`](../../RondiTrack.Tests/OpenApiContractTests.cs) | Fetches `/openapi/v1.json` from the running API (Testcontainers PostgreSQL) and fails when it differs from the committed file, listing every differing JSON path. It also fails if the served document contains a carriage return. |
| Regenerate command | Same file, top comment; root README "The API contract" | `OPENAPI_UPDATE=1 dotnet test RondiTrack.Tests --filter OpenApiContractTests`, then `cd web-next && npm run generate:api`. |
| Types from the committed file | `web-next/package.json` `generate:api` | `openapi-typescript ../openapi/v1.json -o src/lib/schema.d.ts` (it used to read the live API on `localhost:5101`). |
| Type-drift check | [`.github/workflows/ci.yml`](../../.github/workflows/ci.yml), frontend job | Regenerates the types and runs `git diff --exit-code -- src/lib/schema.d.ts` **before** lint and tsc, with an `::error` annotation naming the fix. |

The first regeneration showed the previously committed `schema.d.ts` was already stale: it lacked the 9.1 proof, close-preview/close and invite endpoints. It was regenerated (additions only).

### The first CI failure, and its fix

The first run of PR #13 ([42682594025](https://github.com/mdvar2/RondiTrack/actions/runs/42682594025), commit `337b2a6`) failed in the backend job's "Run automated tests" step. Root cause, reproduced locally:

- The API builds its operation descriptions from XML doc comments, and those carry the **build machine's** newline.
- `openapi/v1.json` had been generated on Windows, so 20 operation descriptions contained `\r\n`. The Linux CI build serves `\n`, so the contract test failed. It was right to fail: the document depended on the OS that built the API.
- The cause was not the Git checkout. A worktree checked out with LF line endings on Windows still produced `\r\n`, while a Linux container build of the same commit served `\n`.

Fix (commit `2894149`), without weakening the test:

- The OpenAPI pipeline in [`API/Program.cs`](../../API/Program.cs) now normalises summaries and operation, parameter, response and schema descriptions to `\n` ([`API/Common/OpenApiText.cs`](../../API/Common/OpenApiText.cs)).
- The committed document was regenerated. Exactly the 20 description lines changed, and the frontend types are unchanged.
- The test also asserts there is no `\r` in the served document, so the same mistake now fails on Windows too.
- Check: the regenerated file equals, as parsed JSON, the document served by a Linux container build of both `337b2a6` and `2894149`.

> **Excluded run.** The first stage-1 red run ([42682476890](https://github.com/mdvar2/RondiTrack/actions/runs/42682476890), `0f461c5`) is **not** used as evidence. It was built on the same Windows-generated document, so the cross-platform newline defect would have failed it even without the rename. The red branch was brought up to date with a merge commit (`c8321f8`; no history rewritten), and stage 1 was run again. Both first runs are kept as screenshots 07 and 08 below.

### Green: the implementation

| Commit | Run | Backend | Frontend | E2E |
|---|---|---|---|---|
| `2894149` | [42762183487](https://github.com/mdvar2/RondiTrack/actions/runs/42762183487) | [success](https://github.com/mdvar2/RondiTrack/actions/runs/42762183487/job/129650230442) | [success](https://github.com/mdvar2/RondiTrack/actions/runs/42762183487/job/129650230128) | [success](https://github.com/mdvar2/RondiTrack/actions/runs/42762183487/job/129650756348) |

### Red: three stages on PR #12, each pushed after the previous run finished

The field renamed is `ContributionWithMemberResponse.amount` → `amountCents`, which the stokvel page shows.

| Stage | Commit | Run | Failed job and step | Error (from CI annotations, or the identical tree run locally) |
|---|---|---|---|---|
| 1. API change alone | `c8321f8` | [42762756856](https://github.com/mdvar2/RondiTrack/actions/runs/42762756856) | [Backend](https://github.com/mdvar2/RondiTrack/actions/runs/42762756856/job/129652095682): "Run automated tests". Frontend passed, E2E skipped. | `OpenApiContractTests.ServedDocument_MatchesCommittedContract` failed, 1 of 161:<br>`/components/schemas/ContributionWithMemberResponse/required/7: "amount" in openapi/v1.json, "amountCents" served`<br>`/components/schemas/ContributionWithMemberResponse/properties/amount: in openapi/v1.json, missing from the running API`<br>`/components/schemas/ContributionWithMemberResponse/properties/amountCents: served by the running API, missing from openapi/v1.json` |
| 2. Document updated, types not | `d6dbedb` | [42763032333](https://github.com/mdvar2/RondiTrack/actions/runs/42763032333) | [Frontend](https://github.com/mdvar2/RondiTrack/actions/runs/42763032333/job/129652966210): "API types match openapi/v1.json (type drift)". Backend passed, E2E skipped. | CI annotation on `web-next/src/lib/schema.d.ts`: `Generated types differ from openapi/v1.json. Run 'npm run generate:api' in web-next and commit src/lib/schema.d.ts.` The diff is `-amount: number;` / `+amountCents: number;`. |
| 3. Types regenerated | `9556024` | [42763290977](https://github.com/mdvar2/RondiTrack/actions/runs/42763290977) | [Frontend](https://github.com/mdvar2/RondiTrack/actions/runs/42763290977/job/129653789646): "Typecheck (route types first)", exit 2. Drift check and lint passed; backend passed, E2E skipped. | tsc names every line that still reads the old name (CI annotations at lines 46, 30, 31):<br>`src/lib/activity.ts(46,20): error TS2339: Property 'amount' does not exist on type …`<br>`src/components/StokvelView.test.tsx(30,33): error TS2353: Object literal may only specify known properties, and 'amount' does not exist …`<br>`src/components/ContributionList.tsx(31,90): error TS2339: Property 'amount' does not exist on type …` |

Where the errors come from:
- **Stages 2 and 3:** CI's own annotations (screenshots 05 and 06).
- **Stage 1:** the error text is from the same tree run locally (`c8321f8`, Release). Its CI annotation says only "Process completed with exit code 1", and GitHub shows job logs only to signed-in users. The CI job fails at the same step (screenshot 02), and the only difference from the green `2894149` is the one-line rename.

### Screenshots

Folder: [`docs/history/evidence/assignment-9.3/`](evidence/assignment-9.3/)
- 01–06 were taken on 10 Nov 2026 from the public run pages, signed out, so they show no account details.
- 07–08 were taken by the author before the fix.

| # | File | Shows |
|---|---|---|
| 01 | [green run](evidence/assignment-9.3/01-green-run-42762183487-2894149.png) | PR #13, `2894149`: Success, all three jobs green |
| 02 | [stage 1](evidence/assignment-9.3/02-red-stage1-backend-42762756856-c8321f8.png) | PR #12, `c8321f8`: Backend failed at "Run automated tests"; Frontend green; E2E skipped |
| 03 | [stage 2](evidence/assignment-9.3/03-red-stage2-frontend-drift-42763032333-d6dbedb.png) | PR #12, `d6dbedb`: Frontend failed at "API types match openapi/v1.json (type drift)"; lint and tsc skipped; Backend green |
| 04 | [stage 3](evidence/assignment-9.3/04-red-stage3-frontend-tsc-42763290977-9556024.png) | PR #12, `9556024`: Frontend failed at "Typecheck (route types first)"; drift and lint green; Backend green |
| 05 | [stage 2 annotations](evidence/assignment-9.3/05-red-stage2-summary-annotations-42763032333-d6dbedb.png) | The drift error on `web-next/src/lib/schema.d.ts` |
| 06 | [stage 3 annotations](evidence/assignment-9.3/06-red-stage3-summary-annotations-42763290977-9556024.png) | The three tsc errors on `amount` |
| 07 | [excluded: PR #13 first run](evidence/assignment-9.3/07-excluded-first-run-pr13-337b2a6-backend-failed.png) | PR #13 at `337b2a6`, before the newline fix: Backend failing |
| 08 | [excluded: PR #12 first run](evidence/assignment-9.3/08-excluded-first-run-pr12-0f461c5-backend-failed.png) | PR #12 at `0f461c5`, before the newline fix: Backend failing (not used as stage-1 evidence) |

### Local verification

| Check | Tree | Result |
|---|---|---|
| `dotnet test RondiTrack.Tests` (Release) | `2894149` | 161 passed, 0 failed |
| `npm run check:api-types` | `2894149` | exit 0 |
| `npm run lint`, `tsc --noEmit` | `337b2a6` | clean |
| Vitest, `TZ=UTC` and `TZ=Africa/Johannesburg` | `337b2a6` | 171 passed in each |
| `npm run build` | `337b2a6` | succeeded |
| `npm audit --omit=dev` | `337b2a6` | 0 vulnerabilities |
| New dependencies | both | none |

`2894149` changed no frontend file. CI's frontend job (lint, tsc, Vitest in both time zones) and the E2E job, which runs `npm run build`, passed on it.

### The old contract copy

`clients/typescript/` (Assignment 6.3) has its own `openapi.json`. It is kept unchanged as that assignment's evidence. Its README now says it is a frozen snapshot and that `openapi/v1.json` is the only contract. Nothing in CI, the tests or either frontend reads it, and its own `check` and `test:money` still pass.

## Part 2: One trace, browser to database

Local evidence only. Every trace below was captured on 10 Nov 2026 from a **throwaway Compose project** (`ronditrack-trace`). It had its own new database volume, synthetic data from `web-next/scripts/seed-perf.mjs`, and credentials generated for the run that were never written to the repo. It **does not prove the deployed environment works**; Part 5 repeats this against the deployed URLs.

### What was built

| Piece | File | What it does |
|---|---|---|
| Next.js joins the trace | [`web-next/src/instrumentation.ts`](../../web-next/src/instrumentation.ts) | `@vercel/otel` through Next's instrumentation hook. `traceparent` is propagated **only** to `API_BASE_URL/` (by default it goes only to Vercel deployment URLs, so it has to be enabled explicitly). |
| One reference | [`web-next/src/lib/trace.ts`](../../web-next/src/lib/trace.ts) | Every server-side call to the API (the three typed clients, both `/bff/` routes, the invite calls) goes through `apiFetch`. If the API never answers, it returns a 503 problem whose `correlationId` is the Next.js trace id. If the API answered, the reference is the API's `correlationId`, which is the same trace. |
| Refresh keeps the session | [`web-next/src/lib/proxy.ts`](../../web-next/src/lib/proxy.ts) `rotateSession` | Throws when the API is unreachable or answers 5xx or 429, so the member stays signed in. Returns `null` (sign in again) only when the API refuses the token. Before this, a 500 during refresh signed the member out. |
| Unexpected errors | `instrumentation.ts` `onRequestError` | Records Next's error digest (never the message) on the request span as `ronditrack.error.digest`. **Not yet exercised against a live error**; to be verified with Part 3's 500 flow. |
| Idempotency outcome | [`API/Services/ContributionService.cs`](../../API/Services/ContributionService.cs) | A `contribution.record` span per attempt, whose only attribute is `ronditrack.idempotency.outcome` (`recorded`, `replayed`, `key_reused` or `rejected`). |
| Job trace | [`API/BackgroundJobs/CycleCloseProcessor.cs`](../../API/BackgroundJobs/CycleCloseProcessor.cs) | Each run is a new root span `cycle-close.run`, ignoring any ambient Activity. It carries the Johannesburg date, the time zone and the number of stokvels due, and logs `Cycle-close run {TraceId} for Johannesburg date {LocalDate}`. Per-stokvel spans are its children; before this, each stokvel was its own trace. |
| Local backend | [`compose.yaml`](../../compose.yaml) `jaeger` | `jaegertracing/jaeger:2.22.0`, profile `observability`, ports bound to 127.0.0.1, in-memory storage. |

New packages, all pinned and published on or before 27 Oct 2026; `npm audit --omit=dev` reports 0:

| Package | Version | Published |
|---|---|---|
| `@vercel/otel` | 2.1.3 | 2026-06-11 (2.1.4 and later are under two weeks old) |
| `@opentelemetry/api` | 1.9.1 | 2026-03-25 (1.10.0 is outside `resources@2.12.0`'s peer range) |
| `@opentelemetry/resources`, `sdk-metrics`, `sdk-trace-base` | 2.12.0 | 2026-10-06 |
| `@opentelemetry/api-logs`, `sdk-logs`, `instrumentation` | 0.223.0 | 2026-10-06 |
| `@opentelemetry/semantic-conventions` (override) | 1.43.0 | 2026-07-09 (1.45.0 was resolved first: 4 days old) |
| `import-in-the-middle` (override) | 3.5.3 | 2026-10-26 (3.6.0 was resolved first: 4 days old) |
| Docker `jaegertracing/jaeger` | 2.22.0 | 2026-10-06 (2.23.0 is 5 days old) |

### Evidence

Folder: [`docs/history/evidence/assignment-9.3/part2/`](evidence/assignment-9.3/part2/). Each trace's raw Jaeger JSON is beside its screenshot.

| # | What | Result |
|---|---|---|
| 09, 10 | **A reference on screen finds one trace, browser to database.** The treasurer recorded a contribution for a member who had already paid; the form showed the API's refusal with `Reference: 8db38ad24f4ddc74913d16a7f9d46a4f`. | Jaeger, looked up by that id, finds exactly one trace: 18 spans across `ronditrack-web` and `ronditrack-api`. The tree is `POST /stokvels/[id]` (Next.js) → `fetch POST …/contributions` (Next.js) → `POST api/v1/stokvels/{stokvelId:guid}/members/{userId:guid}/contributions` (API) → `contribution.record` (outcome `rejected`) → 7 `postgresql` spans, plus 3 `postgresql` spans before it. |
| 11, 12 | **A retry with the same Idempotency-Key writes nothing.** API level: two requests sent directly to the API with the same key, each with its own `traceparent`. | First, trace `cfed822f7203a82dd4bf08a6fda701e4`: 201, 18 spans, outcome `recorded`, 16 SQL statements including `INSERT INTO "Contributions"` and `INSERT INTO "PersistedIdempotencyRecords"`. Retry, trace `7db1002ba926b8d969d5ff9510f701ea`: 201 with the same body, 7 spans, outcome `replayed`, 5 SQL statements and **no INSERT**. The database holds exactly 1 row for that member and cycle. Jaeger marks both traces "Incomplete" because their parent span came from the command line and was never exported. |
| 13, log | **The cycle-close job is its own trace.** A synthetic stokvel with a cycle that ended on 2026-10-31 was created in the throwaway database; the next worker run closed it. | Trace `55010043b2e8e07342e74c0cc95ec03a`: root `cycle-close.run` with **no parent reference**, `ronditrack.job.local_date=2026-11-10`, `ronditrack.job.time_zone=Africa/Johannesburg`, `ronditrack.cycle_close.stokvels_due=1`; children are 1 `stokvel.cycle_close` and 11 `postgresql` spans. Log: `Cycle-close run 55010043b2e8e07342e74c0cc95ec03a for Johannesburg date 2026-11-10 (UTC now 2026-11-10T06:10:33.6693533+00:00).` |
| 14, 15 | **No answer still has a reference.** The throwaway API container was stopped after sign-in, then a contribution was submitted. | The form showed `Reference: 34e26b1ee2534cb19733aa83b4423982`. Jaeger finds that trace: `ronditrack-web` only, 6 spans, with the `fetch POST …/contributions` span marked as an error. |

Still to do for Part 2: **the retry through the UI**, where the first attempt fails as if the connection dropped and the form retries with the same key. It waits on the approved demonstration design, so the retry evidence above is API level only.

### Privacy: what was checked in a real trace

Every attribute of every span in trace `8db38ad2…` was listed (by key, across both services), and the whole trace JSON was searched for each seeded first name and surname, the treasurer's email domain, the amount (`50000`, `500,00`, `500.00`), `Bearer`, `eyJ` (JWT), `Cookie`, `password`, the session cookie name and the Idempotency-Key.

- No member data: the only match was the table name `"PersistedIdempotencyRecords"` in SQL text, where the key is the placeholder `@key`.
- Attribute values are IDs (stokvel and user GUIDs in URLs and routes), HTTP methods and status codes, SQL text with `@placeholders`, and the RondiTrack attributes listed in the README.
- No exception messages and no baggage.
- Two findings to keep in mind:
  - `db.npgsql.data_source` includes the database host and username (`Host=db;…;Username=ronditrack`) but never the password, because Npgsql strips it.
  - The Next.js process is tagged `cloud.provider=vercel` even locally (from `@vercel/otel`).
- The cycle-close trace contains neither the stokvel's name nor its amount.

Automated: `DistributedTracingTests.ContributionTraces_CarryNoMemberDataAmountsOrKeys` fails if a span or baggage contains the payer's name, email, amount (three formats), Idempotency-Key or `Bearer`. It was checked against a deliberate mutation (the payer's name added as a span attribute), and it failed with `Found: "Nomvula"`.

### Tests added

| Test | Checks |
|---|---|
| `DistributedTracingTests.RetryWithSameKey_EachRequestContinuesItsCallersTrace_OnlyTheFirstWrites` | Each request continues its caller's trace; outcomes `recorded` and `replayed`; INSERT only in the first trace; 1 row. |
| `DistributedTracingTests.ContributionTraces_CarryNoMemberDataAmountsOrKeys` | As above. |
| `DistributedTracingTests.CycleCloseRun_IsItsOwnRootTrace_WithTheJohannesburgDateInTagAndLog` | The run has no parent even inside an ambient Activity, which is restored afterwards; the SAST date is in the tag and the log, keyed by trace id; per-stokvel spans share the run's trace. |
| `web-next/src/lib/trace.test.ts` (9) | Real answers pass through; a dropped connection becomes a 503 problem with the trace id; abort still rejects; it reaches `error.tsx` as `RONDITRACK_API;503;<trace id>`; `currentTraceId` ignores invalid spans; `rotateSession` throws on drop, 500 and 429, and returns `null` only on refusal. |

Local results: API **164 passed** (Release); Vitest **180 passed** in UTC and in Africa/Johannesburg; type drift, lint, tsc and `npm run build` pass; `npm audit --omit=dev` 0 vulnerabilities.

CI (PR #13): implementation `6b3b776`, docs `1bc7f3e`. [Run 42767582768](https://github.com/mdvar2/RondiTrack/actions/runs/42767582768) on `1bc7f3e` passed: [backend](https://github.com/mdvar2/RondiTrack/actions/runs/42767582768/job/129667589324), [frontend](https://github.com/mdvar2/RondiTrack/actions/runs/42767582768/job/129667589636) and [end to end](https://github.com/mdvar2/RondiTrack/actions/runs/42767582768/job/129668139776). The e2e job ran the Playwright suite against a production build with the instrumentation in place, with no exporter configured.
