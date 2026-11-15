import { randomBytes } from 'node:crypto'
import { NextRequest } from 'next/server'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'

vi.mock('next/headers', () => ({ cookies: vi.fn() }))

import { proxy } from '../proxy'
import { encrypt, type Session } from './session'
import { expiredLoginUrl, RETURN_PATH_HEADER, returnPathOf, safeReturnPath } from './return-path'

describe('safeReturnPath: only a path on this site', () => {
  it.each(['/stokvels/abc', '/stokvels/abc?tab=history', '/join?code=X1&step=rules'])('keeps %s', (path) => {
    expect(safeReturnPath(path)).toBe(path)
  })

  it.each([
    'https://evil.example/', '//evil.example', '/\\evil.example', 'evil.example', 'javascript:alert(1)',
    '/stokvels\nSet-Cookie: x=1', '', null, undefined,
  ])('refuses %j', (path) => {
    expect(safeReturnPath(path as string)).toBe('/dashboard/member')
  })

  it('drops Next.js internal RSC markers', () => {
    expect(returnPathOf(new URL('http://localhost/stokvels/abc?_rsc=1x2y&tab=history'))).toBe('/stokvels/abc?tab=history')
  })

  it('builds the sign-in URL with the way back encoded', () => {
    expect(expiredLoginUrl('/stokvels/abc?tab=history')).toBe('/login?expired=1&next=%2Fstokvels%2Fabc%3Ftab%3Dhistory')
    expect(expiredLoginUrl('//evil.example')).toBe('/login?expired=1&next=%2Fdashboard%2Fmember')
  })
})

// What the page receives from the proxy (Next.js passes overridden request
// headers to the page as x-middleware-request-<name>).
const forwarded = (response: Response) => response.headers.get(`x-middleware-request-${RETURN_PATH_HEADER}`)

describe('the proxy sets the return path; a client cannot', () => {
  const session = (): Session => {
    const payload = Buffer.from(JSON.stringify({ exp: Math.floor(Date.now() / 1000) + 3600 })).toString('base64url')
    return { accessToken: `h.${payload}.s`, refreshToken: 'r', user: { id: 'u', name: 'Lerato', email: 'lerato@example.test' } }
  }
  beforeEach(() => { process.env.SESSION_SECRET = randomBytes(32).toString('base64') })
  afterEach(() => vi.unstubAllGlobals())

  async function request(path: string, extra: Record<string, string> = {}) {
    const headers = new Headers(extra)
    headers.set('cookie', `session=${await encrypt(session(), new Date(Date.now() + 3600_000))}`)
    return new NextRequest(new URL(path, 'http://localhost:3000'), { headers })
  }

  it('is the page that was asked for', async () => {
    const response = await proxy(await request('/stokvels/abc?tab=history'))
    expect(forwarded(response)).toBe('/stokvels/abc?tab=history')
  })

  it('overwrites a value the client sent', async () => {
    const response = await proxy(await request('/stokvels/abc', { [RETURN_PATH_HEADER]: 'https://evil.example/phish' }))
    expect(forwarded(response)).toBe('/stokvels/abc')
  })

  it('is set for signed-out requests to public pages too', async () => {
    const response = await proxy(new NextRequest(new URL('/join?code=X1', 'http://localhost:3000'), {
      headers: { [RETURN_PATH_HEADER]: '//evil.example' },
    }))
    expect(forwarded(response)).toBe('/join?code=X1')
  })
})
