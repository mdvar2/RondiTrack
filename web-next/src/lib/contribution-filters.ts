export type ContributionStatusFilter = 'all' | 'paid' | 'pending'

export type ContributionHistoryFilterState = {
  status: ContributionStatusFilter
  from?: string
  to?: string
  q?: string
  page?: number
}

export type ContributionActivityItem = {
  id: string
  label: string
  memberName: string
  status: 'paid' | 'pending'
  date: string
  amountCents: number
}

export type ContributionSourceRow = {
  id: string
  stokvelId?: string
  stokvelName?: string
  userName?: string
  amount?: number | string
  recordedAtUtc?: string
  status?: 'paid' | 'pending'
}

export function buildContributionActivityRows(rows: ContributionSourceRow[]): ContributionActivityItem[] {
  return rows
    .filter((row) => row?.id && row.recordedAtUtc)
    .map((row) => ({
      id: row.id,
      label: row.stokvelName ? `${row.stokvelName} contribution` : 'Contribution',
      memberName: row.userName ?? 'Member payment',
      status: row.status ?? 'paid',
      date: row.recordedAtUtc ?? new Date(0).toISOString(),
      amountCents: Math.round(Number(row.amount ?? 0) * 100),
    }))
    .sort((a, b) => Date.parse(b.date) - Date.parse(a.date))
}

export function normaliseContributionHistoryFilters(
  filters: Partial<ContributionHistoryFilterState> = {},
): ContributionHistoryFilterState {
  const status = filters.status === 'paid' || filters.status === 'pending' ? filters.status : 'all'
  const from = typeof filters.from === 'string' && filters.from.trim() ? filters.from.trim() : undefined
  const to = typeof filters.to === 'string' && filters.to.trim() ? filters.to.trim() : undefined
  const q = typeof filters.q === 'string' ? filters.q.trim() : ''
  const page = Number.isFinite(filters.page) && Number(filters.page) > 0 ? Number(filters.page) : 1

  return { status, from, to, q, page }
}

export function matchesContributionHistoryFilters<T extends {
  status?: string
  date?: string
  amountCents?: number
  memberName?: string
  label?: string
}>(
  item: T,
  filters: Partial<ContributionHistoryFilterState>,
): boolean {
  const normalised = normaliseContributionHistoryFilters(filters)
  const itemStatus = item.status ?? 'paid'
  const itemDate = item.date ?? new Date(0).toISOString()
  const date = new Date(itemDate)
  const start = normalised.from ? new Date(`${normalised.from}T00:00:00Z`) : null
  const end = normalised.to ? new Date(`${normalised.to}T23:59:59Z`) : null

  if (normalised.status !== 'all' && itemStatus !== normalised.status) {
    return false
  }

  if (start && date < start) {
    return false
  }

  if (end && date > end) {
    return false
  }

  if (normalised.q) {
    const haystack = [item.memberName ?? '', item.label ?? '', String(item.amountCents ?? '')].join(' ').toLowerCase()
    if (!haystack.includes(normalised.q.toLowerCase())) {
      return false
    }
  }

  return true
}

export function paginateContributionHistory<T>(items: T[], page: number, pageSize: number): { items: T[]; nextCursor: number | null } {
  const safePage = Number.isFinite(page) && Number(page) > 0 ? Number(page) : 1
  const safePageSize = Number.isFinite(pageSize) && Number(pageSize) > 0 ? Number(pageSize) : 10
  const start = (safePage - 1) * safePageSize
  const sliced = items.slice(start, start + safePageSize)

  return {
    items: sliced,
    nextCursor: start + safePageSize < items.length ? safePage + 1 : null,
  }
}
