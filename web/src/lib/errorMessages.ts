import { ApiError } from '../auth/session'
export function parseRetryAfter(value: string | null, now = Date.now()): number | null {
  if (!value) return null
  const text = value.trim()
  if (/^\d+$/.test(text)) return Number(text)
  const time = Date.parse(text)
  return Number.isNaN(time) ? null : Math.max(0, Math.ceil((time - now) / 1000))
}
export function describeError(error: unknown): { message: string; correlationId?: string; retryAfter?: number } {
  if (!(error instanceof ApiError)) return { message: "Can't reach RondiTrack. Check your connection." }
  const id = error.problem?.correlationId
  if (error.status === 401) return { message: 'Email or password is incorrect.' }
  if (error.status === 404) return { message: 'Stokvel not found.' }
  if (error.status === 429) return { message: 'Too many requests. Please try again later.', retryAfter: parseRetryAfter(error.retryAfter) ?? undefined }
  if (error.status === 400) return { message: error.problem?.title || 'Please check the entered information.' }
  if (error.status >= 500) return { message: 'Something went wrong. Please try again.', correlationId: id }
  return { message: error.problem?.title || 'Request failed.', correlationId: id }
}
