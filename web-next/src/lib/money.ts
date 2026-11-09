// Money is whole cents everywhere in RondiTrack. These rules are pure
// functions so they can be tested one line per case (Assignment 9.2, Part 1).

// The API refuses a contribution above R1 000 000 (RecordContributionRequestValidator).
// The form refuses it first, with the same number.
export const MAX_CONTRIBUTION_CENTS = 100_000_000

// Made on first use, never at import: the first en-ZA NumberFormat costs
// 12–30 ms on a 4x-slowed phone CPU, and the contribution form imports this
// module to parse amounts, not to format them (INP, Part 5).
let randFormatter: Intl.NumberFormat | undefined
const rands = () => (randFormatter ??= new Intl.NumberFormat('en-ZA', {
  style: 'currency',
  currency: 'ZAR',
  minimumFractionDigits: 2,
  maximumFractionDigits: 2,
}))

export function formatCents(cents: number): string {
  if (!Number.isSafeInteger(cents)) {
    throw new RangeError('Money must be a safe integer number of cents')
  }

  const negative = cents < 0
  const absoluteCents = Math.abs(cents)

  const wholeRand = Math.floor(absoluteCents / 100)
  const remainder = absoluteCents % 100

  // Intl gets a decimal string built from integers, never cents / 100.
  const decimalAmount =
    `${negative ? '-' : ''}${wholeRand}.${String(remainder).padStart(2, '0')}` as `${number}`

  return rands().format(decimalAmount)
}

// Spaces a person (or Intl) may put between thousands: a plain space, a
// no-break space and the narrow no-break space.
const GROUP = '[ \\u00a0\\u202f]'
const AMOUNT = new RegExp(
  `^(?:R${GROUP}?)?(\\d{1,3}(?:${GROUP}\\d{3})+|\\d+)?(?:[.,](\\d{1,2}))?$`,
  'i',
)

// Text typed by a person -> whole cents, using only string and integer
// operations: "19,99" is 1999 because "19" and "99" are read as integers,
// never because 19.99 is multiplied by 100.
//
// Accepts: 1500, 1 500, 1 500,50, R1500, R 1 500,50, a no-break space
// between thousands, 150.50, and one decimal place (",5" is 50 cents).
// Refuses (null) anything it would have to guess at: three decimal places,
// "1,500.50", letters, an empty string, a negative or zero amount, and
// anything above `max`. It never rounds.
export function parseRandsToCents(text: string, max: number = MAX_CONTRIBUTION_CENTS): number | null {
  const match = AMOUNT.exec(text.trim())
  if (!match) return null
  const [, randsText, centsText] = match
  if (randsText === undefined && centsText === undefined) return null

  const rands = randsText === undefined ? 0 : Number.parseInt(randsText.replace(/[   ]/g, ''), 10)
  const cents = centsText === undefined ? 0 : Number.parseInt(centsText.padEnd(2, '0'), 10)
  if (!Number.isSafeInteger(rands)) return null
  const total = rands * 100 + cents

  if (!Number.isSafeInteger(total) || total <= 0 || total > max) return null
  return total
}

// Whole cents -> text for an input box, like "150,50". It has no "R" and no
// thousands separator, so parseRandsToCents reads it back exactly.
export function centsToInput(cents: number): string {
  if (!Number.isSafeInteger(cents) || cents < 0) {
    throw new RangeError('Money must be a non-negative safe integer number of cents')
  }

  return `${Math.floor(cents / 100)},${String(cents % 100).padStart(2, '0')}`
}
