
import { describe, expect, it } from 'vitest'
import { centsToInput, formatCents, parseRandsToCents } from './money'

describe('formatCents', () => {
  it.each([
    [0, 0, '00'],
    [10, 0, '10'],
    [15050, 150, '50'],
    [125100, 1251, '00'],
    [72500, 725, '00'],
  ])('formats %i cents correctly', (cents, rand, fraction) => {
    const result = formatCents(cents)

    expect(result).toContain('R')
    expect(result).toContain(
      `${fraction}`
    )
    expect(result.replace(/\s/g, '')).toContain(
      `${rand.toLocaleString('en-ZA').replace(/\s/g, '')},${fraction}`
    )
  })

  it('rejects fractional cents', () => {
    expect(() => formatCents(10.5)).toThrow(RangeError)
  })

  it('rejects unsafe integers', () => {
    expect(() => formatCents(Number.MAX_SAFE_INTEGER + 1))
      .toThrow(RangeError)
  })

  it('supports negative amounts', () => {
    const result = formatCents(-15050)
    expect(result).toContain('150,50')
    expect(result).toContain('-')
  })
})

describe('parseRandsToCents', () => {
  it.each([
    ['150.50', 15050],
    ['150.5', 15050],
    ['150', 15000],
    ['19.99', 1999],
    ['0.07', 7],
    ['150,50', 15050],
    ['1 500,50', 150050],
    ['1\u00a0500,50', 150050],
    [' 20 ', 2000],
  ])('reads "%s" as %i cents', (text, cents) => {
    expect(parseRandsToCents(text)).toBe(cents)
  })

  it.each(['', 'abc', '150.505', '1,500.50', '-5', '1.', '.5', '1 50', '12 3456', '1e3'])(
    'refuses "%s"',
    (text) => {
      expect(parseRandsToCents(text)).toBeNull()
    },
  )
})

describe('centsToInput', () => {
  it('writes cents as an editable amount', () => {
    expect(centsToInput(15050)).toBe('150.50')
    expect(centsToInput(7)).toBe('0.07')
    expect(centsToInput(0)).toBe('0.00')
  })

  it('reads back as exactly the same cents for 0 to 100 000', () => {
    for (let cents = 0; cents <= 100000; cents++) {
      expect(parseRandsToCents(centsToInput(cents))).toBe(cents)
    }
  })
})
