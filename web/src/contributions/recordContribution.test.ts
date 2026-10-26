// @vitest-environment jsdom
import { createElement, StrictMode, type ReactNode } from 'react'
import { afterAll, afterEach, beforeAll, beforeEach, describe, expect, it } from 'vitest'
import { act, renderHook } from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { http, HttpResponse } from 'msw'
import { setupServer } from 'msw/node'
import { login } from '../auth/session'
import { describeError } from '../lib/errorMessages'
import { parseRandsToCents } from '../lib/money'
import { useRecordContribution } from './useRecordContribution'

const API = (import.meta.env.VITE_API_BASE_URL || 'http://localhost:8081').replace(/\/$/, '')
const STOKVEL = '11111111-1111-4111-8111-111111111111'
const USER = '22222222-2222-4222-8222-222222222222'
const CYCLE = '33333333-3333-4333-8333-333333333333'

const recorded = { id: 'c1', stokvelId: STOKVEL, userId: USER, contributionCycleId: CYCLE, amount: 15050, status: 'Paid' }

type Seen = { key: string | null; authorization: string | null; body: { amountInCents: unknown } }
const seen: Seen[] = []
// What the API answers to each POST, in order. Once empty, it answers 201.
let replies: (() => Response)[] = []

const server = setupServer(
  http.post(`${API}/api/v1/auth/login`, () => HttpResponse.json({ accessToken: 'token-1' })),
  http.post(`${API}/api/v1/auth/refresh`, () => HttpResponse.json({ accessToken: 'token-2' })),
  http.post(`${API}/api/v1/stokvels/:id/members/:userId/contributions`, async ({ request }) => {
    seen.push({
      key: request.headers.get('Idempotency-Key'),
      authorization: request.headers.get('Authorization'),
      body: await request.json() as Seen['body'],
    })
    return replies.shift()?.() ?? HttpResponse.json(recorded, { status: 201 })
  }),
)

beforeAll(() => server.listen({ onUnhandledRequest: 'error' }))
afterAll(() => server.close())
beforeEach(async () => {
  seen.length = 0
  replies = []
  await login('member@example.com', 'password')
})
afterEach(() => server.resetHandlers())

// StrictMode is on, as in main.tsx.
function setup() {
  const client = new QueryClient()
  const wrapper = ({ children }: { children: ReactNode }) =>
    createElement(StrictMode, null, createElement(QueryClientProvider, { client }, children))
  return renderHook(() => useRecordContribution(STOKVEL), { wrapper })
}

const input = (amountInCents: number) => ({ userId: USER, cycleId: CYCLE, amountInCents })

describe('recording a contribution', () => {
  it('sends the same key when retrying after a network error', async () => {
    replies = [() => HttpResponse.error()]
    const { result } = setup()

    await act(async () => { await expect(result.current.record(input(15050))).rejects.toThrow() })
    await act(async () => { await result.current.record(input(15050)) })

    expect(seen).toHaveLength(2)
    expect(seen[0].key).not.toBeNull()
    expect(seen[0].key).toBe(seen[1].key)
  })

  it('gives the next contribution a new key', async () => {
    const { result } = setup()

    await act(async () => { await result.current.record(input(15050)) })
    await act(async () => { await result.current.record(input(15050)) })

    expect(seen).toHaveLength(2)
    expect(seen[0].key).not.toBe(seen[1].key)
  })

  it('keeps the key on a 429 and exposes the wait from Retry-After', async () => {
    replies = [() => HttpResponse.json(
      { title: 'Too Many Requests', correlationId: 'abc' },
      { status: 429, headers: { 'Retry-After': '1' } },
    )]
    const { result } = setup()

    let failure: unknown
    await act(async () => { await result.current.record(input(15050)).catch((e) => { failure = e }) })
    expect(describeError(failure).retryAfter).toBe(1)
    expect(describeError(failure).correlationId).toBe('abc')

    await act(async () => { await result.current.record(input(15050)) })

    expect(seen).toHaveLength(2)
    expect(seen[0].key).toBe(seen[1].key)
  })

  it('treats a changed amount after a failure as a new intent', async () => {
    replies = [() => HttpResponse.error()]
    const { result } = setup()

    await act(async () => { await expect(result.current.record(input(15050))).rejects.toThrow() })
    await act(async () => { await result.current.record(input(20000)) })

    expect(seen).toHaveLength(2)
    expect(seen[0].key).not.toBe(seen[1].key)
    expect(seen[1].body.amountInCents).toBe(20000)
  })

  it('keeps the key and uses the new token when a 401 is refreshed and retried', async () => {
    replies = [() => new HttpResponse(null, { status: 401 })]
    const { result } = setup()

    await act(async () => { await result.current.record(input(15050)) })

    expect(seen).toHaveLength(2)
    expect(seen[0].authorization).toBe('Bearer token-1')
    expect(seen[1].authorization).toBe('Bearer token-2')
    expect(seen[0].key).not.toBeNull()
    expect(seen[0].key).toBe(seen[1].key)
  })

  it('sends what the person typed as a whole number of cents', async () => {
    const { result } = setup()

    await act(async () => { await result.current.record(input(parseRandsToCents('150.50')!)) })

    expect(seen[0].body.amountInCents).toBe(15050)
    expect(typeof seen[0].body.amountInCents).toBe('number')
  })
})
