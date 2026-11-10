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

The first stage-1 red run ([42682476890](https://github.com/mdvar2/RondiTrack/actions/runs/42682476890), `0f461c5`) is **not** used as evidence. It was built on the same Windows-generated document, so it would have failed without the rename. The red branch was brought up to date with a merge (no history rewritten), and stage 1 was run again.

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

The Stage 1 error text is from the same tree run locally (`c8321f8`, Release). GitHub lets only repository admins download job logs through the API. The CI job shows the same failing step, and the only difference from the green `2894149` is the one-line rename. Screenshots of each job page are to be added under `docs/history/evidence/assignment-9.3/`.

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
