'use client'

import { parseAsString, parseAsStringEnum, useQueryState } from 'nuqs'

const statusParser = parseAsStringEnum(['all', 'paid', 'pending']).withDefault('all')

export function ContributionHistoryFilters() {
  const [status, setStatus] = useQueryState('status', statusParser)
  const [from, setFrom] = useQueryState('from', parseAsString.withDefault(''))
  const [to, setTo] = useQueryState('to', parseAsString.withDefault(''))
  const [search, setSearch] = useQueryState('q', parseAsString.withDefault(''))

  function clearFilters() {
    void setStatus('all')
    void setFrom('')
    void setTo('')
    void setSearch('')
  }

  return (
    <div className="mt-4 grid gap-3 rounded-lg border bg-card p-4 md:grid-cols-[1.2fr_1fr_1fr_1fr_auto]">
      <label className="space-y-1 text-sm">
        <span>Search</span>
        <input
          value={search}
          onChange={(event) => void setSearch(event.target.value)}
          className="w-full rounded border bg-background px-2 py-2"
          placeholder="Member or amount"
        />
      </label>

      <label className="space-y-1 text-sm">
        <span>Status</span>
        <select
          value={status}
          onChange={(event) => void setStatus(event.target.value as 'all' | 'paid' | 'pending')}
          className="w-full rounded border bg-background px-2 py-2"
        >
          <option value="all">All</option>
          <option value="paid">Paid</option>
          <option value="pending">Pending</option>
        </select>
      </label>

      <label className="space-y-1 text-sm">
        <span>From</span>
        <input
          type="date"
          value={from}
          onChange={(event) => void setFrom(event.target.value)}
          className="w-full rounded border bg-background px-2 py-2"
        />
      </label>

      <label className="space-y-1 text-sm">
        <span>To</span>
        <input
          type="date"
          value={to}
          onChange={(event) => void setTo(event.target.value)}
          className="w-full rounded border bg-background px-2 py-2"
        />
      </label>

      <button type="button" onClick={clearFilters} className="self-end rounded border px-3 py-2 text-sm">
        Clear
      </button>
    </div>
  )
}
