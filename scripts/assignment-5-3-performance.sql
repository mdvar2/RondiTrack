-- Assignment 5.3 performance experiment
--
-- Run this against a disposable/local RondiTrack database only.
-- It creates an isolated stokvel/cycle and 10,000 valid members and
-- contributions so EXPLAIN ANALYZE measures a realistic table volume.
--
-- IMPORTANT: Run section A first and save the EXPLAIN ANALYZE output.
-- Then run section B, run ANALYZE, and run the same EXPLAIN ANALYZE again.
-- Record the actual PostgreSQL output in the README; do not invent timings.

-- A. Seed 10,000 contributions
DO $$
DECLARE
    v_stokvel uuid := gen_random_uuid();
    v_cycle uuid := gen_random_uuid();
BEGIN
    INSERT INTO "Stokvels" ("Id", "Name", "ContributionAmount")
    VALUES (v_stokvel, 'Assignment 5.3 Performance Stokvel', 1000);

    INSERT INTO "ContributionCycles" ("Id", "StokvelId", "Period", "TargetAmount", "Status")
    VALUES (v_cycle, v_stokvel, 'PERF-5.3', 10000000, 'Open');

    CREATE TEMP TABLE perf_members (
        user_id uuid PRIMARY KEY,
        sequence_no integer NOT NULL
    ) ON COMMIT DROP;

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
           v_stokvel,
           'Member',
           TIMESTAMPTZ '2026-01-01 00:00:00+00'
               + (sequence_no * INTERVAL '1 second')
    FROM perf_members;

    INSERT INTO "Contributions"
        ("Id", "StokvelId", "UserId", "ContributionCycleId", "Amount", "RecordedAtUtc")
    SELECT gen_random_uuid(),
           v_stokvel,
           user_id,
           v_cycle,
           1000,
           TIMESTAMPTZ '2026-01-01 00:00:00+00'
               + (sequence_no * INTERVAL '1 second')
    FROM perf_members;

    RAISE NOTICE 'Performance StokvelId: %, CycleId: %', v_stokvel, v_cycle;
END $$;

ANALYZE "Contributions";

-- Replace the two UUID placeholders below with the IDs printed by the NOTICE.
EXPLAIN (ANALYZE, BUFFERS)
SELECT c."Id",
       c."StokvelId",
       c."UserId",
       c."ContributionCycleId",
       c."Amount",
       c."RecordedAtUtc"
FROM "Contributions" AS c
WHERE c."StokvelId" = 'REPLACE_STOKVEL_ID'::uuid
  AND c."ContributionCycleId" = 'REPLACE_CYCLE_ID'::uuid
ORDER BY c."RecordedAtUtc" DESC, c."Id" DESC
LIMIT 26;

-- B. Add the index that matches the equality filters followed by the
-- deterministic keyset-pagination ordering columns.
CREATE INDEX IF NOT EXISTS "IX_Contributions_StokvelId_ContributionCycleId_RecordedAtUtc_Id"
ON "Contributions"
    ("StokvelId", "ContributionCycleId", "RecordedAtUtc" DESC, "Id" DESC);

ANALYZE "Contributions";

-- Run the exact same EXPLAIN (ANALYZE, BUFFERS) query from section A again.
-- Compare scan type, execution time, estimated/actual rows, and rows removed
-- by filter (where PostgreSQL reports it).
