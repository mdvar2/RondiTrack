# RondiTrack on Next.js (Assignment 8.1)

RondiTrack's member pages, rebuilt on **Next.js 16.4.1** with the App Router. Every page is a Server Component that fetches from the ASP.NET Core API **on the server**. The browser only ever talks to this Next.js app: it holds one encrypted, `HttpOnly` cookie called `session` and never sees an access or refresh token.

The Vite app in [`../web`](../web/README.md) is unchanged. For the project as a whole, both frontends and the API, see the [root README](../README.md).

## Running it

You need Node 24 and the RondiTrack API running locally. In development it uses the `http` launch profile on **port 5101**.

```powershell
# 1. The API (from the repository root)
dotnet run --project API --launch-profile http

# 2. This app
cd web-next
npm ci
Copy-Item .env.example .env.local
# Put a fresh secret in .env.local:
node -e "console.log(require('crypto').randomBytes(32).toString('base64'))"
npm run dev        # http://localhost:3000
```

| Variable | What it is |
|---|---|
| `API_BASE_URL` | The API's address, e.g. `http://localhost:5101`. Only the Next.js server uses it. |
| `SESSION_SECRET` | 32 random bytes, base64-encoded. It encrypts the `session` cookie. |

**Why neither starts with `NEXT_PUBLIC_`:** Next.js copies every `NEXT_PUBLIC_` variable into the JavaScript it sends to the browser. `SESSION_SECRET` would then let anyone decrypt every session cookie and read the tokens inside. `API_BASE_URL` would tell the browser where the API is, but the point of this design is that the browser never calls the API. Both are read only by server code (`src/lib/session.ts` and `src/lib/api.ts`).

`.env.local` is git-ignored. Only `.env.example` is committed.

| Command | What it does |
|---|---|
| `npm run dev` | Development server on port 3000 |
| `npm run build` / `npm start` | Production build and server |
| `npm run lint` | ESLint (`eslint-config-next`) |
| `npm test` | Vitest unit tests (`src/**/*.test.ts`) |
| `npm run generate:api` | Regenerates `src/lib/schema.d.ts` from `http://localhost:5101/openapi/v1.json` |

Every version in `package.json` is pinned exactly, with no `^` or `~`. `npm audit` reports 0 vulnerabilities. Vitest is 4.1.12 because 4.1.11 (the version `web/` uses) has two moderate advisories in `@vitest/mocker`.

**Port 3000 is shared** with the React/Vite app in `../web`. Run one frontend at a time.

### Dependencies

From `package.json`. The app was created with `create-next-app@16.4.1` (TypeScript, Tailwind, ESLint, App Router, `src/`, React Compiler, no AGENTS.md). `next.config.ts` sets `reactCompiler: true`, `cacheComponents: false` and `agentRules: false`.

| Package | Version | Used for |
|---|---|---|
| `next` | 16.4.1 | App Router, Server Components, Server Actions |
| `react`, `react-dom` | 19.3.0 | |
| `jose` | 6.2.13 | Encrypting the session cookie (`dir` + `A256GCM`) |
| `server-only` | 0.0.1 | Turns importing session or API code into a Client Component into a build error |
| `openapi-fetch` | 0.17.0 | Typed API client, generated from the API's OpenAPI document |
| `class-variance-authority`, `clsx`, `tailwind-merge` | 0.7.1, 2.1.1, 3.7.0 | Badge and button variants, copied from `web/` |
| `lucide-react` | 1.60.0 | Status-badge icons |
| `tailwindcss`, `@tailwindcss/turbopack` | 4.3.3 | Styling |
| `typescript` | 5.9.3 | |
| `eslint`, `eslint-config-next` | 9.39.5, 16.4.1 | `npm run lint` |
| `babel-plugin-react-compiler` | 1.0.0 | React Compiler |
| `openapi-typescript` | 7.13.0 | `npm run generate:api` |
| `vitest` | 4.1.12 | `npm test` |
| `@types/node`, `@types/react`, `@types/react-dom` | 20.19.43, 19.3.0, 19.3.0 | |

### Testing

| Command | Result on 27 Oct 2026 |
|---|---|
| `npm run lint` | No problems |
| `npm test` | **58 passed** in 7 files |
| `npm run build` | Succeeded; 7 routes |
| `npm audit` | 0 vulnerabilities |
| `dotnet test RondiTrack.Tests` (repository root, Docker running) | **145 passed**, including the 8 tests added for the API fixes below |

What the unit tests cover:

| File | Checks |
|---|---|
| `src/lib/treasurer.test.ts` | The treasurer filter: a mixed list, Treasurer nowhere, an empty list, exact role spelling |
| `src/lib/session.test.ts` | Encrypt/decrypt round trip; tampered, wrong-secret, wrong-shape and expired cookies all count as no session; no role is stored |
| `src/lib/api.test.ts` | `readOrFail`: data, 401 → `/login?expired=1`, 404 → `notFound()`, other errors → `ApiError` with the reference |
| `src/lib/errors.test.ts` | The 7.3 messages and the error digest |
| `src/lib/refresh-cookie.test.ts` | Reading the refresh token from the login response's `Set-Cookie` headers |
| `src/lib/money.test.ts`, `dates.test.ts` | Copied unchanged from `web/` |

On 27 Oct 2026 I also checked the signed-in pages with a local script, which isn't part of the repository. It signed three accounts in against the real API, built real session cookies and fetched every page from the production build. **98 checks passed, 0 failed.** The checks covered roles, the treasurer filter, the not-found pages, and that no token, nothing JWT-shaped and no mention of the API's port appears in any page's HTML.

## Security architecture

```
Browser ──(cookie: session, encrypted, HttpOnly, Secure, SameSite=Lax)──▶ Next.js server ──(Authorization: Bearer)──▶ API
```

| Concern | How it's handled | Where |
|---|---|---|
| Where tokens live | Only inside the encrypted `session` cookie. Browser JavaScript can't read it, and neither token appears in the page | `src/lib/session.ts` |
| Tampering | `jose` `dir` + `A256GCM` with `SESSION_SECRET`. A cookie that fails to decrypt, has expired or has the wrong shape counts as no session | `decrypt()` in `session.ts` |
| Who calls the API | Only server code. `api()` adds the bearer token from the session. No `NEXT_PUBLIC_` variable exists, so the browser doesn't know the API's address | `src/lib/api.ts` |
| Server code leaking to the browser | `import 'server-only'` in `session.ts` and `api.ts` makes that a build error (see [Server-only proof](#server-only-proof)) | |
| Client Component props | Only safe values cross the boundary: the login form's message, reference and email | `src/components/LoginForm.tsx` |
| Error details | In production only a digest reaches the browser. `ApiError` puts just the status and `correlationId` in it, never a stack trace | `src/lib/errors.ts`, `(app)/error.tsx` |
| Roles | Never stored in the session or read from the token. Each page uses the role the API returns for each stokvel, and the API still authorizes every request | `src/lib/treasurer.ts` |
| Hiding that a stokvel exists | A non-member, an unknown id and a malformed id all get the same 404 page, which shows nothing from the URL | `stokvels/[id]/not-found.tsx` |
| Signing out | Revokes the API's refresh session (the refresh token is sent as a `Cookie` header), then deletes the local session | `src/lib/auth-actions.ts` |

## API check

These were checked against the running API on port 5101 on 27 October 2026, using a Node script that calls the API the way this server does. Passwords and tokens were typed in or read from a file, and never printed.

| # | Question | Answer |
|---|---|---|
| 1 | Does the list give the caller's role in each stokvel? What about a user in no stokvels? | **`GET /api/v1/stokvels` is for platform admins only.** A normal user gets **403**, so it can't feed these pages. **`GET /api/v1/me/home`** returns each of the caller's stokvels with `role` (`Member`, `Treasurer` or `Chairperson`), plus the current cycle, `amountPaid`, `amountOwed`, `nextPayoutDate` and `arrearsCents`. A user in no stokvels gets **200 with `{"stokvels": []}`** (covered by `MemberHomeTests.NoMemberships_ReturnsEmptyList`). All three list pages use `/me/home`. |
| 2 | `GET /stokvels/{id}` as a non-member, for an unknown id and for `not-a-guid`: is the status the same? | **Yes, all three are 404.** A non-member and an unknown id get identical bodies (`"detail": "Stokvel not found."`). `not-a-guid` doesn't match the `{id:guid}` route, so it's a 404 with `"detail": "The requested endpoint was not found."`. **Decision:** the page ignores the body and treats every 404 the same way, `notFound()`, so all three show the same `not-found.tsx`. Signed out, all three are 401, because authentication runs before routing. |
| 3 | Which header carries the refresh token after login? | `Set-Cookie: __Secure-ronditrack_refresh=…; expires=<+7 days>; path=/api/v1/auth; secure; samesite=strict; httponly`. Node's `response.headers.getSetCookie()` returns it. A browser's `fetch` can't. The body is only `{ accessToken, expiresAtUtc }`. The server keeps the refresh token in the encrypted session and sends it back as a `Cookie` header on logout. |
| 4 | How long does the access token live? What happens after it expires? | **300 seconds (5 minutes)**, with 30 seconds of allowed clock skew (`Jwt:AccessTokenMinutes`, `Jwt:ClockSkewSeconds`). An expired token gets **401** with `WWW-Authenticate: Bearer` and a ProblemDetails body that has a `correlationId`. A missing or malformed token gets the same 401. Until Day 3 adds refresh, **about 5 minutes after signing in** the next page load sends the person to `/login?expired=1`. |
| 5 | Which IP does the login rate limit see? | The limit is 5 attempts per 60 seconds **per remote IP**. The 6th attempt got `429` with `Retry-After: 60`. Every sign-in now comes from the Next.js server, so the API sees one IP for everyone. See [Known gaps](#known-gaps). |
| 6 | Does `GET /stokvels/{id}` return the member count and a summary of the current cycle? | **No, so the API was fixed.** `memberCount` was **always 0**: it counted `Stokvel.Members`, a navigation that EF Core ignores (`RondiTrackDbContext`). Demo Savings returned 0 with 2 members, and Emergency Fund returned 0 with 1. There was also no cycle summary and no caller role. |

### API fixes in this PR
- **`GET /api/v1/stokvels/{id}`** now returns `StokvelDetailResponse` (`API/Services/StokvelDetailService.cs`). It keeps the original fields (`id, name, contributionAmount, memberCount, version`) and adds `contributionAmountCents`, `role` (the **caller's** role in this stokvel) and `currentCycle { period, status, dueDate, membersPaid, collectedCents, targetCents }`. The current cycle follows the same rule as `/me/home`: this Johannesburg month, preferring an Open cycle. `memberCount` is now counted from `StokvelMembers`. Authorization is unchanged: the `StokvelView` policy, with 404 for non-members.
- **`GET /api/v1/me`** (new) returns `{ id, name, email, version }` for the caller. Sign-in uses it to store who the person is. It has no role.
- **`POST /api/v1/auth/login`** OpenAPI metadata: it declared only its 429, which stopped ASP.NET from inferring the 200, so the generated type for a successful login body was `undefined`. It now declares `200 AccessTokenResponse`, `401` and `429`. Login's behaviour hasn't changed.
- Tests: `RondiTrack.Tests/StokvelDetailTests.cs` (7) and `LoginContractTests.cs` (1). The backend suite passes: **145 passed, 0 failed**.

## Routes

```
src/app
├── page.tsx                      /   → /dashboard/member if signed in, else /login
├── (auth)/login/page.tsx         /login   (Server Component) + LoginForm (client)
└── (app)/                        signed in
    ├── layout.tsx                sidebar: 4 links, who's signed in (<Suspense>), Sign out
    ├── loading.tsx               skeleton while a page awaits the API
    ├── error.tsx                 7.3 message + reference + Try again (retry)
    ├── dashboard/member/page.tsx
    ├── dashboard/treasurer/page.tsx
    ├── stokvels/page.tsx
    └── stokvels/[id]/page.tsx + not-found.tsx
```

| Route | API call (server only) | Shows | When there's nothing, or it isn't theirs |
|---|---|---|---|
| `/` | none; reads the session cookie | Redirect | — |
| `/login` | `POST /auth/login`, then `GET /me` (Server Action) | Email and password form. A wrong password shows "Email or password is incorrect." with the `correlationId`. `?expired=1` shows "Your session has expired." | — |
| `/dashboard/member` | `GET /me/home` | Totals paid and owed this cycle, then each stokvel with cycle, paid, still owed and next payout ("No payout scheduled" while the API returns `null`) | Empty state: "You're not in any stokvels yet" |
| `/dashboard/treasurer` | `GET /me/home`, filtered | Only the stokvels where `role === 'Treasurer'`, each linking to `/stokvels/{id}` | Empty state linking to the member dashboard. Never a 403 |
| `/stokvels` | `GET /me/home` | 7.1 `StokvelCard`s with "You're the {role}", each linking to its detail page | Empty state |
| `/stokvels/[id]` | `GET /stokvels/{id}` | Name, contribution, the person's role in this stokvel, member count, and the current cycle (status, due date, members paid of total, collected of target) | `not-found.tsx`: "We couldn't find that stokvel" |

**How data flows.** Each page calls `api()` from `src/lib/api.ts`. It reads the session (or redirects to `/login`) and returns a typed `openapi-fetch` client that sends `Authorization: Bearer …`. `readOrFail(result)` then turns the result into data or the right response:

| API status | What happens |
|---|---|
| 2xx | The page gets typed data |
| 401 | `redirect('/login?expired=1')` |
| 404 | `notFound()`, so the nearest `not-found.tsx` |
| anything else | throws `ApiError`, shown by `(app)/error.tsx` |

**Errors keep their reference in production.** In a production build, Next.js replaces the message of an error thrown in a Server Component before it reaches the browser, and keeps only `error.digest`. If an error already has a digest, Next.js keeps it. So `ApiError` sets `digest = "RONDITRACK_API;<status>;<correlationId>"`. Those two values are safe to show, and `error.tsx` reads them back to show the 7.3 message and its reference. A 403 shows "You don't have permission to do that."; a 500 shows "Something went wrong. Please try again." Both come with the reference and a **Try again** button that calls `retry()`. Neither shows a stack trace.

**Status codes while streaming.** Because `(app)/loading.tsx` exists, a page starts streaming before its fetch finishes. So a `notFound()` or `redirect()` arrives inside an HTTP **200** response: a `<meta name="robots" content="noindex">` tag and the not-found UI, or a `NEXT_REDIRECT` instruction. That's expected in Next.js 16.

**Signed out.** `/` answers with a real `307` to `/login`. The (app) routes stream a redirect to `/login`. A cookie that fails to decrypt counts as no session.

## The treasurer dashboard

RondiTrack has no "treasurer users". A person is **Treasurer of some stokvels** and Member or Chairperson of others, all with one login. So `/dashboard/treasurer` isn't a door with a role check on it. **Everyone may open it.** It shows the stokvels where *this* person is Treasurer, and for most people that list is empty.

- **The filter is a pure function:** `stokvelsWhereTreasurer(list)` in [`src/lib/treasurer.ts`](src/lib/treasurer.ts), with Vitest tests in [`treasurer.test.ts`](src/lib/treasurer.test.ts) for a mixed list, a list where the person is Treasurer nowhere, an empty list, and exact role spelling.
- **The role comes from the API's answer for each stokvel** (`/me/home` → `stokvels[].role`), never from the session or the token.
- **The session doesn't store a role at all, and can't.** A single role per person doesn't exist: Lerato is Treasurer of her family stokvel and Member of her work stokvel at the same moment. A role cached in the cookie would also go stale as soon as a Chairperson changes it, and it would stay wrong for up to the cookie's 7-day life. The API's access token has no role claim either (`sub, jti, iat, nbf, exp, iss, aud`). The API checks membership and role per stokvel on every request, which is why the page is safe whatever the browser sends. A unit test (`session.test.ts`) checks that the session holds only `accessToken`, `refreshToken` and `user`.
- **The empty state is useful, not an error.** It says "You don't run any stokvels yet" and links to the member dashboard. It isn't a 403, a redirect or an alert.
- **The sidebar shows all four links to everyone** (Home, Member dashboard, Treasurer dashboard, My stokvels). It doesn't hide "Treasurer". If a later version hid it for people who are Treasurer nowhere, that would only be a **convenience**: the page would still be safe, because the API decides what each person may see.

## The client boundary

Only these files have `'use client'`. Everything else, including `StokvelCard`, `StatusBadge`, the layout and all the pages, runs on the server and sends no JavaScript of its own.

| File | Why it needs `'use client'` |
|---|---|
| `src/components/LoginForm.tsx` | `useActionState` keeps the sign-in Server Action's result (message and reference) and the pending state between submissions |
| `src/components/DueCountdown.tsx` | `useState` + `useEffect` set a timer to re-render the countdown at Johannesburg midnight |
| `src/app/(app)/error.tsx` | Error boundaries must be Client Components. Its **Try again** and **Copy** buttons use `onClick` |

What else changed from `web/`:
- **Badge and Button.** The shadcn `Badge` in `web/` renders through Base UI's `useRender` hook, which can't run in a Server Component. The web-next copies keep the same variant classes on a plain `<span>` and `<button>`. That lets `StatusBadge`, `StokvelCard` and the Sign out form stay on the server.
- **Sign out** is a `<form action={signOut}>` in the Server Component layout, so it needs no client code.

**Only names cross the boundary.** No Client Component receives the session. `LoginForm` gets no props, and its action state holds only `{ message, correlationId?, retryAfter?, email }`. The layout renders `session.user.name` and `.email` as plain text on the server. Neither `session.ts` nor `api.ts` is imported by any file with `'use client'`. `auth-actions.ts` is a `'use server'` module, so a Client Component that imports it gets only a reference to the action, never its code.

## Server-only proof

`src/lib/session.ts` and `src/lib/api.ts` both start with `import 'server-only'`. To prove the guard works, `getSession` was imported into the `DueCountdown` Client Component:

```tsx
// src/components/DueCountdown.tsx (temporary)
'use client'
import { useEffect, useState } from 'react'
import { getSession } from '@/lib/session'
void getSession
```

`npm run build` **failed**:

```
> Build error occurred
Error: Turbopack build failed with 3 errors:
...
./src/lib/session.ts:1:1
Error: 'server-only' cannot be imported from a Client Component module
> 1 | import 'server-only'
    | ^^^^^^^^^^^^^^^^^^^^
  2 | import { cookies } from 'next/headers'
  3 | import { EncryptJWT, jwtDecrypt } from 'jose'
  4 |

It should only be used from a Server Component.

Import traces:
  Server Component:
    ./src/lib/session.ts
    ./src/app/(app)/layout.tsx

  Client Component Browser:
    ./src/lib/session.ts [Client Component Browser]
    ./src/components/DueCountdown.tsx [Client Component Browser]
    ./src/components/DueCountdown.tsx [Server Component]
    ./src/app/(app)/stokvels/[id]/page.tsx [Server Component]

  Client Component SSR:
    ./src/lib/session.ts [Client Component SSR]
    ./src/components/DueCountdown.tsx [Client Component SSR]
    ./src/components/DueCountdown.tsx [Server Component]
    ./src/app/(app)/stokvels/[id]/page.tsx [Server Component]
```

The other two errors point at the same import from `next/headers` and `server-only` ("This API is only available in Server Components in the App Router").

With the import removed, `npm run build` **passed**:

```
▲ Next.js 16.4.1 (Turbopack)
✓ Compiled successfully
  Finished TypeScript ...
✓ Generating static pages using 7 workers (8/8)

Route (app)
┌ ƒ /
├ ○ /_not-found
├ ƒ /dashboard/member
├ ƒ /dashboard/treasurer
├ ƒ /login
├ ƒ /stokvels
└ ƒ /stokvels/[id]
```

**Without the guard,** `session.ts` and everything it imports would have been bundled into the browser JavaScript for `DueCountdown`. That includes the code that decrypts the cookie, and with it whatever `SESSION_SECRET` handling the bundler could inline. That moves secret-handling code onto every visitor's device and puts the server's session logic in public view. It would also only fail at runtime (`cookies()` can't run in a browser) instead of failing at build time.

## Known gaps

- **One IP for every sign-in.** The API limits login to 5 attempts per 60 seconds per remote IP. Every sign-in now comes from the Next.js server, so the API sees that one address for all users. Five people signing in within the same minute, or one person mistyping five times, locks **everyone** out for a minute (`429`, "Too many requests"). The fix, which is a stretch challenge and not done here, is to forward the person's IP from the Next.js server and have the API trust that header only from the Next.js server, as a known proxy.
- **No refresh until Day 3.** The access token lives 5 minutes. After that, the next page load goes to `/login?expired=1`, even though the session cookie still holds a valid 7-day refresh token. The old cookie stays until the next sign-in replaces it (a Server Component can't delete cookies).
- **Next payout date.** `/me/home` always returns `nextPayoutDate: null` because the API has no payout-scheduling rule yet. The pages show "No payout scheduled" rather than invent a date.
- **`memberCount` elsewhere.** The admin-only `GET /api/v1/stokvels` and the `POST /api/v1/stokvels` response still use `StokvelResponse.FromEntity`, so their `memberCount` is still 0, for the same EF root cause as above. This frontend doesn't use either.
- **No route protection layer yet.** Pages redirect to `/login` when there's no session. A `proxy.ts` check comes on Day 3. The API is still the real gate.
- **Theme.** Dark mode follows the operating system. The 7.2 theme switcher wasn't ported.
