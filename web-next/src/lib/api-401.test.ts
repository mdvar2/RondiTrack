import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'

// A 401 while a page renders: back to sign-in, with the page the proxy recorded.
vi.mock('next/navigation', () => ({
  redirect: vi.fn((url: string) => { throw new Error(`redirect:${url}`) }),
  notFound: vi.fn(() => { throw new Error('notFound') }),
}))
let returnPath: string | null = '/stokvels/abc?tab=history'
vi.mock('next/headers', () => ({
  cookies: vi.fn(),
  headers: vi.fn(async () => new Headers(returnPath === null ? {} : { 'x-ronditrack-return-path': returnPath })),
}))
vi.mock('./session', () => ({
  getSession: vi.fn(async () => ({ accessToken: 'a.b.c', refreshToken: 'r', user: { id: 'u', name: 'n', email: 'e@example.test' } })),
}))

import { api } from './api'

describe('api() and a 401 from the API', () => {
  beforeEach(() => { vi.stubEnv('API_BASE_URL', 'http://api.test') })
  afterEach(() => { vi.unstubAllGlobals(); vi.unstubAllEnvs(); returnPath = '/stokvels/abc?tab=history' })

  it('redirects to sign in, then back to the same page', async () => {
    vi.stubGlobal('fetch', vi.fn(async () => new Response('{}', { status: 401, headers: { 'Content-Type': 'application/problem+json' } })))
    const client = await api()
    await expect(client.GET('/api/v1/me/home')).rejects.toThrow('redirect:/login?expired=1&next=%2Fstokvels%2Fabc%3Ftab%3Dhistory')
  })

  it('falls back to the dashboard when there is no trusted path', async () => {
    returnPath = null
    vi.stubGlobal('fetch', vi.fn(async () => new Response('{}', { status: 401 })))
    const client = await api()
    await expect(client.GET('/api/v1/me/home')).rejects.toThrow('redirect:/login?expired=1&next=%2Fdashboard%2Fmember')
  })

  it('leaves every other answer alone', async () => {
    vi.stubGlobal('fetch', vi.fn(async () => new Response(JSON.stringify({ stokvels: [] }), { status: 200, headers: { 'Content-Type': 'application/json' } })))
    const client = await api()
    await expect(client.GET('/api/v1/me/home')).resolves.toMatchObject({ data: { stokvels: [] } })
  })
})
