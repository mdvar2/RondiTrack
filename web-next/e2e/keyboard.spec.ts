import { expect, test, type Page } from '@playwright/test'
import { api, people, stokvelWithCycle, userId } from './support'

// What axe can't check: where focus goes, and whether a keyboard alone can
// do the job. The screen-reader pass is still done by a person (README).

async function tabTo(page: Page, name: RegExp | string, max = 40) {
  for (let i = 0; i < max; i++) {
    await page.keyboard.press('Tab')
    const focused = page.locator(':focus')
    const label = (await focused.getAttribute('aria-label')) ?? (await focused.innerText().catch(() => '')) ?? ''
    if (typeof name === 'string' ? label.trim() === name : name.test(label)) return focused
  }
  throw new Error(`Tab never reached ${name}`)
}

test.describe('signed out', () => {
  test.use({ storageState: { cookies: [], origins: [] } })

  test('3.3.8: sign-in takes paste and a password manager, with no puzzle', async ({ page }) => {
    await page.goto('/login')
    await expect(page.getByLabel('Email')).toHaveAttribute('autocomplete', 'username')
    await expect(page.getByLabel('Password')).toHaveAttribute('autocomplete', 'current-password')
    const blocked = await page.getByLabel('Password').evaluate((input) => {
      const event = new ClipboardEvent('paste', { clipboardData: new DataTransfer(), bubbles: true, cancelable: true })
      return !input.dispatchEvent(event)
    })
    expect(blocked).toBe(false)
    await expect(page.locator('iframe, [class*="captcha" i]')).toHaveCount(0)
  })
})

test.describe('signed in', () => {
  test.use({ storageState: people.sipho.state })

  test('join a stokvel with the keyboard only: headings take focus, and Back never asks for the code again', async ({ page }) => {
    const owner = await api<{ id: string; name: string }>('thandi', 'POST', '/api/v1/stokvels', { name: `Keyboard join ${Date.now()}`, contributionAmount: 250 })
    const { code } = await api<{ code: string }>('thandi', 'POST', `/api/v1/stokvels/${owner.id}/invites`)

    await page.goto('/join')
    const field = await tabTo(page, '')
    await expect(field).toHaveAccessibleName('Invite code')
    await page.keyboard.insertText(code) // as a paste would
    await page.keyboard.press('Enter')

    const heading = page.getByRole('heading', { name: 'Step 2 of 3: Review the rules' })
    await expect(heading).toBeFocused()
    await expect(page).toHaveTitle('Step 2 of 3: Review the rules · Join a stokvel · RondiTrack')
    await expect(page.getByText(owner.name)).toBeVisible()

    // The browser's Back: the code is still there.
    await page.goBack()
    // getByRole ignores the pages Next.js keeps hidden in the DOM.
    await expect(page.getByRole('textbox', { name: 'Invite code' })).toHaveValue(code)
    await page.goForward()
    await expect(page.getByRole('heading', { name: 'Step 2 of 3: Review the rules' })).toBeFocused()

    await (await tabTo(page, 'Continue')).press('Enter')
    await expect(page.getByRole('heading', { name: 'Step 3 of 3: Confirm your join' })).toBeFocused()
    await (await tabTo(page, `Join ${owner.name}`)).press('Enter')
    await expect(page).toHaveURL(new RegExp(`/stokvels/${owner.id}$`))
    await expect(page.getByRole('heading', { level: 1, name: owner.name })).toBeVisible()
  })

  test('record a contribution with the keyboard only; a refusal keeps what was typed and focuses the field', async ({ page }) => {
    const { stokvelId } = await stokvelWithCycle('sipho', 'Keyboard record')
    await page.goto(`/stokvels/${stokvelId}`)
    await expect(page.getByLabel('Amount')).toBeVisible()

    await page.getByLabel('Amount').focus()
    await page.keyboard.type('12,345')
    await page.keyboard.press('Enter')
    await expect(page.getByLabel('Amount')).toBeFocused()
    await expect(page.getByLabel('Amount')).toHaveAttribute('aria-invalid', 'true')
    await expect(page.getByLabel('Amount')).toHaveValue('12,345')

    await page.getByLabel('Amount').fill('')
    await page.keyboard.type('123,45')
    await page.keyboard.press('Enter')
    await expect(page.getByRole('status').filter({ hasText: 'Recorded R 123,45 for you.' })).toBeVisible()
  })

  test('the Close Cycle dialog: focus starts on Cancel, Escape closes it and focus returns', async ({ page }) => {
    const { stokvelId } = await stokvelWithCycle('sipho', 'Keyboard close')
    await page.goto(`/stokvels/${stokvelId}`)
    // Loading the page doesn't move focus anywhere (it used to jump to "Close cycle").
    await expect(page.getByRole('button', { name: 'Close cycle' })).not.toBeFocused()

    await page.getByRole('button', { name: 'Close cycle' }).focus()
    await page.keyboard.press('Enter')
    await expect(page.getByRole('button', { name: 'Cancel' })).toBeFocused()
    await page.keyboard.press('Escape')
    await expect(page.getByRole('dialog')).toHaveCount(0)
    await expect(page.getByRole('button', { name: 'Close cycle' })).toBeFocused()
  })

  test.describe('on a phone', () => {
    test.use({ viewport: { width: 360, height: 640 } })

    test('2.4.11: a toast never hides the control that has focus', async ({ page }) => {
      const { stokvelId, cycleId } = await stokvelWithCycle('sipho', 'Focus obscured')
      const thandi = await userId('thandi')
      await api('sipho', 'POST', `/api/v1/stokvels/${stokvelId}/members/${thandi}`)
      for (const member of [await userId('sipho'), thandi]) {
        await api('sipho', 'POST', `/api/v1/stokvels/${stokvelId}/members/${member}/contributions`, { amountInCents: 50000, contributionCycleId: cycleId })
      }
      await page.goto(`/stokvels/${stokvelId}`)

      // A real toast: upload a proof.
      const png = Buffer.from('iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==', 'base64')
      await page.getByLabel('Upload your proof of payment').setInputFiles({ name: 'proof.png', mimeType: 'image/png', buffer: png })
      await page.getByRole('button', { name: 'Upload your proof', exact: true }).click()
      const toast = page.getByRole('status').filter({ hasText: 'Uploaded your proof of payment.' })
      await expect(toast).toBeVisible()

      // Tab through everything after the upload button while the toast is up.
      const toastBox = (await toast.boundingBox())!
      for (let i = 0; i < 8; i++) {
        await page.keyboard.press('Tab')
        const focused = await page.evaluate(() => {
          const element = document.activeElement as HTMLElement | null
          if (!element || element === document.body) return null
          const rect = element.getBoundingClientRect()
          // The toast's own Close button belongs inside it.
          return { inToast: !!element.closest('[aria-label="Notifications"]'), box: { x: rect.x, y: rect.y, width: rect.width, height: rect.height } }
        })
        if (!focused || !(await toast.isVisible())) break
        if (focused.inToast) continue
        const { box } = focused
        const overlaps = box.y < toastBox.y + toastBox.height && box.y + box.height > toastBox.y && box.x < toastBox.x + toastBox.width && box.x + box.width > toastBox.x
        expect(overlaps, `focused element at ${JSON.stringify(box)} is under the toast at ${JSON.stringify(toastBox)}`).toBe(false)
      }
    })
  })
})
