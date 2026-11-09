import { expect, test } from '@playwright/test'
import { api, people, stokvelWithCycle, userId } from './support'

test.use({ storageState: people.sipho.state })

test("the UI isn't the rule: Sipho, a Member here, sends the treasurer's close and the API says 403", async ({ page }) => {
  // Thandi's work stokvel, where Sipho is an ordinary Member.
  const work = await stokvelWithCycle('thandi', 'Work stokvel')
  await api('thandi', 'POST', `/api/v1/stokvels/${work.stokvelId}/members/${await userId('sipho')}`)

  await page.goto(`/stokvels/${work.stokvelId}`)
  await expect(page.getByRole('heading', { level: 1, name: work.name })).toBeVisible()
  await expect(page.getByRole('button', { name: 'Close cycle' })).toHaveCount(0)

  // What the Close Cycle dialog would send, sent by hand through the BFF.
  const answer = await page.evaluate(async ({ stokvelId, cycleId }) => {
    const response = await fetch(`/bff/stokvels/${stokvelId}/cycles/${cycleId}/close`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json', 'Idempotency-Key': crypto.randomUUID() },
      body: JSON.stringify({ version: 'forged' }),
    })
    return { status: response.status, body: await response.json() as { title?: string; detail?: string } }
  }, work)

  expect(answer.status).toBe(403)
  expect(answer.body.title).toBe('Forbidden')
  expect(answer.body.detail).toBeTruthy()

  // Still signed in: a refusal is not a sign-out.
  await page.goto('/dashboard/member')
  await expect(page).toHaveURL(/\/dashboard\/member$/)
  await expect(page.getByRole('heading', { level: 1, name: 'Member dashboard' })).toBeVisible()
})

test('in his own stokvel the same person has the treasurer actions', async ({ page }) => {
  const family = await stokvelWithCycle('sipho', 'Family stokvel')
  await api('sipho', 'POST', `/api/v1/stokvels/${family.stokvelId}/members/${await userId('thandi')}`)
  await page.goto(`/stokvels/${family.stokvelId}`)
  await expect(page.getByRole('button', { name: 'Close cycle' })).toBeVisible()
  // Record for another member.
  await expect(page.getByRole('combobox', { name: 'Member' }).getByRole('option', { name: 'Thandi Dlamini' })).toBeAttached()
})
