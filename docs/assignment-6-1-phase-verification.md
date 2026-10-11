# Assignment 6.1 phase verification

Assignment 5.4 gate passed and report committed in 8485f63 before any authentication changes.

## Phase 7 ? Authentication foundation

Status: Completed. Existing User gains nullable PasswordHash and NormalizedEmail; no second user table. PostgreSQL generates normalized email with lower(btrim(Email)) and enforces a unique index, including legacy and bypass writes. Existing passwordless users/data are preserved. AuthService.RegisterAsync hashes with ASP.NET Core PasswordHasher<User>; anonymous AuthController.Register exposes POST /api/auth/register. RegisterRequestValidator enforces a 12-character minimum with 400 problem responses; duplicates produce 409, including database-enforced races. No new package was needed for PasswordHasher.

Navigation: Domain/Entities/User.cs/SetPasswordHash and NormalizeEmail; API/Auth/AuthService.cs/RegisterAsync; API/Controllers/AuthController.cs/Register; API/Validators/RegisterRequestValidator.cs; Infrastructure/Data/RondiTrackDbContext.cs/OnModelCreating; Infrastructure/Repositories/UserRepository.cs/GetByNormalizedEmailAsync.

Migration: 20261011174110_AddUserAuthentication (source, designer and snapshot committed). Applied successfully to fresh Testcontainers database. Verification: dotnet build RondiTrack.sln --no-restore -m:1 succeeded, 0 warnings/errors; dotnet test RondiTrack.Tests/RondiTrack.Tests.csproj --no-build --no-restore passed 28, failed 0, skipped 0 (3 s), including all 25 existing tests and three new registration tests. Outstanding: legacy users need a deliberate password setup/reset process before login; implementing password reset is outside the assignment. Development migration application remains subject to duplicate-email preflight; no accounts will be deleted or merged automatically.

Exact files:

```text
 M API/Program.cs
 M Domain/Entities/User.cs
 M Domain/Repositories/IUserRepository.cs
 M Infrastructure/Data/RondiTrackDbContext.cs
 M Infrastructure/Migrations/RondiTrackDbContextModelSnapshot.cs
 M Infrastructure/Repositories/UserRepository.cs
?? API/Auth/
?? API/Controllers/AuthController.cs
?? API/DTOs/Auth/
?? API/Validators/RegisterRequestValidator.cs
?? Infrastructure/Migrations/20261011174110_AddUserAuthentication.Designer.cs
?? Infrastructure/Migrations/20261011174110_AddUserAuthentication.cs
?? RondiTrack.Tests/RegistrationTests.cs
A  docs/assignment-6-1-phase-verification.md
```

## Phase 8 ? JWT login

Status: Completed. Added only Microsoft.AspNetCore.Authentication.JwtBearer 10.0.12, matching ASP.NET Core. AuthService.LoginAsync normalizes email, verifies PasswordHasher hashes (including a dummy hash for unknown/passwordless users), returns identical 401 problem bodies, and logs only user/correlation IDs. TokenService creates five-minute HS256 JWTs. JwtOptions.Load validates a base64 key of at least 32 random bytes and fails startup with a helpful User Secrets command. A random development key was configured in User Secrets outside Git; the fixture uses an independent runtime-generated test key via UseSetting. Signature, issuer, audience, algorithm and expiry are validated; ClockSkew is 30 seconds; MapInboundClaims=false.

Claims: sub is the stable User.Id; jti identifies this token; iat, nbf and exp describe fixed issuance/validity timestamps; iss/aud identify this issuer and client application. These facts can safely remain fixed for the token lifetime. No email, membership or stokvel role is carried. The configured 30-second tolerance permits minor clock drift, so practical acceptance can last 5 minutes 30 seconds after issuance.

Navigation: API/Auth/JwtOptions.cs/Load and ValidationParameters; TokenService.cs/CreateAccessToken; AuthService.cs/LoginAsync; AuthController.cs/Login; GlobalExceptionHandler.cs/TryHandleAsync. Verification: dotnet build RondiTrack.sln --no-restore -m:1 passed, 0 warnings/errors; all 32 tests passed, failed 0, skipped 0 (4 s), including signed-token validation, wrong signature/issuer/audience/expiry rejection, missing/invalid/short key failure and exact identical login failure bodies. Outstanding: refresh sessions in Phase 9; fallback/resource authorization in Phases 10?11.

Exact files (excluding the private User Secrets file, intentionally never committed):

```text
 M API/Auth/AuthService.cs
 M API/Controllers/AuthController.cs
 M API/Handlers/GlobalExceptionHandler.cs
 M API/Program.cs
 M API/RondiTrack.csproj
 M API/appsettings.json
 M RondiTrack.Tests/TestSupport/PostgresApiFactory.cs
?? API/Auth/JwtOptions.cs
?? API/Auth/TokenService.cs
?? API/DTOs/Auth/LoginRequest.cs
?? API/Validators/LoginRequestValidator.cs
?? Domain/Exceptions/AuthenticationFailedException.cs
?? RondiTrack.Tests/JwtLoginTests.cs
M  docs/assignment-6-1-phase-verification.md
```

## Phase 9 ? Refresh tokens and logout

Status: Completed. Login creates a PostgreSQL login family and 64-byte cryptographically random refresh token; only a SHA-256 hash is stored. RefreshTokenFamilies tracks family revocation and a 30-day absolute limit; individual tokens expire after seven days, with successor database expiry capped by the family limit. Every refresh rotates the token. EfRefreshTokenRepository.RotateAsync locks the family row (FOR UPDATE), marks the old token spent and inserts its successor in one transaction. All family mutations, including logout, acquire the same lock. Concurrent reuse gets 401 and commits whole-family revocation before returning, making even the winning successor unusable. Revocation persists through repository/database calls, not memory.

POST /api/auth/refresh and logout are anonymous so expired access tokens do not prevent session renewal/revocation. Cookie __Secure-ronditrack_refresh is HttpOnly (JavaScript cannot read it), Secure (HTTPS only), SameSite=Strict (limits cross-site cookie requests), Path=/api/auth (limits delivery), host-only (no Domain). Refresh tokens never appear in response bodies. Logout revokes the family and deletes the cookie with matching flags/path; repeat logout remains 204. Access tokens should stay in frontend memory. Putting either token in localStorage exposes it to script/XSS; a stolen refresh token also loses HttpOnly protection and can prolong account compromise until replay revocation. Family reuse requires the legitimate user to sign in again. Already-issued access JWTs remain valid for at most five minutes plus ClockSkew after logout; stokvel permission checks remain live in Phase 11.

Navigation: Domain/Entities/RefreshToken.cs and RefreshTokenFamily.cs; IRefreshTokenRepository; Infrastructure/Repositories/EfRefreshTokenRepository.cs/StartSessionAsync, RotateAsync, RevokeSessionAsync; API/Auth/AuthService.cs/RefreshAsync and LogoutAsync; AuthController.cs/RefreshCookieOptions, Refresh and Logout. Migration: 20261011174943_AddRefreshTokenSessions (source, designer and updated snapshot). Verification: build passed 0 warnings/errors; all 36 tests passed, failed 0, skipped 0 (5 s), including rotation/hash-at-rest, replay/successor rejection, simultaneous refresh, logout idempotence and unrelated family survival. Initial SQL string quoting compile errors were fixed using C# interpolated raw strings; SQL parameters remain parameterized. Outstanding: fallback and resource authorization in Phases 10?11.

Exact files:

```text
 M API/Auth/AuthService.cs
 M API/Controllers/AuthController.cs
 M API/Program.cs
 M Infrastructure/Data/RondiTrackDbContext.cs
 M Infrastructure/Migrations/RondiTrackDbContextModelSnapshot.cs
?? Domain/Entities/RefreshToken.cs
?? Domain/Entities/RefreshTokenFamily.cs
?? Domain/Repositories/IRefreshTokenRepository.cs
?? Infrastructure/Migrations/20261011174943_AddRefreshTokenSessions.Designer.cs
?? Infrastructure/Migrations/20261011174943_AddRefreshTokenSessions.cs
?? Infrastructure/Repositories/EfRefreshTokenRepository.cs
?? RondiTrack.Tests/RefreshSessionTests.cs
M  docs/assignment-6-1-phase-verification.md
```

## Phase 10 ? Secure by default

Status: Completed. API/Program.cs adds a fallback policy requiring authentication, orders UseAuthentication before UseAuthorization, and writes 401/403 status-page failures as application/problem+json. Auth actions have explicit AllowAnonymous; OpenAPI and Scalar endpoints have AllowAnonymous and still load in development. All existing non-auth controllers are covered by fallback. No forgotten route is anonymously accessible.

Regression HTTP tests now use PostgresApiFactory.CreateAuthenticatedClient: the fixture seeds a real database user and mints a token with the application's real TokenService and runtime test signing key. This treats sign-in as a precondition for existing business/concurrency tests, preserving every assertion. Dedicated JwtLoginTests and RefreshSessionTests exercise actual anonymous registration/login/refresh/logout and real JWT validation instead of replacing authentication with a mock scheme.

Navigation: API/Program.cs/AddAuthorization and middleware; RondiTrack.Tests/TestSupport/PostgresApiFactory.cs/CreateAuthenticatedClient; SecureDefaultsTests.cs. Verification: build succeeded, 0 warnings/errors; all 39 tests passed, failed 0, skipped 0 (5 s), including no-token 401 problem and both anonymous documentation routes. Outstanding: stokvel/user permission policies in Phase 11; the middleware and generic 403 writer are implemented, with resource-denial tests added there.

Exact files:

```text
 M API/Program.cs
 M RondiTrack.Tests/ConcurrencyProtectionTests.cs
 M RondiTrack.Tests/ContributionBusinessRuleTests.cs
 M RondiTrack.Tests/EdgeCaseTests.cs
 M RondiTrack.Tests/HappyPathTests.cs
 M RondiTrack.Tests/IdempotencyTests.cs
 M RondiTrack.Tests/MembershipBusinessRuleTests.cs
 M RondiTrack.Tests/NegativePathTests.cs
 M RondiTrack.Tests/TestSupport/PostgresApiFactory.cs
?? RondiTrack.Tests/SecureDefaultsTests.cs
M  docs/assignment-6-1-phase-verification.md
```

## Phase 11 â€” Resource authorization

Completed. Every stokvel route invokes StokvelAuthorization.EnsureAsync and the scoped StokvelAccessHandler, which queries current membership and role from PostgreSQL. Views allow members; management requires Treasurer or Chairperson; ordinary members can record their own contributions. Nonmembers receive 404, insufficient permissions 403. Creating a stokvel atomically creates its creator's Chairperson membership. Platform administrators have no scoped membership bypass. Exactly one plain role attribute guards the global stokvel list; other controller authorization uses policies. PlatformRoleClaimsTransformation loads the platform role from the database on each authenticated request, so neither scoped nor platform permissions depend on stale JWT role claims. Deleted users become unauthenticated.

Navigate in VS Code to API/Auth/Policies.cs, StokvelAccessHandler.cs, StokvelAuthorization.cs and PlatformRoleClaimsTransformation.cs; the three stokvel controllers call the helper at the start of each scoped action. Domain/Entities/Roles.cs centralizes names. Migration 20261011175640_AddPlatformRole adds a default ordinary platform role without promoting existing users.

Verification: build succeeded with zero warnings/errors; 45 tests passed, zero failed/skipped (6 seconds). Tests cover all four permission outcomes, own contributions, live demotion/removal, per-stokvel roles, creator membership and the platform list. Outstanding: final route matrix and development-database isolation run in Phase 12. Development preflight found duplicate normalized-email groups; no development data or migrations were changed. The unique-email upgrade requires an explicit cleanup decision.

Exact files created or modified (no files moved):

```text
API/Controllers/ContributionCyclesController.cs
API/Controllers/PayoutsController.cs
API/Controllers/StokvelsController.cs
API/Controllers/UsersController.cs
API/Handlers/GlobalExceptionHandler.cs
API/Program.cs
Domain/Entities/StokvelMember.cs
Domain/Entities/User.cs
Domain/Repositories/IStokvelRepository.cs
Infrastructure/Data/RondiTrackDbContext.cs
Infrastructure/Migrations/RondiTrackDbContextModelSnapshot.cs
Infrastructure/Repositories/StokvelRepository.cs
RondiTrack.Tests/TestSupport/PostgresApiFactory.cs
API/Auth/PlatformRoleClaimsTransformation.cs
API/Auth/Policies.cs
API/Auth/StokvelAccessHandler.cs
API/Auth/StokvelAuthorization.cs
Domain/Entities/Roles.cs
Domain/Exceptions/ForbiddenException.cs
Infrastructure/Migrations/20261011175640_AddPlatformRole.Designer.cs
Infrastructure/Migrations/20261011175640_AddPlatformRole.cs
RondiTrack.Tests/StokvelAuthorizationTests.cs
RondiTrack.Tests/TestSupport/AuthScenario.cs
docs/assignment-6-1-phase-verification.md
```

## Phase 12 â€” Integration and regression verification

Completed. Added AuthorizationMatrixTests to exercise all 14 scoped routes for anonymous callers (401), nonmembers including nonmember administrators (404), and ordinary members (200 for views, 403 for elevated operations). Treasurer/Chairperson tests prove recording another member's contribution and processing a payout. Platform-role demotion affects an existing JWT. A new deleted-user test initially exposed 403 instead of 401; Program's JwtBearer OnTokenValidated now rejects subjects whose users no longer exist, and the full suite passes.

Exact files: modified API/Program.cs; created RondiTrack.Tests/AuthorizationMatrixTests.cs and docs/assignment-6-1-isolation-evidence.txt; modified this phase record. No files moved. Why: verifies real middleware, resource handler and PostgreSQL transactions without mocking authentication. Navigate to AuthorizationMatrixTests and existing RegistrationTests/JwtLoginTests/RefreshSessionTests/StokvelAuthorizationTests; API/Program.cs/OnTokenValidated is the discovered fix.

Commands/results: dotnet build RondiTrack.sln -m:1 --no-restore: zero warnings/errors. powershell -NoProfile -ExecutionPolicy Bypass -File scripts/verify-assignment-5-4-isolation.ps1: 62 passed, zero failed/skipped, 19.7887 seconds. A real development connection failed before tests; a fresh container at 127.0.0.1:27421/ronditrack_test applied all seven migrations; development connections restored afterward. See evidence for the actual assigned port (ports vary). Original assertions in all 11 original test files compared against 6e3bfa1: unchanged. dotnet ef migrations has-pending-model-changes --project Infrastructure --startup-project API --no-build: no model changes.

Outstanding: development upgrade blocked by existing duplicate normalized emails. User reviewed the exact cleanup proposal and chose to preserve development data. This does not affect disposable-database verification. Phase 13 documents this limitation and the complete submission evidence.


## Phase 13 — Documentation and submission readiness

Completed for source and disposable-database verification; development deployment remains blocked by explicit user decision. README now documents every authentication requirement, stable claims, five-minute JWT/30-second skew, seven-day refresh/30-day family, cookie flags, storage/CSRF assumptions, atomic replay handling, full endpoint/status matrix, role versus resource policies, additional DI lifetimes and actual request-flow navigation. Created a separate Assignment 6.1 checklist/report and exact commit file manifest. The Assignment 5.4 report now records its completion-gate commit explicitly.

Exact files: modified README.md, docs/assignment-5-4-verification-report.md and docs/assignment-6-1-phase-verification.md; created docs/assignment-6-1-verification-report.md, docs/assignment-6-1-file-manifest.txt, docs/development-migration-status.md. No files moved. Private cleanup documents were removed from unpublished history and preserved outside the repository. Development data is unchanged.

Verification: final build zero warnings/errors; final isolation run 62 passed, zero failed/skipped, 19.7887 seconds; no pending model changes. All seven migrations applied in a fresh container. Container disposal verified: Docker reports no running containers after the test. Documentation status codes checked against actual controllers (adding membership is 204). Credential-pattern scan reviewed 371 historical text blobs: 12 candidate blobs were documentation placeholders, password-variable assignments and synthetic test inputs; current candidates have the same explanations. Private comparison found zero actual JWT signing-key or complete development connection-string matches in history; no values displayed. Generic password-word substring matches were false positives in prose/identifier names, not embedded credentials. These checks are heuristic, not a guarantee of exhaustive secret detection. No secrets were committed.

Outstanding: three new migrations remain unapplied to development because normalized emails collide. The user selected keeping development data unchanged; this prevents auth against the old development schema. See the sanitized migration note; private cleanup documents are not published. Password setup for legacy users and frontend deployment remain outside the requested scope. Assignment 6.1 implementation and tests are verified; report does not claim development upgrade completion.

Commits for Phases 7–12: dca4979, 4a08eb2, 961903d, fa8b64d, 96ac6bf, 2bcdffb. Phase 13 documentation commit is recorded in Git history and final delivery. Use git show --name-status <SHA> for exact per-commit paths; the file manifest includes them for every source phase.
