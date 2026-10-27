# Assignment 5.3 — Database protections, concurrency and pagination

| | |
|---|---|
| Official title | Not recorded. The commit calls it "Assignment 5.3 database protections". |
| Dates | 5–6 October 2026 |
| Final commit on `main` | [`6444940`](https://github.com/mdvar2/RondiTrack/commit/6444940) "Add contribution index and document query performance" (merged by [`1377fbe`](https://github.com/mdvar2/RondiTrack/commit/1377fbe)) |
| Branch / PR | `fix/assignment-5-3-completion`, [PR #1](https://github.com/mdvar2/RondiTrack/pull/1) (merged 6 Oct 2026) |
| README as submitted | [README at `6444940`](https://github.com/mdvar2/RondiTrack/blob/6444940/README.md) |
| Re-tested | **No** (same database caution as [5.1](assignment-5.1.md#7-how-to-run-or-reproduce)) |

## 1. Purpose and learning objectives
To make the database itself refuse invalid data, to stop stale writes, and to keep list endpoints bounded and fast. The brief's learning objectives aren't recorded.

## 2. Original requirements
The brief isn't in the repository. The README says: "The assignment also pushes list behavior into the database instead of returning unbounded in-memory collections". It also records an assignment-driven query-plan and index experiment. Its Definition of Done table is still headed with 5.2's extensions, so no separate 5.3 checklist was recorded.

## 3. What I implemented
- **Database-enforced uniqueness**: memberships on `(UserId, StokvelId)`, one contribution per `(StokvelId, UserId, ContributionCycleId)`, and one payout per `(StokvelId, ContributionCycleId)`. Duplicate-key errors become Problem Details responses.
- **Optimistic concurrency.** Each mutable entity has a `Version` mapped to PostgreSQL's row version. Reads return an `ETag`. Updates require `If-Match`; a mismatch gets `412` (`PreconditionFailedException`) and a database update conflict gets `409`.
- **Pagination**: an opaque, validated `pageToken`, a default page of 25 and a maximum of 100, sorting from an allow-list, and fetching `pageSize + 1` rows to know whether there's a next page.
- **A query-plan experiment and an index.** I seeded 10,000 contributions, ran `EXPLAIN` on the exact SQL EF generated, and added the migration `20261006014557_AddContributionPaginationIndex`.

## 4. Technologies, tools and dependencies
The same packages as 5.1 and 5.2. Tools: PostgreSQL's `EXPLAIN` and `psql`, and EF Core command logging.

## 5. Prerequisites and installation
As for [5.2](assignment-5.2.md#5-prerequisites-and-installation).

## 6. Environment configuration
The same user secret as 5.1. The experiment set EF's command log level to `Information`.

## 7. How to run or reproduce
**As submitted:** the clean-machine steps in the README at `6444940`, then `dotnet test`. **Not re-tested.** Use a separate folder and a new, empty database, as explained for [5.1](assignment-5.1.md#7-how-to-run-or-reproduce).

**The experiment:** [scripts/assignment-5-3-performance.sql](../../scripts/assignment-5-3-performance.sql) together with the [performance evidence](evidence/assignment-5-3-performance-evidence.md). The evidence notes that the 10,000-row dataset was removed after measuring.

**In the current codebase:** these protections are all still in force.
- `ETag`/`If-Match` on `PUT /api/v1/stokvels/{id}`.
- The paged `GET /api/v1/stokvels/{stokvelId}/members` and contribution listings.
- Tests: `RondiTrack.Tests/ConcurrencyProtectionTests.cs` and `DatabaseConstraintTests.cs`.

## 8. Important files and components
At `6444940`:
- `Data/RondiTrackDbContext.cs` (unique indexes, row versions)
- `Common/PagedResult.cs` (now `Domain/Common/`)
- `Exceptions/PreconditionFailedException.cs`
- `Handlers/GlobalExceptionHandler.cs` (maps duplicate keys and conflicts)
- Migrations `20261005211608_AddDatabaseDefenseConstraints` and `20261006014557_AddContributionPaginationIndex`
- Tests: `ConcurrencyProtectionTests.cs`, `DatabaseConstraintTests.cs`

## 9. How it works
Business rules in the services catch most problems first. The unique indexes are the last line of defence when two requests race. The row version makes PostgreSQL reject an update based on an old read, and the API turns that into `409` or `412`, so a client knows to re-read.

## 10. Testing and recorded results
| Command | Recorded result (README at `6444940`) |
|---|---|
| `dotnet test` | **25 passed** |

The performance evidence records the query plans before and after the index.

## 11. Challenges and solutions
- **Measuring real SQL.** The experiment uses EF's exact logged SQL, with only the parameter values substituted, so the plan reflects what the API really runs.
- **Gaps recorded at the time:**
  - idempotency still in memory
  - the legacy `Stokvel.Members` collection
  - no `Stokvel → ContributionCycle` navigation
  - payout rotation
  - rollback coverage

## 12. Learning outcomes
The technical notes summarise 5.3 as: "I added database guards, pagination and concurrency checks, and recorded an index/query-plan experiment" ([technical notes §6](technical-notes-to-6.3.md#6-assignments-and-learning-journey)).

## 13. Evidence, commits and pull requests
- [PR #1](https://github.com/mdvar2/RondiTrack/pull/1)
- [Performance evidence](evidence/assignment-5-3-performance-evidence.md)
- [README at `6444940`](https://github.com/mdvar2/RondiTrack/blob/6444940/README.md)
- [README archive: database defence, pagination and concurrency](readme-archive-2026-10-11.md#database-defense-pagination-and-concurrency)

## 14. Relationship to other assignments
- **Before:** [5.2](assignment-5.2.md)'s relationships are what these constraints protect.
- **Next:** [5.4](assignment-5.4.md) split the single project into API, Domain and Infrastructure without changing this behaviour.
- **Later:**
  - The `ETag` it introduced is still returned by `GET /stokvels/{id}`.
  - The legacy `Stokvel.Members` gap was the cause of the `memberCount` bug fixed in [8.1](assignment-8.1.md).
