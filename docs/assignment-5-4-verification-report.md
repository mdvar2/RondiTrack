# Assignment 5.4 verification report

Completed on 11 October 2026, before starting Assignment 6.1.
Working branch: `assignment-5-4-clean-architecture`; baseline: `6e3bfa1`.

## Requirement checklist

| Requirement | Result | Implementation / evidence |
|---|---|---|
| Domain contains entities, exceptions, repository interfaces and no dependencies | PASS | Domain/Domain.csproj; Domain/Entities/*.cs; Domain/Exceptions/*.cs; Domain/Repositories/*.cs; Domain/Common/PagedResult.cs; Domain/Queries/*.cs. CLI lists no packages or project references. |
| Infrastructure contains DbContext, repositories and migrations; references Domain | PASS | Infrastructure/Infrastructure.csproj; Infrastructure/Data/RondiTrackDbContext.cs; Infrastructure/Repositories/*.cs; Infrastructure/Migrations/*.cs |
| API contains controllers, DTOs, validators, services and Program; references both libraries | PASS | API/RondiTrack.csproj; API/Controllers/*.cs; API/DTOs; API/Validators/*.cs; API/Services/*.cs; API/Program.cs |
| Tests reference API and Infrastructure | PASS | RondiTrack.Tests/RondiTrack.Tests.csproj |
| Existing files moved with git mv, namespaces/usings fixed | PASS | Commit bc4f43c and exact manifest in assignment-5-4-phase-verification.md |
| Only API/Program.cs selects concrete repository implementations | PASS | API/Program.cs/AddScoped; services/controllers use Domain interfaces, except the preserved PayoutService's direct DbContext transaction |
| EF Core compatibility and startup Design package | PASS | API and Infrastructure pin Core/Relational 10.0.4; Design 10.0.4 has PrivateAssets=all; existing Npgsql provider remains 10.0.3 |
| Existing migration history preserved and migrations applied | PASS | Four migration IDs below; Infrastructure selected as migrations assembly; CLI reports all applied and no model changes |
| Every DbContext-dependent registration is Scoped | PASS | API/Program.cs; README lifetime table; no lifetime change required |
| Only new library is Testcontainers.PostgreSql 4.15.0 | PASS | RondiTrack.Tests/RondiTrack.Tests.csproj; EF references only pin existing EF libraries |
| PostgreSQL 17 WebApplicationFactory / IAsyncLifetime fixture | PASS | RondiTrack.Tests/TestSupport/PostgresApiFactory.cs |
| Exact connection key overridden early with UseSetting | PASS | PostgresApiFactory.ConfigureWebHost: ConnectionStrings:RondiTrackDb |
| Start/dispose container and apply migrations | PASS | InitializeAsync; explicit IAsyncLifetime.DisposeAsync; sanitized disposal evidence |
| All integration classes use shared collection; no test support folder named Infrastructure | PASS | TestSupport/PostgresCollection.cs and all 11 test classes; direct-context tests use CreateDbContextOptions |
| Existing behavior/assertions preserved | PASS | 25/25 tests pass; assertion statement comparison against baseline passes; no feature/endpoints removed or added |
| Test isolation | PASS | Fresh database per run; serialized collection; existing unique GUID-based setup |
| Full suite passes while development database unavailable | PASS (local PostgreSQL adaptation) | scripts/verify-assignment-5-4-isolation.ps1; development ronditrack rejects real connections before suite; restored afterward |
| README table, real test result and split errors documented | PASS | README.md/Assignment 5.4 sections |
| Logical commits and no new committed secrets | PASS | Commits below; fixture password generated at runtime; credential-pattern scans found no candidates |

## Actual verification

- `dotnet build RondiTrack.sln --no-restore`: succeeded, 0 warnings, 0 errors (20.02 seconds).
- `dotnet ef migrations list --project Infrastructure --startup-project API --no-build`: all four preserved migrations applied.
- `dotnet ef migrations has-pending-model-changes --project Infrastructure --startup-project API --no-build`: no changes since last migration.
- Baseline tests: 25 passed against development PostgreSQL, 0 failed, 0 skipped.
- Phase 2 regression: 25 passed against development PostgreSQL, 0 failed, 0 skipped.
- Phase 5: 25 passed against PostgreSQL 17 Testcontainers, including initial image pulls (1.3598 minutes).
- Phase 6: 25 passed while development database refused connections (12.2869 seconds); script exited 0; database restoration connection succeeded.
- All existing assertion statements and test names remain unchanged.
- `git diff --check`: passed. Domain CLI lists no package/project references.

The development installation uses Windows service postgresql-x64-17. The proof
kept that service running and disabled connections only to the idle ronditrack
database with ALLOW_CONNECTIONS=false. A psql connection was refused before tests
started, with no sessions terminated. The API's context was independently checked
against the container connection, and current_database() returned ronditrack_test
on mapped port 8623 with four applied migrations. Finally restored
ALLOW_CONNECTIONS=true and verified a real development connection. This proves
independence without disrupting the local server or changing schema/data.

Actual summary:

```text
Test Run Successful.
Total tests: 25
     Passed: 25
 Total time: 12,2869 Seconds
```

Sanitized output: [assignment-5-4-isolation-evidence.txt](assignment-5-4-isolation-evidence.txt).
Reproduce with:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/verify-assignment-5-4-isolation.ps1
```

The script requires the existing local connection User Secret and psql only for
this independence proof. Ordinary container tests require neither. Docker Desktop
4.94.0 installed per-user; Docker Engine 29.8.2 uses Linux containers on WSL 2.
The process-only execution policy override does not alter machine policy.

## Preserved migrations

All under Infrastructure/Migrations; IDs and migration operations unchanged:

- 20261001040953_InitialCreate
- 20261004202213_ConfigureRelationshipsAndQueryModel
- 20261005211608_AddDatabaseDefenseConstraints
- 20261006014557_AddContributionPaginationIndex

All designer files and RondiTrackDbContextModelSnapshot.cs moved with them;
entity type and DbContext namespaces were updated. No new schema migration
was necessary, and no migration history was recreated.

## Phase commits and exact file manifests

| Phase | Commit | Message |
|---|---|---|
| 1 | 8d40909 | Document Assignment 5.4 repository audit and migration plan |
| 2 | bc4f43c | Split RondiTrack into Domain Infrastructure and API projects |
| 3 | 3137228 | Pin compatible EF Core packages and Infrastructure migrations assembly |
| 4 | 980bf4a | Document DI lifetimes and update architecture setup commands |
| 5 implementation | 6bdba5c | Move all database tests to shared PostgreSQL Testcontainers fixture |
| 5 verification | b8a6c16 | Record passing PostgreSQL Testcontainers regression verification |

The Phase 6 commit is recorded in Git history and in the final chat delivery.
Exact files created/moved/modified, reasons, navigation and phase results are in
[assignment-5-4-phase-1-audit.md](assignment-5-4-phase-1-audit.md) and
[assignment-5-4-phase-verification.md](assignment-5-4-phase-verification.md).
Phase 6 creates scripts/verify-assignment-5-4-isolation.ps1,
assignment-5-4-isolation-evidence.txt and this report; modifies README.md and
assignment-5-4-phase-verification.md.

## Code walkthrough

For a contribution request:

HTTP POST /api/stokvels/{stokvelId}/members/{userId}/contributions ?
API/Controllers/StokvelsController.cs / RecordContribution ?
API/Services/ContributionService.cs / RecordContributionAsync ?
Infrastructure/Repositories/ContributionRepository.cs / AddAsync ?
Infrastructure/Data/RondiTrackDbContext.cs / Contributions, SaveChangesAsync ? PostgreSQL.

The service validates stokvel, user, membership, cycle ownership, duplicates and
idempotency using Domain repository interfaces. API/Program.cs wires those
interfaces to scoped EF implementations. For paged query results, open
IContributionRepository.GetByCyclePagedAsync, ContributionRepository.GetByCyclePagedAsync,
ContributionCyclesController.GetContributions and ContributionWithMemberResponse.FromQuery:
Infrastructure projects only required columns to a Domain record, then API maps
them to the original JSON DTO. PayoutService.CreatePayoutAsync retains its existing
EF execution strategy and explicit transaction behavior.

## Errors and remaining scope

Sandbox build/logging/network limitations were resolved through normal serial
builds and unrestricted verification. DTO coupling was resolved with Domain query
records. Docker's all-users installer hit canceled UAC; documented per-user install
succeeded. Initial fixture failures occurred only while Docker was absent and are
recorded honestly. PowerShell execution policy and dictionary adaptation were
resolved before the successful isolation run.

There are no unresolved Assignment 5.4 requirements. Existing deliberate behavior,
including in-memory idempotency, legacy Stokvel.Members and payout rotation limits,
remains unchanged. Credential-pattern scans found no candidates across 278 unique
historical text blobs and the scanned current text files; this heuristic is not a
proof that every possible secret is absent. No connection string, real password,
or signing key was added to the commits. Assignment 6.1 begins only after this
verification gate.
