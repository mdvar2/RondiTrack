# Assignment 5.2 — Relationships and query behaviour

| | |
|---|---|
| Official title | Not recorded. The README heading is "Assignment 5.2 — Relationships and Query Behavior". |
| Date | 5 October 2026 |
| Final commit on `main` | [`d891a98`](https://github.com/mdvar2/RondiTrack/commit/d891a98) "Configure entity relationships and database queries" |
| Branch / PR | Committed directly to `main`; no PR |
| README as submitted | [README at `d891a98`](https://github.com/mdvar2/RondiTrack/blob/d891a98/README.md) |
| Re-tested | **No** (same database caution as [5.1](assignment-5.1.md#7-how-to-run-or-reproduce)) |

## 1. Purpose and learning objectives
To model real relationships between the entities and to understand what EF Core does with them in SQL. The brief's learning objectives aren't recorded.

## 2. Original requirements
The brief isn't in the repository. My **Definition of Done** extended 5.1's with **"Relationship modeled with real navigation / database relationship"** and **"N+1 behaviour measured and fixed"**.

## 3. What I implemented
- **Everything in PostgreSQL**: `User` and `Stokvel` moved off their in-memory repositories. Only the idempotency store stayed in memory.
- A **many-to-many User ↔ Stokvel** relationship through `StokvelMember`, with the **composite natural key `(UserId, StokvelId)`** replacing a surrogate `Id`.
- Cycle → contribution, contribution → membership, and payout → recipient membership relationships, with foreign keys.
- The **N+1 experiment** on "get a cycle's contributions":

  | Version | SQL queries | Shipped? |
  |---|---|---|
  | Naive per-row loading | 23 | No |
  | Eager `Include`/`ThenInclude` | 3 | No |
  | DTO projection with `Select` | 3 (fewer columns) | **Yes** |

- An **`AsNoTracking()` audit** of read-only queries.
- A migration (`20261004202213_ConfigureRelationshipsAndQueryModel`), reviewed by hand before applying (see section 11).

## 4. Technologies, tools and dependencies
The same packages as [5.1](assignment-5.1.md#4-technologies-tools-and-dependencies): EF Core, the Npgsql provider and PostgreSQL 17. Nothing new was added.

## 5. Prerequisites and installation
As for 5.1. The README at `d891a98` expands the setup into a 16-step clean Windows machine guide, including common problems such as "`psql` is not recognized". The [README archive](readme-archive-2026-10-11.md#running-the-project-from-a-clean-windows-machine) has a later version of it.

## 6. Environment configuration
The same user secret as 5.1 (`ConnectionStrings:RondiTrackDb`).

## 7. How to run or reproduce
**As submitted:** steps 10–15 of the README at `d891a98`: inspect the migrations, `dotnet ef database update`, build, `dotnet test`, then `dotnet run`. **Not re-tested.** Use a separate folder and a new, empty database, as explained for [5.1](assignment-5.1.md#7-how-to-run-or-reproduce).

**In the current codebase:**
- The composite key is configured in `Infrastructure/Data/RondiTrackDbContext.cs`.
- The shipped projection lives on in `Infrastructure/Repositories/ContributionRepository.cs`, used by `GET /api/v1/stokvels/{stokvelId}/cycles/{cycleId}/contributions`.

## 8. Important files and components
At `d891a98`, at the repository root:
- `Data/RondiTrackDbContext.cs` (relationship configuration)
- `Models/*.cs`
- `Migrations/20261004202213_ConfigureRelationshipsAndQueryModel.cs`
- `Repositories/ContributionRepository.cs` (projection)
- `DTOs/Contributions/ContributionWithMemberResponse.cs`

## 9. How it works
The contributions endpoint projects straight into the response shape inside the SQL query: contribution, member name and email, and role. So one query returns exactly the needed columns, instead of loading entities row by row.

## 10. Testing and recorded results
| Point | Recorded result |
|---|---|
| Before the 5.2 changes | 21 passed |
| After the relationship, query, projection and `AsNoTracking()` changes | **21 passed, 0 failed, 0 skipped** |

The N+1 measurement is also recorded: 23 queries before, 3 after.

## 11. Challenges and solutions
- **An unsafe generated migration.** EF wanted to rename `Payouts.StokvelMemberId` to `RecipientUserId`. But the old column held the membership's surrogate ID, **not** a user ID, so a rename would have produced wrong data. The README's "Unsafe generated payout rename" section shows how the migration was corrected and existing development data was inspected.
- **Tests that relied on shared state.** Four integration tests picked users and stokvels by list position, so they could select a pair that was already related. The duplicate-membership rule was **not** weakened. The tests now create their own users, stokvels, memberships, cycles and keys.

## 12. Learning outcomes
The technical notes summarise 5.2 as: "I worked on relationships and compared loading approaches to understand the cost of fetching related rows one at a time" ([technical notes §6](technical-notes-to-6.3.md#6-assignments-and-learning-journey)).

## 13. Evidence, commits and pull requests
- [README at `d891a98`](https://github.com/mdvar2/RondiTrack/blob/d891a98/README.md): the full N+1 experiment with SQL, the migration review and the AsNoTracking audit.
- The [README archive](readme-archive-2026-10-11.md#assignment-52-----relationships-and-query-behavior) carries the same sections, with its [migration review](readme-archive-2026-10-11.md#assignment-52-migration-review) and [N+1 experiment](readme-archive-2026-10-11.md#query-behavior-and-the-n1-experiment).

## 14. Relationship to other assignments
- **Before:** [5.1](assignment-5.1.md) persisted only part of the model.
- **Next:** [5.3](assignment-5.3.md) added database constraints, concurrency and pagination on top of these relationships.
