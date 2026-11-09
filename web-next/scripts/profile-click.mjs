// Where the time goes in one click (Part 5 diagnosis). Same conditions as
// scripts/inp.mjs (4x CPU, slow 4G, a 412 px phone), then a V8 CPU profile
// around a single click on the stokvel page's "Close cycle" button, totalled
// by function self-time. Not a measurement of INP: use inp.mjs for that.
//
//   APP_URL=... API_BASE_URL=... SESSION_SECRET=... E2E_PASSWORD=... STOKVEL_ID=... \
//   SETTLE_MS=5000 node scripts/profile-click.mjs [button name]
import { writeFileSync, mkdirSync } from 'node:fs'
import { chromium } from '@playwright/test'
import { sessionCookie } from './session-cookie.mjs'

const app = process.env.APP_URL ?? 'http://localhost:3000'
const api = process.env.API_BASE_URL ?? 'http://localhost:5147'
const buttonName = process.argv[2] ?? 'Close cycle'
const { cookie } = await sessionCookie({ api, email: 'perf.treasurer@example.test', password: process.env.E2E_PASSWORD, secret: process.env.SESSION_SECRET })

const browser = await chromium.launch()
const context = await browser.newContext({ viewport: { width: 412, height: 823 }, deviceScaleFactor: 1.75, isMobile: true, hasTouch: true })
await context.addCookies([{ name: 'session', value: cookie.slice('session='.length), url: app, httpOnly: true, secure: true, sameSite: 'Lax' }])
await context.addInitScript(() => {
  window.__events = []
  new PerformanceObserver((list) => {
    for (const e of list.getEntries()) if (e.interactionId) window.__events.push({ name: e.name, duration: e.duration, start: e.startTime, processingStart: e.processingStart, processingEnd: e.processingEnd })
  }).observe({ type: 'event', durationThreshold: 16, buffered: true })
})
const page = await context.newPage()
const cdp = await context.newCDPSession(page)
await cdp.send('Emulation.setCPUThrottlingRate', { rate: 4 })
await cdp.send('Network.enable')
await cdp.send('Network.emulateNetworkConditions', { offline: false, latency: 150, downloadThroughput: 1.6 * 1024 * 1024 / 8, uploadThroughput: 750 * 1024 / 8 })
await page.goto(`${app}/stokvels/${process.env.STOKVEL_ID}`, { waitUntil: 'networkidle' })
if (process.env.SETTLE_MS) await page.waitForTimeout(Number(process.env.SETTLE_MS))

const button = page.getByRole('button', { name: buttonName, exact: true })
await button.waitFor()
if (buttonName === 'Record contribution') {
  // The same entries inp.mjs makes before it presses Record contribution.
  await page.getByLabel('Member').selectOption({ index: 7 })
  await page.getByLabel(/amount/i).fill('250,00')
}
await cdp.send('Profiler.enable')
await cdp.send('Profiler.setSamplingInterval', { interval: 100 })
await cdp.send('Profiler.start')
await button.click()
await page.waitForTimeout(1500)
const { profile } = await cdp.send('Profiler.stop')
const events = await page.evaluate(() => window.__events)
await browser.close()

// Self time per function. Samples are in microseconds between timeDeltas.
const byId = new Map(profile.nodes.map((node) => [node.id, node]))
const self = new Map()
for (let i = 0; i < profile.samples.length; i++) {
  const node = byId.get(profile.samples[i])
  const dt = (profile.timeDeltas[i + 1] ?? 0) / 1000
  const f = node.callFrame
  const key = `${f.functionName || '(anonymous)'}  ${f.url.replace(/^.*\/_next\//, '_next/')}:${f.lineNumber + 1}:${f.columnNumber + 1}`
  self.set(key, (self.get(key) ?? 0) + dt)
}
const total = [...self.values()].reduce((a, b) => a + b, 0)
const top = [...self.entries()].filter(([k]) => !k.startsWith('(idle)') && !k.startsWith('(program)')).sort((a, b) => b[1] - a[1]).slice(0, 25)
mkdirSync('lighthouse-reports/profile', { recursive: true })
writeFileSync('lighthouse-reports/profile/click.cpuprofile', JSON.stringify(profile))
console.log('event timing for the click:', JSON.stringify(events.filter((e) => ['pointerdown', 'pointerup', 'click', 'mousedown', 'mouseup'].includes(e.name)).map((e) => ({ name: e.name, duration: Math.round(e.duration), handlerMs: Math.round(e.processingEnd - e.processingStart) }))))
console.log(`profile: ${Math.round(total)} ms sampled (idle ${Math.round(self.get([...self.keys()].find((k) => k.startsWith('(idle)'))) ?? 0)} ms)`)
for (const [k, ms] of top) console.log(`${ms.toFixed(1).padStart(7)} ms  ${k}`)
