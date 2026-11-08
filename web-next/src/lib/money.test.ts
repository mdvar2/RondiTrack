import { describe, expect, it } from 'vitest'
import { centsToInput, formatCents, MAX_CONTRIBUTION_CENTS, parseRandsToCents } from './money'

const NBSP = ' '
const NNBSP = ' '

describe('parseRandsToCents accepts the ways South Africans write amounts', () => {
  it.each([
    ['1500', 150000],
    ['1 500', 150000],
    ['1 500,50', 150050],
    ['R1500', 150000],
    ['R 1 500,50', 150050],
    ['r1500', 150000],
    [`1${NBSP}500,50`, 150050], // what Intl itself produces between thousands
    [`R${NBSP}1${NBSP}500,50`, 150050], // formatCents(150050), pasted back in
    [`1${NNBSP}500`, 150000], // narrow no-break space, as some locales use
    ['1 000 000', 100000000],
    [',5', 50], // one decimal place: 50 cents, not 5
    ['0,5', 50],
    ['150,5', 15050],
    ['150.5', 15050],
    ['150,50', 15050],
    ['150.50', 15050],
    ['0,07', 7],
    ['0,01', 1],
    ['  20  ', 2000],
    ['R150,50', 15050],
  ])('%j -> %i cents', (text, cents) => {
    expect(parseRandsToCents(text)).toBe(cents)
  })
})

describe('parseRandsToCents refuses anything it would have to guess at', () => {
  it.each([
    ['150,505'], // three decimal places: never rounded to 150,51 or 150,50
    ['19,999'],
    ['1,500.50'], // is the comma thousands or decimals?
    ['1.500,50'],
    ['1,500'], // three digits after a comma: thousands or decimals?
    ['abc'],
    ['R'],
    ['150 rand'],
    ['1e3'],
    [''],
    ['   '],
    ['-5'],
    ['-R5'],
    ['R-5'],
    ['0'],
    ['0,00'],
    ['1 50'], // a group that isn't three digits
    ['12 3456'],
    ['1.'],
    [','],
    ['1000001'], // R1 000 001 is above the API's maximum
    ['1 000 000,01'],
    ['99999999999999999999'],
  ])('%j -> null', (text) => {
    expect(parseRandsToCents(text)).toBeNull()
  })

  it('refuses one cent above the maximum and accepts the maximum itself', () => {
    expect(parseRandsToCents(centsToInput(MAX_CONTRIBUTION_CENTS))).toBe(MAX_CONTRIBUTION_CENTS)
    expect(parseRandsToCents(centsToInput(MAX_CONTRIBUTION_CENTS + 1))).toBeNull()
  })
})

describe('the classic: 19,99 is exactly 1999 cents', () => {
  it('JavaScript floating point really is wrong here', () => {
    expect(19.99 * 100).toBe(1998.9999999999998)
  })

  it.each([['19,99', 1999], ['19.99', 1999], ['0,29', 29], ['1,15', 115], ['4,35', 435], ['1,13', 113]])(
    '%j -> %i, with no multiplication of a decimal',
    (text, cents) => {
      // Each of these is a value where decimal * 100 lands just below the
      // whole number (0.29 * 100 = 28.999999999999996), so a parser that
      // multiplied and truncated would be a cent short. The parser reads
      // "19" and "99" as two integers instead.
      const result = parseRandsToCents(text)
      expect(result).toBe(cents)
      expect(Number.isInteger(result)).toBe(true)
    },
  )
})

describe('formatCents', () => {
  it.each([
    [0, `R${NBSP}0,00`],
    [1, `R${NBSP}0,01`],
    [99, `R${NBSP}0,99`],
    [100, `R${NBSP}1,00`],
    [1999, `R${NBSP}19,99`],
    [15050, `R${NBSP}150,50`],
    [150050, `R${NBSP}1${NBSP}500,50`],
    [MAX_CONTRIBUTION_CENTS, `R${NBSP}1${NBSP}000${NBSP}000,00`],
  ])('%i cents -> %j', (cents, text) => {
    expect(formatCents(cents)).toBe(text)
  })

  it('shows a negative amount with a minus sign', () => {
    expect(formatCents(-15050)).toMatch(/^-R.150,50$/)
  })

  it.each([[10.5], [Number.NaN], [Number.MAX_SAFE_INTEGER + 1]])('refuses %d, which is not whole cents', (cents) => {
    expect(() => formatCents(cents)).toThrow(RangeError)
  })
})

describe('formatting round-trips', () => {
  it.each([[1], [99], [100], [150050], [MAX_CONTRIBUTION_CENTS]])(
    '%i cents -> formatCents -> parseRandsToCents -> the same cents',
    (cents) => {
      expect(parseRandsToCents(formatCents(cents))).toBe(cents)
    },
  )

  it('0 cents formats, and parsing refuses it, because a contribution of R0,00 is not allowed', () => {
    expect(formatCents(0)).toBe(`R${NBSP}0,00`)
    expect(parseRandsToCents(formatCents(0))).toBeNull()
  })

  it.each([[0, '0,00'], [7, '0,07'], [15050, '150,50'], [MAX_CONTRIBUTION_CENTS, '1000000,00']])(
    'centsToInput(%i) -> %j',
    (cents, text) => {
      expect(centsToInput(cents)).toBe(text)
    },
  )

  it('every cent value from 1 to 200 000 reads back exactly through the input format', () => {
    for (let cents = 1; cents <= 200_000; cents++) {
      if (parseRandsToCents(centsToInput(cents)) !== cents) throw new Error(`${cents} did not round-trip`)
    }
  })
})
