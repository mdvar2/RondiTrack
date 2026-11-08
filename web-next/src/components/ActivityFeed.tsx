'use client'

import { useInfiniteQuery } from '@tanstack/react-query'
import { parseAsString, parseAsStringEnum, useQueryState } from 'nuqs'
import { useId, useMemo } from 'react'
import { formatCents } from '@/lib/money'
import { formatJohannesburgDate } from '@/lib/dates'
import type { ContributionActivityItem } from '@/lib/contribution-filters'
import type { ActivityFilters } from '@/lib/activity-filters'

const statusParser = parseAsStringEnum(['all', 'paid', 'pending']).withDefault('all')

export type ActivityPage = { items: ContributionActivityItem[]; nextCursor: number | null }

async function fetchActivityPage(page: number, filters: ActivityFilters): Promise<ActivityPage> {
  const params = new URLSearchParams({ page: String(page), ...filters })
  const response = await fetch(`/api/demo-activity?${params.toString()}`)
  if (!response.ok) throw new Error('Recent activity could not be loaded.')
  return (await response.json()) as ActivityPage
}

type ActivityFeedProps = {
  // The first page, rendered by the server for `initialFilters`, so it is in
  // the HTML (it was the home screen's LCP element, waiting on a client fetch).
  initialPage: ActivityPage
  initialFilters: ActivityFilters
  showFilters?: boolean
}

export function ActivityFeed({ initialPage, initialFilters, showFilters = true }: ActivityFeedProps) {
  const id = useId()
  const [q, setQ] = useQueryState('q', parseAsString.withDefault(''))
  const [status, setStatus] = useQueryState('status', statusParser)
  const [from, setFrom] = useQueryState('from', parseAsString.withDefault(''))
  const [to, setTo] = useQueryState('to', parseAsString.withDefault(''))
  const filters: ActivityFilters = { q, status, from, to }
  const sameAsServer = JSON.stringify(filters) === JSON.stringify(initialFilters)

  const query = useInfiniteQuery({
    queryKey: ['activity', filters],
    initialPageParam: 1,
    queryFn: ({ pageParam }) => fetchActivityPage(Number(pageParam), filters),
    getNextPageParam: (lastPage) => lastPage.nextCursor,
    initialData: sameAsServer ? { pages: [initialPage], pageParams: [1] } : undefined,
    retry: 1,
  })

  const items = useMemo(() => query.data?.pages.flatMap((page) => page.items) ?? [], [query.data])

  return (
    <section aria-labelledby={`${id}-heading`} className="space-y-4 rounded-lg border bg-card p-4">
      <div className="flex flex-col gap-3 md:flex-row md:items-end md:justify-between">
        <h2 id={`${id}-heading`} className="text-lg font-semibold">Recent activity</h2>
        {showFilters && (
          <div className="grid grid-cols-2 gap-2 text-sm sm:flex">
            <label className="col-span-2 space-y-1">
              <span className="block text-xs font-medium">Search activity</span>
              <input type="search" value={q} onChange={(event) => void setQ(event.target.value)} className="h-9 w-full rounded border bg-background px-2" />
            </label>
            <label className="space-y-1">
              <span className="block text-xs font-medium">Status</span>
              <select value={status} onChange={(event) => void setStatus(event.target.value as ActivityFilters['status'])} className="h-9 w-full rounded border bg-background px-2">
                <option value="all">All</option>
                <option value="paid">Paid</option>
                <option value="pending">Pending</option>
              </select>
            </label>
            <label className="space-y-1">
              <span className="block text-xs font-medium">From</span>
              <input type="date" value={from} onChange={(event) => void setFrom(event.target.value)} className="h-9 w-full rounded border bg-background px-2" />
            </label>
            <label className="space-y-1">
              <span className="block text-xs font-medium">To</span>
              <input type="date" value={to} onChange={(event) => void setTo(event.target.value)} className="h-9 w-full rounded border bg-background px-2" />
            </label>
          </div>
        )}
      </div>

      {query.isPending ? (
        <p role="status" className="text-sm text-muted-foreground">Loading recent activity…</p>
      ) : query.isError && items.length === 0 ? (
        <div role="alert" className="space-y-2 text-sm">
          <p>{query.error.message}</p>
          <button type="button" onClick={() => void query.refetch()} className="min-h-8 rounded border px-3">Try again</button>
        </div>
      ) : items.length === 0 ? (
        <p className="text-sm text-muted-foreground">No activity matches those filters.</p>
      ) : (
        <ul className="space-y-3">
          {items.map((item) => (
            <li key={item.id} className="flex items-center justify-between gap-3 rounded-md border p-3">
              <div className="min-w-0">
                <p className="font-medium">{item.label}</p>
                <p className="text-sm text-muted-foreground">{item.memberName} · {formatJohannesburgDate(item.date)}</p>
              </div>
              <div className="text-right">
                <p className="font-medium whitespace-nowrap tabular-nums">{formatCents(item.amountCents)}</p>
                <p className="text-xs tracking-wide text-muted-foreground uppercase">{item.status}</p>
              </div>
            </li>
          ))}
        </ul>
      )}

      {/* A failed "Load more" keeps what is already shown and offers a retry,
          instead of replacing the whole list with an error. */}
      {query.isFetchNextPageError && (
        <p role="alert" className="text-sm">More activity could not be loaded.</p>
      )}
      {(query.hasNextPage || query.isFetchNextPageError) && (
        <button
          type="button"
          onClick={() => { if (!query.isFetchingNextPage) void query.fetchNextPage() }}
          aria-disabled={query.isFetchingNextPage}
          className="min-h-8 rounded border px-3 text-sm aria-disabled:opacity-60"
        >
          {query.isFetchingNextPage ? 'Loading…' : query.isFetchNextPageError ? 'Try again' : 'Load more'}
        </button>
      )}
    </section>
  )
}
