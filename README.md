# RondiTrack

RondiTrack is a backend API I built during my Bitcube software development training. It helps manage stokvels, including members, contributions, savings cycles and payouts.

I started with basic API endpoints and gradually added database relationships, security, testing and monitoring. My latest assignment focused on making the backend ready for a frontend application.

## What the system does

- Manages users, stokvels, memberships and contribution cycles.
- Records contributions and payouts while checking the business rules.
- Uses JWT authentication and membership-based permissions to protect requests.
- Prevents duplicate contribution processing when a request is retried with the same idempotency key.
- Provides a member home endpoint so a logged-in user can see their own stokvels and contribution information.
- Returns consistent error responses and includes tracing, logging and health checks.

The project is **backend only** at the moment. I use Scalar to explore and test the API during development.

## Technologies I used

| Technology | How I used it |
|---|---|
| C# / ASP.NET Core 10 | Built the API endpoints and services |
| EF Core 10 / PostgreSQL 17 | Stored and queried application data |
| FluentValidation | Validated incoming requests |
| JWT authentication | Protected endpoints and identified users |
| xUnit / Testcontainers | Tested the API with disposable PostgreSQL databases |
| OpenAPI / Scalar | Documented and explored the API |
| OpenTelemetry / Aspire | Worked with tracing, metrics and logs |
| TypeScript | Generated frontend-facing types from the API contract |

I separated the solution into **API**, **Domain**, **Infrastructure** and **RondiTrack.Tests** projects. The [architecture guide](docs/architecture.md) explains how they fit together.

## My latest work: Assignment 6.3

For Assignment 6.3, I worked on the parts of the backend that a frontend would depend on:

- **API versioning:** Controller endpoints now use `/api/v1`.
- **CORS:** The API allows the configured frontend origin rather than accepting requests from any origin.
- **Rate limiting:** Login, contribution and payout requests have limits to reduce excessive requests.
- **Contribution retries:** The existing idempotency handling works alongside rate limiting.
- **Member home endpoint:** `GET /api/v1/me/home` returns information for the logged-in member without exposing other members' contribution amounts.
- **Consistent errors:** API errors follow a Problem Details response format.
- **TypeScript contracts:** I generated types from the actual OpenAPI document and checked that a renamed backend property would cause a frontend compile error.

One feature is still incomplete: **`nextPayoutDate` returns `null`** because the system does not yet have a rule for scheduling future payouts. I have not made up a date for it.

For a more detailed explanation of the changes, see the [Assignment 6.3 trainer notes](docs/assignment-6-3-stages.md) and [compliance matrix](docs/assignment-6-3-compliance.md).

## Testing and verification

**On 14 October 2026, all 122 tests passed, with no failures or skipped tests.** The fresh solution build also completed with no warnings or errors.

I also checked that the tests could run without connecting to my development PostgreSQL database. I temporarily disabled new connections to that database, ran the test suite using Testcontainers, and then restored the original connection setting. I confirmed the database was accessible again with `SELECT 1`.

No development data, schemas or migrations were changed during that verification.

The [Assignment 6.3 verification report](docs/assignment-6-3-verification.md#successful-offline-verification) has the full procedure and recorded results. The earlier Assignment 6.2 test result was 101 passing tests; **122 is the latest verified total**.

To run the ordinary tests locally, start Docker Desktop in Linux-container mode and use:

```powershell
dotnet restore RondiTrack.sln
dotnet build RondiTrack.sln --no-restore -m:1
dotnet test RondiTrack.sln --no-restore -m:1
```

These commands run the normal suite. They do **not** disable development database connections. The guarded isolation procedure is explained separately in the verification report.

## Running the API locally

You need the .NET SDK, PostgreSQL and the project's local configuration. From the repository root:

```powershell
dotnet restore RondiTrack.sln
dotnet build RondiTrack.sln
dotnet run --project API
```

The development API documentation is available at `/scalar/v1` and the OpenAPI document at `/openapi/v1.json` on the port used by the running API.

**Important database note:** My existing development database contains duplicate normalized emails that currently block an authentication migration. I am keeping that data intact rather than resetting the database to make the migration pass. See [development migration status](docs/development-migration-status.md) before attempting database updates.

For configuration, local secrets, HTTPS and migration steps, use the [detailed technical notes](docs/README-detailed-technical-notes.md#4-setup-and-running-the-api).

## What I learned and what I still need to improve

This project helped me practise building APIs in stages, keeping business logic separate from controllers, protecting financial operations, and writing integration tests against a real database engine.

There are still limitations I need to address before treating this as a production system. Rate limiting and the idempotency response store currently work **per API process**, not across multiple running instances. I also need a confirmed future payout scheduling rule, and the development migration issue still needs a safe resolution.

## Project documentation

- [API routes and request examples](docs/api-reference.md)
- [Architecture and service lifetimes](docs/architecture.md)
- [Assignment 6.3 implementation notes](docs/assignment-6-3-stages.md)
- [Assignment 6.3 test evidence](docs/assignment-6-3-verification.md)
- [Assignment 6.3 requirement checklist](docs/assignment-6-3-compliance.md)
- [Detailed technical notes and full logging audit](docs/README-detailed-technical-notes.md)
- [Earlier assignment history](README-history.md)

The detailed notes are kept separately so I can still refer to the code, tests and decisions during a trainer walkthrough without making this README difficult to read.
