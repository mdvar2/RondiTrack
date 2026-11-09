import AxeBuilder from '@axe-core/playwright'
import { expect, test, type Page } from '@playwright/test'
import { api, people, stokvelWithCycle, userId } from './support'

// WCAG 2.2 AA, every level up to it. Any serious or critical violation fails.
// No rule is disabled: if one ever has to be, it is disabled here, in this
// one place, with the reason next to it.
const TAGS = ['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa', 'wcag22a', 'wcag22aa']

async function expectNoSeriousViolations(page: Page, label: string) {
  const results = await new AxeBuilder({ page }).withTags(TAGS).analyze()
  const serious = results.violations.filter((violation) => violation.impact === 'serious' || violation.impact === 'critical')
  const report = serious.map((violation) => `${violation.id} (${violation.impact}): ${violation.help}\n  ${violation.nodes.map((node) => node.target.join(' ')).join('\n  ')}`)
  expect(report, `${label}\n${report.join('\n')}`).toEqual([])
}

for (const colorScheme of ['light', 'dark'] as const) {
  test.describe(`${colorScheme} mode`, () => {
    test.use({ colorScheme })

    test.describe('signed out', () => {
      test.use({ storageState: { cookies: [], origins: [] } })

      test('sign in, with and without its error', async ({ page }) => {
        await page.goto('/login')
        await expectNoSeriousViolations(page, 'sign in')
        await page.getByRole('button', { name: 'Sign in' }).click()
        await expect(page.getByRole('main').getByRole('alert')).toContainText('Enter your email and password.')
        await expectNoSeriousViolations(page, 'sign in, with its error')
      })
    })

    test.describe('signed in', () => {
      test.use({ storageState: people.sipho.state })

      test('the home screen', async ({ page }) => {
        await page.goto('/dashboard/member')
        await expect(page.getByRole('heading', { level: 1, name: 'Member dashboard' })).toBeVisible()
        await expectNoSeriousViolations(page, 'home')
      })

      test('a stokvel, the contribution form showing its errors, and the open Close Cycle dialog', async ({ page }) => {
        const stokvel = await stokvelWithCycle('sipho', 'Axe')
        await api('sipho', 'POST', `/api/v1/stokvels/${stokvel.stokvelId}/members/${await userId('sipho')}/contributions`, { amountInCents: 50000, contributionCycleId: stokvel.cycleId })
        await page.goto(`/stokvels/${stokvel.stokvelId}`)
        await expect(page.getByRole('region', { name: "This cycle's contributions" })).toBeVisible()
        await expectNoSeriousViolations(page, 'stokvel')

        await page.getByLabel('Amount').fill('1,500.50')
        await page.getByRole('button', { name: 'Record contribution' }).click()
        await expect(page.getByLabel('Amount')).toHaveAttribute('aria-invalid', 'true')
        await expectNoSeriousViolations(page, 'contribution form with errors')

        await page.getByRole('button', { name: 'Close cycle' }).click()
        const dialog = page.getByRole('dialog', { name: /^Close / })
        await expect(dialog.getByText('Total arrears:')).toBeVisible()
        await expectNoSeriousViolations(page, 'Close Cycle dialog')
      })

      test('the join flow, step by step', async ({ page }) => {
        const owner = await api<{ id: string }>('thandi', 'POST', '/api/v1/stokvels', { name: `Axe join ${Date.now()}`, contributionAmount: 300 })
        const { code } = await api<{ code: string }>('thandi', 'POST', `/api/v1/stokvels/${owner.id}/invites`)
        await page.goto('/join')
        await expectNoSeriousViolations(page, 'join: code')
        await page.goto(`/join?code=${code}&step=rules`)
        await expect(page.getByRole('heading', { name: 'Step 2 of 3: Review the rules' })).toBeVisible()
        await expectNoSeriousViolations(page, 'join: rules')
      })
    })
  })
}
