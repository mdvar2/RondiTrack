import { defineConfig, devices } from '@playwright/test'

// Runs against the production build (npm run build first) and the real API,
// database and seed from `docker compose up`. Nothing here is a real secret:
// CI generates SESSION_SECRET and E2E_PASSWORD fresh in each run.
const port = Number(process.env.PORT ?? 3000)
export const baseURL = `http://localhost:${port}`

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
    { name: 'chromium', use: { ...devices['Desktop Chrome'] }, dependencies: ['setup'], testIgnore: /auth\.setup\.ts/ },
  ],
  webServer: {
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
    },
  },
})
