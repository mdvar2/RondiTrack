# RondiTrack

RondiTrack is a .NET 10 Web API for managing users, stokvels, memberships, contribution cycles, member contributions, and payouts.

The project uses a hybrid persistence approach while it is being migrated from in-memory storage to PostgreSQL. PostgreSQL and Entity Framework Core now persist stokvel memberships, contribution cycles, contributions, and payouts. Users and stokvels still use their existing in-memory repositories, and the idempotency store also remains in memory.

The application demonstrates request and response DTOs, repository abstractions, service-layer business rules, FluentValidation, centralized RFC 9457 error handling, correlation IDs, idempotent contribution recording, PostgreSQL persistence, EF Core migrations, Npgsql retry configuration, and explicit database transactions.

## Technologies

- .NET 10
- ASP.NET Core Web API
- Entity Framework Core 10
- PostgreSQL 17
- Npgsql Entity Framework Core provider
- FluentValidation
- Built-in OpenAPI support
- Scalar API Reference
- xUnit
- Microsoft.AspNetCore.Mvc.Testing
- .NET User Secrets
- In-memory storage for repositories not yet migrated to EF Core
- In-memory idempotency store

---

# Running the Project From a Clean Machine

Assignment 5.1 introduced PostgreSQL as a real persistence layer. The following steps document how another developer can reproduce my development setup from a clean Windows machine.

## 1. Prerequisites

Install:

- .NET 10 SDK
- Git
- PostgreSQL 17

Confirm .NET is available:

```powershell
dotnet --version
```

Confirm Git is available:

```powershell
git --version
```

## 2. Clone the Repository

Clone RondiTrack and enter the project directory:

```powershell
git clone https://github.com/mdvar2/RondiTrack.git
cd RondiTrack
```

Restore the project dependencies:

```powershell
dotnet restore
```

## 3. Install PostgreSQL

I chose to install PostgreSQL locally rather than use a container.

I made this choice because I wanted to work directly with the PostgreSQL Windows service, authentication, command-line client, database creation, and connection configuration.

The development environment uses:

```text
PostgreSQL 17
```

PostgreSQL can be installed on Windows using the PostgreSQL installer or Windows Package Manager.

For example:

```powershell
winget install PostgreSQL.PostgreSQL.17
```

During a normal PostgreSQL installation, configure a password for the local `postgres` administrator account and keep that password private.

After installation, verify that the PostgreSQL service is running.

For example:

```powershell
Get-Service *postgres*
```

The service should report a running state.

## 4. Create the RondiTrack Database

RondiTrack uses a dedicated database named:

```text
ronditrack
```

The database is separate from PostgreSQL's default databases.

From PowerShell, PostgreSQL's command-line client can be started with:

```powershell
& "C:\Program Files\PostgreSQL\17\bin\psql.exe" -U postgres
```

The `&` is required in PowerShell when executing a quoted executable path.

Enter the local PostgreSQL password when prompted.

Inside `psql`, create the database:

```sql
CREATE DATABASE ronditrack;
```

Connect to it:

```text
\c ronditrack
```

List databases if required:

```text
\l
```

Exit using:

```text
\q
```

## 5. Verify PostgreSQL Independently of RondiTrack

Before relying on the API, database connectivity can be tested directly:

```powershell
& "C:\Program Files\PostgreSQL\17\bin\psql.exe" -U postgres -d ronditrack
```

If the PostgreSQL prompt opens successfully, the database is running and reachable independently of the RondiTrack application.

This was an important part of the setup because it separates a PostgreSQL installation or authentication problem from an ASP.NET Core or EF Core problem.

## 6. Configure the Connection String Securely

The PostgreSQL password is not stored in `appsettings.json`, `appsettings.Development.json`, or another tracked source file.

RondiTrack uses .NET User Secrets for the local development connection string.

From the RondiTrack project directory, run:

```powershell
dotnet user-secrets set "ConnectionStrings:RondiTrackDb" "Host=localhost;Port=5432;Database=ronditrack;Username=postgres;Password=YOUR_LOCAL_PASSWORD"
```

Replace:

```text
YOUR_LOCAL_PASSWORD
```

with the password configured on that developer's PostgreSQL installation.

The connection string is stored outside the Git repository under the developer's local user profile.

The project contains only its `UserSecretsId`; the actual secret is never committed.

## 7. Apply the EF Core Migration

The migration files are committed to the repository.

Apply them to the local `ronditrack` database using:

```powershell
dotnet ef database update
```

After the migration has been applied, PostgreSQL contains the RondiTrack tables and EF Core's migration-history table.

## 8. Run the Tests

The current test suite requires the local PostgreSQL instance to be available.

Run:

```powershell
dotnet test .\RondiTrack.Tests\RondiTrack.Tests.csproj
```

The current Assignment 5.1 suite contains:

```text
21 tests
```

The final verified run produced:

```text
total: 21
failed: 0
succeeded: 21
skipped: 0

Build succeeded
```

## 9. Run the API

Start RondiTrack:

```powershell
dotnet run
```

During development the application normally runs at:

```text
http://localhost:5101
```

The Scalar API interface can then be used to exercise the HTTP endpoints.

---

# API Endpoints

## Users

- `GET /api/users` - Get all users
- `GET /api/users/{id}` - Get a user by ID
- `POST /api/users` - Create a user
- `PUT /api/users/{id}` - Update a user
- `DELETE /api/users/{id}` - Delete a user

## Stokvels

- `GET /api/stokvels` - Get all stokvels
- `GET /api/stokvels/{id}` - Get a stokvel by ID
- `POST /api/stokvels` - Create a stokvel
- `PUT /api/stokvels/{id}` - Update a stokvel
- `DELETE /api/stokvels/{id}` - Delete a stokvel

## Membership

- `POST /api/stokvels/{stokvelId}/members/{userId}` - Add a user to a stokvel
- `DELETE /api/stokvels/{stokvelId}/members/{userId}` - Remove a user from a stokvel

Membership is now also represented in PostgreSQL through the `StokvelMember` persistence model.

## Contribution Cycles

- `GET /api/stokvels/{stokvelId}/cycles`
- `GET /api/stokvels/{stokvelId}/cycles/{cycleId}`
- `POST /api/stokvels/{stokvelId}/cycles`
- `PUT /api/stokvels/{stokvelId}/cycles/{cycleId}`
- `DELETE /api/stokvels/{stokvelId}/cycles/{cycleId}`

Example create request:

```json
{
  "period": "2026-09",
  "targetAmount": 5000
}
```

The period uses the `YYYY-MM` format.

## Contributions

```text
POST /api/stokvels/{stokvelId}/members/{userId}/contributions
```

The contribution endpoint requires an `Idempotency-Key` request header.

Example:

```text
Idempotency-Key: contribution-test-001
```

Request:

```json
{
  "amount": 5000,
  "contributionCycleId": "8316ae89-f4b2-4dfc-bbf0-c8ce8b5259b8"
}
```

A contribution references an actual `ContributionCycle` instead of accepting an arbitrary cycle string.

## Payouts

Assignment 5.1 introduced payout processing.

The payout endpoint is:

```text
POST /api/stokvels/{stokvelId}/cycles/{cycleId}/payouts
```

Example request:

```json
{
  "amount": 5000
}
```

Payout processing determines the next eligible recipient, creates a persisted `Payout`, and changes the contribution cycle status as one transaction.

---

# Design Choices

## Controllers

Controllers handle HTTP concerns such as receiving request DTOs, invoking the appropriate repository or service, and returning successful HTTP responses.

Controllers do not manually construct application error responses.

When an operation fails, the appropriate application exception is allowed to reach the centralized exception handler.

This keeps error formatting consistent and avoids repeated `try/catch`, `ProblemDetails`, and status-code logic throughout controllers.

## Domain Models

The current domain/persistence model includes:

- `User`
- `Stokvel`
- `StokvelMember`
- `ContributionCycle`
- `Contribution`
- `Payout`

The models protect their state using private setters and domain methods where appropriate.

Examples include:

```text
UpdateDetails
AddMember
RemoveMember
MarkPaidOut
```

The entities also contain defensive checks so invalid objects cannot easily be created directly.

## Request and Response DTOs

Domain entities are not exposed directly as the HTTP API contract.

Request DTOs include:

- `CreateUserRequest`
- `UpdateUserRequest`
- `CreateStokvelRequest`
- `UpdateStokvelRequest`
- `CreateContributionCycleRequest`
- `UpdateContributionCycleRequest`
- `RecordContributionRequest`
- `CreatePayoutRequest`

Response DTOs include:

- `UserResponse`
- `StokvelResponse`
- `ContributionCycleResponse`
- `ContributionResponse`
- `PayoutResponse`

This separates HTTP contracts from the internal model and reduces the risk of over-posting generated or internal properties.

## Manual Mapping

Mapping remains explicit.

Examples include:

```csharp
UserResponse.FromEntity(user);
StokvelResponse.FromEntity(stokvel);
ContributionCycleResponse.FromEntity(cycle);
ContributionResponse.FromEntity(contribution);
PayoutResponse.FromEntity(payout);
```

Manual mapping remains suitable because the mappings are small and easy to inspect.

---

# Validation vs Business Rules

RondiTrack separates request validation from state-dependent business rules.

## Request Validation — "Is this request well-formed?"

FluentValidation is used for request DTO validation.

Examples include:

- required names;
- valid email format;
- positive monetary values;
- required contribution-cycle IDs; and
- valid contribution-cycle periods using `YYYY-MM`.

Validators do not query repositories to determine whether resources exist.

Malformed JSON/model-binding failures are also routed through the centralized error system.

## Business Rules — "Is this operation allowed?"

After a request is well formed, the application may inspect current state.

Examples include:

- whether a user exists;
- whether a stokvel exists;
- whether a contribution cycle exists;
- whether a user belongs to a stokvel;
- whether a cycle belongs to a stokvel;
- whether a contribution already exists;
- whether an idempotency key conflicts with an earlier request;
- whether a payout cycle is open; and
- whether an eligible payout recipient exists.

These decisions belong to application/business behaviour rather than DTO shape validation.

---

# Domain Exception Hierarchy

RondiTrack uses a small application-specific exception hierarchy based on `RondiTrackException`.

The current types include:

- `RequestValidationException`
- `NotFoundException`
- `BusinessRuleException`

The centralized mapping is:

| Exception | HTTP status |
| --- | --- |
| `RequestValidationException` | `400 Bad Request` |
| `NotFoundException` | `404 Not Found` |
| `BusinessRuleException` | `409 Conflict` |
| Unexpected exception | `500 Internal Server Error` |

## Duplicate Contribution vs Idempotency Conflict

These remain separate checks even though both currently use `BusinessRuleException`.

Same idempotency key with different data:

```text
Same key + different request
→ 409 Conflict
```

Different/new key for an existing member/cycle contribution:

```text
New key + existing member/cycle contribution
→ 409 Conflict
```

---

# Centralized Error Handling

RondiTrack uses one `GlobalExceptionHandler` implementing ASP.NET Core's `IExceptionHandler`.

Controllers and services throw exceptions describing what failed. The global handler translates those exceptions into consistent HTTP responses.

Errors use RFC 9457 Problem Details with:

```text
application/problem+json
```

For example:

```json
{
  "type": "about:blank",
  "title": "Conflict",
  "status": 409,
  "detail": "A contribution already exists for this member and cycle.",
  "instance": "/api/stokvels/.../members/.../contributions",
  "correlationId": "0HN0QKKFQNVLN:00000001"
}
```

---

# Correlation IDs and Structured Logging

Every error response includes the current request's correlation ID.

The centralized exception handler also writes a structured log entry containing that same ID.

This connects a client-visible failure with its corresponding server-side log without exposing internal stack traces to API clients.

---

# Service Layer

Services are used when an operation requires meaningful coordination or business decisions beyond straightforward CRUD.

## MembershipService

`MembershipService` coordinates membership operations.

It checks:

- whether the stokvel exists;
- whether the user exists;
- whether membership already exists; and
- synchronizes the persisted `StokvelMember` representation through `IStokvelMemberRepository`.

## ContributionService

`ContributionService` coordinates contribution recording.

It checks:

- the idempotency key;
- whether the stokvel exists;
- whether the user exists;
- whether the user belongs to the stokvel;
- whether the contribution cycle exists;
- whether the cycle belongs to the stokvel;
- whether the member already contributed to that cycle; and
- whether an idempotency key was previously associated with different request data.

## PayoutService

Assignment 5.1 introduced `PayoutService`.

It coordinates the payout workflow:

1. locate the contribution cycle;
2. verify that the cycle is open;
3. determine the next eligible persisted member;
4. create the payout;
5. persist the payout;
6. mark the cycle as paid out;
7. persist the cycle change; and
8. commit the transaction.

This operation needs a service because it coordinates multiple database operations that must succeed or fail together.

---

# Repository Abstraction and EF Core Migration

Data access continues to be represented through repository abstractions.

These include:

- `IUserRepository`
- `IStokvelRepository`
- `IContributionRepository`
- `IContributionCycleRepository`
- `IStokvelMemberRepository`

Assignment 5.1 changed selected implementations without changing the HTTP contract.

## Contribution Repository Swap

`ContributionRepository` was selected as the primary EF Core repository migration.

I chose it because `ContributionService` contains some of RondiTrack's strongest existing business behaviour:

- membership verification;
- cycle verification;
- cycle ownership;
- duplicate contribution protection; and
- idempotency behaviour.

This made it a useful place to test whether the repository abstraction actually separated business behaviour from persistence.

`ContributionRepository` now uses `RondiTrackDbContext` rather than an in-memory collection.

The contribution controller and DTO contract did not need to be rewritten to perform EF Core queries directly.

### Test-Abstraction Finding

One existing service unit test had directly constructed the old concrete `ContributionRepository`.

Once that concrete repository required a `DbContext`, the test setup exposed that dependency.

A test-only `TestContributionRepository` implementing `IContributionRepository` was introduced so the service-level test could remain isolated from PostgreSQL infrastructure.

This was a useful finding: the production service depended on the repository interface, but part of the test setup had still depended on the old concrete implementation.

## Other EF Core Repositories

`EfContributionCycleRepository` persists contribution cycles.

`EfStokvelMemberRepository` persists membership records.

The following repositories remain in memory:

- `UserRepository`
- `StokvelRepository`

This is deliberate incremental migration rather than claiming that the entire application has already moved to PostgreSQL.

---

# DbContext and Full Schema

RondiTrack uses one:

```text
RondiTrackDbContext
```

The context models all six required entities:

```csharp
public DbSet<User> Users => Set<User>();
public DbSet<Stokvel> Stokvels => Set<Stokvel>();
public DbSet<StokvelMember> StokvelMembers => Set<StokvelMember>();
public DbSet<ContributionCycle> ContributionCycles => Set<ContributionCycle>();
public DbSet<Contribution> Contributions => Set<Contribution>();
public DbSet<Payout> Payouts => Set<Payout>();
```

All six are represented in the EF Core schema even though not every repository has been migrated to EF Core yet.

---

# EF Core Mapping Decision

The property that did not fit the persistence model cleanly was:

```text
Stokvel.Members
```

The domain model exposes members as an `IReadOnlyCollection<User>` backed by a private `_members` collection.

Assignment 5.1 also introduced `StokvelMember` as the explicit persisted representation of membership.

Rather than trying to make EF Core persist both representations as the same relationship, `Stokvel.Members` is explicitly ignored:

```csharp
modelBuilder.Entity<Stokvel>()
    .Ignore(stokvel => stokvel.Members);
```

Persisted membership is represented through the `StokvelMember` table.

This decision keeps the existing domain collection behaviour while providing a separate relational representation for persisted membership.

A consequence of the current hybrid design is that the in-memory `Stokvel.Members` collection and the persisted `StokvelMember` records represent membership in different parts of the application. This is a known transitional gap rather than something hidden by the persistence layer.

---

# Migration Review

The first migration was generated using EF Core migrations.

Before applying it, I inspected the generated migration rather than assuming generated database changes were automatically correct.

I checked specifically for:

- all six expected `CreateTable` operations;
- primary keys;
- the unique membership index; and
- the unique contribution index.

The membership uniqueness rule is protected using:

```text
StokvelId + UserId
```

The contribution uniqueness rule is protected using:

```text
StokvelId + UserId + ContributionCycleId
```

The first generated migration was removed and regenerated after I noticed that the required unique indexes were not represented in the model configuration.

The indexes were configured and the migration was regenerated before being applied.

## Why Migration Review Matters

A migration is a database-schema diff and must be reviewed before execution.

For example, a property rename can sometimes be interpreted as:

```text
DROP old column
ADD new column
```

instead of:

```text
RENAME old column
```

A drop-and-add operation can destroy existing column data.

For that reason, generated migration code must be read before applying it to a database containing important data.

The final migration was applied with:

```powershell
dotnet ef database update
```

The migration files are committed to Git so another developer can reproduce the database schema.

---

# Secret Management

The PostgreSQL connection string includes credentials and therefore must not be committed to Git.

RondiTrack uses .NET User Secrets during local development.

The application requests:

```text
ConnectionStrings:RondiTrackDb
```

from ASP.NET Core configuration.

A teammate configures their own value using:

```powershell
dotnet user-secrets set "ConnectionStrings:RondiTrackDb" "Host=localhost;Port=5432;Database=ronditrack;Username=postgres;Password=YOUR_LOCAL_PASSWORD"
```

No real password is included in the repository.

If the connection string is missing, application startup fails rather than silently running with an invalid database configuration.

---

# Npgsql Retry Configuration

Npgsql is configured with:

```csharp
npgsqlOptions.EnableRetryOnFailure(
    maxRetryCount: 3,
    maxRetryDelay: TimeSpan.FromSeconds(2),
    errorCodesToAdd: null);
```

I selected a maximum of three retries because a small number of retries gives a temporary connection problem an opportunity to recover without causing a long sequence of repeated attempts.

The maximum retry delay is two seconds so that a temporary PostgreSQL/network interruption can be retried while keeping development requests reasonably responsive.

An example of something worth retrying is a temporary database/network connectivity interruption.

An example that should not simply be retried is a permanent constraint failure such as attempting to insert data that violates a unique database constraint.

---

# DbContext and Dependency-Injection Lifetime

The original in-memory repositories were registered as singletons because their collections needed to survive between requests for as long as the application process was running.

A `DbContext` has different lifecycle requirements.

`RondiTrackDbContext` is scoped, and repositories that depend on it are also scoped.

Current EF-backed registrations include:

```text
IContributionRepository
→ ContributionRepository

IContributionCycleRepository
→ EfContributionCycleRepository

IStokvelMemberRepository
→ EfStokvelMemberRepository
```

A DbContext must not be held by a singleton repository.

Sharing one context across unrelated requests could cause tracking conflicts, stale state, concurrency/thread-safety problems, or invalid lifetime behaviour.

The lifetime therefore changes from singleton to scoped for repositories holding the DbContext.

The repositories that remain genuinely in memory keep their previous singleton behaviour.

---

# Money

`decimal` is used for monetary values such as contribution amounts, contribution-cycle targets, and payouts.

`decimal` is appropriate for these values because it avoids the binary floating-point precision behaviour associated with types such as `double`.

---

# Idempotency

Contribution recording remains idempotent.

The endpoint requires:

```text
Idempotency-Key
```

The behaviour is:

- new key + valid request → create contribution;
- same key + same request → return the original contribution result;
- same key + different request → `409 Conflict`;
- new key + duplicate member/cycle contribution → `409 Conflict`.

Idempotency and duplicate-contribution protection solve different problems.

The idempotency store remains in memory, so its records are lost when the application process restarts.

This is documented as a known persistence gap.

---

# Data Storage

RondiTrack is currently in an incremental migration state.

| Data | Current storage |
| --- | --- |
| Users | In memory |
| Stokvels | In memory |
| Stokvel membership records | PostgreSQL / EF Core |
| Contribution cycles | PostgreSQL / EF Core |
| Contributions | PostgreSQL / EF Core |
| Payouts | PostgreSQL / EF Core |
| Idempotency records | In memory |

The PostgreSQL database is therefore real and actively used, but the application has not pretended that every repository has already been migrated.

---

# Payout Processing

Assignment 5.1 introduced `Payout` as a working feature.

A payout records:

- the stokvel;
- the recipient `StokvelMember`;
- the contribution cycle;
- the payout amount; and
- the payment date/time.

## Rotation Rule

The deliberately minimal rotation rule is:

> The next eligible recipient is the earliest joined persisted stokvel member who has not already received a payout for that stokvel.

Eligible members are ordered using:

```text
JoinedAtUtc
```

Members who already appear in the stokvel's payout records are excluded.

I chose this rule because it is deterministic and provides a real payout decision without expanding Assignment 5.1 into scheduling, notifications, partial payouts, or a complete stokvel payout engine.

---

# Explicit Payout Transaction

Payout processing performs multiple related database operations.

The operation:

1. reads the contribution cycle;
2. determines the next eligible recipient;
3. creates a `Payout`;
4. saves the payout;
5. changes the contribution-cycle status to `PaidOut`;
6. saves the cycle update; and
7. commits.

The payout and cycle update must succeed or fail together.

A persisted payout with an open cycle would represent inconsistent state.

Likewise, a cycle marked as paid out without a corresponding payout would also be inconsistent.

For that reason the workflow uses an explicit EF Core database transaction.

Because Npgsql retry-on-failure is enabled, the transaction is executed through EF Core's execution strategy.

The transaction structure therefore uses:

```text
CreateExecutionStrategy
→ ExecuteAsync
→ BeginTransactionAsync
→ database work
→ CommitAsync
```

If an exception occurs, the transaction is rolled back and the exception is allowed to propagate through the application's existing error-handling system.

---

# Transaction Tests

Two additional transaction-related tests were introduced for Assignment 5.1.

## Successful Payout Persistence

The successful path exercises `PayoutService` and then queries PostgreSQL using a fresh DbContext.

The test verifies that:

- the payout exists;
- the correct recipient and amount were persisted; and
- the contribution cycle is `PaidOut`.

## Forced Rollback

The rollback test deliberately opens an EF Core transaction, writes a payout, saves it inside the transaction, and then forces a failure before the contribution-cycle update is allowed to complete.

The transaction is rolled back.

A fresh DbContext then re-queries PostgreSQL.

The test verifies:

```text
No partial Payout remains
AND
ContributionCycle.Status remains Open
```

This proves the database transaction mechanism rolls the first write back rather than leaving partial state.

The current rollback test exercises the EF transaction mechanism directly rather than injecting a failure through `PayoutService` itself. A service-level failure-injection test would be a useful future strengthening of this coverage.

---

# Testing

RondiTrack is tested using xUnit unit and integration tests.

Assignment 5.1 specifically required the existing behaviour to be tested after replacing in-memory persistence with EF Core/PostgreSQL.

## Before the EF Core Migration

At the end of Assignment 4.4, the complete suite contained:

```text
Total: 19
Failed: 0
Succeeded: 19
```

This provides the baseline from before the persistence migration.

## After the EF Core/PostgreSQL Migration

After the repository migration and payout transaction work, the complete suite contains:

```text
Total: 21
Failed: 0
Succeeded: 21
Skipped: 0

Build succeeded
```

The final test output also contained EF Core SQL commands executed against PostgreSQL, including persisted `StokvelMember`, `ContributionCycle`, `Contribution`, and payout-related database operations.

The database therefore was not merely configured in the project; the tests exercised the PostgreSQL-backed persistence.

## Running the Automated Tests

Run:

```powershell
dotnet test .\RondiTrack.Tests\RondiTrack.Tests.csproj
```

For detailed individual test output:

```powershell
dotnet test .\RondiTrack.Tests\RondiTrack.Tests.csproj --logger "console;verbosity=detailed"
```

## Existing Business-Rule Coverage

The suite continues to verify behaviour such as:

- malformed requests;
- nonexistent resources;
- duplicate membership;
- removing a non-member;
- contribution from a non-member;
- cycle ownership;
- duplicate contributions;
- missing idempotency keys;
- same idempotency key with the same request;
- same idempotency key with different request data;
- zero contribution amount; and
- invalid contribution-cycle periods.

## Deliberate RED-to-GREEN Check From Assignment 4.4

The earlier duplicate-membership guard mutation remains useful evidence that the suite can detect a real behavioural regression.

With the duplicate-membership guard disabled, the expected `409 Conflict` became an unexpected `500 Internal Server Error`, causing the test to fail.

After restoring the rule, the same test passed again.

This demonstrates that the test suite is checking behaviour rather than merely executing code.

---

# Definition of Done

Assignment 5.1 extends the previous Definition of Done with:

- `Persisted via EF Core`
- `Explicit transaction tested`

| API area / endpoint | Documented | Validation / business guard | Automated coverage | Status reviewed | Persisted via EF Core | Explicit transaction tested |
| --- | --- | --- | --- | --- | --- | --- |
| `GET /api/users` | Yes | N/A | Happy path | Yes | No | N/A |
| `GET /api/users/{id}` | Yes | Existence | Happy path + not found | Yes | No | N/A |
| `POST /api/users` | Yes | FluentValidation | Happy path | Yes | No | N/A |
| `PUT /api/users/{id}` | Yes | Validation + existence | Contract reviewed | Yes | No | N/A |
| `DELETE /api/users/{id}` | Yes | Existence | Contract reviewed | Yes | No | N/A |
| `GET /api/stokvels` | Yes | N/A | Happy path | Yes | No | N/A |
| `GET /api/stokvels/{id}` | Yes | Existence | Happy path | Yes | No | N/A |
| `POST /api/stokvels` | Yes | FluentValidation | Happy path | Yes | No | N/A |
| `PUT /api/stokvels/{id}` | Yes | Validation + existence | Contract reviewed | Yes | No | N/A |
| `DELETE /api/stokvels/{id}` | Yes | Existence | Contract reviewed | Yes | No | N/A |
| Add stokvel member | Yes | Existence + duplicate rule | Unit + integration | Yes | Yes (`StokvelMember`) | N/A |
| Remove stokvel member | Yes | Existence + membership rule | Integration | Yes | Yes (`StokvelMember`) | N/A |
| `GET` contribution cycles | Yes | Stokvel/cycle checks | Resource coverage | Yes | Yes | N/A |
| `POST` contribution cycle | Yes | Validation + existence | Happy path + edge case | Yes | Yes | N/A |
| `PUT` contribution cycle | Yes | Validation + ownership | Contract reviewed | Yes | Yes | N/A |
| `DELETE` contribution cycle | Yes | Existence + ownership | Contract reviewed | Yes | Yes | N/A |
| Record contribution | Yes | Membership + cycle + duplicate + idempotency | Unit + integration + edge cases | Yes | Yes | N/A |
| Process payout | Yes | Cycle + recipient + amount + rotation rules | Transaction tests | Yes | Yes | Yes |

`N/A` means an explicit transaction is not required for that operation.

---

# Known Gaps and Deliberate Scope Decisions

## Users and Stokvels Remain In Memory

`User` and `Stokvel` are represented in the EF Core model, but their existing repositories remain in memory.

Assignment 5.1 required at least one real repository swap rather than requiring every repository to be migrated at once.

These are therefore reported honestly as not yet migrated.

## Idempotency Remains In Memory

The idempotency store is still memory-based.

A process restart clears its entries.

Persisting idempotency records would be a future improvement.

## Hybrid Membership Representation

`Stokvel.Members` remains part of the existing domain model while `StokvelMember` provides the new persisted membership representation.

This transitional design allowed the persistence work to be introduced incrementally, but a later iteration could consolidate membership into one persistence-aware model.

## Relational Constraints

The initial database schema does not yet introduce foreign-key relationships between every modelled table.

This is partly a consequence of the hybrid migration state, where related `User` and `Stokvel` repositories remain in memory while contribution-related data is persisted.

Adding complete relational relationships and constraints is future persistence work rather than being hidden as if it were already complete.

## Payout Rotation

The current payout rule represents one simple pass through eligible members.

Once every persisted member has received a payout, the current implementation has no next eligible member.

A later version could introduce explicit rotation rounds.

That was intentionally left outside Assignment 5.1 because the payout requirement asked for a minimal real rule rather than a full scheduling system.

## Rollback Coverage

The current tests prove:

- the real `PayoutService` successful transaction path; and
- database rollback through a deliberately interrupted explicit transaction.

The forced rollback is currently tested directly at the EF transaction level rather than by injecting a failure into `PayoutService`.

A future test could add a controlled failure point to prove the exact service orchestration rollback path without changing production behaviour.

## Test Setup Exposed a Concrete Dependency

An Assignment 4.4 service test directly constructed the old concrete `ContributionRepository`.

When that repository became EF-backed, the test setup needed to use a test implementation of `IContributionRepository`.

The business rule being tested did not change, but the migration exposed that the test itself had depended on an implementation detail.

This is recorded as a migration finding rather than being hidden.

---

# Asynchronous Operations

Repository, service, controller, and EF Core operations use Task-based asynchronous contracts where appropriate.
Controllers await repository and service operations instead of synchronously blocking on tasks.
The move from in-memory collections to PostgreSQL demonstrates why those asynchronous boundaries were useful: database access introduces real I/O without requiring controllers to synchronously block on it.

