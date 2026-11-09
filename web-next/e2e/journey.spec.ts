import { expect, test } from '@playwright/test'
import { centsIn, people, stokvelWithCycle } from './support'

test.use({ storageState: people.sipho.state })

test('record R 150,50 and the stokvel total goes up by exactly that, without a reload', async ({ page }) => {
  const { stokvelId } = await stokvelWithCycle('sipho', 'Journey')
  await page.goto(`/stokvels/${stokvelId}`)

  const collected = page.locator('dt:text-is("Collected") + dd')
  await expect(collected).toBeVisible()
  // Read the total first, rather than assuming what it is.
  const before = centsIn(await collected.innerText())

  // A reload would wipe this marker.
  await page.evaluate(() => { (window as unknown as { stillHere: boolean }).stillHere = true })

  await page.getByLabel('Amount').fill('150,50')
  await page.getByRole('button', { name: 'Record contribution' }).click()
  await expect(page.getByRole('status').filter({ hasText: 'Recorded R 150,50 for you.' })).toBeVisible()

  await expect.poll(async () => centsIn(await collected.innerText())).toBe(before + 15050)
  expect(await page.evaluate(() => (window as unknown as { stillHere?: boolean }).stillHere)).toBe(true)
  await expect(page.getByRole('region', { name: "This cycle's contributions" }).getByText('R 150,50')).toBeVisible()
})
