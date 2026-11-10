import { render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { http, HttpResponse } from 'msw'
import { describe, expect, it, vi } from 'vitest'
import { server } from '@/test/msw'
import { CloseCycleDialog } from './CloseCycleDialog'
import { ToastProvider } from './ToastProvider'
vi.mock('next/navigation', () => ({ useRouter: () => ({ push: vi.fn(), refresh: vi.fn(), replace: vi.fn() }) }))

const preview = {
  cycleId: 'c1',
  period: '2026-11',
  asOfDate: '2026-11-30',
  version: 'v1',
  arrears: [{ userId: 'u1', userName: 'Thandi Dlamini', amountCents: 15050 }],
  totalCents: 15050,
}

describe('CloseCycleDialog', () => {
  it('names no amount until the preview has one, then names the real total', async () => {
    // The preview answers only when the test says so, to see the loading state.
    let answer!: () => void
    const answered = new Promise<void>((resolve) => { answer = resolve })
    server.use(http.get('*/bff/stokvels/s1/cycles/c1/close', async () => {
      await answered
      return HttpResponse.json(preview)
    }))
    const user = userEvent.setup()
    render(<ToastProvider><CloseCycleDialog stokvelId="s1" cycleId="c1" /></ToastProvider>)

    await user.click(screen.getByRole('button', { name: 'Close cycle' }))

    // Loading: focus is on Cancel, the confirm button can't be used, and it
    // doesn't claim "R 0,00 in arrears" (what it used to say).
    const dialog = screen.getByRole('dialog')
    expect(screen.getByRole('button', { name: 'Cancel' })).toHaveFocus()
    expect(within(dialog).getByRole('status')).toHaveTextContent('Loading the close preview…')
    const confirm = screen.getByRole('button', { name: 'Waiting for the preview' })
    expect(confirm).toHaveAttribute('aria-disabled', 'true')
    expect(screen.queryByText(/R\s?0,00/)).not.toBeInTheDocument()

    answer()

    // en-ZA puts a no-break space after the R, hence \s.
    expect(await screen.findByRole('button', { name: /^Close 2026-11 and record R\s150,50 in arrears$/ })).toHaveAttribute('aria-disabled', 'false')
    expect(screen.getByText('Thandi Dlamini: R 150,50')).toBeInTheDocument()
  })

  // Assignment 9.3 Part 3: one owner per error, never two messages.
  const notifications = () => screen.getByRole('region', { name: 'Notifications' })

  it('a 5xx on confirm is one persistent toast with its reference; the dialog shows no second message', async () => {
    server.use(
      http.get('*/bff/stokvels/s1/cycles/c1/close', () => HttpResponse.json(preview)),
      http.post('*/bff/stokvels/s1/cycles/c1/close', () => HttpResponse.json({ correlationId: '4bf92f3577b34da6a3ce929d0e0e4736' }, { status: 500 })),
    )
    const user = userEvent.setup()
    render(<ToastProvider><CloseCycleDialog stokvelId="s1" cycleId="c1" /></ToastProvider>)
    await user.click(screen.getByRole('button', { name: 'Close cycle' }))
    await user.click(await screen.findByRole('button', { name: /^Close 2026-11 and record/ }))

    expect(await within(notifications()).findByText('The cycle could not be closed. Confirming again is safe.')).toBeInTheDocument()
    expect(within(notifications()).getByText('4bf92f3577b34da6a3ce929d0e0e4736')).toBeInTheDocument()
    expect(within(screen.getByRole('dialog')).queryByText(/could not be closed/)).not.toBeInTheDocument()
    // The preview stays, so the person can confirm again.
    expect(screen.getByRole('button', { name: /^Close 2026-11 and record/ })).toBeInTheDocument()
  })

  it('a 403 is one message in place, no toast, and the dialog stays open', async () => {
    server.use(
      http.get('*/bff/stokvels/s1/cycles/c1/close', () => HttpResponse.json(preview)),
      http.post('*/bff/stokvels/s1/cycles/c1/close', () => HttpResponse.json({ title: 'Forbidden' }, { status: 403 })),
    )
    const user = userEvent.setup()
    render(<ToastProvider><CloseCycleDialog stokvelId="s1" cycleId="c1" /></ToastProvider>)
    await user.click(screen.getByRole('button', { name: 'Close cycle' }))
    await user.click(await screen.findByRole('button', { name: /^Close 2026-11 and record/ }))

    expect(await within(screen.getByRole('dialog')).findByText("You don't have permission to close this cycle.")).toBeInTheDocument()
    expect(notifications()).not.toHaveTextContent(/\S/)
  })

  it('a 5xx while loading the preview is one toast, and the empty dialog closes', async () => {
    server.use(http.get('*/bff/stokvels/s1/cycles/c1/close', () => HttpResponse.json({ correlationId: 'abc123' }, { status: 503 })))
    const user = userEvent.setup()
    render(<ToastProvider><CloseCycleDialog stokvelId="s1" cycleId="c1" /></ToastProvider>)
    await user.click(screen.getByRole('button', { name: 'Close cycle' }))

    expect(await within(notifications()).findByText('The close preview could not be loaded. Please try again.')).toBeInTheDocument()
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
    expect(screen.getAllByText(/could not be loaded/)).toHaveLength(1)
  })
})
