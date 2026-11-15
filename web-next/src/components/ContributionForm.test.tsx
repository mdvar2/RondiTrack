import { render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import { ContributionForm } from './ContributionForm'
import { ToastProvider } from './ToastProvider'
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
    <ToastProvider>
      <ContributionForm
        stokvelId="33333333-3333-4333-8333-333333333333"
        cycleId="44444444-4444-4444-8444-444444444444"
        currentUserId={SIPHO.userId}
        members={members}
        action={fake.action}
      />
    </ToastProvider>,
  )
  return { fake, user: userEvent.setup() }
}

const amountField = () => screen.getByLabelText('Amount')
const form = () => screen.getByRole('form', { name: 'Record a contribution' })
const notifications = () => screen.getByRole('region', { name: 'Notifications' })
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
    [409, 'A contribution already exists for this member and cycle.', 'Reload the page to see the latest, then decide again.'],
    [422, 'This cycle is closed for new contributions.', 'Change the details and try again.'],
    [412, undefined, 'This page is out of date. Reload it, then try again.'],
  ])('shows a %i inline with its reason, its own recovery step and its reference, keeps what was typed, and no toast (SC 3.3.7)', async (status, detail, recovery) => {
    const { fake, user } = renderForm()
    await user.type(amountField(), '150,50')
    await user.click(recordButton())
    fake.answer({ ok: false, status, problem: { detail, correlationId: 'corr-42' } })

    const alert = await within(form()).findByRole('alert')
    if (detail) expect(alert).toHaveTextContent(detail)
    expect(alert).toHaveTextContent(recovery)
    expect(within(alert).getByText('corr-42')).toBeInTheDocument()
    expect(amountField()).toHaveValue('150,50')
    expect(notifications()).not.toHaveTextContent(/\S/)
  })

  it.each([500, 503])('a %i is one persistent toast with the trace reference, not an inline message; the input and key are kept', async (status) => {
    const { fake, user } = renderForm()
    await user.type(amountField(), '150,50')
    await user.click(recordButton())
    fake.answer({ ok: false, status, problem: { correlationId: '4bf92f3577b34da6a3ce929d0e0e4736' } })

    const toast = await within(notifications()).findByText(/Something went wrong on our side/)
    expect(within(notifications()).getByText('4bf92f3577b34da6a3ce929d0e0e4736')).toBeInTheDocument()
    expect(within(form()).getByRole('alert')).toBeEmptyDOMElement()
    expect(screen.getAllByText(/Something went wrong on our side/)).toHaveLength(1)
    expect(amountField()).toHaveValue('150,50')

    // It stays until it is closed.
    await new Promise((resolve) => setTimeout(resolve, 50))
    expect(toast).toBeInTheDocument()
    await user.click(within(notifications()).getByRole('button', { name: 'Close notification' }))
    expect(within(notifications()).queryByText(/Something went wrong on our side/)).not.toBeInTheDocument()

    await user.click(recordButton())
    expect(fake.calls[1].idempotencyKey).toBe(fake.calls[0].idempotencyKey)
  })

  it('no answer at all is one persistent toast that says it has no reference, rather than inventing one', async () => {
    const { fake, user } = renderForm()
    await user.type(amountField(), '150,50')
    await user.click(recordButton())
    fake.dropConnection()

    expect(await within(notifications()).findByText(/couldn't reach RondiTrack/)).toBeInTheDocument()
    expect(within(notifications()).getByText('Reference unavailable.')).toBeInTheDocument()
    expect(within(form()).getByRole('alert')).toBeEmptyDOMElement()
    expect(screen.getAllByText(/couldn't reach RondiTrack/)).toHaveLength(1)
  })

  it('announces the success and clears the amount for the next contribution', async () => {
    const { fake, user } = renderForm()
    await user.selectOptions(screen.getByRole('combobox', { name: 'Member' }), 'Thandi Dlamini')
    await user.type(amountField(), '150,50')
    await user.click(recordButton())
    fake.answer({ ok: true, amountCents: 15050 })

    expect(await screen.findByText('Recorded R 150,50 for Thandi Dlamini.')).toBeInTheDocument()
    expect(within(form()).getByRole('status')).toHaveTextContent('Recorded R 150,50 for Thandi Dlamini.')
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
