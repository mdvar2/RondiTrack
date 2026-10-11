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
