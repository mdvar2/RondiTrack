# Assignment 6.1 verification report

Implemented and verified on disposable PostgreSQL 17. The development database
upgrade remains blocked by the user's explicit decision to preserve duplicate
email records. This is a deployment gap; do not claim the existing development
database supports the new schema. Assignment 5.4 was fully verified and reported
before Phase 7 began. Branch: `assignment-5-4-clean-architecture`.

## Requirement checklist

| Requirement | Implementation / evidence | Result |
|---|---|---|
| Extend existing User, no duplicate identity table | Domain/Entities/User.cs; Infrastructure/Data/RondiTrackDbContext.cs | Verified |
| PasswordHasher, minimum 12 characters, anonymous registration | API/Auth/AuthService.cs/RegisterAsync; API/Validators/RegisterRequestValidator.cs; API/Controllers/AuthController.cs/Register; RegistrationTests | Verified |
| Normalized email uniqueness, 400/409 problems | User.NormalizeEmail, computed unique database column; GlobalExceptionHandler; RegistrationTests | Verified in fresh DB; legacy upgrade blocked |
| Permitted authentication package matching ASP.NET Core | API/RondiTrack.csproj: JwtBearer 10.0.12; PasswordHasher from shared framework | Verified |
| JWT lifetime ≤15 minutes, explicit skew, key ≥32 bytes in User Secrets | JwtOptions.Load: configured five minutes, 30 seconds, base64 key validation; JwtLoginTests | Verified; key never committed |
| Signature, issuer, audience, expiration validation | JwtOptions.ValidationParameters; API/Program.cs/AddJwtBearer; JwtLoginTests | Verified |
| Identical wrong-password/unknown-email 401 | AuthService.LoginAsync, dummy hash; JwtLoginTests compares exact bodies | Verified |
| Minimal stable claims, no stokvel role claims | TokenService.CreateAccessToken; README claim rationale | Verified |
| Random refresh values, hashes only, cookies only | AuthService.CreateRefreshToken/HashRefreshToken; AuthController; RefreshSessionTests | Verified |
| Explicit secure cookie choices / storage guidance | AuthController.RefreshCookieOptions; README | Verified |
| Rotation, atomic concurrency, replay family revocation | EfRefreshTokenRepository.RotateAsync; PostgreSQL family lock; RefreshSessionTests | Verified |
| Idempotent logout and server-side revocation | AuthService.LogoutAsync; RevokeSessionAsync; RefreshSessionTests | Verified |
| Authentication/authorization middleware and fallback | API/Program.cs; Policies.Configure | Verified |
| Anonymous auth/OpenAPI/Scalar, 401/403 problem JSON | AllowAnonymous, status pages/global handler; SecureDefaultsTests | Verified |
| Custom requirement and resource handler | StokvelAccessRequirement, StokvelAccessResource, StokvelAccessHandler.HandleRequirementAsync | Verified |
| Live membership / scoped role per stokvel | EfStokvelMemberRepository.GetByStokvelAndUserAsync; StokvelAuthorizationTests | Verified, including same-JWT demotion/removal |
| Nonmember 404, ordinary member insufficient-role 403 | StokvelAuthorization.EnsureAsync; AuthorizationMatrixTests (all 14 scoped routes) | Verified |
| Members view and contribute for themselves | StokvelView/RecordContribution policies; StokvelAuthorizationTests | Verified |
| Treasurer/Chairperson cycles, others' contributions, payouts | Controllers + handler; StokvelAuthorizationTests and AuthorizationMatrixTests | Verified |
| One plain role check, policies elsewhere, central names | StokvelsController.GetAll role attribute; Policies; Roles | Verified |
| Live platform role and deleted-user rejection | PlatformRoleClaimsTransformation; JwtBearer.OnTokenValidated; AuthorizationMatrixTests | Verified |
| Existing assertions unchanged | Compared all 11 original test files against 6e3bfa1 | Verified; 25 original cases pass |
| Full suite with development database unavailable | Shared PostgresApiFactory; isolation evidence | 62 passed, 0 failed/skipped |
| README matrix, claims, cookies, reuse, logs and limitations | README Assignment 6.1 section; evidence below | Completed |
| Migrations/source committed; secret scan | Phase commits; scan outcome in phase record | Completed; heuristic limitation documented |

## Actual verification

`dotnet build RondiTrack.sln -m:1 --no-restore`: succeeded, zero warnings/errors
(22.92 seconds). `dotnet ef migrations has-pending-model-changes --project
Infrastructure --startup-project API --no-build`: no model changes.

`powershell -NoProfile -ExecutionPolicy Bypass -File
scripts/verify-assignment-5-4-isolation.ps1`: development PostgreSQL rejected an
actual connection before the test run. The shared fixture verified the API's
actual database connection at 127.0.0.1:27421/ronditrack_test, applied all seven
migrations, and disposed its container. The original PostgreSQL Windows service
remained running to avoid disrupting unrelated databases. Development database
connections were restored and verified in finally.

```text
Test Run Successful.
Total tests: 62
     Passed: 62
 Total time: 19,7887 Seconds
DEVELOPMENT_DATABASE_RESTORED: ronditrack accepts connections again.
```

Full sanitized milestone evidence:
[assignment-6-1-isolation-evidence.txt](assignment-6-1-isolation-evidence.txt).
The test suite adds 37 cases to the original 25. Initial expanded verification
found a deleted-user 403; validation was fixed to return 401, then the complete
suite was rerun successfully. No remaining test failures or build warnings.

## Migrations

The four original migration IDs are unchanged and preserved in Infrastructure.
The fresh container applies these plus:

* `20261011174110_AddUserAuthentication` — nullable legacy PasswordHash, computed
  NormalizedEmail and unique index.
* `20261011174943_AddRefreshTokenSessions` — login families and hashed tokens,
  unique hash index, expiry/spent/revocation fields and foreign keys.
* `20261011175640_AddPlatformRole` — ordinary User role default for existing rows.

Source, designers and model snapshot are committed in Infrastructure/Migrations.
Only the four old migrations remain applied on development; no upgrade was
attempted after discovering the duplicate-email blocker.

## Sanitized actual authentication logs

```text
Login succeeded. UserId: 9500b8e7-56f6-499a-b1bc-16d62a18249b
Refresh succeeded. UserId: 9500b8e7-56f6-499a-b1bc-16d62a18249b; FamilyId: ab4843cb-ba89-4170-b01f-2fbc6573451b
Refresh reuse detected; login family revoked. FamilyId: ab4843cb-ba89-4170-b01f-2fbc6573451b
```

These are test identifiers from the actual run. AuthService logs identifiers and
events, never passwords, JWTs, raw refresh values or signing keys. EF sensitive
data logging is not enabled. The excerpts substantiate the implemented event
logging; they do not guarantee external proxies or future middleware never log
headers. Keep credentials and token headers out of those logs too.

## Commits and exact files

| Phase | Commit | Message |
|---|---|---|
| 7 | dca4979 | Add password hash to User and validated registration endpoint |
| 8 | 4a08eb2 | Add JWT issuing validation and indistinguishable login failures |
| 9 | 961903d | Add hashed refresh sessions with atomic rotation reuse detection and logout |
| 10 | fa8b64d | Require authentication by default and preserve anonymous API documentation |
| 11 | 96ac6bf | Enforce live stokvel resource permissions and platform policies |
| 12 | 2bcdffb | Verify every scoped route and reject tokens for deleted users |

Phase 13's documentation commit is recorded in final delivery/Git history.
[Phase record](assignment-6-1-phase-verification.md) contains exact file changes,
reasons, navigation, verification and outstanding work for each phase. The
[exact file manifest](assignment-6-1-file-manifest.txt) lists each committed
phase's paths. No files were moved during Assignment 6.1. README was expanded
with the full endpoint-by-role/status matrix and additional DI lifetimes.

## Request-flow demonstration in VS Code

Business request: StokvelsController.RecordContribution →
StokvelAuthorization.EnsureAsync → StokvelAccessHandler.HandleRequirementAsync →
EfStokvelMemberRepository.GetByStokvelAndUserAsync → authorization succeeds →
ContributionService.RecordContributionAsync → ContributionRepository.AddAsync →
RondiTrackDbContext.SaveChangesAsync → PostgreSQL. Existing business rules,
idempotency, ETags, pagination and payout transactions remain tested.

Authentication: AuthController.Register → AuthService.RegisterAsync →
PasswordHasher<User>.HashPassword → UserRepository.AddAsync → DbContext/PostgreSQL.
AuthController.Login → AuthService.LoginAsync → PasswordHasher.VerifyHashedPassword
→ EfRefreshTokenRepository.StartSessionAsync + TokenService.CreateAccessToken →
JWT bearer validation → live platform claims → controller resource authorization.
AuthController.Refresh → AuthService.RefreshAsync →
EfRefreshTokenRepository.RotateAsync → new JWT and refresh cookie.
AuthController.Logout → AuthService.LogoutAsync →
EfRefreshTokenRepository.RevokeSessionAsync → cookie deletion.

PayoutsController.Create → authorization helper → PayoutService.CreatePayoutAsync
→ DbContext directly → PostgreSQL transaction; this preserves the existing service
design rather than adding a repository just for the assignment.

## Remaining gaps and impact

* Development migration blocked by duplicate normalized emails. The user chose
  to preserve development data. Private cleanup documents are excluded from
  published history; see [the sanitized note](../../development-migration-status.md).
  Authentication against the existing development database requires an approved
  duplicate resolution and schema upgrade.
* Legacy passwordless users need an approved password-setup process. No public
  role-promotion or password-reset endpoint was added; neither is requested.
* Logout/reuse revoke refresh families; existing access JWTs expire after five
  minutes plus 30 seconds skew. Current membership/platform permissions still
  change immediately through live database checks.
* Cookie expiry can outlast the absolute family boundary; PostgreSQL enforces
  the capped token expiry and rejects it there. Cookie flags assume HTTPS and
  a same-site frontend; client implementation is outside this assignment.

Source implementation and fresh-database integration verification are complete.
The existing development database upgrade remains explicitly blocked.
