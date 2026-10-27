# Assignment 7.4 — Record a Contribution

| | |
|---|---|
| Official title | "Record a Contribution", as in the README heading. The brief isn't in the repository. |
| Date | 26 October 2026 |
| Final commit on `main` | [`e063646`](https://github.com/mdvar2/RondiTrack/commit/e063646) "Assignment 7.4: Record a contribution" |
| Branch / PR | `assignment-7.4`, [PR #5](https://github.com/mdvar2/RondiTrack/pull/5) (merged 26 Oct 2026) |
| README as submitted | [README at `e063646`](https://github.com/mdvar2/RondiTrack/blob/e063646/README.md) |
| Re-tested | **Yes, partly, 27 Oct 2026.** `web/` today is byte-for-byte the `e063646` code: `npm test -- --run` passed 63 and lint was clean. The backend tests of this commit weren't re-run separately (the current suite, which adds 8.1's tests, passes 145) |

## 1. Purpose and learning objectives
To make the first **write** from the frontend safe: a member records a payment, and retries can never record it twice. The brief's learning objectives aren't recorded.

## 2. Original requirements
The brief isn't in the repository. The record below quotes parts of it:
- The assignment named the route `/cycles/{cycleId}/contributions`. I kept the API's real route and explained why.
- Six **idempotency contract questions** were answered against the real API, each with a test (see [Idempotency contract](#idempotency-contract)).
- An **idempotency proof**: a deliberate mistake, its failing output and the restored passing output.
- A decision on whether recording should be optimistic.
- A screen recording, which **wasn't done**.
- Stretch challenges 1–4 (only partly considered).

## 3. What I implemented
See [What I built](#what-i-built) below:
- **API:** `amountInCents` replaced the decimal `amount`, and contributions to a closed cycle are refused (`409`).
- **Money:** `parseRandsToCents` and `centsToInput`, using only whole-number arithmetic.
- **The form:** `ContributionForm` (React Hook Form + Zod + `useMutation`). A Treasurer or Chairperson can choose a member.
- **Idempotency keys:** `useRecordContribution`, which keeps one key per intended payment.

## 4. Technologies, tools and dependencies
New in `web/package.json` at `e063646`: **msw 2.15.0**, for mocking the API in tests. The form libraries were added in 7.3.

## 5. Prerequisites and installation
As for [7.3](assignment-7.3.md#5-prerequisites-and-installation).

## 6. Environment configuration
As for 7.3. The API's CORS must allow the `Idempotency-Key` header, which it does since 6.3.

## 7. How to run or reproduce
**As submitted:** the 7.3 run steps, then open an Open cycle and record a payment.
- **Backend tests:** `dotnet test RondiTrack.Tests`.
- **Frontend tests:** `npm test -- --run` in `web/`.

**In the current codebase:** `web/` hasn't changed since this commit. See [web/README.md](../../web/README.md).

## 8. Important files and components
- `web/src/components/ContributionForm.tsx`, `web/src/contributions/useRecordContribution.ts` (+ `recordContribution.test.ts`), `web/src/lib/money.ts` (+ test), `web/src/App.contributionForm.test.tsx`
- `API/DTOs/Contributions/RecordContributionRequest.cs`, `API/Services/ContributionService.cs`, `RondiTrack.Tests/ContributionIdempotencyContractTests.cs`

## 9. How it works
See [The key, and the decision table](#the-key-and-the-decision-table) below. A key is created in the submit handler and kept per form:
- It's **reused** for a network error, 5xx, 429 or a refreshed 401.
- It's **replaced** when the member, cycle or amount changes, and dropped after a `201`.
- The API stores the response under the key, so a retry gets the same `201` and one row.

## 10. Testing and recorded results
| Check | Recorded result (README at `e063646`) | Re-run 27 Oct 2026 |
|---|---|---|
| Backend | 137 passed | — |
| `web/` tests | 62 passed in 8 files | **63 passed in 9 files**, on identical code |
| `web/` lint / build | Clean / succeeded | Lint clean; build not re-run |

The README's count doesn't include `web/src/App.contributionForm.test.tsx`, which is part of the same commit; with it, the committed code gives 63 tests in 9 files. The repository doesn't record why the README number is lower.

## 11. Challenges and solutions
Answering the six contract questions turned up **two API faults**, both fixed in the API with tests:
- The decimal amount, replaced by `amountInCents`.
- Contributions were accepted into a closed cycle.

The deliberate key bug showed that a `401` retry didn't catch the problem, because `authFetch` resends the same request. Only the two tests where my own code makes a second request failed.

## 12. Learning outcomes
Not recorded as such. [Optimistic or not](#optimistic-or-not) explains why recording isn't optimistic.

## 13. Evidence, commits and pull requests
- [PR #5](https://github.com/mdvar2/RondiTrack/pull/5)
- [`web/idempotency-failing.txt`](../../web/idempotency-failing.txt), [`web/idempotency-passing.txt`](../../web/idempotency-passing.txt)

## 14. Relationship to other assignments
- **Before:**
  - [4.4](assignment-4.4.md)'s idempotency, persisted in [6.4](assignment-6.4.md).
  - [6.3](assignment-6.3.md)'s rule that a 429 never consumes a key.
  - [7.3](assignment-7.3.md)'s live client.
- **Next:** [8.1](assignment-8.1.md) began the Next.js app. Its brief says that "Day 2" of that work adds "Server Actions for writes".

## 15. Original record (as written at the time)

> **Historical record.** This was the Assignment 7.4 section of the root README. It was moved here unchanged (apart from Markdown formatting) on 27 October 2026. Commands, ports and test counts are as recorded at the time and may be out of date. For the current project, see the [root README](../../README.md).

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
