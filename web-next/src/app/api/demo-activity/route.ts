import { NextResponse } from 'next/server'
import { api, readOrFail } from '@/lib/api'
import { getSession } from '@/lib/session'
import { buildContributionActivityRows, matchesContributionHistoryFilters, paginateContributionHistory } from '@/lib/contribution-filters'

async function fetchRealContributionActivity() {
  const client = await api()
  const { stokvels } = readOrFail(await client.GET('/api/v1/me/home'))

  const activityRows = await Promise.all(stokvels.map(async (stokvel) => {
    const cyclesResult = await client.GET('/api/v1/stokvels/{stokvelId}/cycles', {
      params: { path: { stokvelId: stokvel.stokvelId } },
    })
    const cycles = readOrFail(cyclesResult)
    const latestCycleId = cycles.at(-1)?.id

    if (!latestCycleId) {
      return []
    }

    const contributionsResult = await client.GET('/api/v1/stokvels/{stokvelId}/cycles/{cycleId}/contributions', {
      params: {
        path: { stokvelId: stokvel.stokvelId, cycleId: latestCycleId },
        query: {
          pageSize: 100,
          sortBy: 'recordedAtUtc',
          sortDirection: 'desc',
        },
      },
    })

    const page = readOrFail(contributionsResult)
    const items = Array.isArray(page.items) ? page.items : []

    return buildContributionActivityRows(items.map((item) => ({
      id: item.id,
      stokvelId: stokvel.stokvelId,
      stokvelName: stokvel.stokvelName,
      userName: item.userName,
      amount: item.amount,
      recordedAtUtc: item.recordedAtUtc,
      status: 'paid',
    })))
  }))

  return activityRows.flat()
}

export async function GET(request: Request) {
  const session = await getSession()
  if (!session) {
    return NextResponse.json({ error: 'Unauthorized' }, { status: 401 })
  }

  const { searchParams } = new URL(request.url)
  const q = searchParams.get('q') ?? ''
  const status = (searchParams.get('status') ?? 'all') as 'all' | 'paid' | 'pending'
  const from = searchParams.get('from') ?? ''
  const to = searchParams.get('to') ?? ''
  const page = Number(searchParams.get('page') ?? '1')

  const rows = await fetchRealContributionActivity()
  const filtered = rows.filter((row) =>
    matchesContributionHistoryFilters(row, { status, from, to, q }),
  )

  return NextResponse.json(paginateContributionHistory(filtered, page, 10))
}
