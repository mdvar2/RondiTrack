# Assignment 6.3 compliance matrix

Reviewed against every requirement in `Assignment_6_3_RondiTrack.pdf` and the user's staged request on 14 October 2026. PASS means implemented and verified by the cited tests/commands or source inspection; PARTIAL identifies a real limitation; NOT VERIFIED is not a passing result. The Matric Compass explanatory PDF is guidance, not a claim that its results occurred here.

Evidence abbreviations: `V` = VersionedRefreshTests; `C` = CorsContractTests; `R` = RateLimitContractTests; `H` = MemberHomeTests; `E` = ErrorContractTests. Final full solution test result: **122 passed, 0 failed, 0 skipped**. Detailed commands and staged results: [verification](assignment-6-3-verification.md) and [trainer notes](assignment-6-3-stages.md).

| Requirement | Result | Implementation / actual evidence |
|---|---|---|
| Read full assignment and inspect existing repository before editing | PASS | Both PDFs read in full; Stage 0 inventory and plan presented before edits. |
| Extend existing solution; retain 4.1–6.2 functionality | PASS | Existing repository/solution, no replacement app; all previous tests pass. |
| Preserve development database and migration chain | PASS | No database mutation/reset/migration performed; all seven migrations applied only to disposable test databases. |
| Baseline status, tests, environment and exact modification plan | PASS | Clean a733cc6; baseline Docker fixture failures reported, then Docker started; stage notes. |
| Existing assertions unchanged apart from versioned URLs | PASS | 28 existing .cs test files compared to a733cc6; zero differences after URL-only normalization. Shared factory adds configuration only. |
| No new NuGet packages | PASS | Existing package references unchanged; console exporter references existing tests. |
| Logical commits and stage explanations | PASS | Stage commits plus method/file/test/trainer notes. |
| Every existing controller API route under /api/v1 | PASS | ApiRoutes.V1 in all six current controllers; generated document has only versioned controller paths. |
| Literal interpretation: every route including probes/docs moves | PARTIAL | Health/docs operational paths intentionally preserved with existing assertions. Explicit README interpretation. |
| One reusable version prefix, no controller literal duplication | PASS | API/Common/ApiRoutes.cs; source inspection. |
| Refresh cookie path moves with route | PASS | Derived /api/v1/auth path covers refresh and logout; V and existing RefreshSessionTests. |
| Update all existing test URLs; preserve auth/authorization | PASS | URL-only audit; all existing security tests passed. |
| New integration test proves refresh at versioned path | PASS | V uses actual cookie handler through login, rotation and logout. |
| Document moved routes | PASS | README versioning section and docs/api-reference.md. |
| Configuration-driven origins; exactly HTTP localhost:3000 in Development | PASS | appsettings.Development.json and C preflight. |
| Default production origins empty | PASS | appsettings.json and C production-host test. |
| AllowCredentials, explicit methods and headers | PASS | CorsPolicies; credentialed C preflight. |
| Idempotency-Key allowed | PASS | Explicit WithHeaders entry and C browser preflight assertion. |
| Expose Retry-After and existing replay header if any | PASS | Retry-After exposed and R asserts it; no existing replay header exists. ETag/Location also exposed. |
| No wildcard origin/method/header or arbitrary reflection | PASS | Explicit lists, config validation; unknown origin receives no CORS headers. |
| OPTIONS works despite fallback JWT policy | PASS | Anonymous C preflight returns 204; CORS before auth. |
| Scoped 403 and 404 errors readable with CORS | PASS | Both C scoped-error cases passed. |
| README analyzes all three cookie deployments, origins/sites | PASS | Includes schemeful-site/localhost redirect/browser exceptions and cross-site limitations, with primary browser/framework references. |
| Login fixed-window by client IP, limit/window configured | PASS | RateLimitPolicies.Login and named endpoint attribute; R rejects third request at isolated limit 2. |
| Contributions and payouts have named user-ID policies | PASS | Separate token buckets keyed by authenticated GUID sub; R covers both endpoints. |
| Appropriate algorithm justified for twenty contribution entries | PASS | Capacity 30 contribution bucket; README/stage notes explain burst/refill tradeoff. |
| Policy names/rules central; no controller rule hard-coding | PASS | RateLimitPolicies and appsettings; endpoints reference names. |
| Every limiter rejection HTTP 429, not 503 | PASS | RejectionStatusCode and explicit OnRejected; R asserts login/contribution/payout 429. |
| Every limiter rejection valid Retry-After | PASS | Lease metadata, ceiling/minimum 1; R parses positive integer. |
| Every limiter rejection problem+json title/detail/correlation | PASS | R contract assertions; shared existing W3C Correlation.Apply. |
| No limiter internal exception details | PASS | Fixed public message and no exception serialization; R body checks. |
| 429 does not reserve/store/use a fresh contribution key | PASS | Rate limiter precedes controller/service; R directly checks store null and zero database rows. |
| Deterministic exhaust → K429 → wait → Ksuccess → Kreplay → one row | PASS | Three-second isolated bucket with two permits; waits positive Retry-After plus margin; identical body/201 and exactly one member/cycle contribution. |
| Two users do not share financial quota | PASS | R theory for both policies uses same host/IP with Treasurer and Chairperson. |
| Replays can be limited without losing stored response | PASS | R adds a later rejected replay and proves stored K remains; documented pipeline/allowance reasoning. |
| Rate-limit tests use WithWebHostBuilder/UseSetting isolation | PASS | All R tests boot private limiter hosts; shared fixture login allowance 1000. |
| README middleware order, three replicas and shared Wi-Fi | PASS | Includes process-local idempotency gap as well as process-local limiter behavior. |
| GET /api/v1/me/home signed-in response for every membership | PASS | MeController, MemberHomeService; H multi-membership and no-membership tests. |
| Per-stokvel name/role/current-cycle/paid/owed only | PASS | Six fields; H checks roles, exact totals and field count; no IDs/versions or others' money. |
| Next payout date | PARTIAL | Nullable UTC DateTimeOffset exists but is null: domain has no scheduling rule. No financial date invented. |
| Server calculations with TimeProvider | PASS | UTC month selection; H injected October clock independent of host date. |
| Role from each membership; no other members' totals | PASS | H distinct roles, other-member 499 excluded and September contribution excluded. |
| JavaScript-exact money and justification | PASS | Invariant full-precision decimal strings; generated string type; BigInt exact-addition runtime checks. |
| Named statuses and UTC date contract | PASS | Current-cycle Open/PaidOut; nullable DateTimeOffset date-time schema; actual scheduled date remains unresolved above. |
| Empty list for no memberships | PASS | H no-membership returns 200 with empty stokvels. |
| Avoid unnecessary DB queries/fields | PASS | Two projected queries for all memberships; one when empty; caller-only aggregates; H six-field response. |
| Inspect domain and document missing/assumed rules | PASS | Existing YYYY-MM periods, statuses, stokvel obligation and rotation inspected; no schedule; assumptions explicitly documented. |
| Audit application errors and exact shapes/status table | PASS | README covers 400/401/403/404/405/409/412/415/429/500 and operational 503; E and existing negative/correlation/privacy tests. |
| Unrestricted every-error JSON guarantee | PARTIAL | Preserved plain-text health 503; upstream protocol errors before pipeline cannot guarantee application JSON/correlation. |
| FluentValidation errors map with camelCase names | PASS | ValidationFilter → RequestValidationException.Errors → handler extension; E field assertions. |
| No internal type/stack/exception messages in 5xx | PASS | Existing handler generic 500 and existing correlation/log-leak tests retained and passed. |
| JSON numbers supplied as strings rejected 400 | PASS | Strict MVC serializer; E quoted amount case without internal names. |
| Actual OpenAPI integer/decimal schemas numeric-only | PASS | Strict HTTP serializer; E checks generated CreateStokvelRequest/RecordContributionRequest/UserResponse plus schema traversal. |
| Existing 6.2 correlation preserved | PASS | Correlation.Apply in all application paths; full existing CorrelationTests passed; E/R assert IDs. |
| TypeScript folder outside .NET projects; own package.json | PASS | clients/typescript; no .NET project includes its tooling. |
| Exactly pinned openapi-typescript and TypeScript | PASS | 7.13.0 and compatible 5.9.3; npm lockfile. |
| Generate from actual RondiTrack OpenAPI and commit output | PASS | Console exporter uses actual API/Testcontainers; committed openapi.json/schema.d.ts. |
| Small generated-home-type function | PASS | home.ts MemberHome derives from generated endpoint path; totalOutstanding returns exact decimal string. |
| Strict tsc compile and regenerate command | PASS | npm run regenerate exits 0; tsc --noEmit --strict; client README/scripts. |
| Real C# rename, regenerate and actual compiler failure | PASS | AmountOwed→OutstandingBalance; actual TS2339 at home.ts(7,56), exit 2. |
| Revert C# rename, regenerate correct types, strict success | PASS | Restored AmountOwed; actual re-export and exit 0; no renamed field in committed snapshot. |
| README includes actual before/after compiler results | PASS | Full exact compiler diagnostic in main/client README and stage notes. |
| All existing/new tests use existing Testcontainers infrastructure | PASS | Final full solution run 122/122; factory enforces actual container target. |
| Run tests while development database stopped/unavailable | PASS | Approved 14 October 2026 run: real local development connection rejected with admission disabled and zero sessions; freshly built Testcontainers suite 122 passed, 0 failed/skipped; original admission restored and final SELECT 1 succeeded. See verification report for unique artifacts. Windows PostgreSQL service remained running. |
| No silently skipped failures/weakened tests | PASS | Final zero skipped; baseline/audit failures disclosed and corrected; original assertions unchanged. |
| README covers all fourteen requested topics | PASS | Current Assignment 6.3 section, exact error table, real outputs, final results and explicit gaps. |
| No secrets/build artifacts accidentally committed | PASS | Final publication audit checks actual configured credentials in memory and outgoing snapshots; tracked-artifact review. No raw logs or node_modules/bin/obj committed. |
| Final Git status | PENDING REVIEW | Earlier implementation is committed. Updated verification script, README, report and matrix remain uncommitted and unpushed at the user's request. |
| Push only after verification and user approval | PENDING REVIEW | Earlier work exists on remote main at 64a6e7a (confirmed with git ls-remote). This read confirms remote state, not historical approval. No commit or push performed during this verification; current changes await review. |

The development-offline requirement is now verified. A real next-payout schedule remains undefined, and stable probe/documentation contracts remain explicit partial interpretations. Current verification changes await review before commit or push.
