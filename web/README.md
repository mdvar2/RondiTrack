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

The dashboard currently uses **typed sample data** from `src/data/sample.ts`.

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

`http://localhost:8080/openapi/v1.json`

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
npm.cmd run generate:api
```

This retrieves the backend OpenAPI specification and regenerates:

`src/api/schema.d.ts`

The generated schema should be committed whenever relevant backend response contracts change.

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

At the time of the Assignment 7.1 implementation, all 14 frontend unit tests passed, along with TypeScript checking, linting, and the production build.

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

The contribution status is displayed as "Recorded" because the current contribution response contract does not provide a separate status field.

## 12. Repository

GitHub: https://github.com/mdvar2/RondiTrack

The backend is located in the repository root, while the React frontend is located in the `web` directory.

---

**Project:** RondiTrack

**Assignment:** Bitcube Backend Curriculum — Week 7, Assignment 7.1

**Frontend:** React, TypeScript, Vite

**Status:** Sample-data dashboard implemented and validated
