
const randFormatter = new Intl.NumberFormat('en-ZA', {
  style: 'currency',
  currency: 'ZAR',
  minimumFractionDigits: 2,
  maximumFractionDigits: 2,
})

export function formatCents(cents: number): string {
  if (!Number.isSafeInteger(cents)) {
    throw new RangeError('Money must be a safe integer number of cents')
  }

  const negative = cents < 0
  const absoluteCents = Math.abs(cents)

  const wholeRand = Math.floor(absoluteCents / 100)
  const remainder = absoluteCents % 100

  const decimalAmount =
    `${negative ? '-' : ''}${wholeRand}.${String(remainder).padStart(2, '0')}`

  return randFormatter.format(Number(decimalAmount))
}

// Text typed by a person -> whole cents, using only whole-number arithmetic.
// Accepts "150", "150.5", "150,50" and "1 500,50". Returns null for anything
// that is unclear or would need rounding, such as "150.505" or "1,500.50".
export function parseRandsToCents(text: string): number | null {
  const match = /^(\d{1,3}(?:[ \u00a0]\d{3})+|\d+)(?:[.,](\d{1,2}))?$/.exec(text.trim())
  if (!match) return null

  const rands = Number.parseInt(match[1].replace(/[ \u00a0]/g, ''), 10)
  const cents = Number.parseInt((match[2] ?? '').padEnd(2, '0'), 10)
  const total = rands * 100 + cents

  return Number.isSafeInteger(total) ? total : null
}

// Whole cents -> text for an input box, like "150.50". It has no "R" and no
// thousands separator, so parseRandsToCents reads it back exactly.
export function centsToInput(cents: number): string {
  if (!Number.isSafeInteger(cents) || cents < 0) {
    throw new RangeError('Money must be a non-negative safe integer number of cents')
  }

  return `${Math.floor(cents / 100)}.${String(cents % 100).padStart(2, '0')}`
}
