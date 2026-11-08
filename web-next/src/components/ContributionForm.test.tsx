import { render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import { ContributionForm } from './ContributionForm'
import type { ContributionInput, ContributionResult } from '@/lib/contribution-feedback'

// The form's one edge is its Server Action, so that is the only thing faked.
// The fake answers only when the test tells it to, so the test controls
// exactly when a request is "in flight".
function fakeAction() {
  const calls: ContributionInput[] = []
  const waiting: Array<{ resolve: (result: ContributionResult) => void; reject: (error: Error) => void }> = []
  const action = vi.fn((input: ContributionInput) => {
    calls.push(input)
    return new Promise<ContributionResult>((resolve, reject) => waiting.push({ resolve, reject }))
  })
  return {
    action,
    calls,
    answer: (result: ContributionResult) => waiting.shift()!.resolve(result),
    // What a dropped connection looks like to the caller of a Server Action.
    dropConnection: () => waiting.shift()!.reject(new TypeError('Failed to fetch')),
  }
}

const SIPHO = { userId: '11111111-1111-4111-8111-111111111111', userName: 'Sipho Nkosi', role: 'Treasurer' }
const THANDI = { userId: '22222222-2222-4222-8222-222222222222', userName: 'Thandi Dlamini', role: 'Member' }

function renderForm(members = [SIPHO, THANDI]) {
  const fake = fakeAction()
  render(
    <ContributionForm
      stokvelId="33333333-3333-4333-8333-333333333333"
      cycleId="44444444-4444-4444-8444-444444444444"
      currentUserId={SIPHO.userId}
      members={members}
      action={fake.action}
    />,
  )
  return { fake, user: userEvent.setup() }
}

const amountField = () => screen.getByLabelText('Amount')
const recordButton = () => screen.getByRole('button', { name: 'Record contribution' })

describe('ContributionForm', () => {
  it('is found the way a screen reader finds it: fields by label, the button by role and name', () => {
    renderForm()
    expect(screen.getByRole('form', { name: 'Record a contribution' })).toBeInTheDocument()
    expect(screen.getByRole('combobox', { name: 'Member' })).toBeInTheDocument()
    expect(amountField()).toHaveAccessibleDescription('In rands, for example 150,50')
    expect(recordButton()).toBeInTheDocument()
  })

  it('sends whole cents parsed from rands, never a float', async () => {
    const { fake, user } = renderForm()
    await user.selectOptions(screen.getByRole('combobox', { name: 'Member' }), 'Thandi Dlamini')
    await user.type(amountField(), 'R 1 500,50')
    await user.click(recordButton())
    expect(fake.calls).toHaveLength(1)
    expect(fake.calls[0]).toMatchObject({ userId: THANDI.userId, amountCents: 150050 })
  })

  it('refuses an amount it would have to guess at, under the field, without sending anything', async () => {
    const { fake, user } = renderForm()
    await user.type(amountField(), '1,500.50')
    await user.click(recordButton())
    expect(fake.action).not.toHaveBeenCalled()
    expect(amountField()).toHaveAttribute('aria-invalid', 'true')
    expect(amountField()).toHaveAccessibleDescription(/at most two decimal places/)
    expect(amountField()).toHaveFocus()
    expect(amountField()).toHaveValue('1,500.50')
  })

  it.each([
    ['amountInCents', 'Contribution amount must be R1 000 000 or less.', 'Amount'],
    ['userId', 'That person is not a member of this stokvel.', 'Member'],
  ])('shows the API 400 for %s under the %s field, and keeps what was typed', async (apiField, message, label) => {
    const { fake, user } = renderForm()
    await user.type(amountField(), '150,50')
    await user.click(recordButton())
    fake.answer({ ok: false, status: 400, problem: { title: 'Bad Request', errors: { [apiField]: [message] } } })

    // Wait for the message, not the field: the field is there from the start.
    expect(await screen.findByText(message)).toBeInTheDocument()
    const field = screen.getByLabelText(label)
    expect(field).toHaveAttribute('aria-invalid', 'true')
    expect(field).toHaveAccessibleDescription(expect.stringContaining(message))
    expect(amountField()).toHaveValue('150,50')
  })

  it.each([
    [409, 'A contribution already exists for this member and cycle.'],
    [422, 'This cycle is closed for new contributions.'],
  ])('shows a %i at the top of the form with its reference, and keeps what was typed (SC 3.3.7)', async (status, detail) => {
    const { fake, user } = renderForm()
    await user.type(amountField(), '150,50')
    await user.click(recordButton())
    fake.answer({ ok: false, status, problem: { detail, correlationId: 'corr-42' } })

    expect(await screen.findByText(detail)).toBeInTheDocument()
    const alert = screen.getByRole('alert')
    expect(alert).toHaveTextContent(detail)
    expect(within(alert).getByText('corr-42')).toBeInTheDocument()
    expect(amountField()).toHaveValue('150,50')
  })

  it('announces the success and clears the amount for the next contribution', async () => {
    const { fake, user } = renderForm()
    await user.selectOptions(screen.getByRole('combobox', { name: 'Member' }), 'Thandi Dlamini')
    await user.type(amountField(), '150,50')
    await user.click(recordButton())
    fake.answer({ ok: true, amountCents: 15050 })

    expect(await screen.findByText('Recorded R 150,50 for Thandi Dlamini.')).toBeInTheDocument()
    expect(screen.getByRole('status')).toHaveTextContent('Recorded R 150,50 for Thandi Dlamini.')
    expect(amountField()).toHaveValue('')
  })

  it('one click, one request: a second click while the first is pending sends nothing', async () => {
    const { fake, user } = renderForm()
    await user.type(amountField(), '150,50')
    await user.click(recordButton())
    expect(await screen.findByRole('button', { name: 'Recording…' })).toHaveAttribute('aria-disabled', 'true')
    await user.click(screen.getByRole('button', { name: 'Recording…' }))
    await user.keyboard('{Enter}')
    expect(fake.action).toHaveBeenCalledTimes(1)

    fake.answer({ ok: true, amountCents: 15050 })
    expect(await screen.findByText(/^Recorded R 150,50/)).toBeInTheDocument()
    expect(fake.action).toHaveBeenCalledTimes(1)
  })

  it('a retry after a dropped connection reuses the key; the next contribution gets a new one', async () => {
    const { fake, user } = renderForm()
    await user.type(amountField(), '150,50')
    await user.click(recordButton())
    fake.dropConnection()

    expect(await screen.findByText(/couldn't reach RondiTrack/)).toBeInTheDocument()
    expect(amountField()).toHaveValue('150,50')

    await user.click(recordButton())
    fake.answer({ ok: true, amountCents: 15050 })
    expect(await screen.findByText(/^Recorded R 150,50/)).toBeInTheDocument()

    await user.type(amountField(), '200')
    await user.click(recordButton())

    expect(fake.calls).toHaveLength(3)
    const [first, retry, next] = fake.calls.map((call) => call.idempotencyKey)
    expect(first).toMatch(/^[0-9a-f-]{36}$/)
    expect(retry).toBe(first)
    expect(next).not.toBe(first)
    expect(fake.calls[2].amountCents).toBe(20000)
  })

  it('a refusal keeps the key too: only a success replaces it', async () => {
    const { fake, user } = renderForm()
    await user.type(amountField(), '150,50')
    await user.click(recordButton())
    fake.answer({ ok: false, status: 503 })
    expect(await screen.findByText(/Trying again is safe/)).toBeInTheDocument()

    await user.click(recordButton())
    expect(fake.calls[1].idempotencyKey).toBe(fake.calls[0].idempotencyKey)
  })

  it('a Member records only for themselves, and no member picker hints at more', () => {
    renderForm([{ ...SIPHO, role: 'Member' }])
    expect(screen.queryByRole('combobox', { name: 'Member' })).not.toBeInTheDocument()
    expect(screen.getByText("You're recording your own contribution.")).toBeInTheDocument()
  })
})
