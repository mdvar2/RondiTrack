# Assignment 9.2: submission package

- **Branch:** `assignment-9.2`.
- **Verified code commit:** `4cbf9e4`. All three CI jobs passed.
- **Pull request:** https://github.com/mdvar2/RondiTrack/pull/11 (`assignment-9.2` → `assignment-9.1`)
- **Full write-up:** `web-next/README.md` (the "Assignment 9.2" section) and `docs/history/assignment-9.2-pr.md`.

## CI evidence

| Run | Link | Result |
|---|---|---|
| Red, PR #10 (`assignment-9.2-ci-red`, renamed button, not to be merged) | https://github.com/mdvar2/RondiTrack/actions/runs/42517356298 | Frontend **failed** at "Vitest, TZ=UTC": 10 × `Unable to find … button "Record contribution"`. E2E **skipped**. Backend passed |
| Green, PR #11, commit `4cbf9e4` | https://github.com/mdvar2/RondiTrack/actions/runs/42567622925 | Backend, Frontend and End to end **all passed** |

## Screenshots in this folder

All of these were captured on 2026-11-09 from real pages or saved results. Nothing was edited or mocked up.

| File | What it shows | Source |
|---|---|---|
| `01-ci-red-run-42517356298.png` | Red run: Frontend failed, E2E skipped, the failing test annotations | GitHub run page |
| `02-ci-red-frontend-job.png` | The failed Frontend job | GitHub job page |
| `03-ci-green-run-42567622925.png` | Green run: all three jobs passed | GitHub run page |
| `04-pr-11.png` | PR #11 | GitHub PR page |
| `05-lighthouse-home-after-applied.png` | Home screen on the final build: LCP **2.0 s**, CLS **0**, Accessibility **100**, SEO 63 (signed-in pages are `noindex` on purpose). Applied (devtools) throttling | One saved Lighthouse 13.5.0 run, `lhh-after-r1/home-1.json`, 2026-11-09 |
| `06-lighthouse-stokvel-after.png` | 40-member stokvel page on the final build: LCP 2.9 s (simulated), CLS 0 | One saved run, `lh-after-r1/stokvel-1.json` |
| `07-playwright-report.png` | Playwright HTML report: **7 keyboard-spec tests** (with sign-in setup), all passed, run 2026-11-08 | `web-next/playwright-report/index.html`. The full 25-test runs used the line reporter, so no HTML report of them exists |

The Lighthouse screenshots are **single runs**. The reported figures are medians, in `web-next/README.md`.

## Results, stated plainly

- **Tests on `4cbf9e4`:**
  - Vitest: 171/171, in 10 runs in a row alternating `TZ=UTC` and `TZ=Africa/Johannesburg`.
  - Playwright: 25/25, in 3 runs in a row.
  - Backend: 159/159.
  - Lint, typecheck and build are clean, and `npm audit` reports 0 vulnerabilities.
- **INP: NOT MET.** The target is 200 ms. It is **712 ms** clicking right after load (from 1,216 ms) and **280 ms** once the page is idle (no change), on the 40-member stokvel at 4× CPU and slow 4G. The cause and the fixes are documented.
- **Home screen LCP and CLS:** applied throttling gives a median of 1.93 s (all 10 runs under 2.5 s) and CLS 0. Lighthouse's *simulated* LCP is 2.61 s today, for both the old and new builds.

## Missing evidence

| Item | Status |
|---|---|
| **NVDA screen-reader pass** | **Not done.** NVDA wasn't installed, and nothing here could listen to it. Steps: `docs/history/assignment-9.2-screen-reader-checklist.md` |
| **Five-minute demo recording** | **Not recorded.** Use the narration script in `DEMO-SCRIPT.md`, in this folder |
| Playwright trace of the journey | Not kept: traces are only kept when a test fails |
| Lighthouse "before" screenshot | Not captured. Only the numbers are in the docs |
| Lighthouse HTML reports | Not included: the saved reports contain the signed-in session cookie |
