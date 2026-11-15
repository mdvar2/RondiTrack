import { defineConfig, devices } from '@playwright/test'

// Runs against the production build (npm run build first) and the real API,
// database and seed from `docker compose up`. Nothing here is a real secret:
// CI generates SESSION_SECRET and E2E_PASSWORD fresh in each run.
const port = Number(process.env.PORT ?? 3000)
export const baseURL = `http://localhost:${port}`

// Assignment 9.3 Part 3: e2e/session-expiry.spec.ts runs against a second API
// instance whose access tokens last 1 minute, through its own Next.js server,
// so the other tests keep the normal 5-minute tokens. Only when configured.
const shortApi = process.env.SHORT_API_BASE_URL
const shortPort = 3001
const shortBaseURL = `http://localhost:${shortPort}`

export default defineConfig({
  testDir: './e2e',
  fullyParallel: true,
  forbidOnly: !!process.env.CI,
  retries: 0,
  workers: process.env.CI ? 2 : 4,
  reporter: [['list'], ['html', { open: 'never' }]],
  use: {
    baseURL,
    trace: 'retain-on-failure',
    screenshot: 'only-on-failure',
  },
  projects: [
    { name: 'setup', testMatch: /auth\.setup\.ts/ },
    { name: 'chromium', use: { ...devices['Desktop Chrome'] }, dependencies: ['setup'], testIgnore: [/auth\.setup\.ts/, /session-expiry\.spec\.ts/] },
    { name: 'session-expiry', use: { ...devices['Desktop Chrome'], baseURL: shortBaseURL }, testMatch: /session-expiry\.spec\.ts/ },
  ],
  webServer: [{
    command: 'npm run start',
    url: `${baseURL}/login`,
    // Locally, reuse a running `npm run start`. Never let it be `next dev`:
    // CI always starts its own.
    reuseExistingServer: !process.env.CI,
    timeout: 120_000,
    env: {
      PORT: String(port),
      API_BASE_URL: process.env.API_BASE_URL ?? 'http://localhost:5147',
      SESSION_SECRET: process.env.SESSION_SECRET ?? '',
      APP_URL: baseURL,
      // The simulated post-commit response failure (e2e/demo-fault.spec.ts).
      // Off unless the run sets RONDITRACK_DEMO_FAULTS=enabled; CI's throwaway
      // stack does, for its synthetic account only.
      RONDITRACK_ENVIRONMENT: process.env.RONDITRACK_ENVIRONMENT ?? '',
      RONDITRACK_DEMO_FAULTS: process.env.RONDITRACK_DEMO_FAULTS ?? '',
      RONDITRACK_DEMO_FAULT_SECRET: process.env.RONDITRACK_DEMO_FAULT_SECRET ?? '',
      RONDITRACK_DEMO_FAULT_ACCOUNTS: process.env.RONDITRACK_DEMO_FAULT_ACCOUNTS ?? '',
    },
  }, ...(shortApi ? [{
    command: 'npm run start',
    url: `${shortBaseURL}/login`,
    reuseExistingServer: !process.env.CI,
    timeout: 120_000,
    env: {
      PORT: String(shortPort),
      API_BASE_URL: shortApi,
      SESSION_SECRET: process.env.SESSION_SECRET ?? '',
      APP_URL: shortBaseURL,
    },
  }] : [])],
})
