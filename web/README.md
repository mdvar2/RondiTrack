# RondiTrack — React Frontend

RondiTrack is a stokvel management application designed to help members view their savings groups, contribution cycles, payments, and upcoming payouts.

This frontend was developed as part of **Bitcube Backend Curriculum — Week 7, Assignment 7.1: StokvelCard & ContributionCard**.

## 1. Technology Stack

- React 19 with React Compiler
- TypeScript 5 in strict mode
- Vite 8
- Node.js 24
- CSS for responsive layouts
- OpenAPI TypeScript for generated API contracts
- Vitest for unit testing
- ESLint for code quality

Dependency versions are pinned in `package.json` and `package-lock.json`.

## 2. Implemented Features

### StokvelCard

Displays an overview of each savings group, including:

- Stokvel name and member role
- Current contribution cycle and status
- Contribution due date
- Amount paid and amount outstanding
- Next scheduled payout date
- Treasurer-only tools placeholder

The component handles the following scenarios:

- When no contribution cycle exists, payment amounts are hidden.
- When the outstanding amount is zero, the card displays "All paid up".
- When no payout date is available, the card displays "No payout scheduled".
- Treasurer-only information is not displayed to ordinary members.

### ContributionCard

Displays contribution information, including:

- Contribution amount in South African rand
- Recorded contribution status
- Contribution cycle
- Date and time recorded
- Contributor name when available in the supplied data

Contribution cards use stable contribution IDs as React keys.

### South African Formatting

The application includes reusable formatting utilities:

- `formatCents()` formats integer-cent amounts in ZAR.
- `formatJohannesburgDate()` formats dates for South Africa.
- `formatJohannesburgDateTime()` converts UTC timestamps into Africa/Johannesburg local time.
- `formatCycleDueDate()` formats calendar-based cycle due dates.

For example, the UTC timestamp `2026-10-31T22:30:00Z` displays as `01 Nov 2026, 00:30` in Johannesburg.

## 3. Frontend Data and API Contracts

The dashboard currently uses **illustrative typed sample data** from `src/data/sample.ts`. These fixtures are not verified captures of signed-in API responses. The 10-cent entry and status variants are UI demonstration cases, not assertions about persisted records.

It does not make live API requests, implement authentication, or submit contribution forms.

The frontend uses TypeScript interfaces generated from the backend's OpenAPI document. These are stored in:

`src/api/schema.d.ts`

Reusable type aliases are defined in:

`src/api/types.ts`

This approach allows the components to follow backend response contracts without requiring a live API connection during the demonstration.

## 4. Prerequisites

Install:

- Node.js 24
- npm
- Git

For regenerating API contracts, the RondiTrack backend must also be running and exposing its OpenAPI document at:

`http://localhost:8081/openapi/v1.json`

## 5. Installation

From the repository root:

```bash
cd web
npm ci
```

On Windows PowerShell, use `npm.cmd` if the PowerShell execution policy prevents `npm` from running.

```powershell
npm.cmd ci
```

## 6. Run the Frontend

Start the development server:

```powershell
npm.cmd run dev
```

Open:

`http://localhost:3000`

The Vite development server is configured to use port 3000.

The dashboard displays sample stokvels and recent contributions.

## 7. Generate OpenAPI Types

Start the backend before generating the TypeScript API contracts.

From the `web` directory:

```powershell
npx.cmd openapi-typescript http://localhost:8081/openapi/v1.json -o src/api/schema.d.ts
```

This retrieves the backend OpenAPI specification and regenerates:

`src/api/schema.d.ts`

The generated schema should be committed whenever relevant backend response contracts change. **Important:** Docker/WSL may already serve an older API on port 8080. For the updated local API, run `dotnet run --project API --urls http://localhost:8081` from the repository root and use the 8081 command above. The package script still targets port 8080 unless changed separately.

## 8. Testing and Validation

Run the frontend unit tests:

```powershell
npm.cmd run test -- --run
```

Check TypeScript types:

```powershell
npm.cmd run typecheck
```

Run ESLint:

```powershell
npm.cmd run lint
```

Create a production build:

```powershell
npm.cmd run build
```

During the Assignment 7.1 review, the user reported 14 passing frontend unit tests, a successful TypeScript check, a successful lint run, and a successful production build before the illustrative fixture updates. Re-run these commands after applying the review package.

The tests cover money formatting, invalid monetary inputs, Johannesburg timezone conversion, and date formatting.

## 9. Project Structure

```text
web/
├── public/
├── src/
│   ├── api/
│   │   ├── schema.d.ts
│   │   └── types.ts
│   ├── components/
│   │   ├── ContributionCard.tsx
│   │   └── StokvelCard.tsx
│   ├── data/
│   │   └── sample.ts
│   ├── lib/
│   │   ├── dates.ts
│   │   ├── dates.test.ts
│   │   ├── money.ts
│   │   └── money.test.ts
│   ├── App.tsx
│   ├── App.css
│   ├── index.css
│   └── main.tsx
├── package.json
├── package-lock.json
├── vite.config.ts
├── tsconfig.json
└── README.md
```

## 10. Responsive Design

The dashboard uses responsive CSS layouts for desktop, tablet, and mobile screen sizes.

On wider screens, stokvel and contribution cards appear in multiple columns. On smaller screens, the layout adjusts to fewer columns for improved readability.

## 11. Current Scope and Limitations

Assignment 7.1 focuses on displaying typed stokvel and contribution information.

The following functionality is outside the current frontend implementation:

- Live backend data fetching
- User login and authentication
- Creating or editing contributions through forms
- Executable Treasurer management actions
- Real-time payment updates

The Treasurer tools area is a visual placeholder only.

Contribution status is read from the generated API response contract and shown as text (for example, Pending, Paid, or Confirmed).

## 12. Repository

GitHub: https://github.com/mdvar2/RondiTrack

The backend is located in the repository root, while the React frontend is located in the `web` directory.

---

**Project:** RondiTrack

**Assignment:** Bitcube Backend Curriculum — Week 7, Assignment 7.1

**Frontend:** React, TypeScript, Vite

**Status:** Sample-data dashboard implemented and validated

## 13. Assignment handoff and remaining evidence

- `StokvelCard` and `ContributionCard` are presentational components; no frontend API calls are made.
- Treasurer-only content is hidden from Member cards. This is **not** a substitute for backend authorization; the backend must enforce permissions.
- Sample records are **illustrative**, not verified API captures. To satisfy an assignment requirement for actual API samples, capture authorized responses from `/api/v1/me/home` and the contribution-list endpoint, anonymize any personal details, and replace these fixtures without changing the generated types.
- Formatting helpers are reusable. React uses stable contribution IDs as keys. The cards are not individually memoized; add memoization only if profiling demonstrates a need.
- The contribution-list API supplies the status, while the displayed cycle period is derived from the matching sample stokvel. For a future live implementation, resolve the cycle period from its actual cycle identifier rather than assuming the current cycle.
- Backend status lifecycle and permissions should be verified against the running API; demonstration statuses do not establish actual persisted status transitions.
