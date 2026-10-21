
const JOHANNESBURG_TIMEZONE = 'Africa/Johannesburg'
const DAY_MS = 24 * 60 * 60 * 1000
const SA_OFFSET_MS = 2 * 60 * 60 * 1000

const dateTimeFormatter = new Intl.DateTimeFormat('en-GB', {
  timeZone: JOHANNESBURG_TIMEZONE,
  day: '2-digit',
  month: 'short',
  year: 'numeric',
  hour: '2-digit',
  minute: '2-digit',
  hourCycle: 'h23',
})

const dateOnlyFormatter = new Intl.DateTimeFormat('en-GB', {
  timeZone: JOHANNESBURG_TIMEZONE,
  day: '2-digit',
  month: 'short',
  year: 'numeric',
})

function formatParts(
  formatter: Intl.DateTimeFormat,
  date: Date,
  includeTime: boolean,
): string {
  const parts = formatter.formatToParts(date)

  const getPart = (type: Intl.DateTimeFormatPartTypes): string =>
    parts.find((part) => part.type === type)?.value ?? ''

  const dateText =
    `${getPart('day')} ${getPart('month')} ${getPart('year')}`

  if (!includeTime) {
    return dateText
  }

  return `${dateText}, ${getPart('hour')}:${getPart('minute')}`
}

export function formatJohannesburgDateTime(
  utcTimestamp: string,
): string {
  const date = new Date(utcTimestamp)

  if (Number.isNaN(date.getTime())) {
    throw new RangeError('Invalid timestamp')
  }

  return formatParts(dateTimeFormatter, date, true)
}

export function formatJohannesburgDate(
  utcTimestamp: string,
): string {
  const date = new Date(utcTimestamp)

  if (Number.isNaN(date.getTime())) {
    throw new RangeError('Invalid timestamp')
  }

  return formatParts(dateOnlyFormatter, date, false)
}

export function formatCycleDueDate(dateOnly: string): string {
  validateDateOnly(dateOnly)

  return formatJohannesburgDate(`${dateOnly}T12:00:00Z`)
}

function validateDateOnly(dateOnly: string): void {
  if (!/^\d{4}-\d{2}-\d{2}$/.test(dateOnly)) {
    throw new RangeError('Invalid date-only value')
  }

  const date = new Date(`${dateOnly}T12:00:00Z`)

  if (
    Number.isNaN(date.getTime()) ||
    date.toISOString().slice(0, 10) !== dateOnly
  ) {
    throw new RangeError('Invalid date-only value')
  }
}

export function getDueCountdown(
  dueDate: string,
  now: Date = new Date(),
): string {
  validateDateOnly(dueDate)

  const today = new Date(now.getTime() + SA_OFFSET_MS)
    .toISOString()
    .slice(0, 10)

  const dueDay = Date.parse(`${dueDate}T00:00:00Z`)
  const todayDay = Date.parse(`${today}T00:00:00Z`)

  const days = Math.round((dueDay - todayDay) / DAY_MS)

  if (days === 0) {
    return 'Due today'
  }

  if (days === 1) {
    return 'Due tomorrow'
  }

  if (days > 1) {
    return `Due in ${days} days`
  }

  return `Overdue by ${Math.abs(days)} ${days === -1 ? 'day' : 'days'}`
}

export function getNextJohannesburgMidnightDelay(
  now: Date = new Date(),
): number {
  const saTime = now.getTime() + SA_OFFSET_MS
  const elapsed = ((saTime % DAY_MS) + DAY_MS) % DAY_MS

  return DAY_MS - elapsed
}
