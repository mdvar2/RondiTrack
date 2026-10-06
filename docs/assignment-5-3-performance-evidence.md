# Assignment 5.3 performance evidence

The repository contains the implementation needed for the Assignment 5.3
performance experiment, but measured PostgreSQL values are intentionally not
fabricated or committed from another machine.

Use `scripts/assignment-5-3-performance.sql` against the local PostgreSQL
development database.

## Required evidence

1. Run the seed section. It creates an isolated dataset containing 10,000
   contributions.
2. Run the supplied `EXPLAIN (ANALYZE, BUFFERS)` query **before** creating
   the performance index.
3. Record the PostgreSQL output below.
4. Create
   `IX_Contributions_StokvelId_ContributionCycleId_RecordedAtUtc_Id`.
5. Run `ANALYZE "Contributions";`.
6. Run the **same** `EXPLAIN (ANALYZE, BUFFERS)` query again.
7. Record the second PostgreSQL output below.

The index order is intentional: `StokvelId` and `ContributionCycleId` are
equality filters for the endpoint, followed by `RecordedAtUtc` and the unique
`Id` tiebreaker used by deterministic keyset pagination.

## Before index

- Dataset size: 10,000 contributions
- Scan type: **paste measured value**
- Planning time: **paste measured value**
- Execution time: **paste measured value**
- Estimated rows: **paste measured value**
- Actual rows: **paste measured value**
- Rows removed by filter: **paste measured value if reported**

```text
Paste the complete BEFORE EXPLAIN ANALYZE output here.
```

## After index

- Dataset size: 10,000 contributions
- Scan type: **paste measured value**
- Planning time: **paste measured value**
- Execution time: **paste measured value**
- Estimated rows: **paste measured value**
- Actual rows: **paste measured value**
- Rows removed by filter: **paste measured value if reported**

```text
Paste the complete AFTER EXPLAIN ANALYZE output here.
```

## Interpretation

Complete this only after running the experiment. Explain whether PostgreSQL
changed from a sequential scan to an index/index-only/bitmap plan, whether the
measured execution time improved, and how closely estimated rows matched actual
rows. If PostgreSQL chooses a different plan than expected, report the real plan
rather than changing the evidence.
