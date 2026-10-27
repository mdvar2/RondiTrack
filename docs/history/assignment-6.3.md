# Assignment 6.3 — Frontend-Ready Backend

| | |
|---|---|
| Official title | Not recorded. The brief was `Assignment_6_3_RondiTrack.pdf`, which isn't in the repository. The README called it "Frontend-Ready Backend"; the technical notes call it "Ready for the Frontend". |
| Dates | 13–14 October 2026 |
| Final commit on `main` | [`dd83b48`](https://github.com/mdvar2/RondiTrack/commit/dd83b48) "Add detailed technical documentation" (code commits from [`8ba0f27`](https://github.com/mdvar2/RondiTrack/commit/8ba0f27) to [`1fdf02f`](https://github.com/mdvar2/RondiTrack/commit/1fdf02f)) |
| Branch / PR | Committed directly to `main`; no PR |
| README as submitted | [README at `dd83b48`](https://github.com/mdvar2/RondiTrack/blob/dd83b48/README.md), carried into the [technical notes](technical-notes-to-6.3.md#assignment-63--ready-for-the-frontend-14-october-2026) |
| Re-tested | **Yes, 27 Oct 2026**: the `dd83b48` code, exported unchanged, passed **122/122** on disposable PostgreSQL |

## 1. Purpose and learning objectives
To get the backend ready for a browser frontend: versioned routes, CORS, rate limits, a member "home" endpoint, one error format, and generated TypeScript types. The brief's learning objectives aren't recorded.

## 2. Original requirements
The [compliance matrix](evidence/assignment-6-3-compliance.md) checks every requirement of the brief. In summary:
- **Constraints:** extend the existing solution and keep 4.1–6.2 working. Preserve the development database. **No new NuGet packages.** Existing assertions unchanged apart from versioned URLs.
- **Versioned routes:** every controller route under `/api/v1` through one reusable prefix. The refresh cookie's path moves with it.
- **CORS:** origins from configuration, exactly `http://localhost:3000` in Development, none by default in Production. Credentials allowed, with explicit methods and headers, including `Idempotency-Key`. Expose `Retry-After`.
- **Rate limiting:** login limited per client IP (fixed window). Contributions and payouts limited per user. Every rejection is `429` with a valid `Retry-After` and Problem Details. A `429` must never consume a contribution's idempotency key.
- **Member home:** `GET /api/v1/me/home`, with each membership's role, current cycle, paid and owed, and nobody else's totals. Money must be exact in JavaScript.
- **Errors:** one Problem Details contract for every error.
- **TypeScript:** contracts generated from the real OpenAPI document, with proof that a renamed backend property breaks compilation.

Two items are **PARTIAL** in the matrix: moving *every* route (including probes and docs) under `/api/v1`, and the next payout date.

## 3. What I implemented
See the [original record below](#15-original-record-as-written-at-the-time) and the [stage notes](evidence/assignment-6-3-stages.md):
- `API/Common/ApiRoutes.cs` (the `/api/v1` prefix and the refresh cookie path)
- `CorsPolicies.cs`
- `RateLimitPolicies.cs`
- `MeController` with `MemberHomeService`
- The unified validation and Problem Details errors
- `clients/typescript/`

## 4. Technologies, tools and dependencies
- **No new NuGet packages:** ASP.NET Core's built-in CORS and rate limiter.
- **`clients/typescript/` at `dd83b48`:** openapi-typescript 7.13.0 and TypeScript 5.9.3, with `scripts/contracts/ExportContracts.csproj` to export the OpenAPI document.

## 5. Prerequisites and installation
The .NET 10 SDK and Docker for the tests. Node.js and npm for the TypeScript contracts.

## 6. Environment configuration
- `Cors:AllowedOrigins`: `["http://localhost:3000"]` in `appsettings.Development.json`, empty by default.
- `RateLimiting:*` limits in `appsettings.json`.
- The JWT and connection secrets are unchanged from 6.1.

## 7. How to run or reproduce
**As submitted:** `dotnet test RondiTrack.Tests/RondiTrack.Tests.csproj` (Docker running). For the contracts, from `clients/typescript`: `npm ci --ignore-scripts`, then `npm run regenerate` (export → generate → `tsc --strict`) and `npm run test:money`. Re-run on 27 Oct 2026: the backend tests passed **122 of 122**. I didn't re-run the contract scripts.

**In the current codebase:**
- All of this is still active. `/api/v1/me/home` feeds both frontends.
- `clients/typescript/` has drifted from the current API; [7.4](assignment-7.4.md#known-gaps) notes that regenerating it breaks its own check.
- Tests: `CorsContractTests.cs`, `RateLimitContractTests.cs`, `MemberHomeTests.cs`, `ErrorContractTests.cs`, `VersionedRefreshTests.cs`.

## 8. Important files and components
- `API/Common/ApiRoutes.cs`, `CorsPolicies.cs`, `RateLimitPolicies.cs`
- `API/Controllers/MeController.cs`, `API/Services/MemberHomeService.cs`, `API/DTOs/Home/HomeResponse.cs`
- `API/Filters/ValidationFilter.cs`, `API/Handlers/GlobalExceptionHandler.cs`
- `clients/typescript/`

## 9. How it works
- Middleware order matters here: CORS has to answer preflight `OPTIONS` before the fallback "signed in" policy rejects it.
- The rate limiter partitions logins by IP and money operations by user ID, and rejects with a Problem Details `429`. The compliance matrix verifies that a `429` never reserves, stores or uses a contribution's idempotency key, and that two users never share a quota.
- `/me/home` works out paid and owed on the server, from that member's rows only.

## 10. Testing and recorded results
| Point | Result |
|---|---|
| Final suite ([compliance](evidence/assignment-6-3-compliance.md), [verification](evidence/assignment-6-3-verification.md)) | **122 passed**, 0 failed, 0 skipped. The suite also passed with development database connections disabled |
| Re-run 27 Oct 2026 of `dd83b48` | **122 passed, 0 failed** |

## 11. Challenges and solutions
- At the start, 101 tests failed in fixture setup because Docker wasn't running. No application assertion was reached, and they passed once Docker was started ([stage 0](evidence/assignment-6-3-stages.md#stage-0--baseline)).
- `nextPayoutDate` returns `null` because there's no payout-scheduling rule. I didn't make one up (see the [original record](#15-original-record-as-written-at-the-time)).

## 12. Learning outcomes
The reflection written into the README at this stage is kept in [readme-overview-to-7.4.md](readme-overview-to-7.4.md#what-i-learned-and-what-i-still-need-to-improve).

## 13. Evidence, commits and pull requests
- [Compliance matrix](evidence/assignment-6-3-compliance.md)
- [Stage notes](evidence/assignment-6-3-stages.md)
- [Verification](evidence/assignment-6-3-verification.md)
- [Technical notes: 6.3](technical-notes-to-6.3.md#assignment-63--ready-for-the-frontend-14-october-2026)
- [`clients/typescript/README.md`](../../clients/typescript/README.md), including the breaking-change experiment

## 14. Relationship to other assignments
- **Before:** [6.2](assignment-6.2.md)'s `correlationId` became part of the single error contract.
- **Next:** [6.4](assignment-6.4.md) made the API shippable.
- **Later:**
  - [7.1](assignment-7.1.md)–[7.4](assignment-7.4.md) are the frontend this was prepared for.
  - [8.1](assignment-8.1.md) found that the per-IP login limit is shared by every user when sign-ins come from one Next.js server.

## 15. Original record (as written at the time)

> **Historical record.** This was the Assignment 6.3 section and its test-verification section of the root README. It was moved here unchanged (apart from Markdown formatting) on 27 October 2026. Commands, ports and test counts are as recorded at the time and may be out of date. For the current project, see the [root README](../../README.md).

For Assignment 6.3, I worked on the parts of the backend that a frontend would depend on:

- **API versioning:** Controller endpoints now use `/api/v1`.

- **CORS:** The API allows the configured frontend origin rather than accepting requests from any origin.

- **Rate limiting:** Login, contribution and payout requests have limits to reduce excessive requests.

- **Contribution retries:** The existing idempotency handling works alongside rate limiting.

- **Member home endpoint:** `GET /api/v1/me/home` returns information for the logged-in member without exposing other members' contribution amounts.

- **Consistent errors:** API errors follow a Problem Details response format.

- **TypeScript contracts:** I generated types from the actual OpenAPI document and checked that a renamed backend property would cause a frontend compile error.

One feature is still incomplete: **`nextPayoutDate` returns `null`** because the system does not yet have a rule for scheduling future payouts. I have not made up a date for it.

For a more detailed explanation of the changes, see the [Assignment 6.3 trainer notes](evidence/assignment-6-3-stages.md) and [compliance matrix](evidence/assignment-6-3-compliance.md).

### Testing and verification (as recorded for 6.3)

**On 14 October 2026, all 122 tests passed, with no failures or skipped tests.** The fresh solution build also completed with no warnings or errors.

I also checked that the tests could run without connecting to my development PostgreSQL database. I temporarily disabled new connections to that database, ran the test suite using Testcontainers, and then restored the original connection setting. I confirmed the database was accessible again with `SELECT 1`.

No development data, schemas or migrations were changed during that verification.

The [Assignment 6.3 verification report](evidence/assignment-6-3-verification.md#successful-offline-verification) has the full procedure and recorded results. The earlier Assignment 6.2 test result was 101 passing tests; Assignment 6.3 verified 122, and Assignment 6.4 later verified 130 passing tests.

To run the ordinary tests locally, start Docker Desktop in Linux-container mode and use:

```powershell
dotnet restore RondiTrack.sln
dotnet build RondiTrack.sln --no-restore -m:1
dotnet test RondiTrack.sln --no-restore -m:1
```

These commands run the normal suite. They do **not** disable development database connections. The guarded isolation procedure is explained separately in the verification report.
