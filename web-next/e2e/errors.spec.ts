import { randomUUID } from 'node:crypto'
import { expect, test, type Page } from '@playwright/test'
import { accessToken, api, apiBase, people, stokvelWithCycle, userId } from './support'

// Assignment 9.3 Part 3: each error is a flow with one owner. 401 (session
// ended), 429 and "no answer" need their own API instance and live in
// session-expiry.spec.ts; 403's forged request is in roles.spec.ts.

const PNG = Buffer.from('89504e470d0a1a0a0000000d4948445200000001000000010806000000' +
  '1f15c4890000000d49444154789c6360000002000154a24f5d0000000049454e44ae426082', 'hex')

async function contributionWithProof(owner: 'thandi' | 'sipho') {
  const { stokvelId, cycleId } = await stokvelWithCycle(owner, 'Proofs')
  const me = await userId(owner)
  const contribution = await api<{ id: string }>(owner, 'POST', `/api/v1/stokvels/${stokvelId}/members/${me}/contributions`,
    { amountInCents: 30000, contributionCycleId: cycleId })
  const body = new FormData()
  body.append('file', new Blob([PNG], { type: 'image/png' }), 'proof.png')
  const upload = await fetch(`${apiBase}/api/v1/contributions/${contribution.id}/proof`, {
    method: 'POST', headers: { Authorization: `Bearer ${await accessToken(owner)}` }, body,
  })
  expect(upload.ok).toBe(true)
  return { stokvelId, cycleId, contributionId: contribution.id }
}

const notifications = (page: Page) => page.getByRole('region', { name: 'Notifications' })

test.describe('404: "not yours" looks exactly like "doesn\'t exist"', () => {
  test.use({ storageState: people.sipho.state })

  test('a stokvel you are not in, and one that does not exist: the same page and status', async ({ page }) => {
    const theirs = await stokvelWithCycle('thandi', 'Not his')
    const seen: Array<{ status: number | undefined; text: string }> = []
    for (const id of [theirs.stokvelId, randomUUID()]) {
      const response = await page.goto(`/stokvels/${id}`)
      await expect(page.getByRole('heading', { level: 1, name: "We couldn't find that stokvel" })).toBeVisible()
      seen.push({ status: response?.status(), text: await page.locator('main').innerText() })
    }
    // Identical, status included. The status is 200, not 404: the page's shell
    // streams before the API has answered (cacheComponents), so notFound()
    // comes after the status line. Recorded as a known gap in the README.
    expect(seen[0]).toEqual(seen[1])
  })

  test('someone else\'s proof of payment, and one that does not exist: the same page, and the same JSON for fetch', async ({ page }) => {
    const theirs = await contributionWithProof('thandi')
    const pages: Array<{ url: string; status: number | undefined; text: string }> = []
    const answers: Array<{ status: number; type: string | null; detail: unknown }> = []
    for (const id of [theirs.contributionId, randomUUID()]) {
      // A person opening the link: the app's not-found page.
      const response = await page.goto(`/bff/contributions/${id}/proof`)
      await expect(page.getByRole('heading', { level: 1, name: "We couldn't find that page" })).toBeVisible()
      pages.push({ url: new URL(page.url()).pathname, status: response?.status(), text: await page.locator('main').innerText() })
      // A program calling the /bff/ route: the API's problem, unchanged.
      const fetched = await page.evaluate(async (path) => {
        const answer = await fetch(path)
        return { status: answer.status, type: answer.headers.get('content-type'), detail: (await answer.json() as { detail?: string }).detail }
      }, `/bff/contributions/${id}/proof`)
      answers.push(fetched)
    }
    expect(pages[0]).toEqual(pages[1])
    expect(pages[0]).toMatchObject({ url: '/not-found', status: 404 })
    expect(answers[0]).toEqual(answers[1])
    expect(answers[0]).toMatchObject({ status: 404, type: expect.stringContaining('application/problem+json'), detail: 'Proof not found.' })
  })
})

test.describe('the owner still gets the file', () => {
  test.use({ storageState: people.thandi.state })

  test('their own proof downloads as before: the file, no-store and nosniff', async ({ page }) => {
    const mine = await contributionWithProof('thandi')
    await page.goto('/dashboard/member')
    const answer = await page.evaluate(async (path) => {
      const response = await fetch(path)
      return { status: response.status, type: response.headers.get('content-type'), cache: response.headers.get('cache-control'), sniff: response.headers.get('x-content-type-options'), size: (await response.arrayBuffer()).byteLength }
    }, `/bff/contributions/${mine.contributionId}/proof`)
    expect(answer).toMatchObject({ status: 200, type: 'image/png', sniff: 'nosniff', size: PNG.length })
    expect(answer.cache).toContain('no-store')
  })
})

test.describe('409: the reason, the input kept, and a way to look again', () => {
  test.use({ storageState: people.thandi.state })

  test('a second contribution for the same member and cycle', async ({ page }) => {
    const { stokvelId } = await stokvelWithCycle('thandi', 'Twice')
    await page.goto(`/stokvels/${stokvelId}`)
    await page.getByLabel('Amount').fill('150,00')
    await page.getByRole('button', { name: 'Record contribution' }).click()
    await expect(page.getByRole('status').filter({ hasText: 'Recorded R 150,00 for you.' })).toBeVisible()

    await page.getByLabel('Amount').fill('175,00')
    await page.getByRole('button', { name: 'Record contribution' }).click()
    const form = page.getByRole('form', { name: 'Record a contribution' })
    const alert = form.getByRole('alert')
    await expect(alert).toContainText('A contribution already exists for this member and cycle.')
    await expect(alert).toContainText('Reload the page to see the latest, then decide again.')
    await expect(alert).toContainText(/Reference: [0-9a-f]{32}/)
    await expect(page.getByLabel('Amount')).toHaveValue('175,00')
    await expect(notifications(page)).not.toContainText(/\S/)
  })

  test('a stale close preview: told why, shown the new preview, and asked to decide again', async ({ page }) => {
    const { stokvelId, cycleId } = await stokvelWithCycle('thandi', 'Stale close')
    const sipho = await userId('sipho')
    await api('thandi', 'POST', `/api/v1/stokvels/${stokvelId}/members/${sipho}`)
    await page.goto(`/stokvels/${stokvelId}`)
    await page.getByRole('button', { name: 'Close cycle' }).click()
    const dialog = page.getByRole('dialog')
    // Both members owe this cycle: Sipho is in the arrears.
    await expect(dialog).toContainText(people.sipho.name)

    // Sipho's contribution is recorded while the preview is open.
    await api('thandi', 'POST', `/api/v1/stokvels/${stokvelId}/members/${sipho}/contributions`, { amountInCents: 50000, contributionCycleId: cycleId })

    await dialog.getByRole('button', { name: /^Close .* in arrears$/ }).click()
    await expect(dialog.getByRole('alert')).toContainText('The cycle changed since this preview. Review the new preview before confirming again.')
    // The new preview: Sipho has paid, so he is no longer in the arrears.
    await expect(dialog).not.toContainText(people.sipho.name)
    await expect(notifications(page)).not.toContainText(/\S/)
    // Nothing was closed.
    const cycle = await api<{ status: string }>('thandi', 'GET', `/api/v1/stokvels/${stokvelId}/cycles/${cycleId}`)
    expect(cycle.status).toBe('Open')
  })
})
