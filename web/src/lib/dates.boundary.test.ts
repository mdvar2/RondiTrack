import { describe, expect, it } from 'vitest'
import {
  getDueCountdown,
  getNextJohannesburgMidnightDelay,
  getJohannesburgDeadlineUtc,
} from './dates'

describe('Johannesburg calendar deadlines', () => {
  it('treats the date-only due date as the end of that SA calendar day', () => {
    expect(getJohannesburgDeadlineUtc('2026-10-31').toISOString())
      .toBe('2026-10-31T22:00:00.000Z')
  })
  it('does not mark 23:30 SAST on the due date overdue', () => {
    expect(getDueCountdown('2026-10-31', new Date('2026-10-31T21:30:00Z')))
      .toBe('Due today')
  })
  it('becomes overdue just after midnight in Johannesburg', () => {
    expect(getDueCountdown('2026-10-31', new Date('2026-10-31T22:00:01Z')))
      .toBe('Overdue by 1 day')
  })
  it('recognises 00:30 SAST as the due date', () => {
    expect(getDueCountdown('2026-10-31', new Date('2026-10-30T22:30:00Z')))
      .toBe('Due today')
  })
  it('schedules midnight in 30 minutes at 23:30 SAST', () => {
    expect(getNextJohannesburgMidnightDelay(new Date('2026-10-31T21:30:00Z')))
      .toBe(30 * 60 * 1000)
  })
  it('schedules the following midnight rather than looping at midnight', () => {
    expect(getNextJohannesburgMidnightDelay(new Date('2026-10-31T22:00:00Z')))
      .toBe(24 * 60 * 60 * 1000)
  })
})
