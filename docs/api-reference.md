# API and authentication reference

I implemented these routes and security rules in Assignment 6.1 and have kept the reference
current since. See the [main README](../README.md#features) for an overview. The browser
configuration, error format and home contract were introduced in
[Assignment 6.3](history/assignment-6.3.md).

The authoritative, always-current contract is the Development API's OpenAPI document at
`/openapi/v1.json` (browse it at `/scalar/v1`).

## Authentication routes

[AuthController](../API/Controllers/AuthController.cs) explicitly allows anonymous access to
these routes. Bodies use JSON except refresh/logout, which read cookies.

### POST /api/v1/auth/register

- **Input:** name, email, password (minimum 12 characters)

- **Result:** 201 UserResponse; invalid input 400; duplicate normalized email 409.

### POST /api/v1/auth/login

- **Input:** email, password

- **Result:** 200 accessToken/expiresAtUtc plus refresh cookie; unknown email/wrong password
  same safe 401 details, with request-specific correlation; invalid input 400.

### POST /api/v1/auth/refresh

- **Input:** Refresh cookie

- **Result:** 200 new access JWT and rotated cookie; missing, expired, revoked or replayed
  token 401.

### POST /api/v1/auth/logout

- **Input:** Refresh cookie if present

- **Result:** 204, family revoked and cookie deleted; repeated/cookie-free logout idempotent.

Infrastructure outages can still fail requests. Registration returns a Location under
/api/v1/users/{id}, but that resource is platform-admin restricted; ordinary registrants cannot
GET it.

Registration extends the existing User table, hashes with `PasswordHasher<User>` and never
returns the hash. Email lookup trims/lowers invariantly; PostgreSQL computes
`lower(btrim("Email"))` with a unique index, also protecting concurrent registrations/direct
inserts. Legacy users with null PasswordHash cannot sign in. No public password-reset/setup or
privileged-role assignment flow is implemented. Registration does not accept a privileged
role. Admin /api/v1/users creation does not set a password and is not a substitute for
registration.

## Access JWTs

Send `Authorization: Bearer <ACCESS_TOKEN>` on protected requests.
[TokenService](../API/Auth/TokenService.cs) signs with HS256. Validation requires signature,
allowed algorithm, issuer, audience and expiration. OnTokenValidated rejects deleted users.
Default validity is five minutes plus explicit 30-second acceptance skew; configuration caps
nominal lifetime at 15 minutes.

| Claim | Meaning and lifetime rationale |
|---|---|
| sub | Stable User.Id; current existence checked each request. |
| jti | Immutable token identifier, not a session revocation lookup. |
| iat | Fixed issuance timestamp. |
| nbf / exp | Fixed validity boundaries checked with explicit skew. |
| iss / aud | Fixed issuer and intended client identifiers. |

No email, stokvel-role or platform-role claims are issued in the JWT.
PlatformRoleClaimsTransformation adds the current platform role from PostgreSQL to the request
principal. Stokvel roles are separately queried per resource, so demotion/removal affects the
same JWT without waiting for its expiry.

## Refresh tokens, cookie flags and logout

Refresh tokens are 64 random bytes, not JWTs. Only SHA-256 hashes are stored in PostgreSQL.
Raw tokens appear exclusively in the __Secure-ronditrack_refresh cookie, never JSON. Defaults:
seven days per token, fixed 30-day absolute family limit. Successor database expiry is capped
by that limit; cookie expiry can be later, but the server rejects the token at the boundary.

| Cookie option | Choice and purpose |
|---|---|
| HttpOnly | true; prevents JavaScript reads. |
| Secure | true; HTTPS only. |
| SameSite | Strict; limits cross-site delivery. |
| Path | /api/v1/auth; limits routing. |
| Domain | Unset; host-only. |
| Expires | Set from the returned refresh expiry. |

[EfRefreshTokenRepository.RotateAsync](../Infrastructure/Repositories/EfRefreshTokenRepository.cs)
locks the family row with PostgreSQL FOR UPDATE, spends the old token and inserts the
successor atomically. Logout uses the same lock. Reusing a spent token returns 401 and commits
whole-family revocation, invalidating its successor. Concurrent refreshes allow at most one
success; replay then revokes that family. Another login family is unaffected.

Logout deletes the cookie with matching options and revokes the family. Issued access JWTs
remain usable until short expiry plus skew; refresh revocation does not itself revoke them.
Live authorization still applies. Keep access tokens in frontend memory; localStorage exposes
tokens to XSS and would lose the refresh token's HttpOnly protection. Strict cookies assume a
same-site frontend; cross-site deployment needs an explicit CSRF/CORS design.

## Authorization and endpoints

Authentication precedes authorization. Fallback requires authentication for otherwise
unannotated routes. [Roles](../Domain/Entities/Roles.cs) centralizes names;
[Policies](../API/Auth/Policies.cs) defines the rules. Exactly one plain role attribute
protects the global stokvel list. User CRUD uses PlatformAdministration; scoped operations use
resource policies.

[StokvelAccessHandler](../API/Auth/StokvelAccessHandler.cs) queries current membership for the
requested stokvel. Authenticated nonmembers receive **404**, concealing the resource; existing
members without elevated permission receive **403**. Treasurer/Chairperson can manage the
resource; Member can view and record their own contribution. PlatformAdmin has no scoped
membership bypass. New stokvels atomically give their creator Chairperson membership. Added
members default to Member; no public role-change API exists.

Success codes assume valid input and satisfied business rules. All protected routes return 401
without a valid bearer token. Global list/user CRUD return 403 to authenticated callers
lacking PlatformAdmin. Scoped routes return 404 to nonmembers, including nonmember admins.
Ordinary members receive 403 for elevated operations or recording another member's
contribution.

### GET /api/v1/users

- **Required permission:** PlatformAdmin

- **Success:** 200

### GET /api/v1/users/{id}

- **Required permission:** PlatformAdmin

- **Success:** 200

### POST /api/v1/users

- **Required permission:** PlatformAdmin

- **Success:** 201

### PUT /api/v1/users/{id}

- **Required permission:** PlatformAdmin + If-Match

- **Success:** 204

### DELETE /api/v1/users/{id}

- **Required permission:** PlatformAdmin

- **Success:** 204

### GET /api/v1/stokvels

- **Required permission:** PlatformAdmin (plain role attribute)

- **Success:** 200

### POST /api/v1/stokvels

- **Required permission:** Any authenticated user

- **Success:** 201

### GET /api/v1/stokvels/{id}

- **Required permission:** Any member of this stokvel

- **Success:** 200 `StokvelDetailResponse` with an `ETag`: `id`, `name`, `contributionAmount`
  (rands), `memberCount`, `version`, plus (since Assignment 8.1) `contributionAmountCents`,
  `role` (the caller's role in this stokvel) and `currentCycle` (`period`, `status`, `dueDate`,
  `membersPaid`, `collectedCents`, `targetCents`), or `null` when this Johannesburg month has
  no cycle. A nonmember and an unknown id both get the same 404; a malformed id also gets 404
  because the route only matches GUIDs.

### GET /api/v1/me

- **Required permission:** Any authenticated user

- **Success:** 200 `UserResponse` (`id`, `name`, `email`, `version`) for the caller. It has no
  role, because roles belong to each stokvel. Added in Assignment 8.1.

### GET /api/v1/me/home

- **Required permission:** Any authenticated user

- **Success:** 200 `{ stokvels: [...] }`: each of the caller's stokvels with `role`,
  `currentCycle`, `amountPaid`, `amountOwed`, `arrearsCents` (integer cents) and
  `nextPayoutDate` (always `null` until a payout-scheduling rule exists). A user in no
  stokvels gets `{ "stokvels": [] }`, not 404. Only the caller's own money is included.

### PUT /api/v1/stokvels/{id}

- **Required permission:** Treasurer/Chairperson + If-Match

- **Success:** 204

### DELETE /api/v1/stokvels/{id}

- **Required permission:** Treasurer/Chairperson

- **Success:** 204

### GET /api/v1/stokvels/{stokvelId}/members

- **Required permission:** Any member

- **Success:** 200

### POST /api/v1/stokvels/{stokvelId}/members/{userId}

- **Required permission:** Treasurer/Chairperson

- **Success:** 204

### DELETE /api/v1/stokvels/{stokvelId}/members/{userId}

- **Required permission:** Treasurer/Chairperson

- **Success:** 204

### GET /api/v1/stokvels/{stokvelId}/cycles

- **Required permission:** Any member

- **Success:** 200

### GET /api/v1/stokvels/{stokvelId}/cycles/{cycleId}

- **Required permission:** Any member

- **Success:** 200

### GET /api/v1/stokvels/{stokvelId}/cycles/{cycleId}/contributions

- **Required permission:** Any member

- **Success:** 200

### POST /api/v1/stokvels/{stokvelId}/cycles

- **Required permission:** Treasurer/Chairperson

- **Success:** 201

### PUT /api/v1/stokvels/{stokvelId}/cycles/{cycleId}

- **Required permission:** Treasurer/Chairperson + If-Match

- **Success:** 204

### DELETE /api/v1/stokvels/{stokvelId}/cycles/{cycleId}

- **Required permission:** Treasurer/Chairperson

- **Success:** 204

### POST /api/v1/stokvels/{stokvelId}/members/{userId}/contributions

- **Required permission:** Own userId as Member, or Treasurer/Chairperson; Idempotency-Key

- **Success:** 201

### POST /api/v1/stokvels/{stokvelId}/cycles/{cycleId}/payouts

- **Required permission:** Treasurer/Chairperson

- **Success:** 201

There is no standalone contribution CRUD/delete route, payout-list route or role-management
route. Auth and development documentation routes are listed above.

## Business contracts

- Invalid DTOs yield 400; errors use application/problem+json. Common domain
  outcomes are 404 not found, 409 business/uniqueness conflict and 412 stale
  If-Match. Every problem includes a W3C correlationId, including credential
  failures. Identical failure bodies are verified when both requests share the
  same caller trace; the safe authentication detail is identical for all callers.
- GET-by-ID users/stokvels/cycles returns ETag using PostgreSQL xmin row versions.
  PUT requires If-Match: missing/invalid is 400, stale is 412, database concurrency
  race is 409. DELETE does not require If-Match.
- Contributions take amount/contributionCycleId and require Idempotency-Key.
  Same key/request returns the original contribution with 201; same key/different
  request or duplicate member/cycle contribution is 409. Idempotency is
  process-local and lost on restart.
- Membership addition has no body. Cycles take period/targetAmount; payouts take
  amount. Payout processing selects the next eligible member, persists the payout
  and marks the cycle PaidOut in a transaction.
- GET members and GET cycle contributions return PagedResult: items,
  nextPageToken, pageSize. Default size 25, maximum 100. Sorting/filtering/cursors
  are validated and applied in EF repositories.
- Composite membership keys, restricted contribution/payout foreign keys and
  unique contribution/member/cycle and payout/stokvel/cycle indexes protect
  data. Npgsql retries are enabled; explicit transactions use EF's execution strategy.

## Public JWT configuration

[API/appsettings.json](../API/appsettings.json) contains public JWT settings:

| Setting | Current value |
|---|---|
| Jwt:Issuer | https://api.ronditrack.local |
| Jwt:Audience | ronditrack-web |
| Jwt:AccessTokenMinutes | 5 |
| Jwt:ClockSkewSeconds | 30 |
| Jwt:RefreshTokenDays | 7 |

Issuer is a validation identifier, not a site to browse. JwtOptions.Load fails startup
helpfully for missing/malformed keys or fewer than 32 decoded bytes. Configuration enforces
access lifetime 1-15 minutes, skew 0-60 seconds and refresh lifetime 1-30 days; the table
shows actual configured defaults.
