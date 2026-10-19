
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
