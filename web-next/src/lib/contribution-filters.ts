import { johannesburgDayRangeUtc } from './dates'

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
      // The API already sends cents. Multiplying by 100 showed R50 000,00 for a
      // R500,00 contribution (found by the Part 5 seed); never multiply money.
      amountCents: Number.isSafeInteger(row.amount) ? row.amount as number : 0,
    }))
    .sort((a, b) => Date.parse(b.date) - Date.parse(a.date))
}

export function normaliseContributionHistoryFilters(
  filters: Partial<ContributionHistoryFilterState> = {},
): ContributionHistoryFilterState {
  const status = filters.status === 'paid' || filters.status === 'pending' ? filters.status : 'all'
  // A hand-edited ?from=yesterday is ignored rather than failing the request.
  const from = asDateOnly(filters.from)
  const to = asDateOnly(filters.to)
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
  const time = Date.parse(itemDate)
  // Whole Johannesburg days. UTC boundaries put a 00:30 SAST contribution on
  // 1 November outside a "from 1 November" filter.
  const { start, endExclusive } = johannesburgDayRangeUtc(normalised.from, normalised.to)

  if (normalised.status !== 'all' && itemStatus !== normalised.status) {
    return false
  }

  if (start !== null && time < start) {
    return false
  }

  if (endExclusive !== null && time >= endExclusive) {
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

function asDateOnly(value: unknown): string | undefined {
  if (typeof value !== 'string') return undefined
  const text = value.trim()
  if (!/^\d{4}-\d{2}-\d{2}$/.test(text)) return undefined
  return new Date(`${text}T12:00:00Z`).toISOString().slice(0, 10) === text ? text : undefined
}
