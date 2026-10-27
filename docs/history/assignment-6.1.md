# Assignment 6.1 — Authentication and per-stokvel authorization

| | |
|---|---|
| Official title | Not recorded. The README heading at the time is "Assignment 6.1 — authentication and authorization". |
| Date | 11 October 2026 |
| Final commit on `main` | [`67e52fa`](https://github.com/mdvar2/RondiTrack/commit/67e52fa) "Document authentication tests and migration blocker", merged by [`e2bbc32`](https://github.com/mdvar2/RondiTrack/commit/e2bbc32) |
| Branch / PR | `assignment-5-4-clean-architecture`, [PR #2](https://github.com/mdvar2/RondiTrack/pull/2) (merged 11 Oct 2026, together with [5.4](assignment-5.4.md)) |
| README as submitted | [README at `e2bbc32`](https://github.com/mdvar2/RondiTrack/blob/e2bbc32/README.md), kept (with encoding and headings repaired) as the [README archive](readme-archive-2026-10-11.md) |
| Re-tested | **Yes, 27 Oct 2026**: the `e2bbc32` code, exported unchanged, passed **62/62** on disposable PostgreSQL |

## 1. Purpose and learning objectives
To make users prove who they are (JWT access tokens and rotating refresh tokens) and to decide what each person may do **per stokvel**, from their live membership. The brief's learning objectives aren't recorded.

## 2. Original requirements
The brief isn't in the repository. The [verification report](evidence/assignment-6-1-verification-report.md#requirement-checklist) has a 25-row requirement checklist, all verified. It includes:
- Passwords hashed with `PasswordHasher`, at least 12 characters, and anonymous registration.
- Normalized email uniqueness.
- JWTs that live at most 15 minutes, with explicit clock skew and a key of at least 32 bytes kept in user secrets.
- The same `401` for a wrong password and an unknown email.
- Minimal claims, and **no stokvel role claims**.
- Random refresh tokens stored only as hashes and sent only in cookies.
- Rotation with atomic reuse detection that revokes the whole family.
- A custom resource handler that checks **live membership**, with `404` for non-members and `403` for members without the right role.
- Members may record only their own contributions.
- Treasurer and Chairperson may manage cycles, other members' contributions and payouts.

## 3. What I implemented
- **`POST /api/auth/register`, `/login`, `/refresh`, `/logout`** (under `/api/v1` from 6.3), with `AuthService` and `TokenService`.
- A **five-minute access JWT** with 30 seconds of skew, signed with a key from user secrets and validated for signature, issuer, audience and lifetime.
- **Refresh tokens:** random, stored as hashes in token **families**, rotated on every use, and kept in an `HttpOnly`, `Secure`, `SameSite=Strict` cookie. Reusing an old one revokes the family.
- **Authorization:** fallback "signed in" for every route, a `PlatformAdmin` role for user administration, and `StokvelAccessHandler`, which reads the caller's membership of *this* stokvel on every request. Demoting or removing someone takes effect on their next request, even with the same token.
- Three migrations: `AddUserAuthentication`, `AddRefreshTokenSessions`, `AddPlatformRole`.

## 4. Technologies, tools and dependencies
New: **Microsoft.AspNetCore.Authentication.JwtBearer 10.0.12**. `PasswordHasher` comes from the shared framework.

## 5. Prerequisites and installation
As for [5.4](assignment-5.4.md#5-prerequisites-and-installation): the .NET 10 SDK, and Docker for the tests.

## 6. Environment configuration
A JWT signing key in user secrets: random, at least 32 bytes, base64-encoded. It's never committed:
```powershell
dotnet user-secrets --project API set Jwt:SigningKey "<base64 key>"
```
Issuer, audience and token lifetimes are non-secret settings in `API/appsettings.json`.

## 7. How to run or reproduce
**As submitted** (README at `e2bbc32`): `dotnet test RondiTrack.Tests/RondiTrack.Tests.csproj` with Docker running, and `dotnet run --project API --launch-profile https`. The development database **couldn't** take the new migrations at the time (see section 11), so the API was verified against disposable databases only.

**In the current codebase:** the same endpoints are under `/api/v1/auth/*`. [docs/api-reference.md](../api-reference.md) is the current reference for tokens, cookies and permissions. Tests: `JwtLoginTests.cs`, `RefreshSessionTests.cs`, `RegistrationTests.cs`, `StokvelAuthorizationTests.cs`, `AuthorizationMatrixTests.cs`.

## 8. Important files and components
- `API/Auth/`: `AuthService`, `TokenService`, `JwtOptions`, `Policies`, `StokvelAccessHandler`, `StokvelAuthorization`, `PlatformRoleClaimsTransformation`
- `API/Controllers/AuthController.cs`
- `Domain/Entities/RefreshToken.cs`, `RefreshTokenFamily.cs`, `Roles.cs`
- `Infrastructure/Repositories/EfRefreshTokenRepository.cs`

## 9. How it works
1. **Login** checks the password hash, starts a refresh family and returns a short-lived access token. The refresh token goes into a cookie.
2. **Each request** is authenticated from the bearer token.
3. **Scoped operations** call `StokvelAuthorization.EnsureAsync`, which loads the caller's membership of that stokvel. No membership gets `404` (it hides that the stokvel exists). The wrong role gets `403`.
4. **Refresh** swaps the cookie atomically. Presenting an already-used token revokes every token in that family.

## 10. Testing and recorded results
| Point | Result |
|---|---|
| Full suite with the development database unavailable ([report](evidence/assignment-6-1-verification-report.md#actual-verification)) | **62 passed**, 0 failed or skipped. The 25 original tests were unchanged |
| Re-run 27 Oct 2026 of `e2bbc32` | **62 passed, 0 failed** |

## 11. Challenges and solutions
- **The development database couldn't upgrade.** It held duplicate normalized emails, which block the unique-email migration. I chose to preserve that data rather than delete it, and documented the block. The repository doesn't record how it was resolved, but a read-only check on 27 Oct 2026 found the migration applied ([migration status](../development-migration-status.md)).
- **Passwordless legacy users.** No password-setup or role-promotion endpoint was added; these are recorded as gaps.

## 12. Learning outcomes
The technical notes summarise 6.1 as: "I implemented JWT authentication, refresh-token rotation and current membership/role checks" ([technical notes §6](technical-notes-to-6.3.md#6-assignments-and-learning-journey)).

## 13. Evidence, commits and pull requests
- [PR #2](https://github.com/mdvar2/RondiTrack/pull/2)
- [Phase verification](evidence/assignment-6-1-phase-verification.md)
- [Verification report](evidence/assignment-6-1-verification-report.md)
- [File manifest](evidence/assignment-6-1-file-manifest.txt)
- [Isolation evidence](evidence/assignment-6-1-isolation-evidence.txt)
- [README archive: 6.1](readme-archive-2026-10-11.md#assignment-61--authentication-and-authorization)
- [Technical notes §7](technical-notes-to-6.3.md#7-authentication-and-authorization)

## 14. Relationship to other assignments
- **Before:** [5.4](assignment-5.4.md)'s architecture and test fixture.
- **Next:** [6.2](assignment-6.2.md) added observability and kept security events free of personal data.
- **Later:**
  - The per-stokvel role check is the reason [8.1](assignment-8.1.md)'s treasurer dashboard filters roles from the API rather than trusting the token, which carries no role.
  - The refresh cookie is what [7.3](assignment-7.3.md)'s browser flow and 8.1's server flow both build on.
