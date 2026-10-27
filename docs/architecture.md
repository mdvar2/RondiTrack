# Architecture and dependency injection

This is the detailed companion to the [System Architecture](../README.md#system-architecture)
section of the main README. I separated these projects in Assignment 5.4 and kept their
responsibilities while adding authentication and telemetry. A scoped service lasts for one
request; a singleton lasts for the application process.

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

- **Registration:** MembershipService, ContributionService, PayoutService, AuthService,
  MemberHomeService, StokvelDetailService
- **Lifetime:** Scoped
- **Reason:** Scoped repository/DbContext dependencies.

- **Registration:** CycleCloseProcessor, HousekeepingProcessor
- **Lifetime:** Scoped, created per run by the hosted `CycleCloseWorker` and `HousekeepingWorker`
- **Reason:** Each run uses its own DbContext.

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

- **Registration:** IIdempotencyStore -> PostgresIdempotencyStore
- **Lifetime:** Scoped
- **Reason:** Idempotency records are persisted in PostgreSQL (since Assignment 6.4), so they
  survive restarts and are shared by every API instance. The earlier process-local
  `IdempotencyStore` remains in the code but is not registered.

The unregistered in-memory ContributionCycleRepository remains for service tests; production
DI uses EfContributionCycleRepository.

## The two frontends

Both frontends use the same API contract, generated from the Development API's
`/openapi/v1.json` with openapi-typescript, but they reach the API differently.

| | [`web/`](../web/README.md) (React + Vite, Assignments 7.1–7.4) | [`web-next/`](../web-next/README.md) (Next.js, Assignment 8.1) |
|---|---|---|
| Who calls the API | The browser, cross-origin (CORS allows `http://localhost:3000`) | Only the Next.js server |
| Access token | In browser memory | Inside an encrypted, HttpOnly `session` cookie; never readable by the browser |
| Refresh token | `__Secure-ronditrack_refresh` cookie set by the API, sent by the browser | Read by the Next.js server from the login response and kept in the encrypted session |
| Token refresh | Single-flight refresh on 401 | Not yet (Day 3); a 401 sends the person to `/login?expired=1` |
| Data fetching | TanStack Query in the browser | Async Server Components |

Request flow for `web-next/`: browser → Next.js Server Component → `src/lib/api.ts` (adds
`Authorization: Bearer …` from the session) → API → PostgreSQL. `src/lib/session.ts` and
`src/lib/api.ts` import `server-only`, so a Client Component that imports them fails the build.

## Observability registrations

I added the singleton `RondiTrackTelemetry` in Assignment 6.2. It owns the
ActivitySource; `IMeterFactory` owns the Meter. The OpenTelemetry pipeline
subscribes to both names. These additions do not change DbContext-dependent
service lifetimes. Source registrations are in [Program.cs](../API/Program.cs).
