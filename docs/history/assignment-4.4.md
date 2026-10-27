# Assignment 4.4 — The first RondiTrack API (in-memory)

| | |
|---|---|
| Official title | Not recorded. The assignment brief isn't in the repository. |
| Dates | 22–29 September 2026 |
| Final commit on `main` | [`97029dc`](https://github.com/mdvar2/RondiTrack/commit/97029dc) "Add API integration tests and Assignment 4.4 documentation" |
| Branch / PR | Committed directly to `main`; no PR |
| README as submitted | [README at `97029dc`](https://github.com/mdvar2/RondiTrack/blob/97029dc/README.md) |
| Re-tested | **Yes, 27 Oct 2026**: the `97029dc` code, exported unchanged, passed **19/19** tests |

> The 6.2 brief, as quoted in its [compliance matrix](evidence/assignment-6-2-final-compliance-matrix.md), says "Continue in your Assignment 4.1–6.1 repository", so Assignments 4.1 to 4.3 came before this one. The commits from 22–25 September mention "Day 2" and "Day 3" work (DTOs and the service layer, then validation and error handling). The repository doesn't record which of those commits belongs to 4.1, 4.2 or 4.3, so this record covers all the work up to 4.4.

## 1. Purpose and learning objectives
The README at `97029dc` describes RondiTrack as "a .NET 10 Web API for managing users, stokvels, memberships, contribution cycles, and member contributions", built to demonstrate "clean API design through request and response DTOs, repositories, a focused service layer, FluentValidation, centralized RFC 9457 error handling, correlation IDs, idempotent contribution recording, and negative-path integration tests". The brief's learning objectives aren't recorded.

## 2. Original requirements
The brief isn't in the repository. The closest record is my **Definition of Done** table in the README at `97029dc`. For every endpoint it tracks whether it was documented, which validation or business guard it has, its automated coverage, and whether its status codes were reviewed. All 18 rows covered users, stokvels, membership, contribution cycles and contribution recording.

## 3. What I implemented
- CRUD endpoints for users, stokvels and contribution cycles, plus adding and removing members and recording a contribution. These were under `/api/...`; the `/api/v1` prefix came in 6.3.
- Request and response DTOs with manual mapping, a service layer (`MembershipService`, `ContributionService`) and repository interfaces.
- FluentValidation for "is this request well-formed?", and domain exceptions (`RequestValidationException`, `NotFoundException`, `BusinessRuleException`) for "is this allowed?".
- One central error handler that returns RFC 9457 Problem Details, and a correlation ID on each request.
- Idempotent contribution recording. A SHA-256 hash of each request is kept in memory with its `Idempotency-Key`. The same key and request replays the stored response; the same key with different data gets `409`.
- **Storage was entirely in memory**, with seeded users and stokvels, and reset on every restart.

## 4. Technologies, tools and dependencies
Verified against `RondiTrack.csproj` and `RondiTrack.Tests.csproj` at `97029dc`:

| Package | Version |
|---|---|
| Target framework | `net10.0` |
| FluentValidation (+ DependencyInjectionExtensions) | 12.1.1 |
| Microsoft.AspNetCore.OpenApi | 10.0.12 |
| Scalar.AspNetCore | 2.17.7 |
| xunit / xunit.runner.visualstudio | 2.9.3 / 3.1.4 |
| Microsoft.AspNetCore.Mvc.Testing | 10.0.12 |
| Microsoft.NET.Test.Sdk / coverlet.collector | 17.14.1 / 6.0.4 |

## 5. Prerequisites and installation
The .NET 10 SDK, and nothing else: no database and no Docker.

## 6. Environment configuration
None. There was no database and there were no secrets.

## 7. How to run or reproduce
**As submitted.** I checked these commands against the files at `97029dc`. A single `RondiTrack.csproj` sits at the root, and the launch profile uses port 5101.
```powershell
git worktree add ..\RondiTrack-4.4 97029dc     # a separate folder; your current work is untouched
cd ..\RondiTrack-4.4
dotnet restore
dotnet run                                       # http://localhost:5101, Scalar at /scalar/v1
dotnet test RondiTrack.Tests/RondiTrack.Tests.csproj
```
Re-run on 27 Oct 2026: `dotnet test` passed 19 of 19. When you're done, remove the folder with `git worktree remove ..\RondiTrack-4.4`.

**In the current codebase.** The same endpoints still exist under `/api/v1`, with authentication added in 6.1. The original ideas live on in `API/Handlers/GlobalExceptionHandler.cs`, `API/Validators/` and `API/Services/ContributionService.cs`.

## 8. Important files and components
At `97029dc` everything was in one project at the repository root: `Controllers/`, `DTOs/`, `Exceptions/`, `Filters/`, `Handlers/`, `Idempotency/`, `Models/`, `Repositories/`, `Services/`, `Validators/` and `Program.cs`. In 5.4 these moved into `API/`, `Domain/` and `Infrastructure/`.

## 9. How it works
A request reaches a thin controller. FluentValidation rejects malformed input with `400`. The service checks business rules and throws a domain exception when one is broken. The central handler turns every exception into a Problem Details response with the right status code and the correlation ID. Recording a contribution first checks the idempotency store, so a retry can't record twice.

## 10. Testing and recorded results
| Command | Recorded result (README at `97029dc`) | Re-run 27 Oct 2026 |
|---|---|---|
| `dotnet test RondiTrack.Tests/RondiTrack.Tests.csproj` | 19 passed, 0 failed | **19 passed, 0 failed** |

The suite covered unit, happy-path, negative-path, business-rule, idempotency and edge-case tests. The README also records manual testing with Scalar.

## 11. Challenges and solutions
**A deliberate broken-rule test.** I temporarily disabled the duplicate-membership check in `MembershipService.AddMemberAsync`. The test `DuplicateMembership_Returns409ProblemJson` then got `500` instead of `409`, because a defensive check in the `Stokvel` model threw an unexpected exception. So the tests did catch the regression. The README records the exercise in full.

## 12. Learning outcomes
Not recorded as such for this assignment. The README's explanations of the validation-versus-business-rule split and of duplicate contributions versus idempotency conflicts show what was covered.

## 13. Evidence, commits and pull requests
- The [README at `97029dc`](https://github.com/mdvar2/RondiTrack/blob/97029dc/README.md) has the full write-up, the Definition of Done and the test output.
- The later [README archive](readme-archive-2026-10-11.md#api-endpoints) carries the same endpoint and design sections forward.

## 14. Relationship to other assignments
- **Before:** none. This is where RondiTrack starts.
- **Next:** [5.1](assignment-5.1.md) replaced part of the in-memory storage with PostgreSQL and EF Core.
