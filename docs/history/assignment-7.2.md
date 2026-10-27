# Assignment 7.2 — A Styled, Responsive Stokvel View

| | |
|---|---|
| Official title | "A Styled, Responsive Stokvel View", as in the README heading. The brief isn't in the repository. |
| Date | 21 October 2026 |
| Final commit on `main` | [`e303c6c`](https://github.com/mdvar2/RondiTrack/commit/e303c6c) "docs: update README for Assignment 7.2" (code: [`7d5e587`](https://github.com/mdvar2/RondiTrack/commit/7d5e587)) |
| Branch / PR | `assignment-7.2`; no PR |
| README as submitted | [README at `e303c6c`](https://github.com/mdvar2/RondiTrack/blob/e303c6c/README.md), and [`web/README.md` at that stage](web-readme-assignment-7.2.md) |
| Re-tested | **No** |

## 1. Purpose and learning objectives
To turn the 7.1 components into a responsive, themed dashboard: cycles grouped by status, status and arrears badges, and Johannesburg due-date countdowns. The brief's learning objectives aren't recorded.

## 2. Original requirements
The brief isn't in the repository. The records mention required evidence that was **not all verified**: "mobile screenshots, contrast measurements and other assignment evidence". They also mention required sample cases ("the required long stokvel and contributor names", in the [stage 2 notes](evidence/assignment-7-2-stage2.md)).

## 3. What I implemented
See [Implemented features](#implemented-features) below:
- Tailwind CSS 4 and shadcn/ui components.
- Light, Dark and System themes.
- Cycles grouped and ordered (Open, Closed, PaidOut) with expand and collapse.
- Status and arrears badges, and due-date countdowns.
- **API:** `/me/home` gained `arrearsCents`, taken from persisted arrears, for the signed-in member only (`Assignment72ArrearsTests.cs`).

## 4. Technologies, tools and dependencies
New in `web/package.json` at `e303c6c`:
- tailwindcss and @tailwindcss/vite 4.3.3
- @base-ui/react 1.9.0 (the shadcn primitives)
- class-variance-authority 0.7.1, clsx 2.1.1, tailwind-merge 3.7.0
- lucide-react 1.60.0
- @fontsource-variable/geist 5.3.0, tw-animate-css 1.4.0

## 5. Prerequisites and installation
Node.js 24.

## 6. Environment configuration
None. The dashboard still used sample data, and the theme choice was kept in browser storage.

## 7. How to run or reproduce
**As submitted:** [Running the frontend](#running-the-frontend) below (`npm ci`, `npm run dev`, on port 3000). Use `git worktree add ..\RondiTrack-7.2 e303c6c` to try that version. **Not re-tested.**

**In the current codebase:** all of these features are in `web/` today, on live data. See [web/README.md](../../web/README.md).

## 8. Important files and components
- `web/src/components/ContributionList.tsx`, `StatusBadge.tsx`, `DueCountdown.tsx`
- `web/src/lib/groupContributions.ts`, `dates.ts` (each with tests)
- `web/src/data/sampleCycles.ts`
- `web/vite.config.ts` (port 3000)
- `API/Services/MemberHomeService.cs`

## 9. How it works
Contributions are grouped by cycle and ordered by status. The countdown counts whole Johannesburg calendar days, and re-renders at the next Johannesburg midnight. Arrears come from the server, never added up in the browser.

## 10. Testing and recorded results
| Check | Recorded result |
|---|---|
| Backend | 132 passed |
| `web/` | **23 passed**. TypeScript, ESLint and build passed. Production dependency audit: 0 vulnerabilities |

## 11. Challenges and solutions
Recorded as [limitations](#current-limitations): the evidence above wasn't all verified. The [7.2 web README](web-readme-assignment-7.2.md#evidence-still-required-before-full-sign-off) lists exactly what was outstanding.

## 12. Learning outcomes
Not recorded as such. The [7.2 web README](web-readme-assignment-7.2.md#assignment-72-design-decisions) explains the design decisions on grouping state, dates, badges and theme storage.

## 13. Evidence, commits and pull requests
- [Stage 2 notes](evidence/assignment-7-2-stage2.md)
- [The 7.2 `web/README.md`](web-readme-assignment-7.2.md)
- Commits: [`8ed0467`](https://github.com/mdvar2/RondiTrack/commit/8ed0467), [`7d5e587`](https://github.com/mdvar2/RondiTrack/commit/7d5e587), [`e303c6c`](https://github.com/mdvar2/RondiTrack/commit/e303c6c)

## 14. Relationship to other assignments
- **Before:** [7.1](assignment-7.1.md)'s components.
- **Next:** [7.3](assignment-7.3.md) connected them to live data.
- **Later:** [8.1](assignment-8.1.md) reused `StatusBadge`, `DueCountdown`, the date helpers and the theme tokens.

## 15. Original record (as written at the time)

> **Historical record.** This was the Assignment 7.2 section of the root README. It was moved here unchanged (apart from Markdown formatting) on 27 October 2026. Commands, ports and test counts are as recorded at the time and may be out of date. For the current project, see the [root README](../../README.md).

Assignment 7.2 extends the RondiTrack React frontend with responsive styling, contribution-cycle grouping, status badges, South African due-date countdowns, and theme selection.

### Implemented features

- Responsive StokvelCard and ContributionCard components.

- Light, Dark and System theme options.

- Contribution cycles grouped and ordered, including Open, Closed and PaidOut statuses.

- Expandable contribution-cycle sections.

- Contribution status badges for Pending, Paid and Confirmed.

- Arrears indicators for members with outstanding contributions.

- South African currency and Johannesburg-local date formatting.

- Due-date countdowns based on Johannesburg calendar days.

- Additional backend support for retrieving the authenticated member's persisted arrears.

- Automated tests for contribution grouping, date boundaries and arrears behaviour.

### Running the frontend

From the repository root:

```powershell
cd web
npm ci
npm run dev
```

Open the local URL displayed by Vite, normally `http://localhost:3000/`.

### Verification

The latest local verification completed successfully:

- Backend: 132 tests passed.

- Frontend: 23 tests passed.

- TypeScript, ESLint and production build passed.

- Production dependency audit: 0 vulnerabilities.

### Current limitations

The dashboard uses illustrative demonstration data rather than live authenticated API requests. The interface and backend implementation should be reviewed separately when verifying database-backed scenarios.

The contribution-group controls and the required mobile screenshots, contrast measurements and other assignment evidence have not all been verified as fully compliant.

### Further documentation

See [Assignment 7.2 frontend README](../../web/README.md) and [Assignment 7.2 backend/UI notes](evidence/assignment-7-2-stage2.md).
