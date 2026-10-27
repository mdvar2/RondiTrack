# Assignment 8.1 — RondiTrack Routes

| | |
|---|---|
| Official title | "RondiTrack Routes" (Week 8, Assignment 8.1) |
| Date | 27 October 2026 |
| Commits | [`b833502`](https://github.com/mdvar2/RondiTrack/commit/b833502) API fixes, [`f65649f`](https://github.com/mdvar2/RondiTrack/commit/f65649f) the Next.js app |
| Branch / PR | `assignment-8.1`. The PR "Assignment 8.1: RondiTrack routes" hadn't been opened when this was written |
| Full documentation | **[web-next/README.md](../../web-next/README.md)**: running it, dependencies, testing, security architecture, API check, routes, treasurer dashboard, client boundary, server-only proof, known gaps |

This record summarises the assignment. The technical detail lives in `web-next/README.md`, because it's the current frontend.

## 1. Purpose and learning objectives
In the brief's words: "Move RondiTrack to Next.js 16. Four routes … fetch their data in Server Components, through a session that lives in an encrypted, HttpOnly cookie on your own site. The browser never calls your API again: the Next.js server does, with a token the browser can no longer see." It builds on 7.1–7.4 and on the Week 8 Day 1 demo. Day 2 (parallel fetching, streaming, Server Actions for writes) and Day 3 (registration, silent refresh, route protection) come later.

## 2. Original requirements
From the brief, `Assignment 8.1 – RondiTrack Routes.pdf`:
- **Before you start:** answer six questions against the running API and fix the API first, with tests, if an answer is wrong. The questions cover roles in the stokvel list; 404 for a non-member, an unknown id and `not-a-guid`; the refresh-token header; token lifetime and expiry; the login rate limit seen from one server; member count and cycle summary.
- **The new app:** `web-next/` created with `create-next-app@16.4.1`; versions pinned at 16.4.1 or later; a clean `npm audit`; `reactCompiler: true`, `cacheComponents: false`, `agentRules: false`; `API_BASE_URL` and `SESSION_SECRET` in a git-ignored `.env.local`, neither starting with `NEXT_PUBLIC_`; regenerated types; the 7.1 components copied over.
- **The session:** `src/lib/session.ts` uses `jose` `dir` + `A256GCM` in an `HttpOnly`, `Secure`, `SameSite=Lax` cookie at path `/`. `src/lib/api.ts` is a typed `openapi-fetch` client with one helper mapping 401 → `/login?expired=1` and 404 → `notFound()`. Sign in and sign out are Server Actions. Both modules start with `import 'server-only'`.
- **The routes:** `/`, `/dashboard/member`, `/dashboard/treasurer`, `/stokvels` and `/stokvels/[id]`, using `(auth)` and `(app)` route groups, one layout, `loading.tsx`, `error.tsx` (with `retry`) and `not-found.tsx`. `params` is awaited.
- **The twist:** the treasurer dashboard is a *filtered view* for everyone, not a door with a role check. The filter is a pure function with three Vitest tests, and the role comes from the API, never from the session or token.
- **The client boundary:** `'use client'` only where needed, with each file listed. Prove the `server-only` guard with a failing build.
- **Hand-in:** one PR with the README sections, lint, build and test output, and a recording of 2 minutes or less.

## 3. What I implemented
Everything above; the details are in [web-next/README.md](../../web-next/README.md). The API fixes came out of the "Before you start" questions:
- `GET /stokvels/{id}` always returned `memberCount: 0`. It now returns the right count, plus the caller's role and a current-cycle summary.
- `GET /me` is new.
- The login response is now declared in OpenAPI.

## 4. Technologies, tools and dependencies
Next.js 16.4.1, React 19.3, jose, server-only, openapi-fetch, Tailwind 4 and Vitest 4.1.12. See the [full table](../../web-next/README.md#dependencies).

## 5–8. Installation, configuration, running and files
See [Running it](../../web-next/README.md#running-it) (prerequisites, `.env.local`, commands, ports) and [Routes](../../web-next/README.md#routes) (the file layout).

## 9. How it works
See [Security architecture](../../web-next/README.md#security-architecture) and [The treasurer dashboard](../../web-next/README.md#the-treasurer-dashboard).

## 10. Testing and recorded results
See [Testing](../../web-next/README.md#testing):
- `web-next`: lint clean, 58 tests passed, build succeeded, 0 vulnerabilities
- backend: 145 tests passed
- signed-in check against the real API: 98 checks passed

## 11. Challenges and solutions
- **Three API contract faults** were found and fixed in the API, with tests: the `memberCount` bug, the missing `GET /me`, and the undeclared login response.
- **Error details in production.** Next.js hides a server error's message in production, so the error's reference is carried in its digest.
- **React's text markers.** A verification check failed because React puts a `<!-- -->` marker between adjacent pieces of text. The check was corrected to read the visible text, not loosened.

## 12. Learning outcomes
Not recorded separately. The README's sections on the treasurer dashboard and the client boundary explain the reasoning.

## 13. Evidence, commits and pull requests
- Commits [`b833502`](https://github.com/mdvar2/RondiTrack/commit/b833502) and [`f65649f`](https://github.com/mdvar2/RondiTrack/commit/f65649f).
- The PR and the screen recording are still to come.

## 14. Relationship to other assignments
- **Builds on:**
  - [7.1](assignment-7.1.md)'s components, [7.3](assignment-7.3.md)'s error messages, and [6.3](assignment-6.3.md)'s `/me/home`.
  - [6.1](assignment-6.1.md)'s per-stokvel roles, the reason the treasurer dashboard can't trust a token.
  - [5.1](assignment-5.1.md)–[5.3](assignment-5.3.md)'s legacy `Stokvel.Members`, the cause of the `memberCount` bug.
- **Next:** the brief's Day 2 and Day 3 work: streaming, Server Actions for writes, registration, silent refresh and route protection.
