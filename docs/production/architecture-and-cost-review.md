# RondiTrack production architecture and cost review (Assignment 9.3, before Parts 4 and 5)

Prepared 10 Nov 2026 for the author's approval. **Nothing has been provisioned or configured.**

Labels used throughout:
- **Verified**: checked against an official source (linked), or against this repository's code or tests (named).
- **Estimate**: arithmetic on published or third-party rates; to be confirmed in the Azure pricing calculator.
- **Unresolved**: needs a check inside the author's own accounts. Each one names the no-cost check that settles it.

## 1. Recommended architecture

| Part | Recommendation | Status |
|---|---|---|
| Next.js (production) | Vercel Hobby, functions in **Cape Town (`cpt1`)** | Hobby allows **one** function region of the project's choice, set by `regions` in `vercel.json` (**Verified**: [Vercel: configuring regions](https://vercel.com/docs/functions/configuring-functions/region)). That `cpt1` appears in the Hobby region list is **Unresolved**; check the project's Settings → Functions → Region dropdown. Hobby is for non-commercial use only; a student project fits, as long as nobody is paid to build or host it. |
| API | Azure Container Apps, **South Africa North**, consumption plan, 0.25 vCPU / 0.5 GiB, **min = max = 1 replica** | That standard Container Apps is offered in South Africa North is **Unresolved**: Microsoft's pages confirm the region for Container Apps *Express* and *dynamic sessions*, not standard apps ([Express regions](https://learn.microsoft.com/en-us/azure/container-apps/express-overview)). Check the portal's Create Container App → Region list, and the subscription's "Allowed locations" policy. |
| Database | Azure Database for PostgreSQL Flexible Server, **Burstable B1ms**, South Africa North, same region as the API | B1ms defaults to **50 connections, 35 for users** (**Verified**: [limits](https://learn.microsoft.com/en-us/azure/postgresql/configure-maintain/concepts-limits)). Burstable tiers have **no built-in PgBouncer** (same page). Free-offer eligibility is **Unresolved**; check the subscription's free-services blade. |
| Proofs of payment | Stay in PostgreSQL (`bytea`, 5 MB cap) | **Verified** in code (`ContributionProof.Contents`). They survive any API redeploy because they are not on the container's disk. |
| Images | GitHub Container Registry (public; the repository is public) | **Estimate**: $0. It avoids Azure Container Registry's monthly fee. The image holds the published API, which is already public source. |
| Traces | Grafana Cloud (free tier) over OTLP, from both halves; Sentry is the alternative (§9) | Free-tier figures come from third-party sources (§9): **Unresolved** until the account is opened. |
| Errors | Sentry Developer (free), EU data region | Free-tier limits come from secondary sources: **Unresolved** until the account is opened. |

Why this shape:
- The Next.js server calls the API on every page. Cape Town to Johannesburg is one domestic hop (**Estimate**: about 20–25 ms; to be measured after deploying).
- The API and the database share a region, so every query stays local.
- No region outside South Africa sits in the request path.

## 2. Cost (per month)

| Item | Basis | Cost |
|---|---|---|
| Container Apps, production API, always on | 0.25 vCPU × 2,628,000 s = 657,000 vCPU-s; 0.5 GiB → 1,314,000 GiB-s. Minus the per-**subscription** free grant (180,000 vCPU-s, 360,000 GiB-s, 2 M requests: Microsoft's product page). Rates (third-party; Microsoft's own table not yet read): active $0.000024 per vCPU-s, idle $0.000003 (some sources say $0.000008), memory $0.000003 per GiB-s. | **Estimate US$4–15**: about $4 if mostly idle, about $15 if billed as active all month. The cycle-close worker wakes every 60 s, so "mostly idle" is plausible but unverified. |
| Migration job (Container Apps job) | Seconds per release | **Estimate** under $0.10 |
| PostgreSQL B1ms + 32 GiB storage | Free for 12 months: 750 h/month B1ms, 32 GB storage and 32 GB backup on the free and Students offers (**Verified** that the offer exists: [Microsoft Learn](https://learn.microsoft.com/en-ie/azure/postgresql/flexible-server/how-to-deploy-on-azure-free-account), [Azure for Students](https://azure.microsoft.com/en-us/free/students)). Otherwise about $16/month for compute in South Africa North (third-party: [Bytebase](https://www.bytebase.com/dbcost/azure-flexible/instance/B1ms/), [Spare Cores](https://sparecores.com/database/azure/Standard_B1ms)), plus storage. | **$0 if eligible**; **Estimate** about $16–20 if not. **Unresolved**: eligibility. |
| Log Analytics (Container Apps logs) | Per GB ingested | **Unresolved** rate. Keep it small: Information-level logs only, short retention, or send logs to the OTLP backend instead. |
| Vercel Hobby, Sentry Developer, Grafana Cloud free | Free tiers | $0, within their limits |
| Demo environment (§10) | Two apps at one replica each for about 20 h a month; a separate database on the same server | **Estimate** about US$1 |
| **Total** | | **Estimate US$5–16/month** while the database is free; about US$21–36/month otherwise |

**Guardrails before creating anything:**
- An Azure budget alert at, say, US$10.
- A Vercel spend cap: none is needed, because Hobby cannot incur charges.
- Delete or scale everything to zero after Demo Day; a forgotten database bills every month.

## 3. Database sizing, connection pools and replicas

- **Verified:** B1ms allows **35 user connections**.
- **Verified locally (Part 3):** two API instances with Npgsql's **default pool of 100 each** exhausted a PostgreSQL that allows 100 (`53300: too many clients`). 88 connections were in use after the e2e run.
- **Plan:**
  - API `Maximum Pool Size=10` per replica. The connection string's pool settings are not secret, so they can be documented in full.
  - **Max 2 API replicas** during a rolling revision (2 × 10 = 20), plus the migration job (5), plus room for psql and monitoring: 25–30 of the 35.
  - Steady state is **1 replica**.
  - Raising `max_connections` on B1ms is possible but discouraged (memory), and there is no built-in PgBouncer on Burstable (**Verified**, same limits page).
- The Next.js server never connects to the database.

## 4. Rate limiting and client identification

**Facts (Verified):**
- **Container Apps ingress** adds `X-Forwarded-For`; only the **rightmost** value comes from the platform, and any others must be treated as untrusted ([Container Apps ingress](https://learn.microsoft.com/en-us/azure/container-apps/ingress-overview)).
- **ASP.NET Core** applies forwarded headers only from known proxies; the platform's guidance clears those lists and relies on the ingress ([.NET on Container Apps](https://learn.microsoft.com/en-us/azure/container-apps/dotnet-overview), [proxy configuration](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/proxy-load-balancer?view=aspnetcore-10.0)).
- **Members' sign-ins don't come from members.** They reach the API from the **Next.js server**, so the address the API sees is a Vercel egress address, shared by all members and, on Vercel, by other customers too.
- **In this repository:** the `Login` limit is keyed by `RemoteIpAddress` and there is no `UseForwardedHeaders` (`API/Common/RateLimitPolicies.cs`). That is risk register R2: one member's typos lock everyone out.
- **ASP.NET Core's rate limiter is in-memory per process**, so with N replicas each one counts separately.

**Recommended (simplest secure option):**
1. **Key the sign-in limit by the account** (an HMAC of the normalised email, never the email itself). Five wrong passwords on one account don't affect another. The trade-off: someone can lock a specific account for the 60-second window. That is acceptable at this scale, and it goes in the README's decisions.
2. **Keep a coarse per-caller backstop** (for example 60 a minute) keyed by the rightmost `X-Forwarded-For`, which is the Vercel egress address. It slows a flood without singling out members.
3. **Trust forwarded headers for exactly one hop** (`ForwardLimit = 1`), with the known lists cleared as the platform guidance says. Only the API's ingress can reach the container (no other route in), so the one hop is the platform's.
4. **One API replica**, so in-memory limits are exact. If the API ever scales out, the limits loosen in proportion; a shared store would then be needed. That is a known gap, not a 9.3 requirement.
5. **Rejected:** forwarding each member's own address from the Next.js server in a signed header. It works, but adds a shared secret and a code path to protect, for no gain over per-account keys.

**To prove on the deployed app (Part 5):** five wrong passwords on account A, then a correct sign-in on account B succeeds. A request with a spoofed `X-Forwarded-For` gets no extra allowance.

## 5. HTTP 404 for missing or unauthorised resources

- **Verified:** once streaming starts, Next.js has sent 200 and cannot change it to 404. It adds `noindex` instead. The fix is to decide before any `await` inside a Suspense boundary ([Next.js streaming guide](https://nextjs.org/docs/app/guides/streaming), [discussion #89254](https://github.com/vercel/next.js/discussions/89254)).
- **Verified in this repository (Part 3):** a not-found stokvel returns 200, identically for "not yours" and "doesn't exist". So the security requirement (indistinguishable) holds; only the status code is wrong.
- **Options:**
  - **(a) Check in `proxy.ts`** for `/stokvels/[id]`: one extra API call per page view. It answers 404 before rendering.
  - **(b) Restructure the route** so the access check is awaited before any Suspense boundary. That needs care with `cacheComponents` and the `(app)/loading.tsx` boundary. **Unresolved**: whether Next 16 allows it without making the whole segment blocking.
  - **(c) Keep the current behaviour** and document it.
- **Recommendation:** (c) for 9.3, because the rubric's requirement ("the same page as one that doesn't exist") is met. Try (b) as a spike after Demo Day. Proof links already return a real 404 (redirect to the not-found page).

## 6. Authentication and session security (current, Verified in code)

**Browser ↔ Next.js:**
- One cookie, `session`: **encrypted** with `SESSION_SECRET` (`dir` + A256GCM), `HttpOnly`, `Secure`, `SameSite=Lax`, host-only (`web-next/src/lib/session.ts`).
- The browser never sees an access or refresh token.

**Next.js ↔ API:**
- A 5-minute access JWT (HS256, issuer and audience validated, 30 s skew), plus a refresh token.
- The API sets the refresh token as `__Secure-ronditrack_refresh` (`HttpOnly`, `Secure`, `SameSite=Strict`, its own path) on its own domain. The **Next.js server** reads it and keeps it inside the encrypted session.
- Refresh tokens rotate, and reuse is detected: a replayed token revokes the whole session (`AuthService`, `refresh_reuse` security event), within the 8.2 grace window for parallel requests.

**Why this works across two domains:** the browser only ever talks to the Next.js domain, so no cross-site cookie is needed. A browser calling the API directly would need a `SameSite=None` refresh cookie on the API's domain, which third-party-cookie blocking breaks.

**Hardening for production:**
- Production's CORS stays empty (already enforced at startup).
- The API's Scalar and OpenAPI document stay off (they are Development only).

**Unresolved:** whether Next.js Server Actions' built-in origin check is enough against cross-site posts on Vercel. It is the framework default; it should be checked once on the deployed URL with a cross-origin form.

## 7. Secrets: where they live, and rotation

| Secret | Production | Preview / demo | Rotation and its effect |
|---|---|---|---|
| `SESSION_SECRET` (Next.js) | Vercel, Production scope | Different value per environment | Rotating signs everyone out (old cookies no longer decrypt). Do it when it may have leaked; announce it first. |
| `Jwt__SigningKey` (API) | Container Apps secret (or a Key Vault reference) | Different | Access tokens signed with the old key fail. The Next.js server then refreshes (refresh tokens are database rows, not JWTs), so members see at most one silent refresh. **Verified** from the code's design; to be tested once. |
| `ConnectionStrings__RondiTrackDb` | Container Apps secret | Separate database and role | Rotate the database role's password in Azure, update the secret, restart the revision. |
| `RONDITRACK_DEMO_FAULT_SECRET` | **Never set** | Demo only | Free to rotate (it only signs five-minute arm cookies). |
| Sentry DSN, OTLP header token | Platform variables (the browser DSN is public by design) | Different projects or keys | Rotate in Sentry or Grafana, then update. |
| GitHub → Azure deploy | **OIDC federated credential**: no stored Azure secret | — | No secret to rotate. |

Rules (from the PDF, and kept):
- Nothing secret in the repository or in a `NEXT_PUBLIC_` variable.
- Production and preview never share a secret.
- A leaked secret is rotated, not just deleted.
- Demo accounts get long random passwords from the platform's secrets.

## 8. Migrations, backups, restore; rollback and health

**Migrations:**
- An EF Core **migration bundle** runs as a one-off Container Apps **job**, before the new API revision takes traffic. The API never migrates on start-up, so two instances can't both run it.
- Migrations are **additive (expand, then contract)**, so the previous revision still works against the new schema, which is what makes an API rollback safe.

**Backups (Verified: [backup and restore](https://learn.microsoft.com/en-us/azure/postgresql/backup-restore/concepts-backup-restore)):**
- Automatic, with **7 days' retention by default** (7–35 days configurable).
- Point-in-time restore within that window, **always into a new server** in the same region; it never overwrites the existing one.

**Restore test (the PDF's stretch item, recommended):**
1. Restore to a new server.
2. Point a preview at it.
3. Time it.
4. Delete it.

**Unresolved:** whether backup storage beyond the free 32 GB is billed. That doesn't matter at this data size.

**Rollback:**

| Half | How | Status |
|---|---|---|
| API | Container Apps **revisions**: traffic back to the previous revision (multiple-revision mode) | Standard feature; timing to be measured once (Part 5) |
| Frontend | Vercel **Instant Rollback** to the previous production deployment | **Unresolved**: Hobby's rollback depth; check the project's Deployments page |
| Database | Never `down` against shared data; expand/contract makes rolling back the API alone safe; a real data mistake → point-in-time restore into a new server, then repoint | Documented only until tried |

**Health:**
- Readiness `/health/ready` (database check, bounded timeout) gates traffic.
- Liveness `/health/live`.
- Both exist (**Verified**: `HealthProbeTests`).
- The Container Apps probe paths and thresholds are to be set at provisioning.

## 9. Production tracing and error monitoring

- **Traces: Grafana Cloud Tempo** (OTLP from both halves; the code already exports when `OTEL_EXPORTER_OTLP_ENDPOINT` and headers are set). Free tier: about 50 GB of traces, 14-day retention (third-party sources: **Unresolved** until the account is open).
- **Alternative: Sentry for traces too**, which means one account fewer. **Unresolved:** whether Sentry's .NET SDK shares the OpenTelemetry trace id with the Next.js side; its docs don't say for .NET. A one-hour spike would settle it.
- **Errors: Sentry** for both halves (Part 4): releases, browser source maps, trace id on events, three scrubbing layers.
- **Data region:** pick the EU region for both at sign-up. There is no African region (**Unresolved**; check at sign-up).
- **Already done:** spans carry no personal data and no connection details (Part 2). The demo fault mechanism logs only the trace id.

## 10. Demo environment (synthetic data only)

**Constraint (Verified, by design):** the simulated post-commit response failure is "once" **per server process**. Vercel may serve requests from several instances, so the demo frontend **cannot be on Vercel**.

**Recommended:**
- **Apps:** `demo-web` (the same Next.js app as a container, `next start`, one replica) and `demo-api` (the same image, one replica), in the same Container Apps environment.
- **Scaling:** both run only during rehearsals and Demo Day, and are scaled to zero otherwise.
- **Database:** a **separate database and login role** on the production PostgreSQL server, synthetic seed only. The role has no rights on the production database.
- **Settings:** `RONDITRACK_ENVIRONMENT=demo`, demo-only secrets, and the demo-fault settings for the listed synthetic accounts.

**Isolation:**
- Separate URL, secrets, database and role.
- Production never gets the demo-fault variables. If it did, the Next.js server would refuse to start.

**Cost:** **Estimate** about US$1 a month (§2).

**New work:** a Next.js Dockerfile (standalone output).

**Alternative:** a fully separate stack with its own B1ms, at about US$25–40 a month. More isolation, but not needed for synthetic data.

## 11. Does the plan meet the Assignment 9.3 rubric?

| PDF item | Plan | Status |
|---|---|---|
| Both halves on real HTTPS URLs; managed Postgres | §1 | Planned |
| Signed in across two domains; README explains why | §6; Part 5 test of staying signed in past the token's lifetime (already in e2e locally) | Planned, partly evidenced locally |
| CORS closed; a foreign Origin gets no `Access-Control-Allow-Origin` | Empty origins (startup-enforced) | Planned; prove on the deployed app |
| Forwarded headers from the platform proxy only, one hop; one member's typos don't lock out another | §4 | **Not implemented yet** (R2) |
| Cycle close at SAST, once with two instances or a restart | Johannesburg-date eligibility, advisory lock and unique constraint (6.4); trace and log (Part 2) | Implemented; prove on the deployed app with the schedule setting and a log |
| Secrets on the platforms; production ≠ preview; long random demo passwords; no real data | §7, §10 | Planned |
| Proofs survive a redeploy; downloads via `/bff/` with `no-store` and `nosniff` | §1; Part 3 test of the owner's download | Implemented; prove upload → redeploy → download |
| Readiness-gated traffic; each half rolled back once, timed | §8 | Planned; must be tried |
| Link preview shows no names or amounts | Anonymous status only (Part 3 audit, R1) | Implemented; check with a real preview tool on the deployed URL |
| README production section, runbooks, decisions, silent-teammate test | — | Part 6, not started |
| Tracing from the browser's request to the database on the deployed app; failure reference → one trace | Part 2 (local) | Re-capture on the deployed app |
| Sentry on both halves with three scrubbing layers and a real event's JSON | Part 4 | Not started |
| 404 "same page" | §5 | Met (status 200 is a documented gap) |
| 409/422 | — | 409 only; the decision not to split is recorded |

## 12. Decisions needed from the author

1. Approve the architecture in §1 and the demo environment in §10, or name changes.
2. Approve the rate-limit design in §4 (per-account sign-in limit plus a coarse backstop), so Part 5 can implement it.
3. Choose the trace backend: Grafana Cloud, or a Sentry-only spike.
4. Run the four no-cost account checks:
   - Container Apps in the South Africa North region list;
   - the subscription's allowed locations;
   - PostgreSQL free-offer eligibility;
   - `cpt1` in the Vercel project's region list.
5. Set the budget alert before anything is created.
