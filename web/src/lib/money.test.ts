
import { describe, expect, it } from 'vitest'
import { formatCents } from './money'

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
