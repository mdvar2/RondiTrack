# RondiTrack

RondiTrack is a .NET 10 Web API for managing users and stokvels. The application provides CRUD operations for both resources and supports stokvel membership management.

## Technologies

- .NET 10
- ASP.NET Core Web API
- Built-in OpenAPI support
- Scalar API Reference
- In-memory repositories

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

## Design Choices

### Controllers

Controllers were chosen instead of Minimal APIs because they keep the endpoints for each resource grouped together. This makes the API structure easier to follow and maintain as the application grows.

### Domain Models

The `User` and `Stokvel` models protect their state using private setters. Changes are made through domain methods such as `UpdateDetails`, `AddMember`, and `RemoveMember`.
Validation is kept close to the domain models so that invalid states are harder to create. For example, a stokvel contribution must be greater than zero, and the same user cannot be added to the same stokvel more than once.

### Money

`decimal` is used for `ContributionAmount` because it is appropriate for monetary values and avoids the binary floating-point precision issues associated with types such as `double`.

### Repository Abstraction

Data access is placed behind `IUserRepository` and `IStokvelRepository`. The current repository implementations use in-memory collections because persistent database storage is not required at this stage.
The repositories are registered as singletons so that the in-memory data remains available for the lifetime of the running application.

### Asynchronous Operations

Repository methods expose Task-based asynchronous contracts, and the controllers await repository operations. This allows the repository implementations to be replaced later by I/O-based data access without changing the controller contracts.

### Membership Rule

A user can belong to a stokvel, but the same user cannot be added to the same stokvel more than once. This rule is enforced by the `Stokvel` domain model.
Attempting to add a duplicate member returns HTTP `409 Conflict`.

## Data Storage

The application currently uses seeded in-memory data. Data is reset whenever the application restarts.
No database or Entity Framework Core is used at this stage.

