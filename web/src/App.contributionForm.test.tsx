// @vitest-environment jsdom
import { StrictMode } from 'react'
import { afterAll, afterEach, beforeAll, describe, expect, it, vi } from 'vitest'
import { cleanup, render, screen, within } from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { http, HttpResponse } from 'msw'
import { setupServer } from 'msw/node'
import App from './App'

const API = (import.meta.env.VITE_API_BASE_URL || 'http://localhost:8081').replace(/\/$/, '')
const STOKVEL = '11111111-1111-4111-8111-111111111111'
const CYCLE = '33333333-3333-4333-8333-333333333333'
const ME = '22222222-2222-4222-8222-222222222222'
const OTHER = '44444444-4444-4444-8444-444444444444'
const token = `x.${btoa(JSON.stringify({ sub: ME }))}.y`

const member = (userId: string, userName: string, role: string) =>
  ({ stokvelId: STOKVEL, userId, userName, userEmail: `${userName}@example.com`, role, joinedAtUtc: '2026-10-01T00:00:00Z', version: 1 })

const server = setupServer(
  http.post(`${API}/api/v1/auth/refresh`, () => HttpResponse.json({ accessToken: token })),
  http.get(`${API}/api/v1/me/home`, () => HttpResponse.json({ stokvels: [{
    stokvelId: STOKVEL, stokvelName: 'Demo Savings', role: 'Chairperson',
    currentCycle: { period: '2026-10', status: 'Open', dueDate: '2026-10-31' },
    amountPaid: 50000, amountOwed: 0, nextPayoutDate: null, arrearsCents: 0,
  }] })),
  http.get(`${API}/api/v1/stokvels/:id/cycles`, () => HttpResponse.json([{
    id: CYCLE, stokvelId: STOKVEL, period: '2026-10', targetAmount: 500, version: 1,
    endDate: '2026-10-31', status: 'Open', dueDate: '2026-10-31', totalCollectedCents: 50000, amountDueCents: 0,
  }])),
  http.get(`${API}/api/v1/stokvels/:id/cycles/:cycleId/contributions`, () =>
    HttpResponse.json({ items: [], nextPageToken: '', pageSize: 25 })),
  http.get(`${API}/api/v1/stokvels/:id/members`, () => HttpResponse.json({
    // Like the real API: newest member first, so the signed-in user is not the first option.
    items: [member(OTHER, 'Demo Member 2', 'Member'), member(ME, 'Demo Member', 'Chairperson')],
    nextPageToken: '', pageSize: 100,
  })),
)

beforeAll(() => {
  server.listen({ onUnhandledRequest: 'error' })
  // jsdom has no matchMedia, which the theme code uses.
  window.matchMedia = () => ({ matches: false, addEventListener: () => {}, removeEventListener: () => {} }) as unknown as MediaQueryList
})
afterEach(cleanup)
afterAll(() => server.close())

describe('dashboard', () => {
  it('shows one contribution form per stokvel, with every member in its dropdown', async () => {
    const consoleError = vi.spyOn(console, 'error')
    render(
      <StrictMode>
        <QueryClientProvider client={new QueryClient()}><App /></QueryClientProvider>
      </StrictMode>,
    )

    await screen.findByRole('heading', { name: 'Record a contribution' })
    const member = await screen.findByLabelText('Member')
    await within(member).findByRole('option', { name: 'Demo Member 2' })

    expect(screen.getAllByRole('heading', { name: 'Record a contribution' })).toHaveLength(1)
    expect(within(member).getAllByRole('option').map((option) => option.textContent))
      .toEqual(['Demo Member 2', 'Demo Member'])
    // Two siblings with the same key make React warn, and can duplicate the form.
    expect(consoleError.mock.calls.flat().join(' ')).not.toContain('same key')
    consoleError.mockRestore()
    // The dropdown starts on the signed-in user, not on the first option.
    expect((member as HTMLSelectElement).value).toBe(ME)
  })
})
