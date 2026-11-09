import { existsSync, statSync } from 'node:fs'
import { expect, test as setup } from '@playwright/test'
import { apiBase, password, people, type Person } from './support'

// Signs each person in once per run, through the real sign-in form, and saves
// the session for every test to reuse. A saved session is reused while it is
// under two minutes old: the access token lasts five, and the app rotates the
// refresh token in its last minute. Two tests rotating the same saved token
// would make the API revoke the whole session.
const FRESH_MS = 2 * 60 * 1000

for (const person of Object.keys(people) as Person[]) {
  setup(`sign in as ${people[person].name}`, async ({ page }) => {
    const { name, email, state } = people[person]
    if (existsSync(state) && Date.now() - statSync(state).mtimeMs < FRESH_MS) return

    // The seed: register the account if this database doesn't have it yet.
    // Registration isn't rate-limited; 409 means it already exists.
    const registered = await fetch(`${apiBase}/api/v1/auth/register`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ name, email, password: password() }),
    })
    expect([201, 409]).toContain(registered.status)

    await page.goto('/login')
    await page.getByLabel('Email').fill(email)
    await page.getByLabel('Password').fill(password())
    await page.getByRole('button', { name: 'Sign in' }).click()
    await expect(page).toHaveURL(/\/dashboard\/member$/)
    await page.context().storageState({ path: state })
  })
}
