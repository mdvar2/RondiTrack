import { execFileSync } from 'node:child_process'
import { randomUUID } from 'node:crypto'
import { expect, test, type Page } from '@playwright/test'
import { jwtDecrypt } from 'jose'
import { password } from './support'

// Assignment 9.3 Part 3 (and Part 5's "signed in past the token's lifetime"),
// against a second API instance whose access tokens genuinely last 1 minute
// (Jwt__AccessTokenMinutes=1, Jwt__ClockSkewSeconds=0) and its own Next.js
// server on :3001. Token validation is unchanged; only the lifetime is short.
// The steps share one account and one browser page, in order:
//   429 for invite-code guesses, staying signed in past the token's lifetime,
//   401 when the session has ended elsewhere (back to the same page),
//   429 for sign-ins, and 500/no answer with a real trace reference.
const SHORT_API = process.env.SHORT_API_BASE_URL?.replace(/\/$/, '')
const SHORT_API_CONTAINER = process.env.SHORT_API_CONTAINER
test.skip(!SHORT_API || !SHORT_API_CONTAINER, 'Needs the short-token API instance (SHORT_API_BASE_URL, SHORT_API_CONTAINER).')
test.describe.configure({ mode: 'serial' })

const email = `expiry.${randomUUID().slice(0, 8)}@example.test`
const TRACE_ID = /[0-9a-f]{32}/

async function apiCall<T>(path: string, init: RequestInit = {}): Promise<{ status: number; body: T; headers: Headers }> {
  const response = await fetch(`${SHORT_API}${path}`, { ...init, headers: { 'Content-Type': 'application/json', ...init.headers } })
  const text = await response.text()
  return { status: response.status, body: (text ? JSON.parse(text) : undefined) as T, headers: response.headers }
}

async function signIn(page: Page) {
  await page.getByLabel('Email').fill(email)
  await page.getByLabel('Password').fill(password())
  await page.getByRole('button', { name: 'Sign in' }).click()
}

async function waitForApi(up: boolean) {
  for (let i = 0; i < 60; i++) {
    const ok = await fetch(`${SHORT_API}/health/ready`).then((r) => r.ok, () => false)
    if (ok === up) return
    await new Promise((resolve) => setTimeout(resolve, 1000))
  }
  throw new Error(`Short API never became ${up ? 'ready' : 'unreachable'}.`)
}

let page: Page
let stokvelPath = ''

test.beforeAll(async ({ browser }) => {
  page = await browser.newPage()
  expect((await apiCall('/api/v1/auth/register', { method: 'POST', body: JSON.stringify({ name: 'Expiry Tester', email, password: password() }) })).status).toBe(201)
  const login = await apiCall<{ accessToken: string }>('/api/v1/auth/login', { method: 'POST', body: JSON.stringify({ email, password: password() }) })
  const auth = { Authorization: `Bearer ${login.body.accessToken}` }
  const stokvel = await apiCall<{ id: string }>('/api/v1/stokvels', { method: 'POST', headers: auth, body: JSON.stringify({ name: `Expiry ${randomUUID().slice(0, 6)}`, contributionAmount: 500 }) })
  const period = new Date(Date.now() + 2 * 3600 * 1000).toISOString().slice(0, 7)
  await apiCall('/api/v1/stokvels/' + stokvel.body.id + '/cycles', { method: 'POST', headers: auth, body: JSON.stringify({ period, targetAmount: 5000 }) })
  stokvelPath = `/stokvels/${stokvel.body.id}`

  // Ten wrong invite codes, as this account, through the API.
  for (let i = 0; i < 10; i++) {
    expect((await apiCall(`/api/v1/invites/WRONG${i}`, { headers: auth })).status).toBe(404)
  }
})

test.afterAll(async () => {
  // Leave the instance running for anything after us.
  execFileSync('docker', ['start', SHORT_API_CONTAINER!], { stdio: 'ignore' })
  await page.close()
})

test('429 for invite-code guesses: the wait, in seconds, from Retry-After', async () => {
  await page.goto('/login')
  await signIn(page)
  await expect(page).toHaveURL(/\/dashboard\/member$/)
  await page.goto('/join?code=WRONG99')
  await expect(page.getByText(/^Too many code checks\. Try again in \d+ seconds\.$/)).toBeVisible()
})

test('signed in past the access token\'s lifetime: the page carries on without signing in again', async () => {
  test.setTimeout(120_000)
  await page.goto(stokvelPath)
  await expect(page.getByRole('heading', { level: 1 })).toBeVisible()
  await page.waitForTimeout(65_000) // the 1-minute token has expired
  await page.goto(stokvelPath)
  await expect(page).toHaveURL(new RegExp(`${stokvelPath}$`))
  await expect(page.getByRole('button', { name: 'Record contribution' })).toBeVisible()
})

test('401, the session ended elsewhere: sign in, then back to exactly this page', async () => {
  test.setTimeout(120_000)
  // Signing out on another device: the API revokes this session's refresh token.
  const cookie = (await page.context().cookies()).find((item) => item.name === 'session')
  expect(cookie).toBeDefined()
  const { payload } = await jwtDecrypt(cookie!.value, Buffer.from(process.env.SESSION_SECRET ?? '', 'base64'))
  const signedOut = await fetch(`${SHORT_API}/api/v1/auth/logout`, {
    method: 'POST', headers: { Cookie: `__Secure-ronditrack_refresh=${payload.refreshToken as string}` },
  })
  expect(signedOut.status).toBe(204)
  await page.waitForTimeout(61_000) // and the access token it still holds runs out

  await page.goto(stokvelPath)
  await expect(page).toHaveURL(`/login?expired=1&next=${encodeURIComponent(stokvelPath)}`)
  await expect(page.getByText('Your session has expired. Please sign in again.')).toBeVisible()
  await signIn(page)
  await expect(page).toHaveURL(new RegExp(`${stokvelPath}$`))
  await expect(page.getByRole('button', { name: 'Record contribution' })).toBeVisible()
})

// Before the "no answer" step, which restarts the API: in its own browser
// context, so the signed-in session above is untouched.
test('429 for sign-ins: the wait, in seconds, from Retry-After', async ({ browser }) => {
  const context = await browser.newContext({ baseURL: 'http://localhost:3001' })
  const page = await context.newPage()
  await page.goto('/login')
  const wrong = page.getByText('Email or password is incorrect.')
  const limited = page.getByText(/^Retry after \d+ seconds\.$/)
  // Locally, an idle pooled connection through Docker Desktop's port forwarder
  // has lost an answer the API did send (its span shows the 401): the person
  // then sees our apology and a reference instead. The API still counted the
  // attempt, so that outcome counts here too.
  const ourApology = page.getByText('Something went wrong. Please try again.')
  for (let attempt = 0; attempt < 8 && !(await limited.isVisible()); attempt++) {
    await page.getByLabel('Email').fill(email)
    await page.getByLabel('Password').fill('not-the-password')
    await page.getByRole('button', { name: 'Sign in' }).click()
    // Each attempt's own answer, before the form is filled again.
    await expect(wrong.or(limited).or(ourApology).first()).toBeVisible()
    await expect(page.getByRole('button', { name: 'Sign in' })).toBeEnabled()
  }
  await expect(limited).toBeVisible()
  await context.close()
})

test('no answer from the API: one persistent toast with a real trace reference, input kept; and never Next.js\'s error page', async () => {
  test.setTimeout(120_000)
  await page.goto(stokvelPath)
  await page.getByLabel('Amount').fill('150,00')
  execFileSync('docker', ['stop', '-t', '1', SHORT_API_CONTAINER!], { stdio: 'ignore' })
  await waitForApi(false)

  await page.getByRole('button', { name: 'Record contribution' }).click()
  const notifications = page.getByRole('region', { name: 'Notifications' })
  await expect(notifications).toContainText('Something went wrong on our side. Trying again is safe: it will not be recorded twice.')
  await expect(notifications).toContainText(TRACE_ID)
  await expect(page.getByRole('form', { name: 'Record a contribution' }).getByRole('alert')).toBeEmpty()
  await expect(page.getByLabel('Amount')).toHaveValue('150,00')
  await page.waitForTimeout(5_000)
  await expect(notifications).toContainText('Something went wrong on our side') // still there until closed
  await notifications.getByRole('button', { name: 'Close notification' }).click()
  await expect(notifications).not.toContainText('Something went wrong')

  // A page outside the signed-in area, with the API down: our error page and reference.
  await page.goto('/join?code=ANYCODE')
  const alert = page.getByRole('alert').filter({ hasText: 'Something went wrong. Please try again.' })
  await expect(alert).toBeVisible()
  await expect(alert).toContainText(TRACE_ID)
  await expect(page.getByText('Application error', { exact: false })).toHaveCount(0)

  execFileSync('docker', ['start', SHORT_API_CONTAINER!], { stdio: 'ignore' })
  await waitForApi(true)
})
