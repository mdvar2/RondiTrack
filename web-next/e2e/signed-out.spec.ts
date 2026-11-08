import { expect, test } from '@playwright/test'

test.use({ storageState: { cookies: [], origins: [] } })

test('a signed-in page sends you to sign in, with the way back', async ({ page }) => {
  await page.goto('/stokvels/00000000-0000-4000-8000-000000000000?tab=proofs')
  await expect(page).toHaveURL(/\/login\?next=%2Fstokvels%2F00000000-0000-4000-8000-000000000000%3Ftab%3Dproofs$/)
  await expect(page.getByRole('heading', { level: 1, name: 'Sign in' })).toBeVisible()
})

test('a /bff/ fetch with no session gets a JSON 401, not HTML, and is never indexed', async ({ request }) => {
  const paths = [
    '/bff/contributions/00000000-0000-4000-8000-000000000000/proof',
    '/bff/stokvels/00000000-0000-4000-8000-000000000000/cycles/00000000-0000-4000-8000-000000000000/close',
  ]
  for (const path of paths) {
    const response = await request.get(path, { maxRedirects: 0 })
    expect(response.status(), path).toBe(401)
    expect(response.headers()['content-type'], path).toContain('application/json')
    expect(response.headers()['x-robots-tag'], path).toContain('noindex')
    expect(await response.json()).toEqual({ title: 'Authentication required.' })
  }
})
