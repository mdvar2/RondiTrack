# Assignment 6.3 implementation and trainer notes

## Stage 0 — baseline

Read both supplied PDFs in full and the user's pasted request. The RondiTrack assignment supplies requirements; the Matric Compass handout supplies examples, not code to copy blindly.

The working tree initially was clean at `a733cc6`. Existing architecture: five MVC controller route families, JWT subject (`sub`) authentication with a user-existence check, fallback authorization, resource-scoped membership checks, secure Strict refresh cookies, EF/PostgreSQL repositories, payout transactions, in-memory contribution idempotency, W3C correlation, OpenTelemetry, sanitized logging and PostgreSQL Testcontainers tests. No repository AGENTS.md was found.

Financial writes: `ContributionService.RecordContributionAsync` checks an idempotency key/hash, persists a contribution, then saves the response in `IdempotencyStore`. `PayoutService.CreatePayoutAsync` uses a transaction, rotation by membership join date and marks the cycle PaidOut. There is no existing replay response header and no payout calendar.

Baseline command: `dotnet test RondiTrack.Tests/RondiTrack.Tests.csproj --no-restore -m:1`. Build succeeded; 101 tests failed in fixture initialization because Docker was not running. No application assertion was reached. Started Docker Desktop in the background; development PostgreSQL was not changed.

Planned changes: a shared route constant; central CORS/rate-limit policies and configuration in Program; home DTO/service/controller without migrations; validation errors and JSON options; URL-only changes to existing tests plus new tests; a separate TypeScript client; README and verification evidence. No new NuGet packages or database changes.

## Stage 1 — versioned routes

`API/Common/ApiRoutes.cs` defines `V1` and derives the auth-cookie path. All five controllers use that compile-time constant in Route attributes; registration's Location also uses it. Existing test assertions are preserved apart from the required URL/cookie-path changes. The evidence runner's URLs also moved.

Moved families: auth (register/login/refresh/logout), users, stokvels (including memberships/contributions), nested contribution cycles, and nested payouts. Operational `/health/live`, `/health/ready`, `/openapi/v1.json`, and `/scalar/v1` remain stable: these are probes/documentation rather than controller API contracts, and existing health/documentation assertions require them.

`VersionedRefreshTests.BrowserCookieHandler_SendsCookieToVersionedRefreshAndLogout` uses the client's cookie handler rather than manually injecting a Cookie header. It checks login, refresh rotation, logout and refusal after logout at the new paths. This proves the path permits the cookie to reach both refresh and logout.

Trainer explanation: “A const can be used in attributes. Deriving the cookie path from the same prefix prevents a working login followed by a broken refresh when routes move.”
