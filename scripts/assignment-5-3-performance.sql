-- Assignment 5.3 performance experiment
--
-- Run against a disposable/local RondiTrack database only.
-- This psql script seeds 10,000 contributions, runs the logged endpoint SQL
-- before the performance index, creates the index, then runs the same SQL
-- again. It leaves the isolated seed data in the database for inspection.
--
-- The SELECT shape below is copied from the EF Core Database.Command log for
-- GET /api/stokvels/{stokvelId}/cycles/{cycleId}/contributions?pageSize=26.
-- Only bound parameters are substituted; LIMIT 27 is pageSize + 1.

\set ON_ERROR_STOP on

BEGIN;

SELECT gen_random_uuid()::text AS stokvel_id \gset
SELECT gen_random_uuid()::text AS cycle_id \gset
\echo Performance stokvel id: :stokvel_id
\echo Performance cycle id: :cycle_id

INSERT INTO "Stokvels" ("Id", "Name", "ContributionAmount")
VALUES (:'stokvel_id'::uuid, 'Assignment 5.3 Performance Stokvel', 1000);

INSERT INTO "ContributionCycles"
    ("Id", "StokvelId", "Period", "TargetAmount", "Status")
VALUES (:'cycle_id'::uuid, :'stokvel_id'::uuid, 'PERF-5.3', 10000000, 'Open');

CREATE TEMP TABLE perf_members (
    user_id uuid PRIMARY KEY,
    sequence_no integer NOT NULL
);

INSERT INTO perf_members (user_id, sequence_no)
SELECT gen_random_uuid(), n
FROM generate_series(1, 10000) AS n;

INSERT INTO "Users" ("Id", "Name", "Email")
SELECT user_id,
       'Performance User ' || sequence_no,
       'perf-' || user_id || '@example.com'
FROM perf_members;

INSERT INTO "StokvelMembers" ("UserId", "StokvelId", "Role", "JoinedAtUtc")
SELECT user_id,
       :'stokvel_id'::uuid,
       'Member',
       TIMESTAMPTZ '2026-01-01 00:00:00+00'
           + (sequence_no * INTERVAL '1 second')
FROM perf_members;

INSERT INTO "Contributions"
    ("Id", "StokvelId", "UserId", "ContributionCycleId", "Amount", "RecordedAtUtc")
SELECT gen_random_uuid(),
       :'stokvel_id'::uuid,
       user_id,
       :'cycle_id'::uuid,
       1000,
       TIMESTAMPTZ '2026-01-01 00:00:00+00'
           + (sequence_no * INTERVAL '1 second')
FROM perf_members;

-- Ensure the baseline excludes the index even if its EF migration has
-- already been applied to this local database.
DROP INDEX IF EXISTS
    "IX_Contributions_StokvelId_ContributionCycleId_RecordedAtUtc_Id";
ANALYZE "Contributions";

-- Before index: SQL shape captured from the actual paged endpoint log.
EXPLAIN (ANALYZE, BUFFERS)
SELECT c0."Id", c0."StokvelId", c0."UserId", u."Name", u."Email", s."Role", c0."ContributionCycleId", c0."Amount", c0."RecordedAtUtc", c0.xmin AS "Version"
FROM (
    SELECT c."Id", c."Amount", c."ContributionCycleId", c."RecordedAtUtc", c."StokvelId", c."UserId", c.xmin
    FROM "Contributions" AS c
    WHERE c."StokvelId" = :'stokvel_id'::uuid AND c."ContributionCycleId" = :'cycle_id'::uuid
    ORDER BY c."RecordedAtUtc" DESC, c."Id" DESC
    LIMIT 27
) AS c0
INNER JOIN "StokvelMembers" AS s ON c0."UserId" = s."UserId" AND c0."StokvelId" = s."StokvelId"
INNER JOIN "Users" AS u ON s."UserId" = u."Id"
ORDER BY c0."RecordedAtUtc" DESC, c0."Id" DESC;

CREATE INDEX
    "IX_Contributions_StokvelId_ContributionCycleId_RecordedAtUtc_Id"
ON "Contributions"
    ("StokvelId", "ContributionCycleId", "RecordedAtUtc" DESC, "Id" DESC);
ANALYZE "Contributions";

-- After index: same endpoint SQL and parameter values.
EXPLAIN (ANALYZE, BUFFERS)
SELECT c0."Id", c0."StokvelId", c0."UserId", u."Name", u."Email", s."Role", c0."ContributionCycleId", c0."Amount", c0."RecordedAtUtc", c0.xmin AS "Version"
FROM (
    SELECT c."Id", c."Amount", c."ContributionCycleId", c."RecordedAtUtc", c."StokvelId", c."UserId", c.xmin
    FROM "Contributions" AS c
    WHERE c."StokvelId" = :'stokvel_id'::uuid AND c."ContributionCycleId" = :'cycle_id'::uuid
    ORDER BY c."RecordedAtUtc" DESC, c."Id" DESC
    LIMIT 27
) AS c0
INNER JOIN "StokvelMembers" AS s ON c0."UserId" = s."UserId" AND c0."StokvelId" = s."StokvelId"
INNER JOIN "Users" AS u ON s."UserId" = u."Id"
ORDER BY c0."RecordedAtUtc" DESC, c0."Id" DESC;

COMMIT;
