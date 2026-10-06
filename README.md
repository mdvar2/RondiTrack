# RondiTrack

RondiTrack is a .NET 10 Web API for managing users, stokvels,
memberships, contribution cycles, member contributions, and payouts.

The application uses PostgreSQL with Entity Framework Core for its main
domain data. `User`, `Stokvel`, `StokvelMember`, `ContributionCycle`,
`Contribution`, and `Payout` are represented in the EF Core model and
persisted in PostgreSQL. The idempotency store remains in memory.

The project demonstrates request/response DTOs, repository abstractions,
service-layer business rules, FluentValidation, centralized RFC 9457
error handling, correlation IDs, idempotent contribution recording, EF
Core migrations, explicit relationships, composite keys, query-behaviour
analysis, `AsNoTracking()`, N+1 detection and correction, Npgsql retry
configuration, and explicit database transactions.

## Technologies

-   .NET 10
-   ASP.NET Core Web API
-   Entity Framework Core 10
-   PostgreSQL 17
-   Npgsql Entity Framework Core provider
-   FluentValidation
-   Built-in OpenAPI support
-   Scalar API Reference
-   xUnit
-   Microsoft.AspNetCore.Mvc.Testing
-   .NET User Secrets

------------------------------------------------------------------------

# Running the Project From a Clean Windows Machine

This section is intentionally detailed so that a developer who does
**not already have PostgreSQL installed** can clone RondiTrack and
reproduce the development environment.

## 1. Install the prerequisites

You need:

-   .NET 10 SDK
-   Git
-   PostgreSQL 17
-   PowerShell or Windows Terminal

Verify .NET:

``` powershell
dotnet --version
```

Verify Git:

``` powershell
git --version
```

A .NET 10 SDK version should be reported by the first command and a Git
version by the second.

## 2. Clone RondiTrack

``` powershell
git clone https://github.com/mdvar2/RondiTrack.git
cd RondiTrack
dotnet restore
```

`dotnet restore` downloads the NuGet dependencies declared by the
solution.

## 3. Install PostgreSQL 17

RondiTrack uses a local PostgreSQL installation rather than Docker.

PostgreSQL can be installed using the official Windows installer or
Windows Package Manager. With `winget`:

``` powershell
winget install PostgreSQL.PostgreSQL.17
```

During installation:

1.  Keep the PostgreSQL server component selected.
2.  Keep the command-line tools selected so that `psql` is installed.
3.  The default port `5432` can be used unless it is already occupied.
4.  The installer asks for a password for the PostgreSQL administrator
    account named `postgres`.
5.  Choose a password you can remember and keep it private.
6.  Do **not** put that password in Git or in this README.

The `postgres` account is the local PostgreSQL database administrator.
It is not a RondiTrack application user.

## 4. Verify the PostgreSQL Windows service

After installation:

``` powershell
Get-Service *postgres*
```

The PostgreSQL service should show a `Running` status.

If it is installed but stopped, start the service from Windows Services
or with an appropriately elevated PowerShell session.

## 5. Verify `psql`

Try:

``` powershell
psql --version
```

If PowerShell reports that `psql` is not recognized, PostgreSQL may be
installed correctly but its `bin` directory may not be on the Windows
`PATH`.

For PostgreSQL 17, run it directly:

``` powershell
& "C:\Program Files\PostgreSQL\17\bin\psql.exe" --version
```

The `&` is PowerShell's call operator and is needed when an executable
path is quoted.

You can continue using the full path without modifying `PATH`.

If desired, add this directory to the Windows `PATH` later:

``` text
C:\Program Files\PostgreSQL\17\bin
```

After changing `PATH`, open a new terminal before trying `psql` again.

## 6. Connect to PostgreSQL before configuring RondiTrack

Test PostgreSQL independently of the application:

``` powershell
& "C:\Program Files\PostgreSQL\17\bin\psql.exe" -U postgres
```

Enter the local PostgreSQL password when prompted.

A successful connection gives a prompt similar to:

``` text
postgres=#
```

This step is useful because it separates PostgreSQL
installation/authentication problems from ASP.NET Core or EF Core
problems.

## 7. Create the RondiTrack database

At the `psql` prompt:

``` sql
CREATE DATABASE ronditrack;
```

List databases:

``` text
\l
```

Connect to the new database:

``` text
\c ronditrack
```

You should see confirmation that you are connected to `ronditrack`.

Exit:

``` text
\q
```

If `CREATE DATABASE` reports that `ronditrack` already exists, do not
create a second database. Verify that you can connect to the existing
one.

You can also test the database directly from PowerShell:

``` powershell
& "C:\Program Files\PostgreSQL\17\bin\psql.exe" -U postgres -d ronditrack
```

## 8. Configure the connection string securely

The PostgreSQL password must not be committed to the repository.

RondiTrack reads:

``` text
ConnectionStrings:RondiTrackDb
```

from ASP.NET Core configuration and uses .NET User Secrets during local
development.

From the RondiTrack project directory:

``` powershell
dotnet user-secrets set "ConnectionStrings:RondiTrackDb" "Host=localhost;Port=5432;Database=ronditrack;Username=postgres;Password=YOUR_LOCAL_PASSWORD"
```

Replace `YOUR_LOCAL_PASSWORD` with the password configured during
PostgreSQL installation.

Do not paste the real password into source code, `appsettings.json`,
`appsettings.Development.json`, the README, screenshots, or Git commits.

Verify that the secret key exists:

``` powershell
dotnet user-secrets list
```

The connection-string key should appear. Treat the displayed connection
string as sensitive because the command may show the password.

## 9. Verify the EF Core CLI

Check:

``` powershell
dotnet ef --version
```

If `dotnet ef` is unavailable, install the EF Core command-line tool:

``` powershell
dotnet tool install --global dotnet-ef
```

If it is already installed but needs updating:

``` powershell
dotnet tool update --global dotnet-ef
```

Open a new terminal if the tool is installed but the command is not
immediately recognized.

## 10. Inspect the migrations before applying them

List the migrations:

``` powershell
dotnet ef migrations list
```

RondiTrack commits migration files to Git so that another developer can
reproduce the schema.

Before applying a migration to important data, read the generated
migration. Migrations are schema changes and generated code must not
automatically be assumed safe.

## 11. Apply the migrations

``` powershell
dotnet ef database update
```

This applies the committed EF Core migrations to the local `ronditrack`
database.

RondiTrack currently models these six application tables:

``` text
Users
Stokvels
StokvelMembers
ContributionCycles
Contributions
Payouts
```

EF Core also maintains:

``` text
__EFMigrationsHistory
```

## 12. Verify the database schema

Connect:

``` powershell
& "C:\Program Files\PostgreSQL\17\bin\psql.exe" -U postgres -d ronditrack
```

List tables:

``` text
\dt
```

Check migration history:

``` sql
SELECT * FROM "__EFMigrationsHistory";
```

Exit:

``` text
\q
```

This confirms that the database exists and that migrations were actually
applied rather than only generated in the project.

## 13. Build the project

``` powershell
dotnet build
```

A successful build should end with:

``` text
Build succeeded
```

## 14. Run the automated tests

The integration tests require the local PostgreSQL service and the
configured RondiTrack connection string.

``` powershell
dotnet test .\RondiTrack.Tests\RondiTrack.Tests.csproj
```

For detailed output:

``` powershell
dotnet test .\RondiTrack.Tests\RondiTrack.Tests.csproj --logger "console;verbosity=detailed"
```

The current regression run produced:

``` text
Total: 25
Failed: 0
Succeeded: 25
Skipped: 0

Build succeeded
```

## 15. Run the API

``` powershell
dotnet run
```

During development the application normally runs at:

``` text
http://localhost:5101
```

The Scalar API interface can then be used to exercise the HTTP
endpoints.

## 16. Common setup problems

### `psql` is not recognized

Use:

``` powershell
& "C:\Program Files\PostgreSQL\17\bin\psql.exe"
```

or add PostgreSQL's `bin` directory to `PATH`.

### Password authentication failed

Confirm that the username is `postgres` and that the connection string
contains the password configured for the local PostgreSQL installation.

Do not change the application to hard-code the password.

### Connection refused on port 5432

Check:

``` powershell
Get-Service *postgres*
```

Also confirm that the PostgreSQL installation uses port `5432`.

### Database `ronditrack` does not exist

Connect as `postgres` and create it:

``` sql
CREATE DATABASE ronditrack;
```

### Connection string is missing

Run the User Secrets command in the RondiTrack project directory:

``` powershell
dotnet user-secrets set "ConnectionStrings:RondiTrackDb" "Host=localhost;Port=5432;Database=ronditrack;Username=postgres;Password=YOUR_LOCAL_PASSWORD"
```

### `dotnet ef` is not recognized

Install or update the global `dotnet-ef` tool, then open a new terminal.

### Migration fails because existing rows violate a new foreign key

Do not immediately delete the database or hide deletion inside a
migration. Inspect the existing data and the generated migration first.
Decide whether the data must be transformed, preserved, or---only when
it is disposable development data---explicitly cleaned.

------------------------------------------------------------------------

# API Endpoints

## Users

-   `GET /api/users`
-   `GET /api/users/{id}`
-   `POST /api/users`
-   `PUT /api/users/{id}`
-   `DELETE /api/users/{id}`

## Stokvels

-   `GET /api/stokvels`
-   `GET /api/stokvels/{id}`
-   `POST /api/stokvels`
-   `PUT /api/stokvels/{id}`
-   `DELETE /api/stokvels/{id}`

## Membership

-   `POST /api/stokvels/{stokvelId}/members/{userId}`
-   `DELETE /api/stokvels/{stokvelId}/members/{userId}`

Membership is persisted through the explicit `StokvelMember` join
entity.

## Contribution Cycles

-   `GET /api/stokvels/{stokvelId}/cycles`
-   `GET /api/stokvels/{stokvelId}/cycles/{cycleId}`
-   `POST /api/stokvels/{stokvelId}/cycles`
-   `PUT /api/stokvels/{stokvelId}/cycles/{cycleId}`
-   `DELETE /api/stokvels/{stokvelId}/cycles/{cycleId}`

Example create request:

``` json
{
  "period": "2026-09",
  "targetAmount": 5000
}
```

The period uses `YYYY-MM`.

## Contributions

Record a contribution:

``` text
POST /api/stokvels/{stokvelId}/members/{userId}/contributions
```

The request requires an `Idempotency-Key` header.

Example:

``` text
Idempotency-Key: contribution-test-001
```

Request:

``` json
{
  "amount": 5000,
  "contributionCycleId": "8316ae89-f4b2-4dfc-bbf0-c8ce8b5259b8"
}
```

Read the contributions for a cycle:

``` text
GET /api/stokvels/{stokvelId}/cycles/{cycleId}/contributions
```

This Assignment 5.2 endpoint returns contribution information together
with the member information required by the response DTO.

## Payouts

``` text
POST /api/stokvels/{stokvelId}/cycles/{cycleId}/payouts
```

Example:

``` json
{
  "amount": 5000
}
```

Payout processing determines the next eligible recipient, persists the
payout, and changes the contribution-cycle status within one
transaction.

------------------------------------------------------------------------

# Architecture and Design Choices

## Controllers

Controllers handle HTTP concerns: receiving DTOs, invoking
repositories/services, and returning successful HTTP responses.

Application exceptions are allowed to reach the centralized exception
handler instead of duplicating `try/catch` and `ProblemDetails`
construction in every controller.

## Domain Models

The current model contains:

-   `User`
-   `Stokvel`
-   `StokvelMember`
-   `ContributionCycle`
-   `Contribution`
-   `Payout`

Entities protect their state with private setters and domain methods
where appropriate.

## DTOs

Domain entities are not exposed directly as the HTTP request contract.
Request/response DTOs keep HTTP contracts separate from internal
persistence/domain objects and reduce over-posting risk.

Assignment 5.2 also introduced `ContributionWithMemberResponse` for the
cycle-contributions query.

## Validation vs business rules

FluentValidation verifies request shape, such as required names, email
format, positive monetary values, required IDs, and valid `YYYY-MM`
periods.

State-dependent decisions are handled as business behaviour, including:

-   whether a user/stokvel/cycle exists;
-   whether a membership exists;
-   whether a cycle belongs to a stokvel;
-   whether a contribution already exists;
-   whether an idempotency key conflicts;
-   whether a payout cycle is open; and
-   whether an eligible payout recipient exists.

## Domain exception hierarchy

  Exception                      HTTP status
  ------------------------------ -----------------------------
  `RequestValidationException`   `400 Bad Request`
  `NotFoundException`            `404 Not Found`
  `BusinessRuleException`        `409 Conflict`
  Unexpected exception           `500 Internal Server Error`

Errors are returned using RFC 9457 Problem Details and include a
correlation ID.

------------------------------------------------------------------------

# Repository and Persistence Design

Data access is represented through:

-   `IUserRepository`
-   `IStokvelRepository`
-   `IContributionRepository`
-   `IContributionCycleRepository`
-   `IStokvelMemberRepository`

Repositories backed by `RondiTrackDbContext` are scoped because a
`DbContext` must not be held by a singleton across unrelated HTTP
requests.

The six main domain/persistence entities are represented in PostgreSQL.
The idempotency store remains in memory and is therefore cleared when
the process restarts.

------------------------------------------------------------------------

# Assignment 5.2 --- Relationships and Query Behavior

Assignment 5.2 moved RondiTrack beyond simply having tables in
PostgreSQL. It introduced real relational navigation, key design,
foreign keys, measured query behaviour, and explicit read-only query
decisions.

## User ↔ Stokvel many-to-many relationship

`User` and `Stokvel` have a real many-to-many relationship through the
explicit `StokvelMember` join entity.

`StokvelMember` contains:

``` text
UserId
StokvelId
Role
JoinedAtUtc
```

It also has navigation properties to `User` and `Stokvel`.

### Why the join entity has a composite natural key

The key is:

``` text
(UserId, StokvelId)
```

configured using Fluent API.

The pair naturally identifies one user's membership in one stokvel. A
separate synthetic `Guid Id` would add another identifier even though
the membership already has a meaningful unique identity.

The composite primary key also lets PostgreSQL prevent two membership
rows for the same user/stokvel pair.

## Consequence of removing `StokvelMember.Id`

Removing a single-column membership ID means another entity cannot
reference membership using one `StokvelMemberId`.

RondiTrack therefore references a specific membership using the same two
values that identify it.

For `Contribution`:

``` text
(UserId, StokvelId)
    ↓
StokvelMember(UserId, StokvelId)
```

For `Payout`:

``` text
(RecipientUserId, StokvelId)
    ↓
StokvelMember(UserId, StokvelId)
```

A user ID alone is insufficient because the same user can belong to
multiple stokvels.

An alternative would have been to retain a surrogate membership ID and
also keep a unique constraint on `(UserId, StokvelId)`. That can
simplify foreign keys, but it was not selected because this assignment
deliberately uses the natural membership identity as the primary key.

## Repository consequence of the composite key

A repository design that assumes every entity can be retrieved by one
`Guid Id` does not fit `StokvelMember`.

RondiTrack already had a dedicated `IStokvelMemberRepository`, so it was
kept as the membership-specific abstraction rather than forcing
membership through a generic single-ID repository.

Membership lookup is based on both:

``` text
stokvelId
userId
```

The repository now also separates:

``` text
ExistsAsync(...)
```

for existence-only checks from:

``` text
GetByStokvelAndUserAsync(...)
```

when a tracked membership entity is required for removal.

## ContributionCycle → Contribution relationship

Assignment 5.2 wires the real one-to-many relationship:

``` text
ContributionCycle
    1
    |
    many
Contribution
```

`ContributionCycle` exposes its contributions and `Contribution` has a
`Cycle` navigation.

This relationship was chosen because the required Assignment 5.2 query
is specifically "get contributions for a cycle".

A `Stokvel → ContributionCycle` navigation was not added in this
iteration. `ContributionCycle` still contains `StokvelId`, but that
additional navigation is deliberately left as not yet implemented rather
than being claimed as complete.

------------------------------------------------------------------------

# Assignment 5.2 Migration Review

The relationship change required a real ALTER migration rather than
deleting and recreating the schema.

The generated migration was inspected before application.

## Unsafe generated payout rename

The generated migration initially treated:

``` text
Payouts.StokvelMemberId
```

as though it could simply become:

``` text
RecipientUserId
```

That was not semantically safe.

The old `StokvelMemberId` contained the former membership entity's
surrogate ID. It did **not** contain `UserId`.

The migration was corrected to:

1.  add `RecipientUserId` as nullable;
2.  join existing payouts to `StokvelMembers` using the old membership
    ID;
3.  copy the corresponding membership `UserId`;
4.  verify that no `RecipientUserId` remains null;
5.  make the new column non-nullable;
6.  remove the old membership-ID column;
7.  replace the membership surrogate key with `(UserId, StokvelId)`;
8.  create the required indexes and foreign keys.

This avoids pretending that two differently-meaningful GUID values are
interchangeable.

## Existing development-data inspection

Before applying the new foreign keys, the local database was inspected.

The development database contained legacy rows that did not have valid
parent rows for the new relationships. In particular, the existing
membership/cycle data could not be reconciled with `User` and `Stokvel`
parent rows.

Because these were disposable local development/test rows, they were
explicitly cleaned **after inspection**.

The migration itself was not modified to silently delete arbitrary
application data, and the database was not simply recreated to hide the
relationship problem.

This matters because on a real production database the correct response
would be to design an appropriate data migration rather than truncate
valuable data.

------------------------------------------------------------------------

# Query Behavior and the N+1 Experiment

Assignment 5.2 required the query behaviour of:

``` text
GET /api/stokvels/{stokvelId}/cycles/{cycleId}/contributions
```

to be measured rather than estimated.

EF Core SQL command logging is enabled during development with:

``` json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning",
      "Microsoft.EntityFrameworkCore.Database.Command": "Information"
    }
  }
}
```

A measurement dataset with **10 contributions from 10 different
members** in one contribution cycle was used.

## Version 1 --- deliberately naive relationship loading

The first implementation loaded the contribution rows and then
explicitly loaded:

``` text
Contribution.Member
Contribution.Member.User
```

for each contribution separately.

Measured SQL query count:

``` text
1  Stokvel lookup
1  ContributionCycle lookup
1  Contributions query
10 StokvelMember queries
10 User queries
-------------------------
23 SQL queries total
```

The result was therefore **23 measured database queries** for one
endpoint request returning 10 contributions.

This implementation was intentionally temporary so that the N+1
behaviour could be observed in the EF Core SQL log.

No lazy-loading proxies were introduced.

## Version 2 --- eager loading

The second implementation used:

``` csharp
.Include(contribution => contribution.Member)
.ThenInclude(member => member.User)
```

Measured query count:

``` text
1 Stokvel lookup
1 ContributionCycle lookup
1 joined contribution/member/user query
-----------------------------------------
3 SQL queries total
```

This reduced the request from:

``` text
23 → 3 queries
```

The joined entity query selected **13 columns** across `Contribution`,
`StokvelMember`, and `User`.

## Version 3 --- projection

The final implementation projects directly to
`ContributionWithMemberResponse` using `Select(...)`.

Measured query count:

``` text
1 Stokvel lookup
1 ContributionCycle lookup
1 joined projected query
--------------------------
3 SQL queries total
```

The projected relationship query selects only the **9 columns required
by the response DTO**:

``` text
Contribution.Id
Contribution.StokvelId
Contribution.UserId
User.Name
User.Email
StokvelMember.Role
Contribution.ContributionCycleId
Contribution.Amount
Contribution.RecordedAtUtc
```

## Why projection is shipped

  ----------------------------------------------------------------------------------
  Implementation             Total SQL queries   Relationship-result Shipped?
                                                             columns 
  ----------------------- -------------------- --------------------- ---------------
  Naive explicit per-row                    23      Multiple per-row No
  loading                                                    queries 

  Eager                                      3                    13 No
  `Include/ThenInclude`                                              

  DTO projection with                        3                     9 Yes
  `Select`                                                           
  ----------------------------------------------------------------------------------

Eager loading and projection both remove the N+1 round trips.

Projection is shipped because this endpoint returns a DTO and does not
need to modify the loaded entities. It achieves the same measured
three-query request while retrieving only the response fields required
by the endpoint instead of materializing the complete related entity
graph.

------------------------------------------------------------------------

# Loading Strategy

RondiTrack does not use lazy-loading proxies.

For the Assignment 5.2 contribution endpoint, explicit per-row loading
was used only as the deliberately naive version to expose and measure
the N+1 problem.

Eager loading was then implemented and measured as one valid fix when a
complete related entity graph is needed.

The final endpoint uses projection because only response data is
required.

Other read paths that do not require related navigation data avoid
loading a relationship graph unnecessarily. For example, listing or
locating a contribution cycle does not need its full contributions graph
unless the caller specifically requests contributions.

------------------------------------------------------------------------

# `AsNoTracking()` Audit

Read-only EF Core queries use `AsNoTracking()` where entities do not
need to be modified.

A blanket `AsNoTracking()` was **not** added to every `GetByIdAsync`
method because some existing methods are shared with mutation paths.

The repository APIs therefore distinguish read-only and tracked use.

Examples:

``` text
GetByIdReadOnlyAsync
    → read-only
    → AsNoTracking

GetByIdAsync
    → tracked
    → used where the entity will be changed
```

This distinction is important for operations such as updating a
`ContributionCycle` or marking a cycle as `PaidOut`.

For membership:

``` text
ExistsAsync
    → existence-only read

GetByStokvelAndUserAsync
    → tracked lookup when membership will be removed
```

`ContributionService` uses read-only stokvel/user/cycle lookups and an
existence-only membership check because it does not mutate those
objects; it creates a new `Contribution`.

The payout path retains a tracked contribution-cycle lookup because it
calls `MarkPaidOut()` and persists the changed cycle.

------------------------------------------------------------------------

# Secret Management

The PostgreSQL connection string contains credentials and must not be
committed.

RondiTrack uses .NET User Secrets:

``` powershell
dotnet user-secrets set "ConnectionStrings:RondiTrackDb" "Host=localhost;Port=5432;Database=ronditrack;Username=postgres;Password=YOUR_LOCAL_PASSWORD"
```

No real PostgreSQL password belongs in the repository.

------------------------------------------------------------------------

# Npgsql Retry Configuration

Npgsql is configured with a small retry policy for temporary
connectivity failures:

``` csharp
npgsqlOptions.EnableRetryOnFailure(
    maxRetryCount: 3,
    maxRetryDelay: TimeSpan.FromSeconds(2),
    errorCodesToAdd: null);
```

A temporary database/network interruption may be worth retrying.

A permanent data problem such as a unique-constraint violation should
not simply be retried as though it were a transient connection failure.

------------------------------------------------------------------------

# Idempotency

Contribution recording requires:

``` text
Idempotency-Key
```

Behaviour:

-   new key + valid request → create contribution;
-   same key + same request → return the original result;
-   same key + different request → `409 Conflict`;
-   new key + duplicate member/cycle contribution → `409 Conflict`.

Idempotency and duplicate-contribution protection solve different
problems.

The idempotency store remains in memory, so its records are lost when
the process restarts.

------------------------------------------------------------------------

# Payout Processing and Transaction

The payout rule selects the earliest joined persisted member who has not
already received a payout for that stokvel.

Payout processing performs multiple related database operations:

1.  locate the contribution cycle;
2.  verify that it is open;
3.  determine the next eligible recipient;
4.  create and save the payout;
5.  mark the cycle as paid out;
6.  save the cycle;
7.  commit.

The payout and cycle update must succeed or fail together, so the
workflow uses an explicit EF Core database transaction.

Because Npgsql retry-on-failure is enabled, the explicit transaction is
executed through EF Core's execution strategy.

The successful transaction test verifies the persisted payout and
`PaidOut` cycle using a fresh `DbContext`.

The rollback test deliberately interrupts a transaction and verifies
with a fresh `DbContext` that no partial payout remains and the cycle
remains open. The forced rollback currently tests the EF transaction
mechanism directly rather than injecting a failure through
`PayoutService`; service-level failure injection remains a possible
future improvement.

------------------------------------------------------------------------

# Database Defense, Pagination, and Concurrency

The current implementation enforces the assignment's database-safety
concerns at both the application and persistence layers.

## Database-enforced uniqueness and business-rule guards

The EF Core model configures the key uniqueness rules that are already
required by the application business logic:

-   `StokvelMember` uses the composite natural key `(UserId, StokvelId)`;
-   `Contribution` enforces uniqueness for `(StokvelId, UserId, ContributionCycleId)`;
-   `Payout` enforces uniqueness for `(StokvelId, ContributionCycleId)`;
-   PostgreSQL row-version metadata is mapped for all mutable entities using
    `.IsRowVersion()` on the `Version` property.

This means a duplicate member, a duplicate contribution for a member and
cycle, or a second payout for the same cycle cannot silently pass the
service layer and be inserted as a database row. `GlobalExceptionHandler`
classifies duplicate-key and update-conflict exceptions as RFC 9457
problem responses rather than generic server errors.

## Optimistic concurrency via PostgreSQL row versions

RondiTrack exposes `Version` on domain entities and maps the column to a
PostgreSQL row-version using the EF Core row-version pattern. The API
then uses the standard HTTP `ETag` and `If-Match` pattern so stale writes
are rejected:

-   read endpoints set `Response.Headers.ETag = $"\"{entity.Version}\""`;
-   update endpoints require an `If-Match` header;
-   a mismatch raises `PreconditionFailedException` with HTTP 412;
-   a database update conflict is mapped to HTTP 409.

This prevents a stale client request from overwriting current state after
another user or process has already modified the same row.

## Pagination contract

The assignment also pushes list behavior into the database instead of
returning unbounded in-memory collections.

The paged endpoints use an opaque `pageToken` and server-enforced
page-size limits:

-   default page size: `25`;
-   maximum page size: `100`;
-   `sortBy` and `sortDirection` are validated against an allow-list;
-   `pageToken` is base64-encoded and validated to ensure it matches the
    current sort/filter state;
-   the repository applies the `ApplyToken` filter before ordering and
    taking `pageSize + 1` rows to determine whether a next page exists.

This is implemented for the cycle contributions endpoint and the
stokvel-members endpoint, with `PagedResult<T>` carrying the items, the
next token, and the effective page size.

## Assignment 5.3 query-plan and index experiment

The measured query is the SQL captured from EF Core's
`Microsoft.EntityFrameworkCore.Database.Command` log when calling:

```text
GET /api/stokvels/{stokvelId}/cycles/{cycleId}/contributions?pageSize=26
```

Only bound parameter values were substituted for `EXPLAIN`; the SQL shape
was not rewritten. In the EF log `@p` was 27 because the repository fetches
`pageSize + 1` rows to determine whether another page exists.

```sql
SELECT c0."Id", c0."StokvelId", c0."UserId", u."Name", u."Email", s."Role", c0."ContributionCycleId", c0."Amount", c0."RecordedAtUtc", c0.xmin AS "Version"
FROM (
    SELECT c."Id", c."Amount", c."ContributionCycleId", c."RecordedAtUtc", c."StokvelId", c."UserId", c.xmin
    FROM "Contributions" AS c
    WHERE c."StokvelId" = @stokvelId AND c."ContributionCycleId" = @contributionCycleId
    ORDER BY c."RecordedAtUtc" DESC, c."Id" DESC
    LIMIT @p
) AS c0
INNER JOIN "StokvelMembers" AS s ON c0."UserId" = s."UserId" AND c0."StokvelId" = s."StokvelId"
INNER JOIN "Users" AS u ON s."UserId" = u."Id"
ORDER BY c0."RecordedAtUtc" DESC, c0."Id" DESC
```

The experiment used PostgreSQL 17 with 10,000 contributions matching one
stokvel/cycle. Before the performance index, the contribution source was a
sequential scan followed by a top-N sort; after creating the composite index
through the `AddContributionPaginationIndex` migration and running
`ANALYZE`, it became an ordered index scan. Both plans retained nested-loop
joins and primary-key lookups for the 27 projected rows.

| Measurement | Before index | After index |
|---|---:|---:|
| Contribution access and sort | Seq Scan + top-N Sort | Composite Index Scan; no contribution Sort |
| Estimated matching contributions | 9,952 | 9,952 |
| Actual contribution rows scanned/returned by limited subquery | 10,000 scanned; 27 returned | 27 returned (`LIMIT 27`) |
| Rows removed by filter | 48 | None reported |
| Planning time | 12.839 ms | 1.025 ms |
| Execution time for complete endpoint SELECT | 7.578 ms | 0.383 ms |

Before-index plan:

```text
Nested Loop  (cost=571.65..777.99 rows=27 width=163) (actual time=7.272..7.504 rows=27 loops=1)
  Join Filter: ((c."UserId" = s."UserId") AND (s."StokvelId" = c."StokvelId"))
  Buffers: shared hit=296
  ->  Nested Loop  (cost=571.37..767.59 rows=27 width=172) (actual time=7.256..7.371 rows=27 loops=1)
        Buffers: shared hit=215
        ->  Limit  (cost=571.08..571.15 rows=27 width=81) (actual time=7.219..7.223 rows=27 loops=1)
              Buffers: shared hit=134
              ->  Sort  (cost=571.08..595.96 rows=9952 width=81) (actual time=7.216..7.218 rows=27 loops=1)
                    Sort Key: c."RecordedAtUtc" DESC, c."Id" DESC
                    Sort Method: top-N heapsort  Memory: 32kB
                    Buffers: shared hit=134
                    ->  Seq Scan on "Contributions" c  (cost=0.00..284.72 rows=9952 width=81) (actual time=0.033..2.903 rows=10000 loops=1)
                          Filter: (("StokvelId" = '<large-stokvel-id>'::uuid) AND ("ContributionCycleId" = '<large-cycle-id>'::uuid))
                          Rows Removed by Filter: 48
                          Buffers: shared hit=134
        ->  Index Scan using "PK_Users" on "Users" u  (cost=0.29..7.27 rows=1 width=91) (actual time=0.005..0.005 rows=1 loops=27)
              Index Cond: ("Id" = c."UserId")
              Buffers: shared hit=81
  ->  Index Scan using "PK_StokvelMembers" on "StokvelMembers" s  (cost=0.29..0.37 rows=1 width=39) (actual time=0.004..0.004 rows=1 loops=27)
        Index Cond: ("UserId" = u."Id")
        Buffers: shared hit=81
Planning:
  Buffers: shared hit=240
Planning Time: 12.839 ms
Execution Time: 7.578 ms
```

After-index plan:

```text
Nested Loop  (cost=0.86..209.57 rows=27 width=163) (actual time=0.109..0.318 rows=27 loops=1)
  Join Filter: ((c."UserId" = s."UserId") AND (s."StokvelId" = c."StokvelId"))
  Buffers: shared hit=163 read=2
  ->  Nested Loop  (cost=0.57..199.18 rows=27 width=172) (actual time=0.098..0.214 rows=27 loops=1)
        Buffers: shared hit=82 read=2
        ->  Limit  (cost=0.29..2.74 rows=27 width=81) (actual time=0.079..0.106 rows=27 loops=1)
              Buffers: shared hit=1 read=2
              ->  Index Scan using "IX_Contributions_StokvelId_ContributionCycleId_RecordedAtUtc_Id" on "Contributions" c  (cost=0.29..903.85 rows=9952 width=81) (actual time=0.077..0.101 rows=27 loops=1)
                    Index Cond: (("StokvelId" = '<large-stokvel-id>'::uuid) AND ("ContributionCycleId" = '<large-cycle-id>'::uuid))
                    Buffers: shared hit=1 read=2
        ->  Index Scan using "PK_Users" on "Users" u  (cost=0.29..7.27 rows=1 width=91) (actual time=0.003..0.003 rows=1 loops=27)
              Index Cond: ("Id" = c."UserId")
              Buffers: shared hit=81
  ->  Index Scan using "PK_StokvelMembers" on "StokvelMembers" s  (cost=0.29..0.37 rows=1 width=39) (actual time=0.003..0.003 rows=1 loops=27)
        Index Cond: ("UserId" = u."Id")
        Buffers: shared hit=81
Planning:
  Buffers: shared hit=56 read=1
Planning Time: 1.025 ms
Execution Time: 0.383 ms
```

The index keys are ordered `(StokvelId, ContributionCycleId,
RecordedAtUtc DESC, Id DESC)`: the first two columns match the endpoint's
equality predicates, and the last two match its deterministic descending
ordering, including the ID tiebreaker. This lets PostgreSQL seek to one
stokvel/cycle range and deliver the page in order, avoiding a scan of all
10,000 matching rows and a sort. The execution time in this single local run
dropped by about 19.8x; it is not a general latency guarantee.

For the small development database after removing the benchmark seed, the
`Contributions` table contained 48 rows. The same query for a cycle with one
contribution took 0.126 ms (estimated one row, actual one). PostgreSQL chose
the existing `IX_Contributions_ContributionCycleId` and a quicksort of that
single matching row rather than the new composite index. That is a normal
cost-based choice for tiny data, not a bug: index setup and traversal can
cost more than a short scan or an already-available narrower index. The full
captured plans and reproducible SQL are in
[docs/assignment-5-3-performance-evidence.md](docs/assignment-5-3-performance-evidence.md)
and [scripts/assignment-5-3-performance.sql](scripts/assignment-5-3-performance.sql).

Small-data plan excerpt:

```text
Nested Loop  (cost=8.87..17.01 rows=1 width=163) (actual time=0.072..0.073 rows=1 loops=1)
  ->  Nested Loop  (cost=8.59..16.62 rows=1 width=172) (actual time=0.060..0.060 rows=1 loops=1)
        ->  Limit  (cost=8.30..8.31 rows=1 width=81) (actual time=0.049..0.050 rows=1 loops=1)
              ->  Sort  (cost=8.30..8.31 rows=1 width=81) (actual time=0.047..0.048 rows=1 loops=1)
                    Sort Key: c."RecordedAtUtc" DESC, c."Id" DESC
                    Sort Method: quicksort  Memory: 25kB
                    ->  Index Scan using "IX_Contributions_ContributionCycleId" on "Contributions" c  (cost=0.27..8.29 rows=1 width=81) (actual time=0.015..0.015 rows=1 loops=1)
                          Index Cond: ("ContributionCycleId" = '<small-cycle-id>'::uuid)
                          Filter: ("StokvelId" = '<small-stokvel-id>'::uuid)
        ->  Index Scan using "PK_Users" on "Users" u  (cost=0.29..8.30 rows=1 width=91) (actual time=0.008..0.008 rows=1 loops=1)
              Index Cond: ("Id" = c."UserId")
  ->  Index Scan using "PK_StokvelMembers" on "StokvelMembers" s  (cost=0.29..0.37 rows=1 width=39) (actual time=0.011..0.011 rows=1 loops=1)
        Index Cond: ("UserId" = u."Id")
Planning Time: 11.645 ms
Execution Time: 0.126 ms
```

------------------------------------------------------------------------

# Testing and Regression Evidence

At the end of Assignment 4.4:

``` text
Total: 19
Failed: 0
Succeeded: 19
```

After Assignment 5.1:

``` text
Total: 21
Failed: 0
Succeeded: 21
Skipped: 0
```

Before the Assignment 5.2 relationship/query changes, the regression
baseline was:

``` text
Total: 21
Failed: 0
Succeeded: 21
Skipped: 0
```

During Assignment 5.2, moving user/stokvel persistence into the shared
PostgreSQL-backed test environment exposed state-dependent
integration-test setup. Four tests initially reused database entities
through list positions and could accidentally select an already-related
user/stokvel pair.

The production duplicate-membership rule was **not weakened**. Instead,
the affected integration tests were corrected to create their own unique
users, stokvels, memberships, cycles, and idempotency keys.

After the relationship, query, projection, and `AsNoTracking()` changes,
the final regression run was:

``` text
Total: 21
Failed: 0
Succeeded: 21
Skipped: 0

Build succeeded
```

The existing suite therefore remains green after Assignment 5.2.

The tests verify behaviour including:

-   malformed requests;
-   nonexistent resources;
-   duplicate membership;
-   removing a non-member;
-   contribution from a non-member;
-   cycle ownership;
-   duplicate contributions;
-   missing idempotency keys;
-   same idempotency key with the same request;
-   same idempotency key with different request data;
-   zero contribution amount;
-   invalid contribution-cycle periods; and
-   payout transaction behaviour.

The existing tests did not depend on a public `StokvelMember.Id`
contract. The main test changes required by Assignment 5.2 came from
repository/persistence setup and test-data isolation rather than
assertions against the removed surrogate membership ID.

------------------------------------------------------------------------

# Definition of Done

Assignment 5.2 extends the Definition of Done with:

-   `Relationship modeled with real navigation / database relationship`
-   `N+1 behaviour measured and fixed`

  --------------------------------------------------------------------------------------------------------------
  API area /      Documented   Guard /        Automated         PostgreSQL / Relationship       N+1
  endpoint                     validation     coverage          EF Core      modeled            measured/fixed
  --------------- ------------ -------------- ----------------- ------------ ------------------ ----------------
  Users CRUD      Yes          Validation +   Happy/not-found   Yes          User → memberships N/A
                               existence      coverage                                          

  Stokvels CRUD   Yes          Validation +   Happy/not-found   Yes          Stokvel →          N/A
                               existence      coverage                       memberships        

  Add/remove      Yes          Existence +    Unit +            Yes          Composite join     N/A
  membership                   membership     integration                    entity             
                               rules                                                            

  Contribution    Yes          Existence +    Existing suite    Yes          Cycle →            N/A
  cycles                       ownership                                     contributions      

  Record          Yes          Membership +   Unit +            Yes          Contribution →     N/A
  contribution                 cycle +        integration                    membership/cycle   
                               duplicate +                                                      
                               idempotency                                                      

  Get cycle       Yes          Stokvel +      Regression        Yes          Member → User      Yes: 23 → 3
  contributions                cycle          suite + measured               graph / projection 
                               ownership      SQL run                                           

  Process payout  Yes          Cycle +        Transaction tests Yes          Composite          N/A
                               recipient +                                   recipient          
                               amount +                                      membership FK      
                               rotation                                                         
  --------------------------------------------------------------------------------------------------------------

`N/A` means the N+1 measurement is not relevant to that operation.

------------------------------------------------------------------------

# Known Gaps and Deliberate Scope Decisions

## Idempotency remains in memory

The idempotency store is not persisted. Restarting the application
clears idempotency records.

## Legacy `Stokvel.Members`

The older `Stokvel.Members` domain collection remains for compatibility
and is ignored by EF Core. Persisted membership is represented by
`StokvelMember` and the `StokvelMemberships` navigation.

The application services use the persisted membership representation as
the source of truth for membership checks.

A later refactor could remove the legacy representation once
compatibility with earlier work is no longer required.

## Stokvel → ContributionCycle navigation

`ContributionCycle` has `StokvelId`, but a full
`Stokvel.ContributionCycles` navigation was not added in Assignment 5.2.

The required additional relationship was implemented as
`ContributionCycle → Contribution`, which directly supports the required
cycle-contributions endpoint.

## Payout rotation

The current payout rule represents one simple pass through eligible
members. Once every persisted member has received a payout, there is no
next eligible member.

Explicit rotation rounds are outside the current assignment scope.

## Rollback coverage

The successful payout path is tested through `PayoutService`, while the
deliberately forced rollback tests the EF transaction mechanism
directly.

A future test could inject a controlled failure into the service
orchestration itself.

------------------------------------------------------------------------

# Asynchronous Operations

Repository, service, controller, and EF Core operations use Task-based
asynchronous contracts where appropriate.

Controllers await repository and service operations instead of
synchronously blocking on tasks.

This is especially important now that database access introduces real
I/O.
