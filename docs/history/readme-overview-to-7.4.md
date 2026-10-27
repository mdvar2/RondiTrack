# RondiTrack README overview, up to Assignment 7.4

> **Historical record.** This was the general part of the root README up to Assignment 7.4: overview, technologies, how the API was run and the reflections, before the restructure. It was moved here unchanged (apart from Markdown formatting) on 27 October 2026. Commands, ports and test counts are as recorded at the time and may be out of date. For the current project, see the [root README](../../README.md).

[![RondiTrack CI](https://github.com/mdvar2/RondiTrack/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/mdvar2/RondiTrack/actions/workflows/ci.yml)

RondiTrack is a stokvel management application I built during my Bitcube software development training. It includes an ASP.NET Core backend for managing members, contributions, savings cycles and payouts, alongside a React and TypeScript frontend for displaying stokvel and contribution information.

I started with basic API endpoints and gradually added database relationships, security, testing and monitoring. The project progressed through frontend-facing API contracts, containerized deployment, scheduled processing, durable idempotency and automated CI verification. Assignment 7.1 introduced a responsive sample-data React dashboard typed against the backend OpenAPI contract; Assignment 7.3 subsequently connected it to live authenticated data.

# What the system does

- Manages users, stokvels, memberships and contribution cycles.

- Records contributions and payouts while checking the business rules.

- Uses JWT authentication and membership-based permissions to protect requests.

- Prevents duplicate contribution processing when a request is retried with the same idempotency key.

- Provides a member home endpoint so a logged-in user can see their own stokvels and contribution information.

- Returns consistent error responses and includes tracing, logging and health checks.

The project now includes both a backend API and a React frontend. I use Scalar to explore and test the API during development. As of Assignment 7.3, the React dashboard loads live authenticated data from the API. Earlier assignment sections below describe the previous sample-data versions.

# Technologies I used

| Technology | How I used it |
|---|---|
| C# / ASP.NET Core 10 | Built the API endpoints and services |
| EF Core 10 / PostgreSQL 17 | Stored and queried application data |
| FluentValidation | Validated incoming requests |
| JWT authentication | Protected endpoints and identified users |
| xUnit / Testcontainers | Tested the API with disposable PostgreSQL databases |
| OpenAPI / Scalar | Documented and explored the API |
| OpenTelemetry / Aspire | Worked with tracing, metrics and logs |
| React 19 / Vite 8 | Built the responsive StokvelCard and ContributionCard frontend |
| TypeScript 5 / OpenAPI TypeScript | Generated typed frontend contracts from the API OpenAPI document |
| Vitest / ESLint | Tested frontend formatting, session refresh, API errors, and query isolation; checked code quality |

I separated the solution into **API**, **Domain**, **Infrastructure** and **RondiTrack.Tests** projects. The [architecture guide](../architecture.md) explains how they fit together.

# Running the API locally

You need the .NET SDK, PostgreSQL and the project's local configuration. From the repository root:

```powershell
dotnet restore RondiTrack.sln
dotnet build RondiTrack.sln
dotnet run --project API
```

The development API documentation is available at `/scalar/v1` and the OpenAPI document at `/openapi/v1.json` on the port used by the running API.

**Important database note:** My existing development database contains duplicate normalized emails that currently block an authentication migration. I am keeping that data intact rather than resetting the database to make the migration pass. See [development migration status](../development-migration-status.md) before attempting database updates.

For configuration, local secrets, HTTPS and migration steps, use the [detailed technical notes](technical-notes-to-6.3.md#4-setup-and-running-the-api).

# What I learned and what I still need to improve

This project helped me practise building APIs in stages, keeping business logic separate from controllers, protecting financial operations, and writing integration tests against a real database engine.

There are still limitations I need to address before treating this as a production system. Rate limiting requires review before distributed deployment. Assignment 6.4 introduced PostgreSQL-backed durable idempotency records, replacing the earlier per-process limitation. I also need a confirmed future payout scheduling rule, and the development migration issue still needs a safe resolution.

# Project documentation

- [API routes and request examples](../api-reference.md)

- [Architecture and service lifetimes](../architecture.md)

- [Assignment 6.3 implementation notes](evidence/assignment-6-3-stages.md)

- [Assignment 6.3 test evidence](evidence/assignment-6-3-verification.md)

- [Assignment 6.3 requirement checklist](evidence/assignment-6-3-compliance.md)

- [Detailed technical notes and full logging audit](technical-notes-to-6.3.md)

- [Earlier assignment history](readme-archive-2026-10-11.md)

The detailed notes are kept separately so I can still refer to the code, tests and decisions during a trainer walkthrough without making this README difficult to read.
