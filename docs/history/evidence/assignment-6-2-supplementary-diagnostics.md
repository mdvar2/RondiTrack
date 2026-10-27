# Supplementary verification-only diagnostics

I kept the complete Stage 5 audit in the
[README appendix](../technical-notes-to-6.3.md#appendix-a-complete-popia-logging-audit).
This companion preserves later test/script sites. They are not new production
logger calls; raw fixture or third-party diagnostics are not published.

## Stage 7 additions

### Stage 7 entry 01

- **Individual site / method:** scripts/verify-assignment-6-2-isolation.ps1:15 / preflight /
  proof
- **Diagnostic:** PowerShell exception: Development connection configuration is required for
  the isolation proof.
- **Potential contents / risk:** Fixed operational guidance; no credentials or record values.
- **Treatment:** New verification-only site; not production logging.

### Stage 7 entry 02

- **Individual site / method:** scripts/verify-assignment-6-2-isolation.ps1:22 / preflight /
  proof
- **Diagnostic:** PowerShell exception: Isolation is restricted to the existing local
  development database.
- **Potential contents / risk:** Fixed operational guidance; no credentials or record values.
- **Treatment:** New verification-only site; not production logging.

### Stage 7 entry 03

- **Individual site / method:** scripts/verify-assignment-6-2-isolation.ps1:24 / preflight /
  proof
- **Diagnostic:** PowerShell exception: The PostgreSQL client is unavailable.
- **Potential contents / risk:** Fixed operational guidance; no credentials or record values.
- **Treatment:** New verification-only site; not production logging.

### Stage 7 entry 04

- **Individual site / method:** scripts/verify-assignment-6-2-isolation.ps1:40 / preflight /
  proof
- **Diagnostic:** PowerShell exception: Development must initially accept connections; no
  isolation change was made.
- **Potential contents / risk:** Fixed operational guidance; no credentials or record values.
- **Treatment:** New verification-only site; not production logging.

### Stage 7 entry 05

- **Individual site / method:** scripts/verify-assignment-6-2-isolation.ps1:42 / preflight /
  proof
- **Diagnostic:** PowerShell exception: Development has active sessions; no sessions will be
  terminated and no isolation change was made.
- **Potential contents / risk:** Fixed operational guidance; no credentials or record values.
- **Treatment:** New verification-only site; not production logging.

### Stage 7 entry 06

- **Individual site / method:** scripts/verify-assignment-6-2-isolation.ps1:46 / preflight /
  proof
- **Diagnostic:** PowerShell exception: Development connection admission could not be
  disabled.
- **Potential contents / risk:** Fixed operational guidance; no credentials or record values.
- **Treatment:** New verification-only site; not production logging.

### Stage 7 entry 07

- **Individual site / method:** scripts/verify-assignment-6-2-isolation.ps1:49 / preflight /
  proof
- **Diagnostic:** PowerShell exception: A development session appeared during preflight;
  isolation will be restored without terminating sessions.
- **Potential contents / risk:** Fixed operational guidance; no credentials or record values.
- **Treatment:** New verification-only site; not production logging.

### Stage 7 entry 08

- **Individual site / method:** scripts/verify-assignment-6-2-isolation.ps1:58 / preflight /
  proof
- **Diagnostic:** PowerShell exception: A real development connection was not rejected as
  expected; independence is not proven.
- **Potential contents / risk:** Fixed operational guidance; no credentials or record values.
- **Treatment:** New verification-only site; not production logging.

### Stage 7 entry 09

- **Individual site / method:** scripts/verify-assignment-6-2-isolation.ps1:79 / preflight /
  proof
- **Diagnostic:** PowerShell exception: The isolated suite failed; inspect the local test
  artifact without publishing raw diagnostics.
- **Potential contents / risk:** Fixed operational guidance; no credentials or record values.
- **Treatment:** New verification-only site; not production logging.

### Stage 7 entry 10

- **Individual site / method:** scripts/verify-assignment-6-2-isolation.ps1:86 /
  finally/restoration
- **Diagnostic:** PowerShell exception: RESTORATION FAILED: development connection admission
  must be restored before use.
- **Potential contents / risk:** Fixed operational guidance; no credentials or record values.
- **Treatment:** New verification-only site; not production logging.

### Stage 7 entry 11

- **Individual site / method:** scripts/verify-assignment-6-2-isolation.ps1:88 /
  finally/restoration
- **Diagnostic:** PowerShell exception: Development connectivity restoration could not be
  verified.
- **Potential contents / risk:** Fixed operational guidance; no credentials or record values.
- **Treatment:** New verification-only site; not production logging.

### Stage 7 entry 12

- **Individual site / method:** EndToEndLogLeakTests /
  LogCaptureAndLeakInspection_ObserveNestedScopesStateAndExceptionData
- **Diagnostic:** Synthetic InvalidOperationException and Information control log
- **Potential contents / risk:** Harmless control markers in state/scopes/exception data.
- **Treatment:** Test-only positive control, in-memory provider, no production sink; no actual
  sensitive values published.

## Stage 8 additions

### Stage 8 entry 01

- **Individual Stage 8 diagnostic site:** scripts/evidence/EvidenceRunner.cs:26
- **What it can contain / sensitivity:** `EVIDENCE_FAILED: verify local prerequisites; raw
  diagnostics were not published.`: fixed verification guidance/status; no credentials,
  identities or financial values.
- **Before / change:** New evidence-only site; unexpected exceptions are caught and their text
  is not printed.

### Stage 8 entry 02

- **Individual Stage 8 diagnostic site:** scripts/evidence/EvidenceRunner.cs:36
- **What it can contain / sensitivity:** `Snapshot requires a before or after label.`: fixed
  verification guidance/status; no credentials, identities or financial values.
- **Before / change:** New evidence-only site; unexpected exceptions are caught and their text
  is not printed.

### Stage 8 entry 03

- **Individual Stage 8 diagnostic site:** scripts/evidence/EvidenceRunner.cs:43
- **What it can contain / sensitivity:** `Snapshot target must be local development.`: fixed
  verification guidance/status; no credentials, identities or financial values.
- **Before / change:** New evidence-only site; unexpected exceptions are caught and their text
  is not printed.

### Stage 8 entry 04

- **Individual Stage 8 diagnostic site:** scripts/evidence/EvidenceRunner.cs:66
- **What it can contain / sensitivity:** `READ_ONLY_DEVELOPMENT_SNAPSHOT_CAPTURED`: fixed
  verification guidance/status; no credentials, identities or financial values.
- **Before / change:** New evidence-only site; unexpected exceptions are caught and their text
  is not printed.

### Stage 8 entry 05

- **Individual Stage 8 diagnostic site:** scripts/evidence/EvidenceRunner.cs:99
- **What it can contain / sensitivity:** `Evidence API exited before readiness.`: fixed
  verification guidance/status; no credentials, identities or financial values.
- **Before / change:** New evidence-only site; unexpected exceptions are caught and their text
  is not printed.

### Stage 8 entry 06

- **Individual Stage 8 diagnostic site:** scripts/evidence/EvidenceRunner.cs:102
- **What it can contain / sensitivity:** `Evidence API did not start.`: fixed verification
  guidance/status; no credentials, identities or financial values.
- **Before / change:** New evidence-only site; unexpected exceptions are caught and their text
  is not printed.

### Stage 8 entry 07

- **Individual Stage 8 diagnostic site:** scripts/evidence/EvidenceRunner.cs:141
- **What it can contain / sensitivity:** `A genuine payout SQL log was not captured.`: fixed
  verification guidance/status; no credentials, identities or financial values.
- **Before / change:** New evidence-only site; unexpected exceptions are caught and their text
  is not printed.

### Stage 8 entry 08

- **Individual Stage 8 diagnostic site:** scripts/evidence/EvidenceRunner.cs:151
- **What it can contain / sensitivity:** `Evidence refusal status mismatch.`: fixed
  verification guidance/status; no credentials, identities or financial values.
- **Before / change:** New evidence-only site; unexpected exceptions are caught and their text
  is not printed.

### Stage 8 entry 09

- **Individual Stage 8 diagnostic site:** scripts/evidence/EvidenceRunner.cs:153
- **What it can contain / sensitivity:** `Evidence correlation mismatch.`: fixed verification
  guidance/status; no credentials, identities or financial values.
- **Before / change:** New evidence-only site; unexpected exceptions are caught and their text
  is not printed.

### Stage 8 entry 10

- **Individual Stage 8 diagnostic site:** scripts/evidence/EvidenceRunner.cs:159
- **What it can contain / sensitivity:** `Required genuine log evidence was not captured.`:
  fixed verification guidance/status; no credentials, identities or financial values.
- **Before / change:** New evidence-only site; unexpected exceptions are caught and their text
  is not printed.

### Stage 8 entry 11

- **Individual Stage 8 diagnostic site:** scripts/evidence/EvidenceRunner.cs:161
- **What it can contain / sensitivity:** `Sensitive evidence value detected; no artifact
  written.`: fixed verification guidance/status; no credentials, identities or financial
  values.
- **Before / change:** New evidence-only site; unexpected exceptions are caught and their text
  is not printed.

### Stage 8 entry 12

- **Individual Stage 8 diagnostic site:** scripts/evidence/EvidenceRunner.cs:165
- **What it can contain / sensitivity:** `REAL_API_EVIDENCE_READY`: fixed verification
  guidance/status; no credentials, identities or financial values.
- **Before / change:** New evidence-only site; unexpected exceptions are caught and their text
  is not printed.

### Stage 8 entry 13

- **Individual Stage 8 diagnostic site:** scripts/verify-assignment-6-2-isolation.ps1 /
  FailAfterIsolation
- **What it can contain / sensitivity:** Fixed synthetic post-isolation exception; no records
  or supplied values.
- **Before / change:** New controlled failure to verify finally restoration, before tests
  start.

### Stage 8 entry 14

- **Individual Stage 8 diagnostic site:** scripts/evidence/capture-dashboard.py / three
  payout-view assertions and signal assertion
- **What it can contain / sensitivity:** UI presence, safe fixture exclusion and signal
  boolean checks; bare AssertionError has no payload. Playwright itself can emit selector/page
  diagnostics locally if it fails.
- **Before / change:** New optional screenshot verifier; no production sink and no raw browser
  diagnostics published.
