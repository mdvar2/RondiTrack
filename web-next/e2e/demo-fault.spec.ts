import { expect, test, type Page } from '@playwright/test'
import { api, people, stokvelWithCycle, userId } from './support'

// The simulated post-commit response failure (src/lib/demo-fault.ts). It is not
// a network disconnection: the API commits, then the Server Action rejects as if
// its answer were lost. Runs only where the test environment switches it on
// (CI's throwaway stack: RONDITRACK_ENVIRONMENT=test, Thandi is the one allowed
// synthetic account). Everywhere else these tests are skipped.
test.skip(process.env.RONDITRACK_DEMO_FAULTS !== 'enabled', 'Demo faults are not enabled for this run.')

const NETWORK_MESSAGE = "We couldn't reach RondiTrack. Check your connection and try again: it will not be recorded twice."
const ARM_COOKIE = 'rt_demo_fault'

type Page_ = { items: Array<{ userId: string }> }
const contributionsFor = async (stokvelId: string, cycleId: string, member: string) =>
  (await api<Page_>('thandi', 'GET', `/api/v1/stokvels/${stokvelId}/cycles/${cycleId}/contributions?pageSize=100`))
    .items.filter((item) => item.userId === member).length

// The Idempotency-Key each contribution Server Action call carried.
function recordKeys(page: Page): string[] {
  const keys: string[] = []
  page.on('request', (request) => {
    if (request.method() !== 'POST' || !request.headers()['next-action']) return
    const key = /"idempotencyKey":"([^"]+)"/.exec(request.postData() ?? '')?.[1]
    if (key) keys.push(key)
  })
  return keys
}

test.describe('as the allowed demo account', () => {
  test.use({ storageState: people.thandi.state })

  test('the answer is lost after the commit; the retry reuses the key and records once; the next one is not dropped', async ({ page }) => {
    const { stokvelId, cycleId } = await stokvelWithCycle('thandi', 'Demo fault')
    const thandi = await userId('thandi')
    const sipho = await userId('sipho')
    await api('thandi', 'POST', `/api/v1/stokvels/${stokvelId}/members/${sipho}`)
    const keys = recordKeys(page)

    // Arming: the parameter is removed, and only then is the cookie there.
    await page.goto(`/stokvels/${stokvelId}?demoFault=drop-next`)
    await expect(page).toHaveURL(new RegExp(`/stokvels/${stokvelId}$`))
    expect((await page.context().cookies()).some((cookie) => cookie.name === ARM_COOKIE)).toBe(true)

    // First attempt: committed by the API, but the browser hears nothing back.
    await page.getByLabel('Amount').fill('150,00')
    await page.getByRole('button', { name: 'Record contribution' }).click()
    await expect(page.getByText(NETWORK_MESSAGE)).toBeVisible()
    expect(await contributionsFor(stokvelId, cycleId, thandi)).toBe(1)
    // Like a real lost answer, the browser learned nothing: the page still shows nobody paid.
    await expect(page.locator('dt:text-is("Members paid") + dd')).toHaveText('0 of 2')

    // The retry: same key, the stored answer, still one contribution.
    await page.getByRole('button', { name: 'Record contribution' }).click()
    await expect(page.getByRole('status').filter({ hasText: 'Recorded R 150,00 for you.' })).toBeVisible()
    expect(keys).toHaveLength(2)
    expect(keys[1]).toBe(keys[0])
    expect(await contributionsFor(stokvelId, cycleId, thandi)).toBe(1)
    await expect(page.locator('dt:text-is("Members paid") + dd')).toHaveText('1 of 2')
    expect((await page.context().cookies()).some((cookie) => cookie.name === ARM_COOKIE)).toBe(false)

    // A new contribution afterwards succeeds first time.
    await page.getByLabel('Member').selectOption({ label: people.sipho.name })
    await page.getByLabel('Amount').fill('200,00')
    await page.getByRole('button', { name: 'Record contribution' }).click()
    await expect(page.getByRole('status').filter({ hasText: `Recorded R 200,00 for ${people.sipho.name}.` })).toBeVisible()
    expect(keys).toHaveLength(3)
    expect(keys[2]).not.toBe(keys[0])
    expect(await contributionsFor(stokvelId, cycleId, sipho)).toBe(1)
  })
})

test.describe('as an account that is not allowed', () => {
  test.use({ storageState: people.sipho.state })

  test('the parameter is removed and nothing is armed', async ({ page }) => {
    const { stokvelId } = await stokvelWithCycle('sipho', 'Not armed')
    await page.goto(`/stokvels/${stokvelId}?demoFault=drop-next`)
    await expect(page).toHaveURL(new RegExp(`/stokvels/${stokvelId}$`))
    expect((await page.context().cookies()).some((cookie) => cookie.name === ARM_COOKIE)).toBe(false)

    await page.getByLabel('Amount').fill('120,00')
    await page.getByRole('button', { name: 'Record contribution' }).click()
    await expect(page.getByRole('status').filter({ hasText: 'Recorded R 120,00 for you.' })).toBeVisible()
  })
})
