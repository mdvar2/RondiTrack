# Assignment 5.4 phase verification

## Phase 2 ? Clean Architecture split

Status: Completed. API references Domain and Infrastructure; Infrastructure references Domain; Domain has no package or project references. Tests reference API and Infrastructure. Existing files moved with git mv; namespaces/imports updated. Domain query records replace API DTO dependencies in repository contracts; controllers map them to the original response shapes. Existing migration IDs and operations are preserved. No schema migration was created.

Navigation: Domain/Entities/User.cs; Domain/Repositories/IContributionRepository.cs; Domain/Queries/ContributionWithMember.cs; Infrastructure/Data/RondiTrackDbContext.cs; Infrastructure/Repositories/ContributionRepository.cs/GetByCyclePagedAsync; API/Controllers/ContributionCyclesController.cs/GetContributions; API/DTOs/Contributions/ContributionWithMemberResponse.cs/FromQuery; API/Program.cs.

Verification: dotnet restore RondiTrack.sln --disable-parallel (outside sandbox) succeeded; dotnet build RondiTrack.sln --no-restore -m:1 succeeded, 0 warnings/errors; dotnet test RondiTrack.Tests/RondiTrack.Tests.csproj --no-restore -m:1 passed 25, failed 0, skipped 0, duration 4 s (development database still running). Assertion statement comparison against 6e3bfa1 passed for every existing test. Domain dependency checks passed.

Errors: sandbox NuGet restore failed NU1301/NU1900 because network access was refused; unrestricted restore resolved it. Initial sandbox parallel build returned failure without compiler errors; serial build succeeded. Outstanding: container tests belong to Phases 5?6; Docker installation is authorized and in progress.

Exact Phase 2 file manifest (R = moved, M = modified, A = created):

```text
R096	Controllers/ContributionCyclesController.cs	API/Controllers/ContributionCyclesController.cs
R100	Controllers/PayoutsController.cs	API/Controllers/PayoutsController.cs
R097	Controllers/StokvelsController.cs	API/Controllers/StokvelsController.cs
R098	Controllers/UsersController.cs	API/Controllers/UsersController.cs
R093	DTOs/ContributionCycles/ContributionCycleResponse.cs	API/DTOs/ContributionCycles/ContributionCycleResponse.cs
R100	DTOs/ContributionCycles/CreateContributionCycleRequest.cs	API/DTOs/ContributionCycles/CreateContributionCycleRequest.cs
R100	DTOs/ContributionCycles/UpdateContributionCycleRequest.cs	API/DTOs/ContributionCycles/UpdateContributionCycleRequest.cs
R094	DTOs/Contributions/ContributionResponse.cs	API/DTOs/Contributions/ContributionResponse.cs
R072	DTOs/Contributions/ContributionWithMemberResponse.cs	API/DTOs/Contributions/ContributionWithMemberResponse.cs
R100	DTOs/Contributions/RecordContributionRequest.cs	API/DTOs/Contributions/RecordContributionRequest.cs
R100	DTOs/Payouts/CreatePayoutRequest.cs	API/DTOs/Payouts/CreatePayoutRequest.cs
R094	DTOs/Payouts/PayoutResponse.cs	API/DTOs/Payouts/PayoutResponse.cs
R100	DTOs/Stokvels/CreateStokvelRequest.cs	API/DTOs/Stokvels/CreateStokvelRequest.cs
R069	DTOs/Stokvels/StokvelMemberResponse.cs	API/DTOs/Stokvels/StokvelMemberResponse.cs
R092	DTOs/Stokvels/StokvelResponse.cs	API/DTOs/Stokvels/StokvelResponse.cs
R100	DTOs/Stokvels/UpdateStokvelRequest.cs	API/DTOs/Stokvels/UpdateStokvelRequest.cs
R100	DTOs/Users/CreateUserRequest.cs	API/DTOs/Users/CreateUserRequest.cs
R100	DTOs/Users/UpdateUserRequest.cs	API/DTOs/Users/UpdateUserRequest.cs
R090	DTOs/Users/UserResponse.cs	API/DTOs/Users/UserResponse.cs
R098	Filters/ValidationFilter.cs	API/Filters/ValidationFilter.cs
R098	Handlers/GlobalExceptionHandler.cs	API/Handlers/GlobalExceptionHandler.cs
R100	Idempotency/IIdempotencyStore.cs	API/Idempotency/IIdempotencyStore.cs
R100	Idempotency/IdempotencyRecord.cs	API/Idempotency/IdempotencyRecord.cs
R100	Idempotency/IdempotencyStore.cs	API/Idempotency/IdempotencyStore.cs
R094	Program.cs	API/Program.cs
R100	Properties/launchSettings.json	API/Properties/launchSettings.json
R078	RondiTrack.csproj	API/RondiTrack.csproj
R100	RondiTrack.http	API/RondiTrack.http
R097	Services/ContributionService.cs	API/Services/ContributionService.cs
R095	Services/MembershipService.cs	API/Services/MembershipService.cs
R097	Services/PayoutService.cs	API/Services/PayoutService.cs
R100	Validators/CreateContributionCycleRequestValidator.cs	API/Validators/CreateContributionCycleRequestValidator.cs
R100	Validators/CreateStokvelRequestValidator.cs	API/Validators/CreateStokvelRequestValidator.cs
R100	Validators/CreateUserRequestValidator.cs	API/Validators/CreateUserRequestValidator.cs
R100	Validators/RecordContributionRequestValidator.cs	API/Validators/RecordContributionRequestValidator.cs
R100	Validators/UpdateContributionCycleRequestValidator.cs	API/Validators/UpdateContributionCycleRequestValidator.cs
R100	Validators/UpdateStokvelRequestValidator.cs	API/Validators/UpdateStokvelRequestValidator.cs
R100	Validators/UpdateUserRequestValidator.cs	API/Validators/UpdateUserRequestValidator.cs
R100	appsettings.Development.json	API/appsettings.Development.json
R100	appsettings.json	API/appsettings.json
R082	Common/PagedResult.cs	Domain/Common/PagedResult.cs
R097	Models/Contribution.cs	Domain/Entities/Contribution.cs
R097	Models/ContributionCycle.cs	Domain/Entities/ContributionCycle.cs
R097	Models/Payout.cs	Domain/Entities/Payout.cs
R098	Models/Stokvel.cs	Domain/Entities/Stokvel.cs
R096	Models/StokvelMember.cs	Domain/Entities/StokvelMember.cs
R096	Models/User.cs	Domain/Entities/User.cs
R078	Exceptions/BusinessRuleException.cs	Domain/Exceptions/BusinessRuleException.cs
R077	Exceptions/NotFoundException.cs	Domain/Exceptions/NotFoundException.cs
R079	Exceptions/PreconditionFailedException.cs	Domain/Exceptions/PreconditionFailedException.cs
R080	Exceptions/RequestValidationException.cs	Domain/Exceptions/RequestValidationException.cs
R078	Exceptions/RondiTrackException.cs	Domain/Exceptions/RondiTrackException.cs
R085	Repositories/IContributionCycleRepository.cs	Domain/Repositories/IContributionCycleRepository.cs
R075	Repositories/IContributionRepository.cs	Domain/Repositories/IContributionRepository.cs
R073	Repositories/IStokvelMemberRepository.cs	Domain/Repositories/IStokvelMemberRepository.cs
R079	Repositories/IStokvelRepository.cs	Domain/Repositories/IStokvelRepository.cs
R078	Repositories/IUserRepository.cs	Domain/Repositories/IUserRepository.cs
R098	Data/RondiTrackDbContext.cs	Infrastructure/Data/RondiTrackDbContext.cs
R089	Migrations/20261001040953_InitialCreate.Designer.cs	Infrastructure/Migrations/20261001040953_InitialCreate.Designer.cs
R098	Migrations/20261001040953_InitialCreate.cs	Infrastructure/Migrations/20261001040953_InitialCreate.cs
R082	Migrations/20261004202213_ConfigureRelationshipsAndQueryModel.Designer.cs	Infrastructure/Migrations/20261004202213_ConfigureRelationshipsAndQueryModel.Designer.cs
R099	Migrations/20261004202213_ConfigureRelationshipsAndQueryModel.cs	Infrastructure/Migrations/20261004202213_ConfigureRelationshipsAndQueryModel.cs
R085	Migrations/20261005211608_AddDatabaseDefenseConstraints.Designer.cs	Infrastructure/Migrations/20261005211608_AddDatabaseDefenseConstraints.Designer.cs
R088	Migrations/20261005211608_AddDatabaseDefenseConstraints.cs	Infrastructure/Migrations/20261005211608_AddDatabaseDefenseConstraints.cs
R085	Migrations/20261006014557_AddContributionPaginationIndex.Designer.cs	Infrastructure/Migrations/20261006014557_AddContributionPaginationIndex.Designer.cs
R090	Migrations/20261006014557_AddContributionPaginationIndex.cs	Infrastructure/Migrations/20261006014557_AddContributionPaginationIndex.cs
R085	Migrations/RondiTrackDbContextModelSnapshot.cs	Infrastructure/Migrations/RondiTrackDbContextModelSnapshot.cs
R092	Repositories/ContributionCycleRepository.cs	Infrastructure/Repositories/ContributionCycleRepository.cs
R095	Repositories/ContributionRepository.cs	Infrastructure/Repositories/ContributionRepository.cs
R092	Repositories/EfContributionCycleRepository.cs	Infrastructure/Repositories/EfContributionCycleRepository.cs
R094	Repositories/EfStokvelMemberRepository.cs	Infrastructure/Repositories/EfStokvelMemberRepository.cs
R090	Repositories/StokvelRepository.cs	Infrastructure/Repositories/StokvelRepository.cs
R090	Repositories/UserRepository.cs	Infrastructure/Repositories/UserRepository.cs
M	RondiTrack.Tests/ConcurrencyProtectionTests.cs
M	RondiTrack.Tests/ContributionServiceTests.cs
M	RondiTrack.Tests/DatabaseConstraintTests.cs
M	RondiTrack.Tests/MembershipServiceTests.cs
M	RondiTrack.Tests/PayoutTransactionTests.cs
M	RondiTrack.Tests/RondiTrack.Tests.csproj
M	RondiTrack.Tests/TestContributionRepository.cs
M	RondiTrack.sln
A	Domain/Domain.csproj
A	Domain/Queries/ContributionWithMember.cs
A	Domain/Queries/StokvelMemberDetails.cs
A	Infrastructure/Infrastructure.csproj
A	docs/assignment-5-4-phase-verification.md
```

## Phase 3 ? EF Core compatibility

Status: Completed. Modified API/RondiTrack.csproj, API/Program.cs and this report. API explicitly pins EF Core and Relational 10.0.4 to match Infrastructure; EF Core Design remains 10.0.4 with PrivateAssets=all. Program.cs explicitly selects the Infrastructure migrations assembly. No CS1705 occurred, because the versions were aligned before testing.

Navigation: API/RondiTrack.csproj/PackageReference; API/Program.cs/UseNpgsql; Infrastructure/Migrations. Verification: dotnet build RondiTrack.sln -m:1 succeeded, 0 warnings/errors; dotnet ef migrations list --project Infrastructure --startup-project API --no-build listed all four existing migrations as applied; dotnet ef migrations has-pending-model-changes --project Infrastructure --startup-project API --no-build reported no model changes. No migration history or database data was recreated. Regression suite last passed 25/25 in Phase 2; no application behavior changed in this phase. Outstanding: container verification in Phases 5?6.

## Phase 4 ? Dependency injection

Status: Completed. Modified README.md and this report. No registrations were changed: DbContext, five repositories and three services are already Scoped. Singleton IdempotencyStore and GlobalExceptionHandler have no DbContext dependency. README documents application, validator and framework registration lifetimes, plus updated API project and EF CLI paths. Navigation: API/Program.cs/AddScoped and AddSingleton; API/Idempotency/IdempotencyStore.cs; README.md/Dependency-injection lifetime audit. Phase 3 build passed 0 warnings/errors; Phase 2 tests passed 25/25. This phase changes documentation only, so no repeat build/test was necessary. Outstanding: Docker installation and container verification.

## Phase 5 ? PostgreSQL Testcontainers

Status: Partially completed (implemented and compiled; runtime verification blocked on Docker). Added Testcontainers.PostgreSql 4.15.0 as the only new library. Created RondiTrack.Tests/TestSupport/PostgresApiFactory.cs and PostgresCollection.cs. Converted all 11 test classes, including direct-DbContext service, transaction, constraint and concurrency tests, to the shared collection. Removed development User Secrets reads from tests. No assertion statement changed; verified against baseline 6e3bfa1.

Navigation: PostgresApiFactory/ConfigureWebHost uses the exact early-read key ConnectionStrings:RondiTrackDb with UseSetting; InitializeAsync starts PostgreSQL 17, resolves the API's real context, validates its endpoint and applies the preserved migrations; CreateDbContextOptions supplies direct test contexts; explicit IAsyncLifetime.DisposeAsync disposes the host and container. PostgresCollection serializes database classes; existing GUID-based setup isolates records. README documents prerequisites, lifecycle and isolation.

Verification: dotnet build RondiTrack.sln --no-restore -m:1 succeeded, 0 warnings/errors. dotnet test RondiTrack.Tests/RondiTrack.Tests.csproj --no-build --no-restore --list-tests discovered all 25 tests. Assertion statement comparison passed. dotnet list Domain/Domain.csproj reference found no references; dotnet list Domain/Domain.csproj package --no-restore found no packages. git diff --check passed. Container test execution attempted with no Docker engine: failed 25, passed 0, skipped 0; all failures occurred during shared fixture construction, before assertions.

Environment: Docker Desktop 4.94.0 installer downloaded and hash verified through winget, with user authorization. Initial installation failed (exit 4294967291); installer log says administrator elevation was canceled. One authorized retry is pending its Windows UAC prompt. Development PostgreSQL remains running. Isolation preflight found only postgres and ronditrack non-template databases, and no other active client sessions. No service was stopped while Docker was unavailable.

Outstanding: complete Docker installation/startup, execute tests successfully against the container, then perform Phase 6 with development PostgreSQL unavailable and restore its original state. Assignment 5.4 is not complete; Assignment 6.1 has not started.

Exact Phase 5 files (M = modified, A = created):

```text
M	README.md
M	RondiTrack.Tests/ConcurrencyProtectionTests.cs
M	RondiTrack.Tests/ContributionBusinessRuleTests.cs
M	RondiTrack.Tests/ContributionServiceTests.cs
M	RondiTrack.Tests/DatabaseConstraintTests.cs
M	RondiTrack.Tests/EdgeCaseTests.cs
M	RondiTrack.Tests/HappyPathTests.cs
M	RondiTrack.Tests/IdempotencyTests.cs
M	RondiTrack.Tests/MembershipBusinessRuleTests.cs
M	RondiTrack.Tests/MembershipServiceTests.cs
M	RondiTrack.Tests/NegativePathTests.cs
M	RondiTrack.Tests/PayoutTransactionTests.cs
M	RondiTrack.Tests/RondiTrack.Tests.csproj
A	RondiTrack.Tests/TestSupport/PostgresApiFactory.cs
A	RondiTrack.Tests/TestSupport/PostgresCollection.cs
M	docs/assignment-5-4-phase-verification.md
```
