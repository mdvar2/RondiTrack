'use client'

import { useInfiniteQuery } from '@tanstack/react-query'
import { parseAsString, parseAsStringEnum, useQueryState } from 'nuqs'
import { useMemo } from 'react'

const statusParser = parseAsStringEnum(['all', 'paid', 'pending']).withDefault('all')

type ActivityApiResponse = {
  items: Array<{
    id: string
    label: string
    memberName: string
    status: 'paid' | 'pending'
    date: string
    amountCents: number
  }>
  nextCursor: number | null
}

async function fetchActivityPage(page: number, status: string, q: string, from: string, to: string): Promise<ActivityApiResponse> {
  const params = new URLSearchParams({ page: String(page), status, q, from, to })
  const response = await fetch(`/api/demo-activity?${params.toString()}`)

  if (!response.ok) {
    throw new Error('Unable to load recent activity')
  }

  return (await response.json()) as ActivityApiResponse
}

export function ActivityFeed() {
  const [q, setQ] = useQueryState('q', parseAsString.withDefault(''))
  const [status, setStatus] = useQueryState('status', statusParser)
  const [from, setFrom] = useQueryState('from', parseAsString.withDefault(''))
  const [to, setTo] = useQueryState('to', parseAsString.withDefault(''))

  const query = useInfiniteQuery({
    queryKey: ['demo-activity', q, status, from, to],
    initialPageParam: 1,
    queryFn: ({ pageParam }) => fetchActivityPage(Number(pageParam), status, q, from, to),
    getNextPageParam: (lastPage) => lastPage.nextCursor,
    retry: 1,
  })

  const items = useMemo(
    () => query.data?.pages.flatMap((page) => page.items) ?? [],
    [query.data],
  )

  if (query.isPending) {
    return <div className="rounded-lg border bg-card p-4 text-sm text-muted-foreground">Loading recent activity…</div>
  }

  if (query.isError) {
    return <div className="rounded-lg border border-destructive/40 bg-destructive/5 p-4 text-sm text-destructive">{query.error.message}</div>
  }

  return (
    <section className="space-y-4 rounded-lg border bg-card p-4">
      <div className="flex flex-col gap-3 md:flex-row md:items-center md:justify-between">
        <h2 className="text-lg font-semibold">Recent activity</h2>
        <div className="flex gap-2 text-sm">
          <input
            value={q}
            onChange={(event) => void setQ(event.target.value)}
            className="rounded border bg-background px-2 py-1.5"
            placeholder="Search activity"
          />
          <select
            value={status}
            onChange={(event) => void setStatus(event.target.value as 'all' | 'paid' | 'pending')}
            className="rounded border bg-background px-2 py-1.5"
          >
            <option value="all">All</option>
            <option value="paid">Paid</option>
            <option value="pending">Pending</option>
          </select>
          <input type="date" value={from} onChange={(event) => void setFrom(event.target.value)} className="rounded border bg-background px-2 py-1.5" />
          <input type="date" value={to} onChange={(event) => void setTo(event.target.value)} className="rounded border bg-background px-2 py-1.5" />
        </div>
      </div>

      {items.length === 0 ? (
        <p className="text-sm text-muted-foreground">No activity matches those filters.</p>
      ) : (
        <ul className="space-y-3">
          {items.map((item) => (
            <li key={item.id} className="flex items-center justify-between gap-3 rounded-md border p-3">
              <div>
                <p className="font-medium">{item.label}</p>
                <p className="text-sm text-muted-foreground">{item.memberName} · {new Date(item.date).toLocaleDateString('en-ZA')}</p>
              </div>
              <div className="text-right">
                <p className="font-medium">{item.amountCents >= 0 ? `+R ${item.amountCents / 100}` : `-R ${Math.abs(item.amountCents) / 100}`}</p>
                <p className="text-xs uppercase tracking-wide text-muted-foreground">{item.status}</p>
              </div>
            </li>
          ))}
        </ul>
      )}

      {query.hasNextPage && (
        <button type="button" onClick={() => void query.fetchNextPage()} className="rounded border px-3 py-2 text-sm">
          Load more
        </button>
      )}
    </section>
  )
}
