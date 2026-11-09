// Where the time goes in the stokvel page's interactions (Part 5 diagnosis).
// Records a Chrome performance trace (the data behind the DevTools
// Performance panel) of the interactions scripts/inp.mjs makes, under the
// same conditions (4x CPU, slow 4G, a 412 px phone), then summarises each
// interaction:
//   - phases, from the browser's own Event Timing record: input delay,
//     event handlers, then waiting for the next frame;
//   - main-thread self time inside it: script, style + layout, paint;
//   - with a React profiling build (`next build --profile`), React's own
//     Scheduler tracks (Render, Commit, Hydrated, ...). A development build
//     also names each component; a production profiling build doesn't.
// It measures nothing for the report: inp.mjs does that.
//
//   APP_URL=... API_BASE_URL=... SESSION_SECRET=... E2E_PASSWORD=... STOKVEL_ID=... \
//   SETTLE_MS=0|5000 node scripts/trace-click.mjs <label>
//   ANALYSE_ONLY=1 node scripts/trace-click.mjs <label>   # re-read a saved trace
import { mkdirSync, readFileSync, writeFileSync } from 'node:fs'
import { join } from 'node:path'
import { chromium } from '@playwright/test'
import { sessionCookie } from './session-cookie.mjs'

const label = process.argv[2] ?? 'trace'
const app = process.env.APP_URL ?? 'http://localhost:3000'
const api = process.env.API_BASE_URL ?? 'http://localhost:5147'
const dir = join('lighthouse-reports', label)
const tracePath = join(dir, 'trace.json')

if (process.env.ANALYSE_ONLY !== '1') await record()
const summary = analyse(JSON.parse(readFileSync(tracePath, 'utf8')))
writeFileSync(join(dir, 'trace-summary.json'), JSON.stringify(summary, null, 2))
for (const row of summary.slice(0, 6)) console.log(JSON.stringify(row))

async function record() {
  const { cookie } = await sessionCookie({ api, email: 'perf.treasurer@example.test', password: process.env.E2E_PASSWORD, secret: process.env.SESSION_SECRET })
  const browser = await chromium.launch()
  const context = await browser.newContext({ viewport: { width: 412, height: 823 }, deviceScaleFactor: 1.75, isMobile: true, hasTouch: true })
  await context.addCookies([{ name: 'session', value: cookie.slice('session='.length), url: app, httpOnly: true, secure: true, sameSite: 'Lax' }])
  const page = await context.newPage()
  const cdp = await context.newCDPSession(page)
  await cdp.send('Emulation.setCPUThrottlingRate', { rate: 4 })
  await cdp.send('Network.enable')
  await cdp.send('Network.emulateNetworkConditions', { offline: false, latency: 150, downloadThroughput: 1.6 * 1024 * 1024 / 8, uploadThroughput: 750 * 1024 / 8 })

  mkdirSync(dir, { recursive: true })
  await browser.startTracing(page, { path: tracePath, categories: ['devtools.timeline', 'disabled-by-default-devtools.timeline', 'blink.user_timing', 'v8.execute', 'latencyInfo'] })
  await page.goto(`${app}/stokvels/${process.env.STOKVEL_ID}`, { waitUntil: 'networkidle' })
  if (process.env.SETTLE_MS) await page.waitForTimeout(Number(process.env.SETTLE_MS))

  // The interactions inp.mjs makes, in the same order.
  await page.getByRole('button', { name: 'Close cycle' }).click()
  await page.getByRole('dialog').waitFor()
  await page.getByRole('button', { name: 'Cancel' }).click()
  await page.getByLabel('Member').selectOption({ index: 7 })
  const amount = page.getByLabel(/amount/i)
  await amount.click()
  await amount.pressSequentially('250,00', { delay: 50 })
  await page.getByRole('button', { name: 'Record contribution' }).click()
  await page.locator('[role=alert], [role=status]').filter({ hasText: /recorded|could not|already|permission|Enter|refused/i }).first().waitFor({ timeout: 30000 })
  await page.waitForTimeout(500) // let the last frames reach the trace
  await browser.stopTracing()
  await browser.close()
}

function analyse(trace) {
  const events = trace.traceEvents ?? trace
  const mainThread = events.find((e) => e.name === 'thread_name' && e.args?.name === 'CrRendererMain' && events.some((x) => x.pid === e.pid && x.name === 'EventTiming'))
  const main = events.filter((e) => e.pid === mainThread?.pid && e.tid === mainThread?.tid)

  // One row per interaction: its longest Event Timing entry, whose begin
  // record carries the whole breakdown.
  const interactions = new Map()
  for (const begin of events.filter((e) => e.name === 'EventTiming' && e.ph === 'b' && e.args?.data?.interactionId)) {
    const d = begin.args.data
    const current = interactions.get(d.interactionId)
    if (current && current.duration >= d.duration) continue
    interactions.set(d.interactionId, {
      type: d.type,
      duration: d.duration,
      inputDelay: d.processingStart - d.timeStamp,
      processing: d.processingEnd - d.processingStart,
      presentation: d.timeStamp + d.duration - d.processingEnd,
      start: begin.ts,
      end: begin.ts + d.duration * 1000,
    })
  }

  // Self time: each instant of the main thread is charged to the innermost
  // event covering it, as in the DevTools Bottom-up view, so nothing is
  // counted twice. An event that outlives its parent is clipped to it.
  const tasks = main.filter((e) => e.ph === 'X' && e.dur > 0 && e.cat?.includes('devtools.timeline')).sort((x, y) => x.ts - y.ts || y.dur - x.dur)
  function selfTimes(from, to) {
    const totals = {}
    const add = (name, lo, hi) => {
      lo = Math.max(lo, from)
      hi = Math.min(hi, to)
      if (hi > lo) totals[bucket(name)] = (totals[bucket(name)] ?? 0) + (hi - lo) / 1000
    }
    const stack = []
    let cursor = -Infinity
    const popUntil = (t) => {
      while (stack.length && stack.at(-1).end <= t) {
        const top = stack.pop()
        add(top.name, cursor, top.end)
        cursor = top.end
      }
    }
    for (const e of tasks) {
      popUntil(e.ts)
      if (stack.length) add(stack.at(-1).name, cursor, e.ts)
      cursor = Math.max(cursor, e.ts)
      stack.push({ name: e.name, end: Math.min(e.ts + e.dur, stack.length ? stack.at(-1).end : Infinity) })
    }
    popUntil(Infinity)
    delete totals.other
    return totals
  }

  // React 19.2 profiling and development builds label their work with
  // console.timeStamp: TimeStamp events whose start and end are trace
  // timestamps, written after the commit.
  const reactMarks = events.filter((e) => e.name === 'TimeStamp' && e.pid === mainThread?.pid && typeof e.args?.data?.start === 'number')
  function reactWork(from, to) {
    const scheduler = {}
    const components = {}
    for (const e of reactMarks) {
      const d = e.args.data
      const overlap = (Math.min(d.end, to) - Math.max(d.start, from)) / 1000
      if (overlap <= 0) continue
      const isComponent = /Components/.test(d.track ?? '')
      const into = isComponent ? components : scheduler
      const key = isComponent ? d.message : `${d.track}: ${d.message}`
      into[key] = (into[key] ?? 0) + overlap
    }
    return { scheduler, components }
  }

  const rounded = (entries, n) => Object.fromEntries(Object.entries(entries).sort((x, y) => y[1] - x[1]).slice(0, n).map(([k, v]) => [k, Math.round(v)]))
  return [...interactions.entries()].sort((x, y) => y[1].duration - x[1].duration).map(([id, it]) => {
    const totals = selfTimes(it.start, it.end)
    const busy = Object.values(totals).reduce((x, y) => x + y, 0)
    const { scheduler, components } = reactWork(it.start, it.end)
    return {
      interactionId: id,
      type: it.type,
      durationMs: Math.round(it.duration),
      phases: { inputDelay: Math.round(it.inputDelay), processing: Math.round(it.processing), presentation: Math.round(it.presentation) },
      mainThread: { ...rounded(totals, 8), idle: Math.max(0, Math.round(it.duration - busy)) },
      reactSchedulerMs: rounded(scheduler, 8),
      reactComponentsMs: rounded(components, 12),
    }
  })
}

function bucket(name) {
  if (['EventDispatch', 'FunctionCall', 'TimerFire', 'FireAnimationFrame', 'v8.compile', 'v8.compileModule', 'EvaluateScript', 'v8.evaluateModule', 'RunMicrotasks', 'MinorGC', 'MajorGC', 'HandlePostMessage', 'V8.Execute'].includes(name)) return 'script'
  if (['UpdateLayoutTree', 'RecalculateStyles', 'Layout'].includes(name)) return 'style + layout'
  if (['Paint', 'PrePaint', 'Layerize', 'Commit', 'PaintImage', 'UpdateLayer', 'UpdateLayerTree', 'CompositeLayers'].includes(name)) return 'paint + composite'
  if (['ParseHTML', 'ParseAuthorStyleSheet'].includes(name)) return 'parse'
  return 'other'
}
