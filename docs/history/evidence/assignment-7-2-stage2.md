# Assignment 7.2 stage-two changes and verification

This package is an incremental patch **on top of** the already applied frontend patch and the `assignment-7.2` branch. It does not replace the project or delete files.

## Backend

- `/api/v1/me/home` now includes `arrearsCents`, calculated from persisted `ContributionArrears` for the authenticated user and each stokvel. It does not sum contribution rows in React or disclose another user's arrears.
- The home service uses the Johannesburg month for current-cycle selection.
- `Assignment72ArrearsTests.cs` checks isolation and zero arrears.

## Frontend

- `StokvelCard` shows the signed-in user's arrears only when positive and explains the membership role in words.
- Sample fixtures include the required long stokvel and contributor names. They are *illustrative*, not captured Scalar output.
- Responsive CSS is appended to the existing stylesheet by the installation script; it must not replace `web/src/App.css`.

## Required after installation

1. Run backend tests, then start the updated API in Development mode and regenerate `web/src/api/schema.d.ts` from its OpenAPI endpoint.
2. Run all frontend checks.
3. Capture actual, anonymised authenticated API samples and provide a real persisted three-cycle (Open/Closed/PaidOut) + arrears scenario. Demo fixtures alone do not satisfy the seed requirement.
4. Capture both 360px screenshots and measure badge contrast in both themes. Record actual numbers, not guesses.
5. Complete StrictMode console evidence and README screenshots before final hand-in.

**Note:** This patch has not been executed in the user's .NET/Node environment. A failed check must be fixed before commit.
