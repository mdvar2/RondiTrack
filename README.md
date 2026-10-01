# RondiTrack

RondiTrack is a .NET 10 Web API for managing users, stokvels, memberships, contribution cycles, and member contributions.

The project currently uses in-memory storage and demonstrates clean API design through request and response DTOs, repositories, a focused service layer, FluentValidation, centralized RFC 9457 error handling, correlation IDs, idempotent contribution recording, and negative-path integration tests.

## Technologies

- .NET 10
- ASP.NET Core Web API
- FluentValidation
- Built-in OpenAPI support
- Scalar API Reference
- xUnit
- Microsoft.AspNetCore.Mvc.Testing
- In-memory repositories
- In-memory idempotency store

## Running the Project

1. Clone the repository.
2. Open a terminal in the project directory.
3. Restore the dependencies:

   ```bash
   dotnet restore
   ```

4. Run the application:

   ```bash
   dotnet run
   ```

5. Open the Scalar API interface for the running application.

During development the application normally runs at:

```text
http://localhost:5101
```

## API Endpoints

### Users

- `GET /api/users` - Get all users
- `GET /api/users/{id}` - Get a user by ID
- `POST /api/users` - Create a user
- `PUT /api/users/{id}` - Update a user
- `DELETE /api/users/{id}` - Delete a user

### Stokvels

- `GET /api/stokvels` - Get all stokvels
- `GET /api/stokvels/{id}` - Get a stokvel by ID
- `POST /api/stokvels` - Create a stokvel
- `PUT /api/stokvels/{id}` - Update a stokvel
- `DELETE /api/stokvels/{id}` - Delete a stokvel

### Membership

- `POST /api/stokvels/{stokvelId}/members/{userId}` - Add a user to a stokvel
- `DELETE /api/stokvels/{stokvelId}/members/{userId}` - Remove a user from a stokvel

### Contribution Cycles

- `GET /api/stokvels/{stokvelId}/cycles` - Get contribution cycles for a stokvel
- `GET /api/stokvels/{stokvelId}/cycles/{cycleId}` - Get a contribution cycle
- `POST /api/stokvels/{stokvelId}/cycles` - Create a contribution cycle
- `PUT /api/stokvels/{stokvelId}/cycles/{cycleId}` - Update a contribution cycle
- `DELETE /api/stokvels/{stokvelId}/cycles/{cycleId}` - Delete a contribution cycle

Example create request:

```json
{
  "period": "2026-09",
  "targetAmount": 5000
}
```

The period uses the `YYYY-MM` format.

### Contributions

- `POST /api/stokvels/{stokvelId}/members/{userId}/contributions` - Record a contribution for a stokvel member

The contribution endpoint requires an `Idempotency-Key` request header.

Example:

```text
Idempotency-Key: contribution-test-001
```

Request body:

```json
{
  "amount": 5000,
  "contributionCycleId": "8316ae89-f4b2-4dfc-bbf0-c8ce8b5259b8"
}
```

A contribution therefore references an actual `ContributionCycle` instead of accepting an arbitrary cycle string.

## Design Choices

### Controllers

Controllers group HTTP endpoints around the resources exposed by the application.

Controllers are responsible for HTTP concerns such as receiving request DTOs, calling the appropriate repository or service, and returning successful HTTP responses.

Controllers do not construct error responses manually. When an operation fails, the application throws an appropriate exception and allows the centralized exception handler to create the HTTP error response.

This keeps error formatting consistent and avoids repeating `ProblemDetails`, status-code, and error-message logic in every action.

### Domain Models

The main domain models are:

- `User`
- `Stokvel`
- `ContributionCycle`
- `Contribution`

The models protect their state using private setters. Changes are made through constructors and domain methods such as `UpdateDetails`, `AddMember`, and `RemoveMember`.

The entities also contain defensive checks so that invalid domain objects cannot easily be constructed directly.

### Request and Response DTOs

Domain entities are not exposed directly through the HTTP API.

Request DTOs define the information callers are allowed to provide, including:

- `CreateUserRequest`
- `UpdateUserRequest`
- `CreateStokvelRequest`
- `UpdateStokvelRequest`
- `CreateContributionCycleRequest`
- `UpdateContributionCycleRequest`
- `RecordContributionRequest`

Response DTOs define the shape returned to callers, including:

- `UserResponse`
- `StokvelResponse`
- `ContributionCycleResponse`
- `ContributionResponse`

This separates the HTTP contract from the internal domain model and reduces the risk of over-posting internal properties such as generated IDs.

### Manual Mapping

Mapping between domain entities and response DTOs is performed manually using methods such as:

```csharp
UserResponse.FromEntity(user)
StokvelResponse.FromEntity(stokvel)
ContributionCycleResponse.FromEntity(cycle)
ContributionResponse.FromEntity(contribution)
```

Manual mapping was chosen because the mappings in RondiTrack are small and explicit.

This is also useful for a money-related application because it makes the HTTP boundary easy to inspect and debug rather than relying on hidden mapping configuration.

## Validation vs Business Rules

RondiTrack deliberately separates request validation from application and business-rule checks.

### Request Validation — "Is this request well-formed?"

FluentValidation is used for request DTO validation.

Validators check the shape of incoming data, for example:

- required names;
- valid email format;
- positive monetary values;
- required contribution-cycle IDs; and
- contribution-cycle periods using `YYYY-MM`.

Validators do not query repositories and do not decide whether a resource exists.

For example, an empty contribution-cycle period or a non-positive target amount is a malformed request and results in `400 Bad Request`.

Malformed JSON/model-binding failures are also routed through the same centralized error system so that they use the same error shape as FluentValidation failures.

### Exceptions — "Is this operation allowed?"

Once the request is well-formed, the application may need to inspect current state.

Examples include:

- whether a user exists;
- whether a stokvel exists;
- whether a contribution cycle exists;
- whether a user belongs to a stokvel;
- whether a cycle belongs to the requested stokvel;
- whether a contribution already exists; and
- whether an idempotency key conflicts with an earlier request.

These are not FluentValidation rules because they depend on application state rather than the shape of the request.

This separation prevents validators from becoming a second service or repository layer.

## Domain Exception Hierarchy

RondiTrack uses a small application-specific exception hierarchy based on `RondiTrackException`.

The current exception types are:

- `RequestValidationException`
- `NotFoundException`
- `BusinessRuleException`

They represent different categories of failure.

### RequestValidationException

Used when a request is malformed or missing required transport input.

It maps to:

```text
400 Bad Request
```

### NotFoundException

Used when a well-formed identifier refers to a resource that does not exist.

It maps to:

```text
404 Not Found
```

### BusinessRuleException

Used when the request is well-formed but conflicts with the current application state or a business rule.

It maps to:

```text
409 Conflict
```

Examples include duplicate membership, duplicate contribution, and idempotency-key conflicts.

### Duplicate Contribution vs Idempotency-Key Conflict

Duplicate contribution and idempotency-key conflict currently use the same `BusinessRuleException` category because both requests are well-formed but conflict with existing application state.

They are still separate business checks.

An idempotency-key conflict occurs when the same key is reused with different request data:

```text
Same key + different request
→ 409 Conflict
```

A duplicate contribution occurs when a different/new key is supplied but a contribution already exists for that member and contribution cycle:

```text
New key + existing member/cycle contribution
→ 409 Conflict
```

Using the same exception category avoids creating unnecessary exception classes while preserving different error messages that identify the actual rule that failed.

## Centralized Error Handling

RondiTrack uses one `GlobalExceptionHandler` implementing ASP.NET Core's `IExceptionHandler`.

Controllers and services throw exceptions describing what failed. The global handler is responsible for translating those exceptions into HTTP responses.

The mapping is:

| Exception | HTTP status |
| --- | --- |
| `RequestValidationException` | `400 Bad Request` |
| `NotFoundException` | `404 Not Found` |
| `BusinessRuleException` | `409 Conflict` |
| Unexpected exception | `500 Internal Server Error` |

This replaced the earlier approach of formatting errors individually in controllers.

As a result, controllers do not contain repeated `try/catch` blocks or manually construct `ProblemDetails`.

## RFC 9457 Problem Details

API errors are returned using a consistent Problem Details structure with the media type:

```text
application/problem+json
```

For example:

```json
{
  "type": "about:blank",
  "title": "Conflict",
  "status": 409,
  "detail": "A contribution already exists for this member and cycle.",
  "instance": "/api/stokvels/.../members/.../contributions",
  "correlationId": "0HN0QKKFQNVLN:00000001"
}
```

The same general shape is used for validation, not-found, business-rule, and unexpected server errors.

## Correlation IDs and Structured Logging

Every error response includes the current request's correlation ID.

The centralized exception handler also writes a structured log entry containing that same ID.

For example, a duplicate contribution produced the following response information:

```json
{
  "type": "about:blank",
  "title": "Conflict",
  "status": 409,
  "detail": "A contribution already exists for this member and cycle.",
  "correlationId": "0HN0QKKFQNVLN:00000001"
}
```

The matching server log contained:

```text
Request failed. CorrelationId: 0HN0QKKFQNVLN:00000001
RondiTrack.Exceptions.BusinessRuleException:
A contribution already exists for this member and cycle.
```

The matching correlation ID connects the client-visible failure to the corresponding server-side log entry. This makes troubleshooting easier without exposing stack traces or internal implementation details to API clients.

An idempotency-key conflict was also verified with the same mechanism:

```text
Request failed. CorrelationId: 0HNOQKKFQNVLM:00000006
RondiTrack.Exceptions.BusinessRuleException:
Idempotency-Key was already used with a different request.
```

## Service Layer

Services are used only where an operation requires meaningful coordination or business decisions beyond straightforward CRUD.

### MembershipService

`MembershipService` coordinates membership operations.

It checks:

- whether the stokvel exists;
- whether the user exists; and
- whether membership already exists.

### ContributionService

`ContributionService` coordinates contribution recording.

It checks:

- the idempotency key;
- whether the stokvel exists;
- whether the user exists;
- whether the user belongs to the stokvel;
- whether the contribution cycle exists;
- whether the contribution cycle belongs to the stokvel;
- whether the member already has a contribution for that cycle; and
- whether an idempotency key has already been associated with different request data.

This is a genuine multi-resource workflow, so a service is appropriate.

### Why ContributionCycle Does Not Have a Service

`ContributionCycle` intentionally does not have its own service.

Its current operations are straightforward CRUD. There is no meaningful multi-resource workflow or complex business process that would justify an additional service layer.

The controller therefore communicates directly with `IContributionCycleRepository`.

This keeps the architecture proportional to the problem rather than creating services for every entity automatically.

If contribution-cycle operations gain meaningful business rules in the future, a service can be introduced when it is actually needed.

## Repository Abstraction

Data access is placed behind repository interfaces:

- `IUserRepository`
- `IStokvelRepository`
- `IContributionRepository`
- `IContributionCycleRepository`

The current implementations use in-memory collections because persistent database storage is not required at this stage.

Repositories separate data access from controllers and services. Their interfaces describe what data operations are available while their implementations decide how the data is currently stored.

The repositories are registered as singletons so the in-memory state remains available for the lifetime of the running application.

## Money

`decimal` is used for monetary values such as contribution amounts and cycle target amounts.

`decimal` is appropriate for financial values because it avoids the binary floating-point precision behaviour associated with types such as `double`.

## Idempotency

Recording a contribution represents an operation that should be safe to retry.

The contribution endpoint therefore requires an `Idempotency-Key`.

The contribution service creates a SHA-256 hash representing the request and checks the in-memory idempotency store.

The behaviour is:

- A new key and valid request records the contribution and stores the successful response.
- The same key with the same request returns the original stored response without creating another contribution.
- The same key with different request data returns `409 Conflict`.
- A new key does not bypass the duplicate-contribution rule.

During Scalar testing, a contribution was created using:

```text
Idempotency-Key: contribution-test-001
```

with:

```json
{
  "amount": 5000,
  "contributionCycleId": "8316ae89-f4b2-4dfc-bbf0-c8ce8b5259b8"
}
```

The first request returned `201 Created`.

Repeating the exact request with the same idempotency key returned the same contribution ID and original `recordedAtUtc` value, demonstrating that another contribution was not created.

Changing only the amount while keeping the same idempotency key returned:

```json
{
  "type": "about:blank",
  "title": "Conflict",
  "status": 409,
  "detail": "Idempotency-Key was already used with a different request.",
  "correlationId": "0HNOQKKFQNVLM:00000006"
}
```

Using a new key (`contribution-test-002`) for the already-recorded member and cycle returned:

```json
{
  "type": "about:blank",
  "title": "Conflict",
  "status": 409,
  "detail": "A contribution already exists for this member and cycle.",
  "correlationId": "0HN0QKKFQNVLN:00000001"
}
```

This demonstrates that idempotency and duplicate-contribution protection solve different problems.

The idempotency store is currently in memory, so its records are reset when the application restarts.

## HTTP Status Codes

RondiTrack currently uses:

- `200 OK` when a successful request returns data.
- `201 Created` when a new resource or contribution is created.
- `204 No Content` for successful operations that require no response body.
- `400 Bad Request` for malformed or invalid request input.
- `404 Not Found` when a requested resource does not exist.
- `409 Conflict` when a well-formed request conflicts with current application state or a business rule.
- `500 Internal Server Error` for unexpected exceptions.

The important distinction is:

```text
400 → Is the request well-formed?
404 → Does the requested resource exist?
409 → Is the well-formed operation allowed in the current state?
```

## Data Storage

The application currently uses seeded in-memory data for users and stokvels.

Contribution cycles, contributions, and idempotency records are also stored in memory.

All in-memory data is reset whenever the application restarts.

No database or Entity Framework Core is used at this stage.

## Testing

RondiTrack is tested manually through Scalar and automatically through xUnit unit and integration tests.

The automated test suite currently contains **19 tests** covering happy paths, validation failures, not-found behaviour, business rules, idempotency, and additional edge cases.

### Scalar Testing

The following scenarios were manually verified:

- adding a stokvel member returns `204 No Content`;
- creating a contribution cycle returns `201 Created`;
- recording a valid contribution returns `201 Created`;
- repeating the same contribution request with the same idempotency key returns the original contribution response;
- reusing the same idempotency key with different request data returns `409 Conflict`;
- using a new key for an existing member/cycle contribution returns `409 Conflict`;
- malformed request data returns `400 Bad Request`;
- requesting a nonexistent resource returns `404 Not Found`;
- error responses use `application/problem+json`; and
- response correlation IDs match the IDs written to server logs.

Scalar also exposes the generated OpenAPI contract. Endpoint summaries, descriptions, success responses, and applicable `400`, `404`, and `409` responses are documented in the controllers and displayed in the API reference.

### Unit Tests

Unit tests exercise business behaviour directly without going through HTTP or dependency injection.

The unit tests verify:

- adding a user who is already a member throws `BusinessRuleException`;
- recording another contribution for the same member and contribution cycle throws `BusinessRuleException`; and
- reusing the same idempotency key with different request data throws `BusinessRuleException`.

These tests verify business rules independently from the HTTP layer.

### Integration Tests

Integration tests use `Microsoft.AspNetCore.Mvc.Testing` and `WebApplicationFactory<Program>` to exercise the actual ASP.NET Core request pipeline.

Happy-path integration tests verify:

- creating a user and retrieving the created user;
- creating a stokvel and retrieving the created stokvel;
- creating a contribution cycle and retrieving the created cycle; and
- recording a valid contribution.

Negative-path and business-rule integration tests verify:

- malformed input returns `400 Bad Request`;
- a nonexistent user returns `404 Not Found`;
- duplicate membership returns `409 Conflict`;
- removing a user who is not a member returns `409 Conflict`;
- a contribution from a non-member returns `409 Conflict`;
- using a contribution cycle belonging to another stokvel returns `409 Conflict`;
- recording another contribution for the same member and cycle with a different idempotency key returns `409 Conflict`; and
- omitting the required `Idempotency-Key` returns `400 Bad Request`.

Where applicable, error tests also verify the `application/problem+json` media type and Problem Details response information.

### Idempotency Tests

Idempotency is tested separately because retry behaviour forms part of the contribution endpoint contract.

The automated tests verify:

```text
Same key + same request
→ same stored contribution response
```

and:

```text
Same key + different request
→ 409 Conflict
```

Using a different key also does not bypass the duplicate-contribution business rule.

### Edge Cases

Three edge cases beyond the normal happy paths were deliberately identified and tested.

#### 1. Missing Idempotency-Key

The contribution endpoint requires an `Idempotency-Key` header.

This edge case was identified by considering what happens when the contribution request body is otherwise valid but the required retry key is omitted.

Expected result:

```text
400 Bad Request
```

The automated test confirms that the request is rejected with:

```text
Idempotency-Key header is required.
```

#### 2. Zero Contribution Amount

The contribution validator requires the amount to be greater than zero.

This edge case was identified by testing the exact boundary of the `GreaterThan(0)` rule rather than testing only a negative value.

Test value:

```text
amount = 0
```

Expected result:

```text
400 Bad Request
```

The automated test confirms that zero cannot be recorded as a valid contribution.

#### 3. Invalid Contribution-Cycle Period

Contribution-cycle periods use the `YYYY-MM` format.

The following value was tested:

```text
2027-13
```

Although it resembles the required format, month 13 is not a valid calendar month.

This edge case was identified by checking whether validation verifies a real year/month value rather than only the visible structure of the string.

Expected result:

```text
400 Bad Request
```

The automated test confirms that the invalid period is rejected.

### Deliberate Broken-Rule Test

One business-rule guard was deliberately disabled temporarily to verify that the automated tests detect a regression.

The duplicate-membership check in `MembershipService.AddMemberAsync` was temporarily commented out.

The existing integration test:

```text
DuplicateMembership_Returns409ProblemJson
```

normally expects:

```text
409 Conflict
```

With the service guard disabled, the defensive check in the `Stokvel` domain model threw an unexpected `InvalidOperationException`. The request therefore resulted in:

```text
500 Internal Server Error
```

The test correctly failed with:

```text
Expected: Conflict
Actual:   InternalServerError

total: 1
failed: 1
succeeded: 0
```

The duplicate-membership guard was then restored without changing the test.

The exact same test was run again and passed:

```text
total: 1
failed: 0
succeeded: 1
skipped: 0
```

This RED-to-GREEN check demonstrates that the test detects a real change in the API contract rather than simply executing code.

### Running the Automated Tests

Run the complete suite with:

```bash
dotnet test RondiTrack.Tests/RondiTrack.Tests.csproj
```

To display the individual test names:

```bash
dotnet test RondiTrack.Tests/RondiTrack.Tests.csproj --logger "console;verbosity=detailed"
```

The final detailed run produced:

```text
Test Run Successful.
Total tests: 19
Passed: 19

Test summary:
total: 19
failed: 0
succeeded: 19
skipped: 0

Build succeeded
```

The final detailed run showed the individual unit, happy-path, negative-path, business-rule, idempotency, and edge-case tests and finished with all **19 tests passing**.

## Definition of Done

| API area / endpoint | Documented | Validation / business guard | Automated coverage | Status codes reviewed |
| --- | --- | --- | --- | --- |
| `GET /api/users` | Yes | N/A - no request body | Resource happy path | Yes |
| `GET /api/users/{id}` | Yes | Resource existence | Happy path + not found | Yes |
| `POST /api/users` | Yes | FluentValidation | Happy path | Yes |
| `PUT /api/users/{id}` | Yes | FluentValidation + existence | Contract reviewed | Yes |
| `DELETE /api/users/{id}` | Yes | Resource existence | Contract reviewed | Yes |
| `GET /api/stokvels` | Yes | N/A - no request body | Resource happy path | Yes |
| `GET /api/stokvels/{id}` | Yes | Resource existence | Resource happy path | Yes |
| `POST /api/stokvels` | Yes | FluentValidation | Happy path | Yes |
| `PUT /api/stokvels/{id}` | Yes | FluentValidation + existence | Contract reviewed | Yes |
| `DELETE /api/stokvels/{id}` | Yes | Resource existence | Contract reviewed | Yes |
| `POST /api/stokvels/{stokvelId}/members/{userId}` | Yes | Existence + duplicate-membership rule | Unit + integration | Yes |
| `DELETE /api/stokvels/{stokvelId}/members/{userId}` | Yes | Existence + membership rule | Integration | Yes |
| `GET /api/stokvels/{stokvelId}/cycles` | Yes | Stokvel existence | Resource coverage | Yes |
| `GET /api/stokvels/{stokvelId}/cycles/{cycleId}` | Yes | Existence + ownership | Resource happy path | Yes |
| `POST /api/stokvels/{stokvelId}/cycles` | Yes | FluentValidation + existence | Happy path + edge case | Yes |
| `PUT /api/stokvels/{stokvelId}/cycles/{cycleId}` | Yes | FluentValidation + existence/ownership | Contract reviewed | Yes |
| `DELETE /api/stokvels/{stokvelId}/cycles/{cycleId}` | Yes | Existence + ownership | Contract reviewed | Yes |
| `POST /api/stokvels/{stokvelId}/members/{userId}/contributions` | Yes | Validation + membership + cycle + duplicate + idempotency rules | Unit + integration + edge cases | Yes |

`N/A` is used where an endpoint has no request body requiring FluentValidation. Existence checks and business guards are still applied where required.

## Asynchronous Operations

Repository, service, and controller operations use Task-based asynchronous contracts where appropriate.
Controllers await repository and service operations instead of synchronously blocking on tasks.
This allows the current in-memory repository implementations to be replaced later by I/O-based persistence without requiring major changes to the API structure.

---

# Assignment 5.1 — EF Core & Database Foundations

Assignment 5.1 introduces PostgreSQL and Entity Framework Core persistence to RondiTrack. The goal was to replace selected in-memory persistence with a real relational database while preserving the existing API behaviour and repository abstractions.

## PostgreSQL Setup

I chose to install PostgreSQL locally rather than use a container because I wanted to understand the database installation, Windows service, authentication, command-line connectivity, and connection-string configuration directly.

The development environment uses PostgreSQL 17 and a dedicated database named:

```text
ronditrack

