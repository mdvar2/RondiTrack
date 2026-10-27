# Assignment 5.4 — Clean architecture and self-contained database tests

| | |
|---|---|
| Official title | Not recorded. The README heading at the time is "Assignment 5.4 - Architecture and dependency injection". |
| Date | 11 October 2026 |
| Final commit on `main` | [`e35a70c`](https://github.com/mdvar2/RondiTrack/commit/e35a70c) "Add database isolation checks and verification report" |
| Branch / PR | `assignment-5-4-clean-architecture`, [PR #2](https://github.com/mdvar2/RondiTrack/pull/2) (merged 11 Oct 2026, together with [6.1](assignment-6.1.md)) |
| README as submitted | [README at `e35a70c`](https://github.com/mdvar2/RondiTrack/blob/e35a70c/README.md) |
| Re-tested | **Yes, 27 Oct 2026**: the `e35a70c` code, exported unchanged, passed **25/25** on disposable PostgreSQL |

## 1. Purpose and learning objectives
To split the single project into **API**, **Domain** and **Infrastructure** with the right dependency directions, to check every service lifetime, and to make the database tests independent of any developer's local database. The brief's learning objectives aren't recorded.

## 2. Original requirements
The brief isn't in the repository. The [verification report](evidence/assignment-5-4-verification-report.md#requirement-checklist) has a 19-row requirement checklist, all marked PASS, covering:
- **Project boundaries:** Domain has no dependencies. Infrastructure holds the DbContext, repositories and migrations. API holds controllers, DTOs, validators and services.
- **Moves:** files moved with `git mv`, and only `API/Program.cs` chooses concrete repositories.
- **Lifetimes:** every DbContext-dependent registration is scoped.
- **Migrations:** preserved and applied, with EF Core packages kept compatible.
- **Tests:** "Only new library is Testcontainers.PostgreSql 4.15.0", a PostgreSQL 17 `WebApplicationFactory` fixture, test isolation, and "Full suite passes while development database unavailable".

## 3. What I implemented
- `Domain/` holds entities, exceptions, repository interfaces, query records and `PagedResult`, with no project or package dependencies.
- `Infrastructure/` holds `RondiTrackDbContext`, the EF repositories and the migrations. It's now the migrations assembly, and the four existing migrations were preserved.
- `API/` holds controllers, DTOs, validators, services and `Program.cs`, and references both.
- `RondiTrack.Tests/TestSupport/PostgresApiFactory.cs` starts a **disposable PostgreSQL 17 container** per test run, applies the migrations and overrides `ConnectionStrings:RondiTrackDb`. Every database test class shares it through `PostgresCollection`.
- [`scripts/verify-assignment-5-4-isolation.ps1`](../../scripts/verify-assignment-5-4-isolation.ps1) proves the suite passes with the development database unavailable.

## 4. Technologies, tools and dependencies
New: **Testcontainers.PostgreSql 4.15.0** (test project only) and **Docker**. EF Core Core, Relational and Design are pinned to 10.0.4 in both API and Infrastructure; Npgsql stays at 10.0.3.

## 5. Prerequisites and installation
The .NET 10 SDK and **Docker Desktop** (Linux containers) for the tests. Running the API itself still needs a PostgreSQL database.

## 6. Environment configuration
- **Tests:** none. The fixture supplies its own connection string.
- **API:** `dotnet user-secrets --project API set "ConnectionStrings:RondiTrackDb" "<connection string>"`. User secrets now belong to the `API` project, under the same `UserSecretsId`.

## 7. How to run or reproduce
**As submitted** (the commands are in the README at `e35a70c`; I checked them against that commit's project layout):
```powershell
git worktree add ..\RondiTrack-5.4 e35a70c
cd ..\RondiTrack-5.4
dotnet test RondiTrack.Tests/RondiTrack.Tests.csproj        # Docker must be running
dotnet ef migrations list --project Infrastructure --startup-project API
dotnet run --project API
```
Re-run on 27 Oct 2026: `dotnet test` passed **25 of 25**. Only use `dotnet ef database update` against a throwaway database, because your user secrets point at your current development database.

**In the current codebase:** the same three-project split and the same Testcontainers fixture are used today. `dotnet test RondiTrack.Tests` runs the whole suite. The DI lifetimes are listed in [architecture.md](../architecture.md#lifetime-inventory).

## 8. Important files and components
- `Domain/Domain.csproj`, `Infrastructure/Infrastructure.csproj`, `API/RondiTrack.csproj`, `RondiTrack.sln`
- `Domain/Queries/ContributionWithMember.cs`, `StokvelMemberDetails.cs`: records that let Infrastructure return query results without depending on API DTOs
- `RondiTrack.Tests/TestSupport/PostgresApiFactory.cs`, `PostgresCollection.cs`

## 9. How it works
Dependencies point inward: API → Infrastructure → Domain, and API → Domain. Only `Program.cs` knows which repository class implements each interface. Tests start a fresh PostgreSQL container, migrate it, and point the app at it before the app is built. That's why the suite runs on any machine with Docker and never touches local data.

## 10. Testing and recorded results
| Point | Result |
|---|---|
| Baseline before the split ([phase 1 audit](evidence/assignment-5-4-phase-1-audit.md)) | 25 passed |
| After the split, with the development database unavailable | 25 passed ([verification report](evidence/assignment-5-4-verification-report.md#actual-verification)) |
| Re-run 27 Oct 2026 of `e35a70c` | **25 passed, 0 failed** |

## 11. Challenges and solutions
From the report's "Errors and remaining scope":
- API DTOs were leaking into Infrastructure. Domain query records fixed that.
- Docker's all-users installer was blocked by a cancelled UAC prompt; a per-user install worked.
- Fixture failures appeared while Docker wasn't running. They were recorded honestly and passed once Docker was started.
- PowerShell execution policy had to be dealt with before the isolation script would run.

## 12. Learning outcomes
The technical notes summarise 5.4 as: "I separated API, Domain and Infrastructure, checked service lifetimes and introduced self-contained PostgreSQL integration tests" ([technical notes §6](technical-notes-to-6.3.md#6-assignments-and-learning-journey)).

## 13. Evidence, commits and pull requests
- [PR #2](https://github.com/mdvar2/RondiTrack/pull/2)
- [Phase 1 audit](evidence/assignment-5-4-phase-1-audit.md)
- [Phase verification](evidence/assignment-5-4-phase-verification.md)
- [Verification report](evidence/assignment-5-4-verification-report.md)
- [Isolation evidence](evidence/assignment-5-4-isolation-evidence.txt)
- [README archive: 5.4](readme-archive-2026-10-11.md#assignment-54---architecture-and-dependency-injection)

## 14. Relationship to other assignments
- **Before:** [5.3](assignment-5.3.md)'s behaviour was kept exactly; its 25 tests still passed.
- **Next:** [6.1](assignment-6.1.md) was built on the same branch and merged in the same PR. Every later assignment's tests rely on this fixture.
