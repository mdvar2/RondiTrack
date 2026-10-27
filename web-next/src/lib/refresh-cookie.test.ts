import { describe, expect, it } from 'vitest'
import { readRefreshCookie } from './refresh-cookie'

describe('readRefreshCookie', () => {
  it('finds the refresh token and its expiry among the Set-Cookie headers', () => {
    const result = readRefreshCookie([
      'other=1; path=/',
      '__Secure-ronditrack_refresh=abc+/def==; expires=Tue, 03 Nov 2026 18:38:03 GMT; path=/api/v1/auth; secure; samesite=strict; httponly',
    ])

    expect(result?.token).toBe('abc+/def==')
    expect(result?.expiresAt?.toISOString()).toBe('2026-11-03T18:38:03.000Z')
  })

  it('returns null when the API sent no refresh cookie', () => {
    expect(readRefreshCookie(['other=1'])).toBeNull()
    expect(readRefreshCookie([])).toBeNull()
  })

  it('returns null for an empty value, as when the cookie is being deleted', () => {
    expect(readRefreshCookie(['__Secure-ronditrack_refresh=; expires=Thu, 01 Jan 1970 00:00:00 GMT'])).toBeNull()
  })
})
