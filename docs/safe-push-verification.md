# Safe publication verification

The assignment branch was backed up locally before modifying unpublished history.
The original private cleanup documents were copied outside the repository and
their SHA-256 checksums verified against the originals. The backup branch is
local only and must not be pushed.

Both private documents first appeared in the final documentation commit. That
commit was rewritten to remove them entirely, replace cleanup details with
development-migration-status.md, sanitize references in README/verification
reports, and add ignore rules for private documents and database backups.
The preceding thirteen commits and all implementation code, migrations, tests
and scripts remain unchanged. Assignment reports remain available.

Every outgoing commit snapshot was inspected for private account identifiers,
exported email records, credential files, database backups, actual configured
signing-key/connection-string values, and token/key patterns. Fixed synthetic
test-fixture literals already present on remote main are distinguished from
private database exports; no private record identifiers are permitted. Actual
credential values are never included in scan output. The final check is rerun
after rewriting the commit and before pushing. These checks reduce publication
risk but cannot guarantee detection of every possible secret format.

Verification rerun:

```text
dotnet build RondiTrack.sln -m:1 --no-restore
Build succeeded. 0 warnings, 0 errors. Elapsed: 17.95 seconds.

dotnet test RondiTrack.Tests/RondiTrack.Tests.csproj --no-build --no-restore --logger "console;verbosity=minimal"
Passed: 62. Failed: 0. Skipped: 0. Duration: 7 seconds.
```

Only the named assignment branch is pushed with an explicit refspec; no force
push, main update, backup-branch publication or Git configuration change is used.
The development schema upgrade remains blocked by duplicate normalized emails;
no development database records were changed during publication preparation.
