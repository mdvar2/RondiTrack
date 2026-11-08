// INP while recording a contribution and opening Close Cycle (Part 5).
// A page-load audit makes no interaction, so this drives the page with
// Playwright under Lighthouse's mobile conditions (4x CPU slowdown, slow 4G)
// and reads INP from the web-vitals library's onINP, plus every raw Event
// Timing entry over 16 ms so fast interactions are visible too.
//
//   APP_URL=... API_BASE_URL=... SESSION_SECRET=... E2E_PASSWORD=... STOKVEL_ID=... \
//   node scripts/inp.mjs <label>
import { readFileSync, mkdirSync, writeFileSync } from 'node:fs'
import { dirname, join } from 'node:path'
import { createRequire } from 'node:module'
import { chromium } from '@playwright/test'
import { sessionCookie } from './session-cookie.mjs'

const require = createRequire(import.meta.url)
const label = process.argv[2] ?? 'run'
const app = process.env.APP_URL ?? 'http://localhost:3000'
const api = process.env.API_BASE_URL ?? 'http://localhost:5147'
const runs = Number(process.env.RUNS ?? 3)
const webVitals = readFileSync(join(dirname(require.resolve('web-vitals')), 'web-vitals.attribution.iife.js'), 'utf8')

const { cookie } = await sessionCookie({ api, email: 'perf.treasurer@example.test', password: process.env.E2E_PASSWORD, secret: process.env.SESSION_SECRET })
const browser = await chromium.launch()
const results = []

for (let run = 1; run <= runs; run++) {
  const context = await browser.newContext({ viewport: { width: 412, height: 823 }, deviceScaleFactor: 1.75, isMobile: true, hasTouch: true })
  await context.addCookies([{ name: 'session', value: cookie.slice('session='.length), url: app, httpOnly: true, secure: true, sameSite: 'Lax' }])
  await context.addInitScript(`${webVitals}
    window.__inp = []; window.__events = [];
    webVitals.onINP((metric) => window.__inp.push({ value: metric.value, target: metric.attribution?.interactionTarget, type: metric.attribution?.interactionType, inputDelay: Math.round(metric.attribution?.inputDelay ?? 0), processing: Math.round(metric.attribution?.processingDuration ?? 0), presentation: Math.round(metric.attribution?.presentationDelay ?? 0), scripts: (metric.attribution?.longAnimationFrameEntries ?? []).flatMap((frame) => frame.scripts ?? []).map((script) => [script.invoker, Math.round(script.duration)]).slice(0, 5) }), { reportAllChanges: true });
    new PerformanceObserver((list) => { for (const e of list.getEntries()) if (e.interactionId) window.__events.push({ name: e.name, duration: e.duration, id: e.interactionId }) })
      .observe({ type: 'event', durationThreshold: 16, buffered: true });`)
  const page = await context.newPage()
  const cdp = await context.newCDPSession(page)
  await cdp.send('Emulation.setCPUThrottlingRate', { rate: 4 })
  await cdp.send('Network.enable')
  await cdp.send('Network.emulateNetworkConditions', { offline: false, latency: 150, downloadThroughput: 1.6 * 1024 * 1024 / 8, uploadThroughput: 750 * 1024 / 8 })

  await page.goto(`${app}/stokvels/${process.env.STOKVEL_ID}`, { waitUntil: 'networkidle' })
  // SETTLE_MS=5000 waits for hydration to finish before the first click, to
  // tell the cost of the interaction from the cost of hydrating on demand.
  if (process.env.SETTLE_MS) await page.waitForTimeout(Number(process.env.SETTLE_MS))
  // Interaction 1 and 2: open Close Cycle, then cancel it.
  await page.getByRole('button', { name: 'Close cycle' }).click()
  await page.getByRole('dialog').waitFor()
  await page.getByRole('button', { name: 'Cancel' }).click()
  // Interaction 3+: choose a member, type an amount, press Record contribution.
  const member = page.getByLabel('Member')
  const options = await member.locator('option').allTextContents()
  await member.selectOption({ index: Math.min(options.length - 1, 3 + run * 4) })
  const amount = page.getByLabel(/amount/i)
  await amount.click()
  await amount.pressSequentially((await amount.getAttribute('type')) === 'number' ? '25000' : '250,00', { delay: 50 })
  await page.getByRole('button', { name: 'Record contribution' }).click()
  // Wait for whichever live region gets words (they are always in the page, empty).
  await page.locator('[role=alert], [role=status]').filter({ hasText: /recorded|could not|already|permission|Enter|refused/i }).first().waitFor({ timeout: 30000 })
  // Make the last interaction reportable: web-vitals reports INP on page hide.
  await page.evaluate(() => document.dispatchEvent(new Event('visibilitychange')))
  await page.waitForTimeout(500) // let the final frames paint before reading entries (measurement script, not a test)
  const { inp, events } = await page.evaluate(() => ({ inp: window.__inp, events: window.__events }))
  const byInteraction = new Map()
  for (const event of events) {
    if ((byInteraction.get(event.id)?.duration ?? -1) < event.duration) byInteraction.set(event.id, event)
  }
  const worst = [...byInteraction.values()].sort((a, b) => b.duration - a.duration)[0]
  const result = { run, inpMs: inp.at(-1)?.value ?? null, inpTarget: inp.at(-1)?.target ?? null, inpParts: inp.at(-1) ? { inputDelay: inp.at(-1).inputDelay, processing: inp.at(-1).processing, presentation: inp.at(-1).presentation, scripts: inp.at(-1).scripts } : null, worstEventMs: worst?.duration ?? null, worstEvent: worst?.name ?? null, interactions: byInteraction.size }
  results.push(result)
  console.log(JSON.stringify(result))
  await context.close()
}
await browser.close()

const values = results.map((result) => result.inpMs ?? result.worstEventMs ?? 0).sort((a, b) => a - b)
const summary = { label, runs: results, medianInpMs: values[Math.floor(values.length / 2)] }
mkdirSync(join('lighthouse-reports', label), { recursive: true })
writeFileSync(join('lighthouse-reports', label, 'inp.json'), JSON.stringify(summary, null, 2))
console.log(JSON.stringify(summary, null, 2))
