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

## Stage 2 ? CORS

CorsPolicies reads exact origins from configuration (localhost:3000 only in Development; empty production list). Explicit methods/headers allow Idempotency-Key and concurrency/tracing headers. Retry-After, ETag and Location are exposed; no existing replay header exists. CORS runs before authentication. Four browser-style tests passed: credentialed preflight, rejected origin, scoped 403 and scoped 404. Stage 1 full suite passed 102/102.

Trainer: CORS answers preflights before JWT authorization and makes scoped errors readable. Authentication still protects data.

## Stage 3 ? rate limiting

RateLimitPolicies centrally defines Login (fixed window, 5 per 60 seconds by remote IP), Contributions (token bucket, capacity/refill 30 per 60 seconds by authenticated sub), and Payouts (separate token bucket, capacity/refill 5 per 60 seconds by sub). Endpoints reference only names. Settings are validated; queues are disabled. Anonymous financial calls reach existing 401 authorization without consuming a user quota. No forwarded headers are trusted implicitly.

Program order: routing, CORS, authentication, rate limiting, authorization, controller/resource authorization, contribution idempotency. Each rejection writes 429 problem+json with Retry-After (positive integer ceiling), title, detail, instance and the existing W3C correlationId. Replays count against limits too; rejection delays them but never removes or reserves a key. A rejected fresh key never reaches ContributionService.

The shared factory overrides only the login allowance to 1000; every rate-limit test uses WithWebHostBuilder and UseSetting to isolate its app and counters. Four rate-limit tests plus four CORS tests passed. The short three-second contribution test exhausts two permits, proves K absent and zero writes after 429, waits the reported Retry-After, succeeds and replays with K, then counts exactly one contribution. It also proves a later rejected replay preserves K. Contributions and payouts both isolate users sharing the test IP; payout writes remain single.

Trainer: A bucket permits a meeting-sized burst of twenty contributions without allowing unlimited traffic. Per-user identity avoids penalizing everyone using one Wi-Fi IP. Limits are process-local; three balanced instances can grant up to three times the allowance, and restart resets it. Existing idempotency is also process-local, so cross-instance retry safety is an existing production gap, not solved by rate limiting.

## Stage 4 ? member home

MeController obtains the caller from JWT sub; MemberHomeService performs one projected membership query and one projected current-month-cycle query with caller-only contribution sums. Empty memberships return immediately. TimeProvider supplies the UTC month, matched against existing YYYY-MM periods. CurrentCycle contains only Period and the named Status; AmountPaid and AmountOwed are invariant decimal strings preserving full domain precision. Owed = max(ContributionAmount - own paid, 0); no current cycle means zero paid/owed. Cycle TargetAmount is not assumed to be a per-member obligation. Duplicate current periods prefer Open then a stable cycle ID, because the existing schema permits duplicates. These are documented presentation assumptions, not new write rules.

NextPayoutDate is nullable DateTimeOffset and currently null: there is no scheduling rule/date in the domain, so inventing a payout deadline would mislead members. A future non-null value serializes in UTC. No migration or new financial rule was introduced.

Three MemberHomeTests passed using Testcontainers. A fixed TimeProvider proves October calculations independent of host date, per-stokvel Member/Treasurer roles, own totals (excluding another member and September), overpayment clamped to zero, PaidOut name, missing cycle, six fields only, empty memberships and anonymous refusal.

Trainer: The server fills one screen rather than sending tables to the browser. Decimal strings prevent JavaScript rounding without placing an artificial safe-integer limit on existing money. A missing schedule is explicitly null.

## Stage 5 ? one error contract

RequestValidationException gains an optional errors map. ValidationFilter groups FluentValidation messages by camelCase property path; GlobalExceptionHandler adds the map as a ProblemDetails extension. Existing detail messages remain intact so no old assertion changed. Invalid model binding has a fixed safe detail instead of echoing input or type names. Both MVC and HTTP/OpenAPI serializer options use JsonNumberHandling.Strict. Status-code pages cover otherwise empty routing/protocol errors in addition to 401/403. Existing sanitized 5xx and correlation behavior remain.

Full suite: 120 passed, zero failed/skipped, including seven ErrorContractTests. Tests prove field-name maps, string-number/bool/malformed JSON rejection, generic details without internal names, route 404/method 405 Problem Details, and actual generated OpenAPI decimal/integer schemas with no numeric-string unions. Existing telemetry/log privacy, authentication, financial, concurrency and correlation tests all still pass.

Trainer: Forms can attach errors to the fields users entered. Strict JSON settings must agree for both controllers and OpenAPI; otherwise generated types do not describe actual accepted input. Existing generic 5xx responses keep server internals private.

## Stage 6 ? generated TypeScript

clients/typescript contains its own package.json, exact compatible tool pins (openapi-typescript 7.13.0, TypeScript 5.9.3), lockfile, actual OpenAPI snapshot, generated schema.d.ts, strict tsconfig and a typed home function. scripts/contracts exports from the real API through the existing disposable PostgreSQL fixture with an explicit API content root. No new NuGet package. npm run regenerate performs export/generation/strict compilation; npm run test:money proves exact decimal addition using BigInt including values beyond safe integers.

Before rename: strict compilation exit 0. Actual C# AmountOwed -> OutstandingBalance rename, API rebuild, actual OpenAPI export and regeneration produced TS2339 at src/home.ts(7,56), compiler exit 2. Restoring the field and rebuilding/regenerating succeeded, exit 0. Full actual error and reproducible commands are in clients/typescript/README.md. No test assertions were changed for the experiment. A saved-source timestamp initially caused a stale incremental DLL; refreshed timestamp and real re-export resolved it.

Trainer: The generated endpoint type connects the API field name to the function. A breaking rename becomes a compiler error before a user sees a blank screen. The function returns an exact decimal string instead of losing precision in binary floating point.

## Stage 7 ? final integration verification

Added the default-production CORS test and audited unsupported content types. The first audit run passed production CORS but failed the 415 full-contract assertion: framework ProblemDetails used a different type and lacked detail/instance. Fixed shared ProblemDetails customization to normalize these fields; retained the test assertion. Added explicit 429 response metadata to login/contribution/payout OpenAPI and regenerated actual types. Final dotnet test RondiTrack.sln --no-restore -m:1: 122 passed, zero failed/skipped, 34 seconds. Final build: zero warnings/errors. Final npm regeneration/strict compilation and exact-money runtime checks passed. Existing test-source comparison: 28 files, zero non-URL changes.

Development-offline verification remains NOT VERIFIED pending the explicit approval requested for the existing guarded isolation script. No development database was stopped or changed. The factory verifies its actual disposable database target, but this is not claimed as offline evidence.

Trainer: These tests send browser preflight headers and exercise real PostgreSQL, not a mocked data layer. Failures were corrected in implementation, not hidden by changing assertions. The offline proof requires one more approved run.

## Stage 8 ? README and submission documentation

README now has a current Assignment 6.3 contract section covering all requested topics and distinguishing historical earlier examples. docs/api-reference.md route URLs moved. The error-status table includes automatic routing/content-type errors and explicitly preserves operational text/plain health 503. Cookie analysis distinguishes origin from schemeful site: HTTP localhost frontend to HTTPS API is cross-site for Strict cookies. The existing secure/Strict cookie behavior is preserved and deployment limitations are explained using primary references. Real compiler output and all verification gaps are documented.

Trainer: CORS permission does not override cookie SameSite/Secure rules, and a cookie-handler server test is not proof of actual browser enforcement. A real next payout deadline needs an agreed domain schedule.

## Stage 9 ? compliance and publication review

Compared every PDF requirement and the staged user request in docs/assignment-6-3-compliance.md. Explicit partials: operational health/docs paths/error contracts and unknown payout schedule. Offline run remains NOT VERIFIED. The publication audit checks outgoing commit snapshots and current files against actual configured signing/connection credentials in memory, with assignment-context checks for common field-word passwords; values are never printed. No actual credential literals or tracked bin/obj/node_modules/build artifacts were found. This is a practical credential scan, not a guarantee of detection of every possible secret format. No push performed. Authorized changes are committed in logical units; final Git status is checked after documentation commit.
