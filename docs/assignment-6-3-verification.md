# Assignment 6.3 verification — 14 October 2026

## Results

| Check | Actual result |
|---|---|
| Initial baseline | Build succeeded; 101 fixture failures because Docker was stopped; no application assertions reached. |
| Versioning full suite | 102 passed, 0 failed, 0 skipped. |
| CORS stage | 4 passed, 0 failed. |
| Rate-limit stage, including CORS | 8 passed, 0 failed. |
| Home stage | 3 passed, 0 failed. |
| Error-contract full suite | 120 passed, 0 failed, 0 skipped. |
| Final audit first run | 1 passed (production CORS), 1 failed (framework 415 contract). Fixed the actual API contract; assertions retained. |
| Final full solution suite | **122 passed, 0 failed, 0 skipped**, 34 seconds. |
| TypeScript before temporary C# rename | Strict compilation exit 0. |
| TypeScript after actual C# rename and actual OpenAPI regeneration | TS2339, exit 2. |
| TypeScript after restoring C# and regenerating | Strict compilation exit 0. |
| Exact-money runtime checks | Passed empty, fractional, mixed-scale, beyond-safe-integer and invalid-input checks. |
| Existing test source comparison | 28 existing C# files compared to a733cc6; zero changes beyond required versioned URLs. Factory additionally receives a generous login configuration, with assertions unchanged. |
| Development database unavailable run | **NOT VERIFIED.** PostgreSQL service is running. No approval was given to stop it or change connection admission. |
| Push | Not performed; requires the user's approval after review. |

Commands:

```powershell
dotnet build RondiTrack.sln --no-restore -m:1 --verbosity quiet
dotnet test RondiTrack.sln --no-restore -m:1 --logger "trx;LogFileName=final.trx" --results-directory obj/assignment-6-3 --verbosity quiet
cd clients/typescript
npm ci --ignore-scripts
npm run regenerate
npm run test:money
```

Local raw logs/TRX/build outputs stay in ignored `obj`, `bin` and `node_modules`; no raw connection diagnostics are committed. The committed OpenAPI document contains schema information, not live user data. The actual compiler output is documented in the client README and main README.

Each PostgreSQL factory compares the API connection target to its disposable container, applies all seven preserved migrations and verifies `current_database() = ronditrack_test`. This proves the tests' configured target; it does **not** replace the requested run with development unavailable. Existing isolated container-outage health tests stop only their own disposable database.

## Safe offline verification awaiting approval

The existing `scripts/verify-assignment-6-2-isolation.ps1` is concrete and reviewable. It restricts its target to the local `ronditrack` database, requires zero active sessions, temporarily sets `ALLOW_CONNECTIONS false`, proves a real development connection is rejected, runs the built full test assembly against Testcontainers, and restores `ALLOW_CONNECTIONS true` in `finally`, then verifies `SELECT 1`. It does not alter tables, business rows or migrations or terminate sessions. It **does** modify database connection-admission metadata, so the user's Stage 7 rule requires approval before running it. Its stage-numbered filenames are historical; the rebuilt assembly includes all Assignment 6.3 tests.

After approval only, from repository root:

```powershell
dotnet build RondiTrack.sln --no-restore -m:1
./scripts/verify-assignment-6-2-isolation.ps1
```

Until approval and a successful actual run, report this requirement as NOT VERIFIED.

## Boundaries and unresolved domain rules

All controller routes are versioned. Stable health and documentation routes are preserved to retain their existing operational/test contracts. Health readiness intentionally returns plain text `Unhealthy` with 503; controller errors use Problem Details. Protocol failures rejected by the web server/proxy before this application pipeline cannot be guaranteed an application correlation ID or JSON body.

There is no next-payout schedule in the existing entities/services. `nextPayoutDate` is null; its nullable UTC date-time contract does not invent a deadline. Current cycle means the server's UTC month; per-member obligation uses the stokvel contribution amount; no cycle means no current obligation. Duplicate periods prefer Open then stable ID. These assumptions need confirmation if the business expects something else.

Rates and the preserved idempotency store are process-local. Three load-balanced instances can grant three times the nominal allowance and do not share replay records. A restart forgets both. The existing unique member/cycle constraint prevents duplicate contribution rows, but cross-instance/restart retries can return conflict instead of the original response. This assignment preserves that existing contract rather than introducing distributed infrastructure or changing financial rules.

## Publication audit

Final solution build succeeded with zero warnings and errors. The local in-memory audit compared outgoing commit snapshots and current files against actual configured signing keys/connection credentials without exposing values. It uses assignment-context checks for short dictionary credentials to distinguish actual password literals from API field/type vocabulary. Credential matches: 0. Tracked build artifacts: 0. git diff --check passed. This cannot guarantee every possible secret format. Final status is checked after the logical documentation commit; no push is authorized yet.
