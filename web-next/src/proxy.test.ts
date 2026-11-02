import { randomBytes } from 'node:crypto'
import { NextRequest } from 'next/server'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'

vi.mock('next/headers', () => ({ cookies: vi.fn() }))

import { proxy } from './proxy'
import { decrypt, encrypt, type Session } from './lib/session'

// Only the exp claim matters to the proxy; the signature is the API's business.
const accessToken = (secondsLeft: number) => {
  const payload = Buffer.from(JSON.stringify({ exp: Math.floor(Date.now() / 1000) + secondsLeft })).toString('base64url')
  return `header.${payload}.signature`
}
const session = (secondsLeft: number, refreshToken = randomBytes(8).toString('hex')): Session => ({
  accessToken: accessToken(secondsLeft),
  refreshToken,
  user: { id: 'user-1', name: 'Lerato', email: 'lerato@example.com' },
})
const inAnHour = () => new Date(Date.now() + 60 * 60 * 1000)

async function request(path: string, value?: Session) {
  const headers = new Headers()
  if (value) headers.set('cookie', `session=${await encrypt(value, inAnHour())}`)
  return new NextRequest(new URL(path, 'http://localhost:3000'), { headers })
}

function refreshResponse(status: number, newToken = 'rotated-refresh') {
  if (status !== 200) return new Response(JSON.stringify({ status }), { status, headers: { 'content-type': 'application/problem+json' } })
  const headers = new Headers({ 'content-type': 'application/json' })
  headers.append('set-cookie', `__Secure-ronditrack_refresh=${newToken}; expires=Wed, 01 Jan 2099 00:00:00 GMT; path=/api/v1/auth; secure; httponly`)
  return new Response(JSON.stringify({ accessToken: accessToken(300), expiresIn: 300 }), { status, headers })
}

const fetchMock = vi.fn()

beforeEach(() => {
  process.env.SESSION_SECRET = randomBytes(32).toString('base64')
  process.env.API_BASE_URL = 'http://api.test'
  fetchMock.mockReset()
  vi.stubGlobal('fetch', fetchMock)
})

afterEach(() => vi.unstubAllGlobals())

describe('proxy', () => {
  it('sends someone without a session to sign in', async () => {
    const response = await proxy(await request('/dashboard/member'))
    expect(new URL(response.headers.get('location')!).pathname).toBe('/login')
  })

  it('lets a fresh session through without calling the API', async () => {
    const response = await proxy(await request('/dashboard/member', session(240)))
    expect(response.headers.get('location')).toBeNull()
    expect(fetchMock).not.toHaveBeenCalled()
  })

  it('refreshes a token close to expiry and saves the rotated tokens in the cookie', async () => {
    fetchMock.mockResolvedValueOnce(refreshResponse(200))
    const response = await proxy(await request('/dashboard/member', session(30)))

    expect(fetchMock).toHaveBeenCalledTimes(1)
    const saved = await decrypt(response.cookies.get('session')?.value)
    expect(saved?.refreshToken).toBe('rotated-refresh')
    // The page rendered in this request is given the new cookie too.
    expect(response.headers.get('x-middleware-request-cookie') ?? response.headers.get('x-middleware-override-headers')).toBeTruthy()
  })

  it('rotates a refresh token once when parallel requests carry the same cookie', async () => {
    fetchMock.mockResolvedValueOnce(refreshResponse(200))
    const shared = session(30, 'shared-refresh')
    await Promise.all([proxy(await request('/dashboard/member', shared)), proxy(await request('/stokvels', shared))])
    expect(fetchMock).toHaveBeenCalledTimes(1)
  })

  it('clears a session the API no longer accepts and asks to sign in again', async () => {
    fetchMock.mockResolvedValueOnce(refreshResponse(401))
    const response = await proxy(await request('/dashboard/member', session(30)))

    const location = new URL(response.headers.get('location')!)
    expect(location.pathname).toBe('/login')
    expect(location.searchParams.get('expired')).toBe('1')
    expect(response.cookies.get('session')?.value).toBe('')
  })

  it('keeps the session when the API is unreachable', async () => {
    fetchMock.mockRejectedValueOnce(new TypeError('fetch failed'))
    const response = await proxy(await request('/dashboard/member', session(30)))
    expect(response.headers.get('location')).toBeNull()
    expect(response.cookies.get('session')).toBeUndefined()
  })

  it('on /login?expired=1 forgets the session instead of returning to the dashboard', async () => {
    const response = await proxy(await request('/login?expired=1', session(240)))
    expect(response.headers.get('location')).toBeNull()
    expect(response.cookies.get('session')?.value).toBe('')
  })

  it('sends a signed-in person away from /login', async () => {
    const response = await proxy(await request('/login?next=/stokvels', session(240)))
    expect(new URL(response.headers.get('location')!).pathname).toBe('/stokvels')
  })
})
