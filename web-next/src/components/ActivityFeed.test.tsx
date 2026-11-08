import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { NuqsTestingAdapter } from 'nuqs/adapters/testing'
import { http, HttpResponse } from 'msw'
import { describe, expect, it } from 'vitest'
import { server } from '@/test/msw'
import { ActivityFeed, type ActivityPage } from './ActivityFeed'
import type { ActivityFilters } from '@/lib/activity-filters'

// "Load more" goes over the network from the browser, so MSW answers it.
// The first page comes from the server, as it does on the real page.
const row = (n: number) => ({ id: `c${n}`, label: 'Masakhane contribution', memberName: `Member ${n}`, status: 'paid' as const, date: '2026-11-02T08:00:00Z', amountCents: 50000 })
const firstPage: ActivityPage = { items: [row(1), row(2)], nextCursor: 2 }
const filters: ActivityFilters = { q: '', status: 'paid', from: '', to: '' }

function renderFeed() {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false, staleTime: 60_000 } } })
  render(
    <NuqsTestingAdapter searchParams="?status=paid" rateLimitFactor={1}>
      <QueryClientProvider client={client}>
        <ActivityFeed initialPage={firstPage} initialFilters={filters} />
      </QueryClientProvider>
    </NuqsTestingAdapter>,
  )
  return userEvent.setup()
}

describe('ActivityFeed', () => {
  it('shows the server page at once, without a request, in rands and SAST dates', () => {
    renderFeed()
    expect(screen.getByText('Member 1 · 02 Nov 2026')).toBeInTheDocument()
    expect(screen.getAllByText('R 500,00')).toHaveLength(2)
  })

  it('"Load more" asks for page 2 with the same filter and appends it', async () => {
    const requests: URL[] = []
    server.use(http.get('*/api/demo-activity', ({ request }) => {
      requests.push(new URL(request.url))
      return HttpResponse.json({ items: [row(3)], nextCursor: null } satisfies ActivityPage)
    }))
    const user = renderFeed()
    await user.click(screen.getByRole('button', { name: 'Load more' }))

    expect(await screen.findByText('Member 3 · 02 Nov 2026')).toBeInTheDocument()
    expect(requests).toHaveLength(1)
    expect(requests[0].searchParams.get('page')).toBe('2')
    expect(requests[0].searchParams.get('status')).toBe('paid')
    expect(screen.queryByRole('button', { name: 'Load more' })).not.toBeInTheDocument()
  })

  it('a failed "Load more" keeps what is shown and offers "Try again"', async () => {
    let fail = true
    server.use(http.get('*/api/demo-activity', () => (fail
      ? HttpResponse.json({ title: 'Boom' }, { status: 500 })
      : HttpResponse.json({ items: [row(3)], nextCursor: null } satisfies ActivityPage))))
    const user = renderFeed()
    await user.click(screen.getByRole('button', { name: 'Load more' }))

    // The feed retries once (about 1 s) before it reports the failure.
    expect(await screen.findByText('More activity could not be loaded.', {}, { timeout: 5000 })).toBeInTheDocument()
    expect(screen.getByText('Member 1 · 02 Nov 2026')).toBeInTheDocument()
    fail = false
    await user.click(screen.getByRole('button', { name: 'Try again' }))
    expect(await screen.findByText('Member 3 · 02 Nov 2026')).toBeInTheDocument()
  })
})
