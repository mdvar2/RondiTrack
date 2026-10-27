
import { describe, expect, it } from 'vitest'
import {
  formatCycleDueDate,
  formatJohannesburgDate,
  formatJohannesburgDateTime,
} from './dates'

describe('Johannesburg date formatting', () => {
  it('converts UTC timestamps across midnight', () => {
    expect(
      formatJohannesburgDateTime('2026-10-31T22:30:00Z'),
    ).toBe('01 Nov 2026, 00:30')
  })

  it('formats a UTC timestamp during the day', () => {
    expect(
      formatJohannesburgDateTime('2026-10-19T12:45:00Z'),
    ).toBe('19 Oct 2026, 14:45')
  })

  it('formats the Johannesburg calendar date', () => {
    expect(
      formatJohannesburgDate('2026-10-31T22:30:00Z'),
    ).toBe('01 Nov 2026')
  })

  it('preserves date-only due dates', () => {
    expect(
      formatCycleDueDate('2026-10-31'),
    ).toBe('31 Oct 2026')
  })

  it('rejects invalid timestamps', () => {
    expect(() =>
      formatJohannesburgDateTime('invalid'),
    ).toThrow(RangeError)
  })

  it('rejects invalid calendar dates', () => {
    expect(() =>
      formatCycleDueDate('2026-02-30'),
    ).toThrow(RangeError)
  })
})
