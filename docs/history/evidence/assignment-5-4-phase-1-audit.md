# Assignment 5.4 ? Phase 1 audit and migration plan

Audit date: 11 October 2026. Baseline: `main` at `6e3bfa1`. Working branch: `assignment-5-4-clean-architecture`. No AGENTS.md was found in the repository or its checked parent.

## Current solution

`RondiTrack.sln` contains `RondiTrack.csproj` (ASP.NET Core, net10.0) and `RondiTrack.Tests/RondiTrack.Tests.csproj` (xUnit, net10.0); Tests references API. No Domain or Infrastructure project exists. SDK: 10.0.401; ASP.NET Core runtime: 10.0.12.

API packages: FluentValidation and DI extensions 12.1.1; OpenApi 10.0.12; EF Core Design 10.0.4 (PrivateAssets=all); Npgsql EF provider 10.0.3; Scalar 2.17.7. Resolved EF Core/Relational: 10.0.4. Tests: coverlet.collector 6.0.4; MVC.Testing 10.0.12; Test.Sdk 17.14.1; xunit 2.9.3; runner 3.1.4.

Entities are in Models; DbContext in Data; interfaces and implementations in Repositories; services in Services; controllers in Controllers. PayoutService directly holds a DbContext and must stay in API under this assignment. ContributionCycleRepository is an unregistered in-memory implementation used by service tests; it will be preserved in Infrastructure.

All five registered repositories and MembershipService, ContributionService, PayoutService are Scoped. DbContext is Scoped. IdempotencyStore is Singleton and only holds an in-memory dictionary; no lifetime corrections are needed. Validators are registered with the default Scoped lifetime. GlobalExceptionHandler is Singleton and only holds a logger.

## Tests and baseline verification

25 Facts across 11 test classes. Seven HTTP classes use IClassFixture<WebApplicationFactory<Program>>. MembershipServiceTests, ContributionServiceTests, PayoutTransactionTests and DatabaseConstraintTests read the development connection string directly; ConcurrencyProtectionTests combines HTTP and direct database access. Every class must use the shared container fixture, including direct DbContext tests. Unique GUID-based setup isolates test records; the shared collection serializes database classes. No assertions will change.

`dotnet build RondiTrack.sln --no-restore -m:1`: succeeded, 0 warnings, 0 errors. Initial parallel sandbox build failed with no compiler errors; serial build succeeded.

`dotnet test RondiTrack.Tests/RondiTrack.Tests.csproj --no-restore -m:1`: sandbox run failed 17, passed 8 because Windows Event Log access was denied. The same command outside the sandbox passed 25, failed 0, skipped 0 (3 seconds). These baseline tests still use development PostgreSQL.

`dotnet ef migrations list --no-build`: all four migrations listed without Pending:

- 20261001040953_InitialCreate
- 20261004202213_ConfigureRelationshipsAndQueryModel
- 20261005211608_AddDatabaseDefenseConstraints
- 20261006014557_AddContributionPaginationIndex

Development PostgreSQL is the running Windows service `postgresql-x64-17`, not a Docker container. Docker CLI and the standard Docker Desktop executable are absent. Docker installation or access to a remote engine is required before Testcontainers can run; user decision requested. No PostgreSQL service has been stopped.

## File-by-file plan (before source changes)

Repository interfaces currently return API DTOs. Introduce Domain query records ContributionWithMember and StokvelMemberDetails, then map those records back to the existing API DTOs in controllers. Preserve JSON contracts, filtering, projections, pagination tokens and queries. Domain must never reference API.

| Existing file | Planned destination/action |
|---|---|
| `Models/Contribution.cs` | `Domain/Entities/Contribution.cs` (git mv) |
| `Models/ContributionCycle.cs` | `Domain/Entities/ContributionCycle.cs` (git mv) |
| `Models/Payout.cs` | `Domain/Entities/Payout.cs` (git mv) |
| `Models/Stokvel.cs` | `Domain/Entities/Stokvel.cs` (git mv) |
| `Models/StokvelMember.cs` | `Domain/Entities/StokvelMember.cs` (git mv) |
| `Models/User.cs` | `Domain/Entities/User.cs` (git mv) |
| `Exceptions/BusinessRuleException.cs` | `Domain/Exceptions/BusinessRuleException.cs` (git mv) |
| `Exceptions/NotFoundException.cs` | `Domain/Exceptions/NotFoundException.cs` (git mv) |
| `Exceptions/PreconditionFailedException.cs` | `Domain/Exceptions/PreconditionFailedException.cs` (git mv) |
| `Exceptions/RequestValidationException.cs` | `Domain/Exceptions/RequestValidationException.cs` (git mv) |
| `Exceptions/RondiTrackException.cs` | `Domain/Exceptions/RondiTrackException.cs` (git mv) |
| `Common/PagedResult.cs` | `Domain/Common/PagedResult.cs` (git mv) |
| `Data/RondiTrackDbContext.cs` | `Infrastructure/Data/RondiTrackDbContext.cs` (git mv) |
| `Migrations/20261001040953_InitialCreate.cs` | `Infrastructure/Migrations/20261001040953_InitialCreate.cs` (git mv) |
| `Migrations/20261001040953_InitialCreate.Designer.cs` | `Infrastructure/Migrations/20261001040953_InitialCreate.Designer.cs` (git mv) |
| `Migrations/20261004202213_ConfigureRelationshipsAndQueryModel.cs` | `Infrastructure/Migrations/20261004202213_ConfigureRelationshipsAndQueryModel.cs` (git mv) |
| `Migrations/20261004202213_ConfigureRelationshipsAndQueryModel.Designer.cs` | `Infrastructure/Migrations/20261004202213_ConfigureRelationshipsAndQueryModel.Designer.cs` (git mv) |
| `Migrations/20261005211608_AddDatabaseDefenseConstraints.cs` | `Infrastructure/Migrations/20261005211608_AddDatabaseDefenseConstraints.cs` (git mv) |
| `Migrations/20261005211608_AddDatabaseDefenseConstraints.Designer.cs` | `Infrastructure/Migrations/20261005211608_AddDatabaseDefenseConstraints.Designer.cs` (git mv) |
| `Migrations/20261006014557_AddContributionPaginationIndex.cs` | `Infrastructure/Migrations/20261006014557_AddContributionPaginationIndex.cs` (git mv) |
| `Migrations/20261006014557_AddContributionPaginationIndex.Designer.cs` | `Infrastructure/Migrations/20261006014557_AddContributionPaginationIndex.Designer.cs` (git mv) |
| `Migrations/RondiTrackDbContextModelSnapshot.cs` | `Infrastructure/Migrations/RondiTrackDbContextModelSnapshot.cs` (git mv) |
| `Controllers/ContributionCyclesController.cs` | `API/Controllers/ContributionCyclesController.cs` (git mv) |
| `Controllers/PayoutsController.cs` | `API/Controllers/PayoutsController.cs` (git mv) |
| `Controllers/StokvelsController.cs` | `API/Controllers/StokvelsController.cs` (git mv) |
| `Controllers/UsersController.cs` | `API/Controllers/UsersController.cs` (git mv) |
| `DTOs/ContributionCycles/ContributionCycleResponse.cs` | `API/DTOs/ContributionCycles/ContributionCycleResponse.cs` (git mv) |
| `DTOs/ContributionCycles/CreateContributionCycleRequest.cs` | `API/DTOs/ContributionCycles/CreateContributionCycleRequest.cs` (git mv) |
| `DTOs/ContributionCycles/UpdateContributionCycleRequest.cs` | `API/DTOs/ContributionCycles/UpdateContributionCycleRequest.cs` (git mv) |
| `DTOs/Contributions/ContributionResponse.cs` | `API/DTOs/Contributions/ContributionResponse.cs` (git mv) |
| `DTOs/Contributions/ContributionWithMemberResponse.cs` | `API/DTOs/Contributions/ContributionWithMemberResponse.cs` (git mv) |
| `DTOs/Contributions/RecordContributionRequest.cs` | `API/DTOs/Contributions/RecordContributionRequest.cs` (git mv) |
| `DTOs/Payouts/CreatePayoutRequest.cs` | `API/DTOs/Payouts/CreatePayoutRequest.cs` (git mv) |
| `DTOs/Payouts/PayoutResponse.cs` | `API/DTOs/Payouts/PayoutResponse.cs` (git mv) |
| `DTOs/Stokvels/CreateStokvelRequest.cs` | `API/DTOs/Stokvels/CreateStokvelRequest.cs` (git mv) |
| `DTOs/Stokvels/StokvelMemberResponse.cs` | `API/DTOs/Stokvels/StokvelMemberResponse.cs` (git mv) |
| `DTOs/Stokvels/StokvelResponse.cs` | `API/DTOs/Stokvels/StokvelResponse.cs` (git mv) |
| `DTOs/Stokvels/UpdateStokvelRequest.cs` | `API/DTOs/Stokvels/UpdateStokvelRequest.cs` (git mv) |
| `DTOs/Users/CreateUserRequest.cs` | `API/DTOs/Users/CreateUserRequest.cs` (git mv) |
| `DTOs/Users/UpdateUserRequest.cs` | `API/DTOs/Users/UpdateUserRequest.cs` (git mv) |
| `DTOs/Users/UserResponse.cs` | `API/DTOs/Users/UserResponse.cs` (git mv) |
| `Validators/CreateContributionCycleRequestValidator.cs` | `API/Validators/CreateContributionCycleRequestValidator.cs` (git mv) |
| `Validators/CreateStokvelRequestValidator.cs` | `API/Validators/CreateStokvelRequestValidator.cs` (git mv) |
| `Validators/CreateUserRequestValidator.cs` | `API/Validators/CreateUserRequestValidator.cs` (git mv) |
| `Validators/RecordContributionRequestValidator.cs` | `API/Validators/RecordContributionRequestValidator.cs` (git mv) |
| `Validators/UpdateContributionCycleRequestValidator.cs` | `API/Validators/UpdateContributionCycleRequestValidator.cs` (git mv) |
| `Validators/UpdateStokvelRequestValidator.cs` | `API/Validators/UpdateStokvelRequestValidator.cs` (git mv) |
| `Validators/UpdateUserRequestValidator.cs` | `API/Validators/UpdateUserRequestValidator.cs` (git mv) |
| `Services/ContributionService.cs` | `API/Services/ContributionService.cs` (git mv) |
| `Services/MembershipService.cs` | `API/Services/MembershipService.cs` (git mv) |
| `Services/PayoutService.cs` | `API/Services/PayoutService.cs` (git mv) |
| `Handlers/GlobalExceptionHandler.cs` | `API/Handlers/GlobalExceptionHandler.cs` (git mv) |
| `Filters/ValidationFilter.cs` | `API/Filters/ValidationFilter.cs` (git mv) |
| `Idempotency/IdempotencyRecord.cs` | `API/Idempotency/IdempotencyRecord.cs` (git mv) |
| `Idempotency/IdempotencyStore.cs` | `API/Idempotency/IdempotencyStore.cs` (git mv) |
| `Idempotency/IIdempotencyStore.cs` | `API/Idempotency/IIdempotencyStore.cs` (git mv) |
| `Properties/launchSettings.json` | `API/Properties/launchSettings.json` (git mv) |
| `Repositories/ContributionCycleRepository.cs` | `Infrastructure/Repositories/ContributionCycleRepository.cs` (git mv) |
| `Repositories/ContributionRepository.cs` | `Infrastructure/Repositories/ContributionRepository.cs` (git mv) |
| `Repositories/EfContributionCycleRepository.cs` | `Infrastructure/Repositories/EfContributionCycleRepository.cs` (git mv) |
| `Repositories/EfStokvelMemberRepository.cs` | `Infrastructure/Repositories/EfStokvelMemberRepository.cs` (git mv) |
| `Repositories/IContributionCycleRepository.cs` | `Domain/Repositories/IContributionCycleRepository.cs` (git mv) |
| `Repositories/IContributionRepository.cs` | `Domain/Repositories/IContributionRepository.cs` (git mv) |
| `Repositories/IStokvelMemberRepository.cs` | `Domain/Repositories/IStokvelMemberRepository.cs` (git mv) |
| `Repositories/IStokvelRepository.cs` | `Domain/Repositories/IStokvelRepository.cs` (git mv) |
| `Repositories/IUserRepository.cs` | `Domain/Repositories/IUserRepository.cs` (git mv) |
| `Repositories/StokvelRepository.cs` | `Infrastructure/Repositories/StokvelRepository.cs` (git mv) |
| `Repositories/UserRepository.cs` | `Infrastructure/Repositories/UserRepository.cs` (git mv) |
| `RondiTrack.csproj` | `API/RondiTrack.csproj` (git mv) |
| `Program.cs` | `API/Program.cs` (git mv) |
| `appsettings.json` | `API/appsettings.json` (git mv) |
| `appsettings.Development.json` | `API/appsettings.Development.json` (git mv) |
| `RondiTrack.http` | `API/RondiTrack.http` (git mv) |
| `RondiTrack.Tests/ConcurrencyProtectionTests.cs` | Keep location; update imports/contracts and container setup only |
| `RondiTrack.Tests/ContributionBusinessRuleTests.cs` | Keep location; update imports/contracts and container setup only |
| `RondiTrack.Tests/ContributionServiceTests.cs` | Keep location; update imports/contracts and container setup only |
| `RondiTrack.Tests/DatabaseConstraintTests.cs` | Keep location; update imports/contracts and container setup only |
| `RondiTrack.Tests/EdgeCaseTests.cs` | Keep location; update imports/contracts and container setup only |
| `RondiTrack.Tests/HappyPathTests.cs` | Keep location; update imports/contracts and container setup only |
| `RondiTrack.Tests/IdempotencyTests.cs` | Keep location; update imports/contracts and container setup only |
| `RondiTrack.Tests/MembershipBusinessRuleTests.cs` | Keep location; update imports/contracts and container setup only |
| `RondiTrack.Tests/MembershipServiceTests.cs` | Keep location; update imports/contracts and container setup only |
| `RondiTrack.Tests/NegativePathTests.cs` | Keep location; update imports/contracts and container setup only |
| `RondiTrack.Tests/PayoutTransactionTests.cs` | Keep location; update imports/contracts and container setup only |
| `RondiTrack.Tests/TestContributionRepository.cs` | Keep location; update imports/contracts and container setup only |
| `RondiTrack.Tests/RondiTrack.Tests.csproj` | Reference API and Infrastructure; add Testcontainers.PostgreSql 4.15.0 in Phase 5 |
| `RondiTrack.sln` | Update API path and add Domain and Infrastructure |
| `README.md` | Update paths/setup, lifetime table, actual verification and split errors |

Create Domain/Domain.csproj (no package/project dependencies), Infrastructure/Infrastructure.csproj (Domain reference, existing EF provider/design), Domain/Queries/ContributionWithMember.cs and StokvelMemberDetails.cs, and RondiTrack.Tests/TestSupport/PostgresApiFactory.cs plus PostgresCollection.cs. Update namespaces consistently; preserve existing migration IDs, operations and table names. No new migration is required for this split.
