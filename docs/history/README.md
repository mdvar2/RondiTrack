# Assignment history

RondiTrack is built one Bitcube Software Development Trainee assignment at a time. Each assignment extends the same repository. This folder is the learning record: for every assignment, what it asked for, what I built and how, how to run and test it, what went wrong, and how it connects to the others.

The [root README](../../README.md) describes the project **as it is now**. Everything in this folder describes the project **as it was** at each stage. Current technical references are listed in the [documentation index](../README.md).

## How to read a record

Each `assignment-<n>.md` follows the same outline:

| # | Section |
|---|---|
| 1 | Purpose and learning objectives |
| 2 | Original requirements |
| 3 | What I implemented |
| 4 | Technologies, tools and dependencies |
| 5 | Prerequisites and installation |
| 6 | Environment configuration |
| 7 | How to run or reproduce, **as submitted** and **in the current codebase** |
| 8 | Important files and components |
| 9 | How it works |
| 10 | Testing and recorded results |
| 11 | Challenges and solutions |
| 12 | Learning outcomes |
| 13 | Evidence, commits and pull requests |
| 14 | Relationship to other assignments |
| 15 | Original record, *for 6.3–7.4 only*: the README section written at the time, unchanged |

**Where something isn't recorded, the record says so.** The original assignment briefs (PDFs) aren't in the repository. Requirements are given only where a record of them exists:
- 6.2 and 8.1: the brief itself (6.2 as quoted in its compliance matrix)
- 5.4, 6.1 and 6.3: requirement checklists
- 4.4–5.3: my Definition-of-Done tables
- 6.4 and 7.x: requirements quoted in the README of the time

Learning objectives are mostly not recorded.

**Reproducing an old version.** Each record names the commit on `main` that holds that assignment. The safe way to look at it is a separate folder: `git worktree add ..\RondiTrack-<n> <commit>`, removed afterwards with `git worktree remove`. Your current work is untouched. **For 5.1–5.3, use a new, empty database:** their tests and migrations use the database in your user secrets, which is your current development database.

## Assignments, oldest first

"Re-run" means the commit's own code was exported unchanged and its tests were run on 27 Oct 2026. Commits and PRs link to GitHub.

| Assignment | Purpose | Main implementation | Technologies introduced | Commit / branch / PR | Re-run |
|---|---|---|---|---|---|
| [4.1–4.4](assignment-4.4.md) — first API | A working, validated, testable stokvel API | CRUD for users, stokvels, memberships and cycles; contribution recording; DTOs, services, RFC 9457 errors, correlation IDs, idempotency keys (in memory) | .NET 10, ASP.NET Core, FluentValidation, OpenAPI + Scalar, xUnit | [`97029dc`](https://github.com/mdvar2/RondiTrack/commit/97029dc) on `main` | ✅ 19/19 |
| [5.1](assignment-5.1.md) — EF Core and PostgreSQL | Move from memory to a real database | DbContext, first migration, payouts with an explicit transaction, Npgsql retry | EF Core 10, Npgsql, PostgreSQL 17, user secrets | [`b4d19dc`](https://github.com/mdvar2/RondiTrack/commit/b4d19dc) on `main` | Not run (uses your dev DB) |
| [5.2](assignment-5.2.md) — relationships | Real relationships and their SQL cost | Composite membership key, many-to-many, the N+1 experiment (23 → 3 queries), `AsNoTracking()` audit | — | [`d891a98`](https://github.com/mdvar2/RondiTrack/commit/d891a98) on `main` | Not run (uses your dev DB) |
| [5.3](assignment-5.3.md) — database protections | Refuse bad data, stale writes and unbounded lists | Unique indexes, row-version concurrency (`ETag`/`If-Match`), token pagination, query-plan and index experiment | `EXPLAIN` | `fix/assignment-5-3-completion`, [PR #1](https://github.com/mdvar2/RondiTrack/pull/1), [`6444940`](https://github.com/mdvar2/RondiTrack/commit/6444940) | Not run (uses your dev DB) |
| [5.4](assignment-5.4.md) — clean architecture | Proper project boundaries and self-contained tests | API / Domain / Infrastructure split, DI lifetimes, Testcontainers fixture | Testcontainers, Docker | `assignment-5-4-clean-architecture`, [PR #2](https://github.com/mdvar2/RondiTrack/pull/2), [`e35a70c`](https://github.com/mdvar2/RondiTrack/commit/e35a70c) | ✅ 25/25 |
| [6.1](assignment-6.1.md) — authentication and authorization | Who you are, and what you may do in each stokvel | JWT, rotating refresh-token families in cookies, live per-stokvel role checks | JwtBearer, PasswordHasher | same branch, [PR #2](https://github.com/mdvar2/RondiTrack/pull/2), [`e2bbc32`](https://github.com/mdvar2/RondiTrack/commit/e2bbc32) | ✅ 62/62 |
| [6.2](assignment-6.2.md) — observability | See what happened, how often, and where time went, without leaking personal data | OpenTelemetry traces and metrics, `correlationId`, POPIA log audit, health checks | OpenTelemetry, OTLP, Aspire dashboard | [`c06d617`](https://github.com/mdvar2/RondiTrack/commit/c06d617) on `main` | ✅ 101/101 |
| [6.3](assignment-6.3.md) — Frontend-Ready Backend | Make the API safe and convenient for a browser | `/api/v1`, CORS, rate limits, `/me/home`, one error contract, generated TypeScript | ASP.NET rate limiter and CORS, openapi-typescript | [`dd83b48`](https://github.com/mdvar2/RondiTrack/commit/dd83b48) on `main` | ✅ 122/122 |
| [6.4](assignment-6.4.md) — Shipped, Scheduled and Handed Over | Ship it, and let it maintain itself | Docker and Compose, CI, cycle closing with arrears, housekeeping, durable idempotency | Docker, Compose, GitHub Actions, `BackgroundService`, `TimeProvider` | [`16d1a4c`](https://github.com/mdvar2/RondiTrack/commit/16d1a4c) on `main`; CI proof [PR #3](https://github.com/mdvar2/RondiTrack/pull/3) | ✅ 130/130 |
| [7.1](assignment-7.1.md) — StokvelCard & ContributionCard | Start the frontend | `web/`: two typed components on sample data | React 19, Vite 8, TypeScript, Vitest | [`a5cf3c6`](https://github.com/mdvar2/RondiTrack/commit/a5cf3c6) on `main` | Not re-run |
| [7.2](assignment-7.2.md) — A Styled, Responsive Stokvel View | A usable, responsive dashboard | Themes, cycle grouping, badges, due-date countdowns, `arrearsCents` | Tailwind CSS 4, shadcn/ui | `assignment-7.2`, [`e303c6c`](https://github.com/mdvar2/RondiTrack/commit/e303c6c) | Not re-run |
| [7.3](assignment-7.3.md) — Live RondiTrack Data | Real, signed-in data in the browser | Sign-in, single-flight refresh, typed client, stokvel-switch isolation | TanStack Query, openapi-fetch | `assignment-7.3`, [PR #4](https://github.com/mdvar2/RondiTrack/pull/4), [`2a4db93`](https://github.com/mdvar2/RondiTrack/commit/2a4db93) | Not re-run |
| [7.4](assignment-7.4.md) — Record a Contribution | The first safe write from the browser | Contribution form, rands → cents, one idempotency key per payment, `amountInCents`, closed cycles refused | React Hook Form, Zod, MSW | `assignment-7.4`, [PR #5](https://github.com/mdvar2/RondiTrack/pull/5), [`e063646`](https://github.com/mdvar2/RondiTrack/commit/e063646) | ✅ web 63 (README recorded 62) |
| [8.1](assignment-8.1.md) — RondiTrack Routes | Move to Next.js; tokens never reach the browser | `web-next/`: Server Components, encrypted session cookie, treasurer dashboard as a filtered view; API fixes | Next.js 16.4.1, jose, server-only | `assignment-8.1` ([`b833502`](https://github.com/mdvar2/RondiTrack/commit/b833502), [`f65649f`](https://github.com/mdvar2/RondiTrack/commit/f65649f)); PR not yet opened | Current: 145 backend, 58 web-next |

**4.1–4.3:** the 6.2 brief refers to "your Assignment 4.1–6.1 repository", so these existed. Their work is in the commits from 22–25 September, but the repository doesn't record which commit belongs to which of them. They're covered in the 4.4 record.

## Earlier versions of the documentation

| File | What it is |
|---|---|
| [readme-archive-2026-10-11.md](readme-archive-2026-10-11.md) | The root README as of `e2bbc32` (11 Oct 2026, after 5.4 and 6.1), with its encoding and headings repaired (about 190 of 1,800 lines differ), including its clean-machine setup guide |
| [technical-notes-to-6.3.md](technical-notes-to-6.3.md) | Detailed notes up to Assignment 6.3, including the full POPIA logging audit (Appendix A) |
| [readme-overview-to-7.4.md](readme-overview-to-7.4.md) | The general part of the root README up to 7.4, including my reflections on what I learned |
| [web-readme-assignment-7.2.md](web-readme-assignment-7.2.md) | `web/README.md` from the sample-data stage |
| [assignment-7.3-working-notes.md](assignment-7.3-working-notes.md) | Working notes kept during 7.3 |
| [evidence/](evidence/) | Each assignment's verification reports, compliance matrices and artifacts, under their original file names |

The README as it was submitted for every assignment is also available in git: `git show <commit>:README.md`, or the "README as submitted" link in each record.

## Conventions

- One record per assignment, `assignment-<n>.md`, using the outline above.
- Evidence keeps its original names (`assignment-<major>-<minor>-<topic>.md`) in [`evidence/`](evidence/). Files from the old `docs/evidence/` folder are in [`evidence/artifacts/`](evidence/artifacts/).
- When an assignment is finished, its detail moves here. The root README keeps a short summary with a link.
