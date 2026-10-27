# Assignment 5.1 — EF Core and PostgreSQL foundations

| | |
|---|---|
| Official title | Not recorded. The commit calls it "Assignment 5.1 EF Core and PostgreSQL foundations". |
| Date | 1 October 2026 |
| Final commit on `main` | [`b4d19dc`](https://github.com/mdvar2/RondiTrack/commit/b4d19dc) "Document EF Core and PostgreSQL setup" (code: [`790ac56`](https://github.com/mdvar2/RondiTrack/commit/790ac56)) |
| Branch / PR | Committed directly to `main`; no PR |
| README as submitted | [README at `b4d19dc`](https://github.com/mdvar2/RondiTrack/blob/b4d19dc/README.md) |
| Re-tested | **No.** Its tests use the database in your connection string; see [section 7](#7-how-to-run-or-reproduce) |

## 1. Purpose and learning objectives
The README at `b4d19dc` says the project was "being migrated from in-memory storage to PostgreSQL", with EF Core persisting memberships, contribution cycles, contributions and payouts. The brief's learning objectives aren't recorded.

## 2. Original requirements
The brief isn't in the repository. Two requirements are quoted in the README:
- "Assignment 5.1 required at least one real repository swap rather than requiring every repository to be migrated at once."
- "The payout requirement asked for a minimal real rule rather than a full scheduling system."

My **Definition of Done** extended 4.4's with two columns, **"Persisted via EF Core"** and **"Explicit transaction tested"**, plus a new row for processing a payout.

## 3. What I implemented
- `RondiTrackDbContext` with the full schema and the first migration, `20261001040953_InitialCreate`.
- PostgreSQL persistence for `StokvelMember`, `ContributionCycle`, `Contribution` and `Payout`. **Users and stokvels stayed in memory**, a hybrid stage the README calls out openly.
- **Payouts** (`PayoutsController`, `PayoutService`). The rotation rule picks "the earliest joined persisted stokvel member who has not already received a payout for that stokvel".
- An **explicit database transaction** for a payout: create the payout, mark the cycle `PaidOut`, then commit.
- Npgsql retry on failure (3 retries, at most 2 seconds apart) and the connection string kept in user secrets.

## 4. Technologies, tools and dependencies
Verified against `RondiTrack.csproj` at `b4d19dc`. New since 4.4: **Npgsql.EntityFrameworkCore.PostgreSQL 10.0.3** and **Microsoft.EntityFrameworkCore.Design 10.0.4**. Everything else is unchanged (FluentValidation 12.1.1, OpenApi 10.0.12, Scalar 2.17.7, xUnit 2.9.3). Tools: PostgreSQL 17 and `dotnet-ef`.

## 5. Prerequisites and installation
The .NET 10 SDK, PostgreSQL 17 and `dotnet-ef`. The README at `b4d19dc` has a step-by-step clean-machine guide, sections 1–9. A later, expanded version of that guide is in the [README archive](readme-archive-2026-10-11.md#running-the-project-from-a-clean-windows-machine).

## 6. Environment configuration
The connection string went in user secrets. The project's `UserSecretsId` is the same one the API uses today:
```powershell
dotnet user-secrets set "ConnectionStrings:RondiTrackDb" "Host=localhost;Port=5432;Database=ronditrack;Username=postgres;Password=YOUR_LOCAL_PASSWORD"
```

## 7. How to run or reproduce
**As submitted** (from the README at `b4d19dc`): `dotnet ef database update`, then `dotnet test .\RondiTrack.Tests\RondiTrack.Tests.csproj`, then `dotnet run`.

> **Caution: not re-tested.** These tests and `dotnet ef database update` use whatever database `ConnectionStrings:RondiTrackDb` points at. Your user secrets are shared with the current API and point at your current development database, and this version's migrations would conflict with it. To reproduce 5.1, work in a separate folder (`git worktree add ..\RondiTrack-5.1 b4d19dc`) and first point the connection string at a **new, empty database**, for example `Database=ronditrack_5_1`.

**In the current codebase.** Payouts are `POST /api/v1/stokvels/{stokvelId}/cycles/{cycleId}/payouts` (`API/Services/PayoutService.cs`). The transaction tests are in `RondiTrack.Tests/PayoutTransactionTests.cs` and now run on Testcontainers.

## 8. Important files and components
At `b4d19dc`, still at the repository root:
- `Data/RondiTrackDbContext.cs` (now `Infrastructure/Data/`)
- `Migrations/20261001040953_InitialCreate.cs` (now `Infrastructure/Migrations/`)
- `Models/Payout.cs`, `Models/StokvelMember.cs` (now `Domain/Entities/`)
- `Repositories/Ef*Repository.cs`
- `Services/PayoutService.cs`, `Controllers/PayoutsController.cs`
- Tests: `PayoutTransactionTests.cs`, `MembershipServiceTests.cs`, `TestContributionRepository.cs`

## 9. How it works
EF-backed repositories replaced the in-memory ones for the contribution-related entities. The payout service opens a transaction, picks the next recipient by `JoinedAtUtc`, saves the payout and the cycle's new status, and commits. A failure part-way through rolls back both.

## 10. Testing and recorded results
| Command | Recorded result (README at `b4d19dc`) |
|---|---|
| `dotnet test .\RondiTrack.Tests\RondiTrack.Tests.csproj` | 21 passed, 0 failed, 0 skipped. Needs the local PostgreSQL |

Two transaction tests cover a successful payout and a forced rollback.

## 11. Challenges and solutions
From the README's "Known Gaps":
- **A test that depended on an implementation detail.** A 4.4 service test built the concrete `ContributionRepository`. When that repository became EF-backed, the test switched to a test implementation of `IContributionRepository`.
- **Rollback coverage.** The forced rollback is tested at the EF transaction level, not by injecting a failure into `PayoutService`.

## 12. Learning outcomes
Not recorded as such. The README explains its choices: the retry settings, why `DbContext` is scoped, and why the migration was reviewed.

## 13. Evidence, commits and pull requests
- [README at `b4d19dc`](https://github.com/mdvar2/RondiTrack/blob/b4d19dc/README.md): the clean-machine guide, Definition of Done and Known Gaps.
- Commits: [`790ac56`](https://github.com/mdvar2/RondiTrack/commit/790ac56) (code) and [`b4d19dc`](https://github.com/mdvar2/RondiTrack/commit/b4d19dc) (documentation).

## 14. Relationship to other assignments
- **Before:** [4.4](assignment-4.4.md) was entirely in memory.
- **Gaps carried forward:**
  - The in-memory users and stokvels moved to PostgreSQL in [5.2](assignment-5.2.md).
  - The in-memory idempotency store was persisted in [6.4](assignment-6.4.md).
  - The README listed "the hybrid `Stokvel.Members` representation" as a gap. That legacy collection is why `GET /stokvels/{id}` reported `memberCount: 0` until [8.1](assignment-8.1.md).
