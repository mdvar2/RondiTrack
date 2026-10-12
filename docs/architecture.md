# Architecture and dependency injection

This is the detailed companion to [the main guide](../README.md#3-project-architecture). I
separated these projects in Assignment 5.4 and kept their responsibilities while adding
authentication and telemetry. A scoped service lasts for one request; a singleton lasts for
the application process.

Open [RondiTrack.sln](../RondiTrack.sln) in VS Code from the repository root.

## Project responsibilities

- **Project:** [API](../API/RondiTrack.csproj)
- **Responsibility and dependencies:** Controllers, DTOs, validators, application services,
  auth, error handling and DI composition; references Domain and Infrastructure.

- **Project:** [Domain](../Domain/Domain.csproj)
- **Responsibility and dependencies:** Entities, exceptions, repository interfaces, query
  records and pagination types; no project or NuGet dependencies.

- **Project:** [Infrastructure](../Infrastructure/Infrastructure.csproj)
- **Responsibility and dependencies:** RondiTrackDbContext, EF repositories and migrations;
  references Domain.

- **Project:** [RondiTrack.Tests](../RondiTrack.Tests/RondiTrack.Tests.csproj)
- **Responsibility and dependencies:** xUnit business, regression, auth and database
  integration tests; references API and Infrastructure.

I kept all projects targeting net10.0. EF Core/Relational/Design are pinned to 10.0.4, Npgsql
provider to 10.0.3, JWT bearer/OpenAPI to 10.0.12, FluentValidation to 12.1.1, Scalar to
2.17.7 and Testcontainers.PostgreSql to 4.15.0. Linked project files are authoritative.

## Contribution request flow

The existing contribution flow is: `StokvelsController.RecordContribution` ->
`StokvelAuthorization.EnsureAsync` -> `StokvelAccessHandler.HandleRequirementAsync` (live
membership lookup) -> `ContributionService.RecordContributionAsync` ->
`ContributionRepository.AddAsync` -> `RondiTrackDbContext.SaveChangesAsync` -> PostgreSQL.
Controllers also call repositories directly for simple CRUD/queries.
`PayoutService.CreatePayoutAsync` intentionally retains direct DbContext access and its
existing explicit transaction rather than adding a payout repository. Infrastructure projects
to Domain query records; API maps these to response DTOs.

## Dependency injection

Registrations are in [API/Program.cs](../API/Program.cs).

## Lifetime inventory

- **Registration:** RondiTrackDbContext
- **Lifetime:** Scoped
- **Reason:** Request unit of work.

- **Registration:** IUserRepository -> UserRepository
- **Lifetime:** Scoped
- **Reason:** Uses scoped DbContext.

- **Registration:** IStokvelRepository -> StokvelRepository
- **Lifetime:** Scoped
- **Reason:** Uses scoped DbContext.

- **Registration:** IContributionRepository -> ContributionRepository
- **Lifetime:** Scoped
- **Reason:** Uses scoped DbContext.

- **Registration:** IContributionCycleRepository -> EfContributionCycleRepository
- **Lifetime:** Scoped
- **Reason:** Uses scoped DbContext.

- **Registration:** IStokvelMemberRepository -> EfStokvelMemberRepository
- **Lifetime:** Scoped
- **Reason:** Uses scoped DbContext.

- **Registration:** IRefreshTokenRepository -> EfRefreshTokenRepository
- **Lifetime:** Scoped
- **Reason:** Transactional family locking.

- **Registration:** MembershipService, ContributionService, PayoutService, AuthService
- **Lifetime:** Scoped
- **Reason:** Scoped repository/DbContext dependencies.

- **Registration:** `IPasswordHasher<User>` -> `PasswordHasher<User>`
- **Lifetime:** Scoped
- **Reason:** Authentication hashing/verification.

- **Registration:** StokvelAuthorization, IAuthorizationHandler -> StokvelAccessHandler
- **Lifetime:** Scoped
- **Reason:** Live resource authorization.

- **Registration:** IClaimsTransformation -> PlatformRoleClaimsTransformation
- **Lifetime:** Scoped
- **Reason:** Current platform role from PostgreSQL.

- **Registration:** FluentValidation validators
- **Lifetime:** Scoped
- **Reason:** Assembly registration.

- **Registration:** JwtOptions, TokenService
- **Lifetime:** Singleton
- **Reason:** Configuration/signing without scoped dependencies.

- **Registration:** IIdempotencyStore -> IdempotencyStore
- **Lifetime:** Singleton
- **Reason:** Process-local state without DbContext.

The unregistered in-memory ContributionCycleRepository remains for service tests; production
DI uses EfContributionCycleRepository.

## Observability registrations

I added the singleton `RondiTrackTelemetry` in Assignment 6.2. It owns the
ActivitySource; `IMeterFactory` owns the Meter. The OpenTelemetry pipeline
subscribes to both names. These additions do not change DbContext-dependent
service lifetimes. Source registrations are in [Program.cs](../API/Program.cs).
