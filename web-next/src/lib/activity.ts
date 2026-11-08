import 'server-only'
import { api, readOrFail } from './api'
import { pickCurrentCycle } from './cycles'
import {
  buildContributionActivityRows,
  matchesContributionHistoryFilters,
  normaliseContributionHistoryFilters,
  paginateContributionHistory,
  type ContributionActivityItem,
  type ContributionHistoryFilterState,
} from './contribution-filters'
import type { HomeStokvel } from './types'

export type ActivityPage = { items: ContributionActivityItem[]; nextCursor: number | null }
export const ACTIVITY_PAGE_SIZE = 10

// The current cycle's contributions in every stokvel this person belongs to,
// newest first. Used by /api/demo-activity for "Load more" and filters, and
// by the pages themselves for the first page, so the first page arrives with
// the HTML instead of after the JavaScript has loaded and asked for it.
export async function loadActivityPage(
  stokvels: readonly HomeStokvel[],
  filters: Partial<ContributionHistoryFilterState>,
): Promise<ActivityPage> {
  const client = await api()
  const normalised = normaliseContributionHistoryFilters(filters)

  const perStokvel = await Promise.all(stokvels.map(async (stokvel) => {
    const cycles = readOrFail(await client.GET('/api/v1/stokvels/{stokvelId}/cycles', {
      params: { path: { stokvelId: stokvel.stokvelId } },
    }))
    const cycle = pickCurrentCycle(cycles, stokvel.currentCycle?.period)
    if (!cycle) return []

    const page = readOrFail(await client.GET('/api/v1/stokvels/{stokvelId}/cycles/{cycleId}/contributions', {
      params: {
        path: { stokvelId: stokvel.stokvelId, cycleId: cycle.id },
        query: { pageSize: 100, sortBy: 'recordedAtUtc', sortDirection: 'desc' },
      },
    }))
    return buildContributionActivityRows((page.items ?? []).map((item) => ({
      id: item.id,
      stokvelId: stokvel.stokvelId,
      stokvelName: stokvel.stokvelName,
      userName: item.userName,
      amount: item.amount,
      recordedAtUtc: item.recordedAtUtc,
      status: 'paid' as const,
    })))
  }))

  const rows = perStokvel.flat()
    .sort((a, b) => Date.parse(b.date) - Date.parse(a.date))
    .filter((row) => matchesContributionHistoryFilters(row, normalised))
  return paginateContributionHistory(rows, normalised.page ?? 1, ACTIVITY_PAGE_SIZE)
}

export async function loadHomeStokvels(): Promise<HomeStokvel[]> {
  const client = await api()
  return readOrFail(await client.GET('/api/v1/me/home')).stokvels
}
