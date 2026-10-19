
const JOHANNESBURG_TIMEZONE = 'Africa/Johannesburg'

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

  const getPart = (type: Intl.DateTimeFormatPartTypes): string => {
    return parts.find((part) => part.type === type)?.value ?? ''
  }

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

export function formatCycleDueDate(
  dateOnly: string,
): string {
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

  return formatJohannesburgDate(date.toISOString())
}
