import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { trace } from '@opentelemetry/api'

vi.mock('next/navigation', () => ({ redirect: vi.fn((url: string) => { throw new Error(`redirect:${url}`) }) }))
vi.mock('next/headers', () => ({ cookies: vi.fn() }))

import { currentTraceId, UNREACHABLE_DETAIL, withTraceReference } from './trace'
import { rotateSession } from './proxy'
import { readOrFail } from './api'
import { ApiError } from './errors'

const TRACE = '4bf92f3577b34da6a3ce929d0e0e4736'

describe('withTraceReference', () => {
  it('passes a real answer through untouched, whatever its status', async () => {
    const answer = new Response('{}', { status: 409 })
    const fetchWith = withTraceReference(async () => answer, () => TRACE)
    expect(await fetchWith('http://api.test/x')).toBe(answer)
  })

  it('turns a dropped connection into a 503 problem whose reference is the trace id', async () => {
    const fetchWith = withTraceReference(async () => { throw new TypeError('fetch failed') }, () => TRACE)
    const response = await fetchWith('http://api.test/x')
    expect(response.status).toBe(503)
    expect(response.headers.get('content-type')).toBe('application/problem+json')
    expect(await response.json()).toEqual({
      type: 'about:blank', title: 'Service Unavailable', status: 503, detail: UNREACHABLE_DETAIL, correlationId: TRACE,
    })
  })

  it('still rejects when the caller aborted on purpose', async () => {
    const abort = new DOMException('aborted', 'AbortError')
    const fetchWith = withTraceReference(async () => { throw abort }, () => TRACE)
    await expect(fetchWith('http://api.test/x')).rejects.toBe(abort)
  })

  it('reaches error.tsx as an ApiError carrying the trace id', async () => {
    const fetchWith = withTraceReference(async () => { throw new TypeError('fetch failed') }, () => TRACE)
    const response = await fetchWith('http://api.test/x')
    try {
      readOrFail({ error: await response.json(), response })
      expect.unreachable()
    } catch (error) {
      expect((error as ApiError).digest).toBe(`RONDITRACK_API;503;${TRACE}`)
    }
  })
})

describe('currentTraceId', () => {
  afterEach(() => vi.restoreAllMocks())

  it('is the active span’s trace id', () => {
    vi.spyOn(trace, 'getActiveSpan').mockReturnValue(trace.wrapSpanContext({ traceId: TRACE, spanId: '00f067aa0ba902b7', traceFlags: 1 }))
    expect(currentTraceId()).toBe(TRACE)
  })

  it('is undefined without a valid span, rather than an all-zero id', () => {
    expect(currentTraceId()).toBeUndefined()
    vi.spyOn(trace, 'getActiveSpan').mockReturnValue(trace.wrapSpanContext({ traceId: '0'.repeat(32), spanId: '0'.repeat(16), traceFlags: 0 }))
    expect(currentTraceId()).toBeUndefined()
  })
})

// The refresh must not sign a member out because the API was unreachable or broken.
describe('rotateSession when the API cannot decide', () => {
  const session = (token: string) => ({ accessToken: 'a.b.c', refreshToken: token, userId: 'u', name: 'n', email: 'e' }) as never
  beforeEach(() => { vi.stubEnv('API_BASE_URL', 'http://api.test') })
  afterEach(() => { vi.unstubAllGlobals(); vi.unstubAllEnvs() })

  it('throws (session kept) when the connection drops', async () => {
    vi.stubGlobal('fetch', vi.fn(async () => { throw new TypeError('fetch failed') }))
    await expect(rotateSession(session('drop-token'))).rejects.toThrow('Session refresh unavailable (503).')
  })

  it('throws (session kept) on a 500 or a 429', async () => {
    vi.stubGlobal('fetch', vi.fn(async () => new Response('{}', { status: 500, headers: { 'Content-Type': 'application/problem+json' } })))
    await expect(rotateSession(session('five-hundred-token'))).rejects.toThrow('(500)')
    vi.stubGlobal('fetch', vi.fn(async () => new Response('{}', { status: 429, headers: { 'Content-Type': 'application/problem+json' } })))
    await expect(rotateSession(session('throttled-token'))).rejects.toThrow('(429)')
  })

  it('returns null (sign in again) only when the API refuses the token', async () => {
    vi.stubGlobal('fetch', vi.fn(async () => new Response('{}', { status: 401, headers: { 'Content-Type': 'application/problem+json' } })))
    await expect(rotateSession(session('refused-token'))).resolves.toBeNull()
  })
})
