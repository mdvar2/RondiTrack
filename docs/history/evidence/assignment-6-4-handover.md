# Assignment 6.4 handover and verification checklist

## Delivered in this updated working copy
- Multi-stage .NET 10 Dockerfile, non-root runtime, Compose PostgreSQL 17 and one-shot migrations.
- CI restore/build/test/Docker build.
- Cycle-close background service with configurable interval, Johannesburg calendar cutoff, cents-based arrears, transaction-scoped advisory locking, execution-strategy-wrapped transactions, activity span and counters.
- Set-based refresh-token-family housekeeping and configurable grace period.
- Test fixture isolates scheduled hosted services; new cycle-close integration tests cover cutoff, future dates, repeated runs, and concurrent processors.

## Outstanding or not verified
- Run the full Testcontainers suite and fix any failures; no .NET SDK or Docker engine was available in the artifact-building environment.
- Add tests for three-cycle catch-up, per-stokvel failure isolation, partial payments and refresh-token housekeeping.
- Current idempotency store is scoped in-memory; it does not have persistent rows to delete with ExecuteDeleteAsync. Implement a database-backed store, retention and replay tests.
- Add unique cycle `(StokvelId, Period)` constraint with an EF migration after checking existing data and period semantics.
- Gather actual image size, clean and cached build timings, image history, two-replica evidence, and a fresh-clone peer walkthrough.
- Add and validate seeded demo accounts if the trainer requires a reproducible walkthrough.
- Capture green and red PR CI links and attach them to the submission evidence.

## Safety notes
- Never commit `.env`, real JWT keys, refresh tokens or personal data.
- Compose runs API in Production: Scalar/OpenAPI UI is intentionally unavailable there.
- Do not report this artifact as fully tested until local `dotnet test` and CI are green.
