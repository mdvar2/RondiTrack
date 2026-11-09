import { render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { http, HttpResponse } from 'msw'
import { describe, expect, it } from 'vitest'
import { server } from '@/test/msw'
import { CloseCycleDialog } from './CloseCycleDialog'
import { ToastProvider } from './ToastProvider'

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
})
