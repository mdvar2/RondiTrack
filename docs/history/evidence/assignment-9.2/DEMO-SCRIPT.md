# Assignment 9.2: five-minute demo script

**Before recording:** have these open, so nothing waits on a build during the take.
- **Terminal A**, in Git Bash: `. /c/Users/gwiny/AppData/Local/Temp/e2e/env.sh && cd /c/Users/gwiny/RondiTrack/web-next`. The e2e API stack is already running on :5147.
- **Terminal B**, the same, then `npm run build && npm run start`. Wait for "Ready".
- **Browser tabs:**
  1. the red run, https://github.com/mdvar2/RondiTrack/actions/runs/42517356298
  2. the green run, https://github.com/mdvar2/RondiTrack/actions/runs/42567622925
  3. PR #11, https://github.com/mdvar2/RondiTrack/pull/11
  4. `docs/history/evidence/assignment-9.2/05-lighthouse-home-after-applied.png`
  5. http://localhost:3000/login
- **Sign-in:** `perf.treasurer@example.test`. The password is `$E2E_PASSWORD` (`echo $E2E_PASSWORD` in Terminal A; don't show it on screen).

| Time | Show | Say |
|---|---|---|
| 0:00–0:20 | PR #11 tab | "Assignment 9.2: tests, performance, accessibility and privacy for RondiTrack. Everything is on PR #11." |
| 0:20–1:00 | Terminal A: `npm run test:jhb` | "171 unit and component tests, run under Johannesburg time and again under UTC in CI. Money is integer cents. R 150,50 parses to 15 050 cents, and 23:30 on the last day of the month stays in that month." |
| 1:00–1:40 | Terminal A: `npx playwright test e2e/journey.spec.ts e2e/roles.spec.ts` (about 20 s) | "End-to-end tests against a production build and a real API. The journey records R 150,50, and the total rises by exactly that. A Member sending the treasurer's close gets 403 from the API, not just a hidden button." |
| 1:40–2:40 | Browser: sign in, open the Masakhane stokvel. **Keyboard only:** Tab to Amount, press Record contribution with it empty, then type 150,50, choose a member, and record | "Keyboard only. The empty amount gives a field error; focus goes back to the field, and it's announced. A valid amount gives 'Recorded R 150,50 for…', announced politely. Then Close cycle: focus starts on Cancel, Escape closes it, and focus returns." |
| 2:40–3:20 | Lighthouse screenshot (05 png) | "Home screen on the final build: LCP 2.0 s, CLS 0, accessibility 100. axe runs in Playwright on every page, in light and dark mode, and serious or critical issues fail the build. **INP is not met:** 712 ms clicking straight after load, down from 1,216, and 280 ms once idle. The cause is hydration; it's documented in the README." |
| 3:20–4:00 | Terminal A: `curl -s -A "WhatsApp/2.23.20.0" "http://localhost:3000/join?code=<real code>" \| grep -iE "<title>\|og:\|robots"`, then `curl -si http://localhost:3000/robots.txt` | "A link preview sees 'You're invited to join a stokvel': no stokvel name, no amount, and noindex. Every signed-in page is noindex, and the BFF sends X-Robots-Tag." |
| 4:00–4:45 | Red run tab, then green run tab | "CI on every PR. The red PR renamed a button: the frontend tests failed, and end-to-end was skipped. The green run passes backend, frontend in two time zones, and end-to-end on a fresh database." |
| 4:45–5:00 | PR #11 | "Not done: the NVDA pass. There's a checklist for it in the repo. And INP is still above 200 ms." |

**Optional:** to show a red unit test, put a `throw` at the top of `parseRandsToCents` in `src/lib/money.ts`, run `npx vitest run src/lib/money.test.ts`, then undo the change. Don't commit it.
