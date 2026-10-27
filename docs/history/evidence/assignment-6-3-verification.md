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
| Development database unavailable run | **PASS, 14 October 2026, 06:26 SAST.** Fresh build: zero warnings/errors. Real development connection rejected with admission disabled; 122 passed, 0 failed, 0 skipped using Testcontainers; original admission restored and final `SELECT 1` succeeded. |
| Push | Remote `main` verified with `git ls-remote` at `64a6e7a8339b24325f4a1d12d5bdbfb927eb669b`, matching local HEAD. Current script/documentation changes remain uncommitted and unpushed pending review. |

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

## Successful offline verification

The user approved the strengthened `scripts/verify-assignment-6-2-isolation.ps1`. It checks Docker Linux availability, builds the solution without incremental compilation, confirms PostgreSQL connectivity/ownership, records original admission, and requires zero development sessions before and after disabling admission. Restoration is marked necessary before attempting ALTER, including ambiguous client failures. It restores the original setting in `finally`, checks metadata and verifies `SELECT 1`. It does not delete development records, alter schemas/migrations or terminate sessions. Recovery commands are saved before isolation; restoration failure produces a prominent warning and cannot produce success.

Actual approved invocation from repository root (process-only execution-policy override; elevated tool permissions were needed for Docker pipe access):

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File ./scripts/verify-assignment-6-2-isolation.ps1
```

Successful run ID: `20261014T042604243Z-5eed28819c41447da68c20cfaade93a9` (14 October 2026, 06:26 SAST). The script exited 0. TRX outcome `Completed`: total/executed/passed 122, failed 0, notExecuted 0. The development probe was rejected with `ALLOW_CONNECTIONS=false` and zero existing sessions. Original admission was `true`; restoration metadata and final `SELECT 1` were verified. The TRX records two disposable PostgreSQL containers, both `ronditrack_test`, with seven migrations. All database-backed tests use the guarded Testcontainers factory.

Local evidence directory: `C:\Users\gwiny\RondiTrack\obj\assignment-6-3-offline-20261014T042604243Z-5eed28819c41447da68c20cfaade93a9`. Files use the same run ID:

- `tests-20261014T042604243Z-5eed28819c41447da68c20cfaade93a9.trx`
- `tests-20261014T042604243Z-5eed28819c41447da68c20cfaade93a9.log`
- `build-20261014T042604243Z-5eed28819c41447da68c20cfaade93a9.log`
- `docker-20261014T042604243Z-5eed28819c41447da68c20cfaade93a9.log`
- `evidence-20261014T042604243Z-5eed28819c41447da68c20cfaade93a9.txt`
- `recovery-20261014T042604243Z-5eed28819c41447da68c20cfaade93a9.txt`

Earlier attempts stopped before building or accessing PostgreSQL: Docker was unavailable, then sandbox access to its pipe was denied. These attempts are not passing evidence. Raw artifacts remain local and ignored. The successful offline run supersedes the pending status in historical trainer notes.

## Boundaries and unresolved domain rules

All controller routes are versioned. Stable health and documentation routes are preserved to retain their existing operational/test contracts. Health readiness intentionally returns plain text `Unhealthy` with 503; controller errors use Problem Details. Protocol failures rejected by the web server/proxy before this application pipeline cannot be guaranteed an application correlation ID or JSON body.

There is no next-payout schedule in the existing entities/services. `nextPayoutDate` is null; its nullable UTC date-time contract does not invent a deadline. Current cycle means the server's UTC month; per-member obligation uses the stokvel contribution amount; no cycle means no current obligation. Duplicate periods prefer Open then stable ID. These assumptions need confirmation if the business expects something else.

Rates and the preserved idempotency store are process-local. Three load-balanced instances can grant three times the nominal allowance and do not share replay records. A restart forgets both. The existing unique member/cycle constraint prevents duplicate contribution rows, but cross-instance/restart retries can return conflict instead of the original response. This assignment preserves that existing contract rather than introducing distributed infrastructure or changing financial rules.

## Publication audit

Final solution build succeeded with zero warnings and errors. The earlier local in-memory audit compared outgoing commit snapshots and current files against actual configured signing keys/connection credentials without exposing values. It uses assignment-context checks for short dictionary credentials to distinguish actual password literals from API field/type vocabulary. Credential matches: 0. Tracked build artifacts: 0. This cannot guarantee every possible secret format. Remote `main` now contains the earlier Assignment 6.3 work at `64a6e7a`. Current verification changes remain uncommitted and unpushed pending review; no new publication is authorized.
