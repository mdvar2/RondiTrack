// Lighthouse 13.5.0, mobile profile (simulated slow 4G, 4x CPU slowdown),
// three runs per page against `npm run start`, reporting each run and the
// median. Signed-in pages get a session cookie made by session-cookie.mjs.
//
//   APP_URL=http://localhost:3000 API_BASE_URL=http://localhost:5147 \
//   SESSION_SECRET=... E2E_PASSWORD=... STOKVEL_ID=... \
//   node scripts/lighthouse.mjs <label>
//
// Reports go to lighthouse-reports/<label>/ (git-ignored).
import { execFileSync } from 'node:child_process'
import { mkdirSync, readFileSync, writeFileSync } from 'node:fs'
import { join } from 'node:path'
import { sessionCookie } from './session-cookie.mjs'

const label = process.argv[2] ?? 'run'
const app = process.env.APP_URL ?? 'http://localhost:3000'
const api = process.env.API_BASE_URL ?? 'http://localhost:5147'
const runs = Number(process.env.RUNS ?? 3)
const outDir = join('lighthouse-reports', label)
mkdirSync(outDir, { recursive: true })

const pages = [
  { name: 'login', path: '/login', signedIn: false },
  { name: 'home', path: '/dashboard/member', signedIn: true },
  { name: 'stokvel', path: `/stokvels/${process.env.STOKVEL_ID}`, signedIn: true },
]

const median = (values) => [...values].sort((a, b) => a - b)[Math.floor(values.length / 2)]
const summary = []

for (const page of pages) {
  let headersFile
  if (page.signedIn) {
    // A fresh sign-in per page keeps the 5-minute access token well inside its life.
    const { cookie } = await sessionCookie({ api, email: 'perf.treasurer@example.test', password: process.env.E2E_PASSWORD, secret: process.env.SESSION_SECRET })
    headersFile = join(outDir, '.headers.json')
    writeFileSync(headersFile, JSON.stringify({ Cookie: cookie }))
  }
  const results = []
  for (let run = 1; run <= runs; run++) {
    const output = join(outDir, `${page.name}-${run}.json`)
    execFileSync('npx', ['--yes', 'lighthouse@13.5.0', `${app}${page.path}`,
      '--only-categories=performance,accessibility,seo',
      '--output=json', `--output-path=${output}`, '--quiet',
      // THROTTLING=devtools applies the same slow 4G and 4x CPU to the real
      // page load instead of simulating it. The simulation can't replay
      // streamed HTML or a late font, so CLS needs both views.
      ...(process.env.THROTTLING === 'devtools' ? ['--throttling-method=devtools'] : []),
      '--chrome-flags=--headless=new --no-first-run --disable-extensions',
      ...(headersFile ? [`--extra-headers=${headersFile}`] : [])], { stdio: 'inherit', shell: process.platform === 'win32' })
    const report = JSON.parse(readFileSync(output, 'utf8'))
    const audits = report.audits
    // Lighthouse 13 names the LCP element in the LCP breakdown insight.
    const lcpElement = audits['lcp-breakdown-insight']?.details?.items?.find((item) => item.type === 'node')
    const scripts = (audits['network-requests']?.details?.items ?? []).filter((item) => item.resourceType === 'Script')
    const result = {
      run,
      finalUrl: report.finalDisplayedUrl,
      performance: Math.round(report.categories.performance.score * 100),
      accessibility: Math.round(report.categories.accessibility.score * 100),
      seo: Math.round(report.categories.seo.score * 100),
      lcpMs: Math.round(audits['largest-contentful-paint'].numericValue),
      fcpMs: Math.round(audits['first-contentful-paint'].numericValue),
      cls: Number(audits['cumulative-layout-shift'].numericValue.toFixed(3)),
      tbtMs: Math.round(audits['total-blocking-time'].numericValue),
      jsTransferKb: Math.round(scripts.reduce((sum, item) => sum + (item.transferSize ?? 0), 0) / 1024),
      totalTransferKb: Math.round(audits['total-byte-weight'].numericValue / 1024),
      lcpElement: lcpElement ? `${lcpElement.nodeLabel} [${lcpElement.selector}]` : null,
    }
    results.push(result)
    console.log(page.name, JSON.stringify(result))
  }
  const pick = (key) => median(results.map((result) => result[key]))
  summary.push({
    page: page.name, path: page.path,
    performance: pick('performance'), accessibility: pick('accessibility'), seo: pick('seo'),
    lcpMs: pick('lcpMs'), fcpMs: pick('fcpMs'), cls: pick('cls'), tbtMs: pick('tbtMs'),
    jsTransferKb: pick('jsTransferKb'), totalTransferKb: pick('totalTransferKb'),
    lcpElements: [...new Set(results.map((result) => result.lcpElement))],
    finalUrls: [...new Set(results.map((result) => result.finalUrl))],
    runs: results.map(({ lcpMs, cls, performance }) => ({ lcpMs, cls, performance })),
  })
}

writeFileSync(join(outDir, 'summary.json'), JSON.stringify(summary, null, 2))
console.log(JSON.stringify(summary, null, 2))
