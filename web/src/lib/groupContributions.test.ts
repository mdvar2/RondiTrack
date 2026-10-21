import { describe, expect, it } from 'vitest'
import { groupContributionsByCycle, type Cycle, type Contribution } from './groupContributions'

const stokvelId = '11111111-1111-4111-8111-111111111111'
const makeCycle = (id: string, period: string): Cycle => ({
  id, stokvelId, period, targetAmount: 100, version: 1,
  endDate: null, status: 'Open', dueDate: null,
  totalCollectedCents: 0, amountDueCents: 10000,
})
const makeContribution = (id: string, cycleId: string, recordedAtUtc: string): Contribution => ({
  id, stokvelId, contributionCycleId: cycleId,
  userId: '22222222-2222-4222-8222-222222222222',
  amount: 100, status: 'Paid', recordedAtUtc, version: 1,
})

describe('groupContributionsByCycle', () => {
  it('orders cycles and their contributions newest first without mutating input', () => {
    const old = makeCycle('old', '2026-09')
    const current = makeCycle('current', '2026-10')
    const cycles = [old, current]
    const olderPayment = makeContribution('older', 'current', '2026-10-01T00:00:00Z')
    const newerPayment = makeContribution('newer', 'current', '2026-10-20T00:00:00Z')
    const contributions = [olderPayment, newerPayment]
    const groups = groupContributionsByCycle(cycles, contributions)
    expect(groups.map((group) => group.cycle?.id)).toEqual(['current', 'old'])
    expect(groups[0].contributions.map((c) => c.id)).toEqual(['newer', 'older'])
    expect(cycles).toEqual([old, current])
    expect(contributions).toEqual([olderPayment, newerPayment])
  })

  it('retains empty cycles and never drops unknown-cycle contributions', () => {
    const empty = makeCycle('empty', '2026-10')
    const missing = makeContribution('unmatched', 'missing', '2026-10-20T00:00:00Z')
    const groups = groupContributionsByCycle([empty], [missing])
    expect(groups).toHaveLength(2)
    expect(groups[0].cycle?.id).toBe('empty')
    expect(groups[0].contributions).toEqual([])
    expect(groups[1].cycle).toBeNull()
    expect(groups[1].contributions.map((c) => c.id)).toEqual(['unmatched'])
    expect(groups.reduce((sum, group) => sum + group.contributions.length, 0)).toBe(1)
  })

  it('preserves every contribution across known and unknown cycles', () => {
    const cycles = [makeCycle('one', '2026-10'), makeCycle('two', '2026-09')]
    const payments = [
      makeContribution('a', 'one', '2026-10-03T00:00:00Z'),
      makeContribution('b', 'two', '2026-09-03T00:00:00Z'),
      makeContribution('c', 'missing', '2026-09-03T00:00:00Z'),
    ]
    const groups = groupContributionsByCycle(cycles, payments)
    expect(groups.flatMap((group) => group.contributions).map((c) => c.id).sort())
      .toEqual(['a', 'b', 'c'])
  })
})
