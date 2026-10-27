// The 7.3 error messages, for errors that now happen on the Next.js server.
// No 'server-only': error.tsx (a Client Component) reads errors back too.

export type Problem = {
  title?: string
  detail?: string
  status?: number
  correlationId?: string
}

// In production, Next.js replaces a Server Component error's message before it
// reaches the browser, but keeps a digest we set ourselves. So the digest
// carries the two safe facts error.tsx needs: the status and the reference.
const DIGEST_PREFIX = 'RONDITRACK_API'

export class ApiError extends Error {
  readonly status: number
  readonly problem: Problem | null
  readonly retryAfter: string | null
  readonly digest: string

  constructor(status: number, problem: Problem | null, retryAfter: string | null = null) {
    super(problem?.title || `Request failed (${status})`)
    this.name = 'ApiError'
    this.status = status
    this.problem = problem
    this.retryAfter = retryAfter
    this.digest = [DIGEST_PREFIX, status, problem?.correlationId ?? ''].join(';')
  }
}

// Turns error.tsx's `error` back into what describeError needs.
export function fromDigest(digest: string | undefined): { status: number; correlationId?: string } | null {
  const [prefix, status, correlationId] = digest?.split(';') ?? []
  if (prefix !== DIGEST_PREFIX || !/^\d{3}$/.test(status ?? '')) return null
  return { status: Number(status), correlationId: correlationId || undefined }
}

export function parseRetryAfter(value: string | null, now = Date.now()): number | null {
  if (!value) return null
  const text = value.trim()
  if (/^\d+$/.test(text)) return Number(text)
  const time = Date.parse(text)
  return Number.isNaN(time) ? null : Math.max(0, Math.ceil((time - now) / 1000))
}

export type ErrorDescription = { message: string; correlationId?: string; retryAfter?: number }

export function describeError(error: { status: number; problem?: Problem | null; retryAfter?: string | null; correlationId?: string } | null): ErrorDescription {
  if (!error) return { message: "Can't reach RondiTrack. Please try again in a moment." }
  const id = error.problem?.correlationId ?? error.correlationId
  if (error.status === 401) return { message: 'Email or password is incorrect.', correlationId: id }
  if (error.status === 403) return { message: "You don't have permission to do that.", correlationId: id }
  if (error.status === 404) return { message: 'Stokvel not found.', correlationId: id }
  if (error.status === 429) return { message: 'Too many requests. Please try again later.', retryAfter: parseRetryAfter(error.retryAfter ?? null) ?? undefined, correlationId: id }
  if (error.status === 400) return { message: error.problem?.title || 'Please check the entered information.', correlationId: id }
  if (error.status === 409 || error.status === 422) return { message: error.problem?.detail || error.problem?.title || 'Request failed.', correlationId: id }
  if (error.status >= 500) return { message: 'Something went wrong. Please try again.', correlationId: id }
  return { message: error.problem?.title || 'Request failed.', correlationId: id }
}
