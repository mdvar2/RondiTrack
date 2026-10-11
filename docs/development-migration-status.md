# Development migration status

The development database upgrade is blocked by duplicate normalized emails.
Development data remains unchanged. An approved resolution is required before
the unique-email migration can be applied. Implementation and tests are verified
against a disposable PostgreSQL database. Private cleanup records are kept outside
this repository and are excluded from published Git history.
