import { describe, expect, it } from 'vitest'
import {
  buildContributionActivityRows,
  matchesContributionHistoryFilters,
  normaliseContributionHistoryFilters,
  paginateContributionHistory,
} from './contribution-filters'

describe('contribution history filters', () => {
  it('normalises a partial filter set', () => {
    expect(normaliseContributionHistoryFilters({ status: 'paid', from: ' 2025-01-01 ', page: 0 })).toEqual({
      status: 'paid',
      from: '2025-01-01',
      to: undefined,
      q: '',
      page: 1,
    })
  })

  it('matches the expected date and status filters', () => {
    const item = {
      label: 'May contribution',
      memberName: 'Ame',
      status: 'paid',
      date: '2025-05-12T10:00:00Z',
      amountCents: 45000,
    }

    expect(matchesContributionHistoryFilters(item, { status: 'paid', from: '2025-05-01', to: '2025-05-31' })).toBe(true)
    expect(matchesContributionHistoryFilters(item, { status: 'pending' })).toBe(false)
    expect(matchesContributionHistoryFilters(item, { q: 'ame' })).toBe(true)
  })

  it('builds real contribution activity from backend rows', () => {
    const rows = buildContributionActivityRows([
      {
        id: 'c1',
        stokvelName: 'Building Fund',
        userName: 'Jane Smith',
        // The API's int64 cents, as ContributionWithMemberResponse sends them.
        amount: 12550,
        recordedAtUtc: '2025-05-15T08:30:00Z',
      },
      {
        id: 'c2',
        stokvelName: 'School Fees',
        userName: 'Ame Ndlovu',
        amount: 3500,
        recordedAtUtc: '2025-05-10T08:00:00Z',
      },
    ])

    expect(rows).toEqual([
      {
        id: 'c1',
        label: 'Building Fund contribution',
        memberName: 'Jane Smith',
        status: 'paid',
        date: '2025-05-15T08:30:00Z',
        amountCents: 12550,
      },
      {
        id: 'c2',
        label: 'School Fees contribution',
        memberName: 'Ame Ndlovu',
        status: 'paid',
        date: '2025-05-10T08:00:00Z',
        amountCents: 3500,
      },
    ])
  })

  it('never multiplies: 12 550 cents from the API stays 12 550 cents', () => {
    expect(buildContributionActivityRows([{ id: 'c', amount: 12550, recordedAtUtc: '2025-05-15T08:30:00Z' }])[0].amountCents).toBe(12550)
  })

  // Whole Johannesburg days, whatever TZ the test process runs in (CI runs
  // this file with TZ=UTC and TZ=Africa/Johannesburg).
  it.each([
    ['00:30 SAST on 1 Nov is in "from 1 Nov"', '2026-10-31T22:30:00Z', { from: '2026-11-01' }, true],
    ['23:30 SAST on 31 Oct is not in "from 1 Nov"', '2026-10-31T21:30:00Z', { from: '2026-11-01' }, false],
    ['23:30 SAST on 31 Oct is in "to 31 Oct"', '2026-10-31T21:30:00Z', { to: '2026-10-31' }, true],
    ['00:00 SAST on 1 Nov is not in "to 31 Oct"', '2026-10-31T22:00:00Z', { to: '2026-10-31' }, false],
  ] as const)('%s', (_name, date, filters, expected) => {
    expect(matchesContributionHistoryFilters({ status: 'paid', date }, filters)).toBe(expected)
  })

  it('paginates a list of recent activities', () => {
    const items = Array.from({ length: 23 }, (_, index) => ({ id: `${index}` }))
    expect(paginateContributionHistory(items, 2, 10)).toEqual({
      items: items.slice(10, 20),
      nextCursor: 3,
    })
  })
})
