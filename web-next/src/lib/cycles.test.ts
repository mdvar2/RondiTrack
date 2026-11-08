import { describe, expect, it } from 'vitest'
import { pickCurrentCycle } from './cycles'

// GET /cycles lists the newest first, as the API's repository orders it.
const year = [
  { id: 'nov', period: '2026-11', status: 'Open' },
  { id: 'oct', period: '2026-10', status: 'Closed' },
  { id: 'sep', period: '2026-09', status: 'Closed' },
  { id: 'dec', period: '2025-12', status: 'Closed' },
]

describe('pickCurrentCycle', () => {
  it('picks the current month, not the oldest cycle (the cycles.at(-1) bug)', () => {
    expect(pickCurrentCycle(year, '2026-11')?.id).toBe('nov')
    expect(year.at(-1)?.id).toBe('dec') // what the page used to record against
  })

  it('prefers the open cycle when a month has two', () => {
    expect(pickCurrentCycle([{ id: 'a', period: '2026-11', status: 'Closed' }, { id: 'b', period: '2026-11', status: 'Open' }], '2026-11')?.id).toBe('b')
  })

  it('falls back to an open cycle, then the newest, when the month has none', () => {
    expect(pickCurrentCycle(year, '2027-01')?.id).toBe('nov')
    expect(pickCurrentCycle(year.slice(1), null)?.id).toBe('oct')
  })

  it('has nothing to pick from an empty list', () => {
    expect(pickCurrentCycle([], '2026-11')).toBeNull()
  })
})
