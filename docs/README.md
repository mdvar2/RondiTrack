# RondiTrack documentation

Start with the [root README](../README.md), which describes what RondiTrack is, how to run it and its current state.

## Current references

These describe the project as it is now and are kept up to date.

| Document | What's in it |
|---|---|
| [api-reference.md](api-reference.md) | Every endpoint, authentication and cookies, permissions by role, business contracts |
| [architecture.md](architecture.md) | Projects and dependencies, the contribution request flow, DI lifetimes, how the two frontends reach the API |
| [development-migration-status.md](development-migration-status.md) | Migration state of the local development database (verified 27 Oct 2026) |
| [../web-next/README.md](../web-next/README.md) | The Next.js frontend (Assignment 8.1): running it, API check, routes, treasurer dashboard, client boundary, server-only proof, known gaps |
| [../web/README.md](../web/README.md) | The React/Vite frontend (Assignments 7.1–7.4) |
| [../clients/typescript/README.md](../clients/typescript/README.md) | The Assignment 6.3 TypeScript contract experiment |

The live API contract is the Development API's OpenAPI document at `/openapi/v1.json`, which you can browse at `/scalar/v1`.

## Records

| Document | What's in it |
|---|---|
| [safe-push-verification.md](safe-push-verification.md) | Historical (11 Oct 2026): how two private database-cleanup documents were removed from unpublished commits before the branch was published |

## Assignment history

[history/README.md](history/README.md) lists every assignment in order, with its record and evidence. Documents in `history/` describe the project as it was at the time.
