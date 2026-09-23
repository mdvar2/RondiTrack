# RondiTrack

RondiTrack is a .NET 10 Web API for managing users, stokvels, memberships, and member contributions. The application provides CRUD operations for users and stokvels, supports stokvel membership management, and allows member contributions to be recorded safely using idempotency.

## Technologies

- .NET 10
- ASP.NET Core Web API
- Built-in OpenAPI support
- Scalar API Reference
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

5. Open the Scalar API interface using the `/scalar` endpoint of the running application.

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

### Contributions

- `POST /api/stokvels/{stokvelId}/members/{userId}/contributions` - Record a contribution for a stokvel member

The contribution endpoint requires an `Idempotency-Key` request header.

Example request body:

```json
{
  "amount": 500,
  "cycle": "2026-09"
}
```

The contribution cycle uses the `YYYY-MM` format.

## Design Choices

### Controllers

Controllers were chosen instead of Minimal APIs because they keep the endpoints for each resource grouped together. This makes the API structure easier to follow and maintain as the application grows.

Controllers are responsible for HTTP concerns such as receiving request DTOs, calling the appropriate repository or service, and translating the result into an HTTP response. Business decisions involving membership and contributions are handled by services rather than directly in controllers.

### Domain Models

The `User`, `Stokvel`, and `Contribution` models represent the application's internal domain state.

The models protect their state using private setters. Changes are made through constructors and domain methods such as `UpdateDetails`, `AddMember`, and `RemoveMember`.

Domain validation helps prevent invalid objects from being created. For example, contribution amounts must be greater than zero and a contribution must have a cycle.

### Request and Response DTOs

Domain entities are not exposed directly through the HTTP API.

Request DTOs define the information that callers are allowed to provide. For example:

- `CreateUserRequest`
- `UpdateUserRequest`
- `CreateStokvelRequest`
- `UpdateStokvelRequest`
- `RecordContributionRequest`

Using request DTOs prevents callers from directly supplying internal entity properties such as generated IDs and reduces the risk of over-posting.

Response DTOs define the information that is returned to callers:

- `UserResponse`
- `StokvelResponse`
- `ContributionResponse`

For example, `StokvelResponse` returns a `memberCount` rather than exposing the internal members collection.

### Manual Mapping

Mapping between domain entities and response DTOs is performed manually using methods such as:

```csharp
UserResponse.FromEntity(user)
StokvelResponse.FromEntity(stokvel)
ContributionResponse.FromEntity(contribution)
```

Manual mapping was chosen instead of an external mapping library because the mappings in RondiTrack are small and explicit.

This is especially appropriate for a money-related application because it makes the HTTP boundary easy to inspect and debug. There is no hidden mapping configuration deciding which financial or domain values are exposed to callers.

### Service Layer

Services are introduced only for operations that require business decisions beyond straightforward CRUD.

`MembershipService` coordinates membership operations. It checks whether the stokvel and user exist and prevents the same user from being added to the same stokvel more than once.

`ContributionService` coordinates contribution recording. It checks that:

- the stokvel exists;
- the user exists;
- the user belongs to the stokvel;
- the contribution amount is valid;
- the cycle is valid;
- the member has not already contributed for the same cycle; and
- the `Idempotency-Key` has not been reused incorrectly.

Simple CRUD operations continue to use repositories directly because they do not require the same business orchestration.

### Money

`decimal` is used for monetary values such as `ContributionAmount` and contribution `Amount` because it is appropriate for monetary calculations and avoids the binary floating-point precision issues associated with types such as `double`.

### Repository Abstraction

Data access is placed behind:

- `IUserRepository`
- `IStokvelRepository`
- `IContributionRepository`

The current implementations use in-memory collections because persistent database storage is not required at this stage.

The repositories are registered as singletons so that the in-memory data remains available for the lifetime of the running application.

### Idempotency

Recording a contribution represents an operation that should be safe to retry. The contribution endpoint therefore requires an `Idempotency-Key`.

When a contribution request is received, the service creates a SHA-256 hash representing the request and checks the in-memory idempotency store.

The behaviour is:

- A new key and valid request records the contribution and stores the successful response.
- The same key with the same request returns the original stored response without recording another contribution.
- The same key with a different request is rejected with `409 Conflict`.
- A different key does not bypass the duplicate-contribution rule. A member still cannot contribute twice for the same stokvel and cycle.

This provides two separate protections. Idempotency protects against accidental retries of the same operation, while the duplicate-contribution rule protects the business data even when a different key is used.

The idempotency store is currently in memory, so its records are reset when the application restarts.

### Error Responses

API errors use RFC 9457 Problem Details and are returned as `application/problem+json`.

The error response contains fields such as:

```json
{
  "type": "about:blank",
  "title": "Conflict",
  "status": 409,
  "detail": "User is already a member of this stokvel.",
  "instance": "/api/stokvels/..."
}
```

This gives callers one consistent error shape across the API.

### HTTP Status Codes

RondiTrack uses HTTP status codes according to the type of result.

- `200 OK` is used when a request succeeds and returns a response body.
- `201 Created` is used when a resource such as a user, stokvel, or contribution is created.
- `204 No Content` is used for successful operations that do not need to return a response body.
- `400 Bad Request` is used when the request does not meet the expected input requirements. For example, a contribution cycle that does not use the required `YYYY-MM` format returns `400`.
- `404 Not Found` is used when the requested user or stokvel does not exist.
- `409 Conflict` is used when the request conflicts with existing state, such as duplicate membership, duplicate contribution, or reuse of an idempotency key with a different request.
- `422 Unprocessable Entity` is used when the request can be understood but a business rule prevents it from being processed. For example, attempting to record a contribution with an amount of zero returns `422`.

The distinction between `400` and `422` is therefore that `400` represents a problem with the request/input itself, while `422` represents an understandable request that cannot be processed because of a business rule.

### Asynchronous Operations

Repository, service, and controller operations use Task-based asynchronous contracts where appropriate. Controllers await repository and service operations rather than blocking on asynchronous work.

This also allows the in-memory implementations to be replaced later by I/O-based persistence without requiring major changes to the API structure.

## Data Storage

The application currently uses seeded in-memory data for users and stokvels. Contributions and idempotency records are also stored in memory.

All in-memory data is reset whenever the application restarts.

No database or Entity Framework Core is used at this stage.

## Testing

The API is tested through Scalar.

The contribution endpoint was verified with the following scenarios:

- A valid contribution returns `201 Created`.
- Repeating the same request with the same `Idempotency-Key` returns the exact original contribution response.
- Reusing the same `Idempotency-Key` with a different payload returns `409 Conflict`.
- Using a new key for a member and cycle that already has a contribution returns `409 Conflict`.
- An invalid contribution amount returns `422 Unprocessable Entity`.
- An invalid cycle format returns `400 Bad Request`.
- Error responses use `application/problem+json`.