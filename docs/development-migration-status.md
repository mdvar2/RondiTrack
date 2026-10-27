# Development migration status

## Current status: all migrations applied (verified 27 October 2026)

A read-only check against the local development database (`ronditrack` on
`localhost:5432`, the connection the Development API uses) found:

| Check | Result |
|---|---|
| Migrations in `Infrastructure/Migrations` | 11 |
| Applied (`__EFMigrationsHistory`) | 11, including `20261011174110_AddUserAuthentication` |
| Pending | 0 |
| Model changes without a migration (`has-pending-model-changes`) | None |

Commands used (they only read the database):

```powershell
$env:ASPNETCORE_ENVIRONMENT = "Development"
dotnet ef migrations list --project Infrastructure --startup-project API --no-build
dotnet ef migrations has-pending-model-changes --project Infrastructure --startup-project API --no-build
```

This only describes that one local database. A different database, such as the Docker
Compose volume, has its own migration history; Compose applies migrations with its `migrate`
service.

## Earlier status (11 October 2026), now historical

> The development database upgrade is blocked by duplicate normalized emails.
> Development data remains unchanged. An approved resolution is required before
> the unique-email migration can be applied. Implementation and tests are verified
> against a disposable PostgreSQL database. Private cleanup records are kept outside
> this repository and are excluded from published Git history.

The repository doesn't record when or how the duplicate emails were resolved; the check above
only shows that the authentication migration has since been applied.
