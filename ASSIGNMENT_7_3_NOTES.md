# Assignment 7.3 — implementation notes

## API contract notes
- CORS: `http://localhost:3000`, credentials enabled; `Retry-After` exposed (source `API/Common/CorsPolicies.cs`).
- Refresh: HttpOnly, Secure, Strict cookie, scoped to `/api/v1/auth`; browser cookie behavior still requires verification.
- Member data: `/api/v1/me/home` exists; `/api/v1/me` does not exist. The admin `/stokvels` route must not be used for member listing.
- Contribution paging: real controller returns `PagedResult` with `items`, `nextPageToken`, `pageSize`, but OpenAPI incorrectly annotates an array. Fix the controller annotation and regenerate schema before final submission.
- Concurrent refresh: backend rotates cookies; single-flight implementation added; mutation evidence still needs to be captured.

## Single-flight proof
TODO: capture deliberately failing mutation test and passing restored test output. Do not claim proof before running it.

## Stokvel switch proof
TODO: capture failing keepPreviousData mutation and passing slow-second-stokvel test.

## Shared phone check
TODO: login A, view A-only data, logout, login B without reload; verify React Query Devtools cache is clear. No URL-based stokvel state in current UI.

## Known gaps
- The current backend lacks `GET /me` and stokvel-level contribution route named in assignment. The frontend uses actual existing endpoints.
- Add required automated session tests and stokvel-switch UI test and mutation evidence.
- Confirm real 429, secure refresh cookie, multi-user credentials and video manually.
- No deployment/production verification performed.
