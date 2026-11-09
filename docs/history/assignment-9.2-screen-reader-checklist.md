# Assignment 9.2: NVDA screen-reader check

**Status: not done yet.** NVDA isn't installed on the machine the rest of the work was done on, and nobody has listened to it there, so nothing below has been heard yet. Fill in the "NVDA said" column word for word, as you hear it or as NVDA's Speech Viewer shows it.

## Set-up (about 10 minutes)

1. Install NVDA from nvaccess.org (current stable).
2. In NVDA, open **Tools → Speech Viewer**. It shows every utterance as text, which you can copy into the table.
3. Optional, for proof: **NVDA menu → Preferences → Settings → General → Logging level: Input/output**. NVDA's log (`%TEMP%\nvda.log`) then lists every utterance after a `Speaking` line.
4. Start the API and a production build of the app, as in `web-next/README.md` ("Running the tests"). Use Chrome or Edge, signed in as a treasurer of a stokvel with an open cycle and at least two members (`npm run seed:perf` makes one: sign in as `perf.treasurer@example.test`).
5. Start screen recording if this is also the demo take.

Use only the keyboard: **Tab** and **Shift+Tab** to move, **Enter** and **Space** to activate. **NVDA+Space** switches between browse and focus mode if NVDA stops passing keys to a field.

## What to check

The "Expected" column is the text the app produces (from the code). NVDA adds the role and state words itself ("edit", "invalid entry", "button", "unavailable"), so it won't be word for word. What matters: the message is spoken, it's spoken once, and it names the right field.

| # | Do this | Expected (from the code) | NVDA said | Pass? |
|---|---|---|---|---|
| 1 | Open the stokvel page. Tab to the **Amount** field. | Label "Amount", then the hint "In rands, for example 150,50" (it's the field's description) | | |
| 2 | Leave Amount empty and press **Record contribution**. | Focus returns to Amount, now marked invalid, and NVDA reads "Enter the amount that was paid." (the field's description) | | |
| 3 | Type `150,505` and press Record contribution. | Amount, invalid, "Enter an amount in rands with at most two decimal places, like 150,50." | | |
| 4 | Type `1000000,01` and press Record contribution. | Amount, invalid, "A contribution can be at most R 1 000 000,00." | | |
| 5 | Choose a member, type `150,50`, press Record contribution. | The button says "Recording…" while it waits. Then the status region is announced politely: "Recorded R 150,50 for *name*." (or "…for you.") | | |
| 6 | **The amount utterance the assignment asks for.** Move to that member's row in "This cycle's contributions" (browse mode, arrow keys). | "R 150,50". Note how NVDA reads the rand amount (for example "R one hundred fifty comma five zero") | | |
| 7 | **The date utterance.** On the next line of the same row. | "Recorded 09 Nov 2026, 14:05" style (SAST). Note how NVDA reads it | | |
| 8 | Press **Close cycle**. | A dialog named "Close 2026-11" (or "Close cycle" while loading); focus on **Cancel**; "Loading the close preview…" is announced; then the total. Escape closes it, and focus goes back to Close cycle | | |
| 9 | Open an invite link: `/join?code=<code>`. | Page heading "Join a stokvel"; focus lands on "Step 1 of 3: Enter your invite code" (or step 2 if the link already has a valid code) | | |
| 10 | Continue to step 2, then 3. | Each step moves focus to its heading: "Step 2 of 3: Review the rules", "Step 3 of 3: Confirm your join". The tab title changes too | | |
| 11 | On step 2, use **Back to the code** or the browser's Back. | Step 1 again, with the code already filled in (not asked for twice) | | |

## The date field the task mentions

The contribution form has **no date field**: a contribution is dated by the API when it's recorded. The only date inputs in the app are the **From** and **To** filters on the home screen's activity feed. They are native date inputs with visible labels, and they have **no validation message** (a "From" after "To" just shows no rows). So there's no date validation error to hear. If one is wanted, it's a feature to add first, not something to test now.

For the record, on the home screen: Tab to **From**, and note what NVDA says for the label and for the date-picker parts (day, month, year).

## What to keep as evidence

- The completed table above.
- The two utterances from rows 6 and 7, copied into `web-next/README.md` under "Screen reader" (replacing "_to be recorded_").
- Either the Speech Viewer text, copied, or the lines after `Speaking` in `nvda.log` for those steps.
- Optionally, the screen recording, with NVDA's speech audible.
