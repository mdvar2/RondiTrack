# Assignment 5.3 performance evidence

This experiment used PostgreSQL 17 and a seeded dataset of 10,000
contributions for one stokvel and cycle. The dataset was removed after
measurement; the performance index remains installed through the EF Core
migration. The full plans and discussion are also included in README.

## Query source and method

The paged endpoint was called with `pageSize=26` while EF Core database
command logging was set to `Information`. The SELECT below is copied from
the `Microsoft.EntityFrameworkCore.Database.Command` log. Only the bound
parameters are substituted to make it executable in `psql`; the query
shape was not rewritten. Since the endpoint fetches `pageSize + 1` rows,
the logged `@p` value was 27.

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

The composite index is ordered by the two equality filters,
`(StokvelId, ContributionCycleId)`, followed by the requested descending
sort and tiebreaker, `(RecordedAtUtc DESC, Id DESC)`. This allows
PostgreSQL to seek to one stokvel/cycle range and return rows in the
endpoint's deterministic page order without sorting that range.

## Before index: 10,000 matching contributions

The filtered scan estimated 9,952 rows and scanned 10,000; it removed 48
other contribution rows. The complete endpoint query took 7.578 ms.

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

## After index: same query and data

After creating
`IX_Contributions_StokvelId_ContributionCycleId_RecordedAtUtc_Id` and
running `ANALYZE "Contributions"`, PostgreSQL used the composite index.
It still estimated 9,952 rows in the full matching range, but the `LIMIT`
stopped the index scan after 27 rows. No contribution rows were removed
by a post-index filter. Complete query time was 0.383 ms.

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

## Small development data

After removing the seeded benchmark rows, the development database had
48 contributions total. Running the same endpoint SQL for a cycle with
one matching contribution took 0.126 ms (estimated one row, actual one).
PostgreSQL used the existing cycle index and sorted the single matching
row; it did not use the new composite index. For this small range, this
was a valid low-cost plan. A cost-based planner can prefer a narrower
existing index or even a sequential scan on tiny tables; that is not a
bug or evidence that the composite index is malformed.

```text
Nested Loop  (cost=8.87..17.01 rows=1 width=163) (actual time=0.072..0.073 rows=1 loops=1)
  Join Filter: ((c."UserId" = s."UserId") AND (s."StokvelId" = c."StokvelId"))
  Buffers: shared hit=15
  ->  Nested Loop  (cost=8.59..16.62 rows=1 width=172) (actual time=0.060..0.060 rows=1 loops=1)
        Buffers: shared hit=12
        ->  Limit  (cost=8.30..8.31 rows=1 width=81) (actual time=0.049..0.050 rows=1 loops=1)
              Buffers: shared hit=9
              ->  Sort  (cost=8.30..8.31 rows=1 width=81) (actual time=0.047..0.048 rows=1 loops=1)
                    Sort Key: c."RecordedAtUtc" DESC, c."Id" DESC
                    Sort Method: quicksort  Memory: 25kB
                    Buffers: shared hit=9
                    ->  Index Scan using "IX_Contributions_ContributionCycleId" on "Contributions" c  (cost=0.27..8.29 rows=1 width=81) (actual time=0.015..0.015 rows=1 loops=1)
                          Index Cond: ("ContributionCycleId" = '<small-cycle-id>'::uuid)
                          Filter: ("StokvelId" = '<small-stokvel-id>'::uuid)
                          Buffers: shared hit=3
        ->  Index Scan using "PK_Users" on "Users" u  (cost=0.29..8.30 rows=1 width=91) (actual time=0.008..0.008 rows=1 loops=1)
              Index Cond: ("Id" = c."UserId")
              Buffers: shared hit=3
  ->  Index Scan using "PK_StokvelMembers" on "StokvelMembers" s  (cost=0.29..0.37 rows=1 width=39) (actual time=0.011..0.011 rows=1 loops=1)
        Index Cond: ("UserId" = u."Id")
        Buffers: shared hit=3
Planning:
  Buffers: shared hit=578 dirtied=4
Planning Time: 11.645 ms
Execution Time: 0.126 ms
```

The large-data measured time decreased from 7.578 ms to 0.383 ms, about
19.8 times faster for this run. The plan retained nested-loop joins and
primary-key lookups for the member/user projection, as those only run for
the 27-row limited result. Measurements vary by machine, cache state, and
data distribution; they are evidence for this local run, not a general
latency guarantee.
