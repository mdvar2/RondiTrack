
import { describe, expect, it } from 'vitest'
import {
  formatCycleDueDate,
  formatJohannesburgDate,
  formatJohannesburgDateTime,
  getDueCountdown,
  getJohannesburgDeadlineUtc,
  johannesburgDayRangeUtc,
  johannesburgPeriod,
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

// CI runs every test file twice: TZ=UTC and TZ=Africa/Johannesburg. Nothing
// below may depend on the process's own time zone.
describe(`date rules (process TZ: ${process.env.TZ ?? 'unset'})`, () => {
  it.each([
    ['23:30 SAST on the last day of October', '2026-10-31T21:30:00Z', '2026-10'],
    ['23:59:59 SAST on the last day of October', '2026-10-31T21:59:59Z', '2026-10'],
    ['00:00 SAST on 1 November', '2026-10-31T22:00:00Z', '2026-11'],
    ['00:30 SAST on 1 November (still 31 Oct in UTC)', '2026-10-31T22:30:00Z', '2026-11'],
    ['23:30 SAST on 31 December rolls the year', '2026-12-31T21:30:00Z', '2026-12'],
    ['00:30 SAST on 1 January', '2026-12-31T22:30:00Z', '2027-01'],
    ['23:30 SAST on 28 February', '2027-02-28T21:30:00Z', '2027-02'],
    ['a +02:00 timestamp from the API', '2026-10-31T23:30:00+02:00', '2026-10'],
  ])('a contribution at %s belongs to %s', (_name, utc, period) => {
    expect(johannesburgPeriod(utc)).toBe(period)
  })

  it('a due date ends at midnight SAST, which is 22:00 UTC the day before', () => {
    expect(getJohannesburgDeadlineUtc('2026-10-31').toISOString()).toBe('2026-10-31T22:00:00.000Z')
  })

  it.each([
    ['2026-10-31T21:59:00Z', 'Due today'], // 23:59 SAST on the due date
    ['2026-10-31T22:00:00Z', 'Overdue by 1 day'], // 00:00 SAST the next day
    ['2026-10-30T22:00:00Z', 'Due today'], // 00:00 SAST on the due date
    ['2026-10-30T21:59:00Z', 'Due tomorrow'],
    ['2026-10-21T10:00:00Z', 'Due in 10 days'],
  ])('at %s a cycle due 2026-10-31 says %j', (now, text) => {
    expect(getDueCountdown('2026-10-31', new Date(now))).toBe(text)
  })

  it('"this month" filter days are whole SAST days', () => {
    expect(johannesburgDayRangeUtc('2026-11-01', '2026-11-30')).toEqual({
      start: Date.parse('2026-10-31T22:00:00Z'),
      endExclusive: Date.parse('2026-11-30T22:00:00Z'),
    })
  })

  it('refuses a date that does not exist', () => {
    expect(() => johannesburgDayRangeUtc('2026-02-30')).toThrow(RangeError)
    expect(() => johannesburgPeriod('not a date')).toThrow(RangeError)
  })
})
