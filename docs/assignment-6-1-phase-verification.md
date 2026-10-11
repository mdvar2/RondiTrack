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
