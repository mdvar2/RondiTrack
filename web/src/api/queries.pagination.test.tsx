
// @vitest-environment jsdom
import { afterEach, describe, expect, it, vi } from 'vitest'
import { act, cleanup, render, screen, waitFor } from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import type { ReactNode } from 'react'
import { useContributions } from './queries'
import { api } from './client'

vi.mock('./client', () => ({
  api: { GET: vi.fn() },
  unwrap: (result: { data?: unknown; error?: unknown }) => {
    if (result.error) throw result.error
    return result.data
  },
}))

const STOKVEL_A = '11111111-1111-4111-8111-111111111111'
const STOKVEL_B = '22222222-2222-4222-8222-222222222222'
const CYCLE_A = '33333333-3333-4333-8333-333333333333'
const CYCLE_B = '44444444-4444-4444-8444-444444444444'

const contribution = (
  number: number,
  stokvelId: string,
  cycleId: string,
) => ({
  id: `payment-${number}`,
  stokvelId,
  contributionCycleId: cycleId,
  userId: `member-${number}`,
  amount: 100,
  status: 'Paid',
  recordedAtUtc: '2026-10-22T00:00:00Z',
  version: 1,
})

function deferred<T>() {
  let resolve!: (value: T) => void
  let reject!: (reason: unknown) => void

  const promise = new Promise<T>((res, rej) => {
    resolve = res
    reject = rej
  })

  return { promise, resolve, reject }
}

function Harness({ stokvelId }: { stokvelId: string }) {
  const result = useContributions(stokvelId)

  return (
    <div>
      <output data-testid="selected-stokvel">{stokvelId}</output>

      <output data-testid="loading">
        {String(result.isPending || result.isFetching)}
      </output>

      <output data-testid="count">
        {result.contributions.length}
      </output>

      <output data-testid="ids">
        {result.contributions.map(c => c.id).join(',')}
      </output>

      <output data-testid="has-next">
        {String(Boolean(result.hasNextPage))}
      </output>

      <button
        onClick={() => void result.fetchNextPage()}
        disabled={!result.hasNextPage || result.isFetchingNextPage}
      >
        Load more contributions
      </button>
    </div>
  )
}

function wrapper(client: QueryClient) {
  return function Wrapper({ children }: { children: ReactNode }) {
    return (
      <QueryClientProvider client={client}>
        {children}
      </QueryClientProvider>
    )
  }
}

function makeClient() {
  return new QueryClient({
    defaultOptions: {
      queries: { retry: false },
    },
  })
}

type MockApiOptions = {
  params?: {
    path?: {
      stokvelId?: string
      cycleId?: string
    }
    query?: {
      pageSize?: number
      pageToken?: string
    }
  }
}

type MockApiResponse = {
  data?: unknown
  error?: unknown
}

const get = api.GET as unknown as ReturnType<
  typeof vi.fn<(path: string, options?: MockApiOptions) => Promise<MockApiResponse>>
>

afterEach(() => {
  cleanup()
  vi.clearAllMocks()
})

describe('useContributions pagination and stokvel isolation', () => {
  it('loads 25 records, sends the continuation token, and appends the final record', async () => {
    get.mockImplementation(async (path, options) => {
      if (path.endsWith('/cycles')) {
        return {
          data: [{
            id: CYCLE_A,
            stokvelId: STOKVEL_A,
            period: '2026-10',
          }],
        } as never
      }

      if (path.endsWith('/contributions')) {
        const params = (
          options as {
            params: {
              query: {
                pageSize: number
                pageToken?: string
              }
            }
          }
        ).params.query

        if (params.pageToken === 'page-two') {
          return {
            data: {
              items: [contribution(26, STOKVEL_A, CYCLE_A)],
              nextPageToken: null,
              pageSize: 25,
            },
          } as never
        }

        return {
          data: {
            items: Array.from(
              { length: 25 },
              (_, i) => contribution(i + 1, STOKVEL_A, CYCLE_A),
            ),
            nextPageToken: 'page-two',
            pageSize: 25,
          },
        } as never
      }

      throw new Error(`Unexpected endpoint: ${path}`)
    })

    const client = makeClient()

    render(
      <Harness stokvelId={STOKVEL_A} />,
      { wrapper: wrapper(client) },
    )

    await waitFor(() => {
      expect(screen.getByTestId('count').textContent).toBe('25')
    })

    expect(screen.getByTestId('has-next').textContent).toBe('true')

    await act(async () => {
      screen.getByRole('button', {
        name: 'Load more contributions',
      }).click()
    })

    await waitFor(() => {
      expect(screen.getByTestId('count').textContent).toBe('26')
    })

    expect(screen.getByTestId('has-next').textContent).toBe('false')

    expect(
      screen.getByRole('button', {
        name: 'Load more contributions',
      }).hasAttribute('disabled'),
    ).toBe(true)

    const ids = screen.getByTestId('ids').textContent!.split(',')

    expect(new Set(ids).size).toBe(26)

    const contributionCalls = get.mock.calls.filter(
      ([path]) => path.endsWith('/contributions'),
    )

    expect(contributionCalls).toHaveLength(2)

    expect(contributionCalls[0][1]?.params?.query).toMatchObject({
      pageSize: 25,
    })

    expect(contributionCalls[1][1]?.params?.query).toMatchObject({
      pageSize: 25,
      pageToken: 'page-two',
    })

    client.clear()
  })

  it('never displays Stokvel A contributions while slow Stokvel B is loading', async () => {
    const slowB = deferred<{
      data: {
        items: ReturnType<typeof contribution>[]
        nextPageToken: null
        pageSize: number
      }
    }>()

    let bContributionRequested = false

    get.mockImplementation(async (path, options) => {
      const stokvelId = (
        options as {
          params: {
            path: { stokvelId: string }
          }
        }
      ).params.path.stokvelId

      const cycleId = stokvelId === STOKVEL_A
        ? CYCLE_A
        : CYCLE_B

      if (path.endsWith('/cycles')) {
        return {
          data: [{
            id: cycleId,
            stokvelId,
            period: '2026-10',
          }],
        } as never
      }

      if (path.endsWith('/contributions')) {
        if (stokvelId === STOKVEL_B) {
          bContributionRequested = true
          return await slowB.promise as never
        }

        return {
          data: {
            items: [contribution(1, STOKVEL_A, CYCLE_A)],
            nextPageToken: null,
            pageSize: 25,
          },
        } as never
      }

      throw new Error(`Unexpected endpoint: ${path}`)
    })

    const client = makeClient()

    const view = render(
      <Harness stokvelId={STOKVEL_A} />,
      { wrapper: wrapper(client) },
    )

    await waitFor(() => {
      expect(screen.getByTestId('ids').textContent).toBe('payment-1')
    })

    // Switch to B, but deliberately keep B's contribution request pending.
    view.rerender(<Harness stokvelId={STOKVEL_B} />)

    await waitFor(() => {
      expect(bContributionRequested).toBe(true)
    })

    expect(
      screen.getByTestId('selected-stokvel').textContent,
    ).toBe(STOKVEL_B)

    // This is the critical regression assertion:
    // B is selected, but A's private data must not remain visible.
    expect(screen.getByTestId('ids').textContent).toBe('')
    expect(screen.getByTestId('count').textContent).toBe('0')
    expect(screen.getByTestId('loading').textContent).toBe('true')

    // Only now allow B's response to arrive.
    await act(async () => {
      slowB.resolve({
        data: {
          items: [contribution(2, STOKVEL_B, CYCLE_B)],
          nextPageToken: null,
          pageSize: 25,
        },
      })
    })

    await waitFor(() => {
      expect(screen.getByTestId('ids').textContent).toBe('payment-2')
    })

    expect(screen.getByTestId('ids').textContent).not.toContain('payment-1')

    // Switching back to A should retrieve A's own cached records.
    view.rerender(<Harness stokvelId={STOKVEL_A} />)

    await waitFor(() => {
      expect(screen.getByTestId('ids').textContent).toBe('payment-1')
    })

    client.clear()
  })

  it('handles an empty first page without offering Load more', async () => {
    get.mockImplementation(async path => {
      if (path.endsWith('/cycles')) {
        return {
          data: [{
            id: CYCLE_A,
            stokvelId: STOKVEL_A,
            period: '2026-10',
          }],
        } as never
      }

      return {
        data: {
          items: [],
          nextPageToken: null,
          pageSize: 25,
        },
      } as never
    })

    const client = makeClient()

    render(
      <Harness stokvelId={STOKVEL_A} />,
      { wrapper: wrapper(client) },
    )

    await waitFor(() => {
      expect(screen.getByTestId('loading').textContent).toBe('false')
    })

    expect(screen.getByTestId('count').textContent).toBe('0')
    expect(screen.getByTestId('has-next').textContent).toBe('false')

    client.clear()
  })
})
