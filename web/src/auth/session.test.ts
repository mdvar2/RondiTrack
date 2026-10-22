import { beforeEach, describe, expect, it, vi } from 'vitest'

async function setup() {
  vi.resetModules()
  const mod = await import('./session')
  return mod
}
const json = (data: object, status = 200) => new Response(JSON.stringify(data), { status, headers: { 'Content-Type': 'application/json' } })
const delay = (ms: number) => new Promise(resolve => setTimeout(resolve, ms))
describe('single-flight session', () => {
  beforeEach(() => { vi.unstubAllGlobals(); vi.resetModules() })
  it('five simultaneous 401s share exactly one refresh', async () => {
    let refreshes = 0
    const mod = await setup()
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => {
      const request = input instanceof Request ? input : new Request(input)
      if (request.url.endsWith('/auth/login')) return json({ accessToken: 'old' })
      if (request.url.endsWith('/auth/refresh')) { refreshes++; await delay(20); return json({ accessToken: 'new' }) }
      return new Response(null, { status: request.headers.get('Authorization') === 'Bearer new' ? 200 : 401 })
    }))
    await mod.login('user@example.com', 'password')
    const responses = await Promise.all(Array.from({ length: 5 }, () => mod.authFetch(new Request('http://localhost:8081/api/v1/me/home'))))
    expect(responses.every(r => r.status === 200)).toBe(true)
    expect(refreshes).toBe(1)
  })
  it('refreshes again after previous promise settles', async () => {
    let count = 0
    const mod = await setup()
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => {
      const request = input instanceof Request ? input : new Request(input)
      if (request.url.endsWith('/auth/refresh')) return json({ accessToken: `token-${++count}` })
      if (request.url.endsWith('/auth/login')) return json({ accessToken: 'expired' })
      return new Response(null, { status: request.headers.get('Authorization')?.startsWith('Bearer token-') ? 200 : 401 })
    }))
    await mod.login('user@example.com', 'password')
    await mod.refreshAccessToken()
    await mod.refreshAccessToken()
    expect(count).toBe(2)
  })
  it('failed refresh signs out once without repeated retries', async () => {
    const mod = await setup()
    let refreshes = 0, signals = 0
    mod.onSignedOut(() => { signals++ })
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => {
      const request = input instanceof Request ? input : new Request(input)
      if (request.url.endsWith('/auth/login')) return json({ accessToken: 'old' })
      if (request.url.endsWith('/auth/refresh')) { refreshes++; return new Response(null, { status: 401 }) }
      return new Response(null, { status: 401 })
    }))
    await mod.login('user@example.com', 'password')
    expect((await mod.authFetch(new Request('http://localhost:8081/api/v1/me/home'))).status).toBe(401)
    expect(refreshes).toBe(1)
    expect(signals).toBe(1)
  })
  it('without token or cookie attempts refresh only once', async () => {
    const mod = await setup()
    let count = 0
    vi.stubGlobal('fetch', vi.fn(async () => { count++; return new Response(null, { status: 401 }) }))
    expect((await mod.authFetch(new Request('http://localhost:8081/api/v1/me/home'))).status).toBe(401)
    expect(count).toBe(1)
  })
  it('preserves JSON POST body across a 401 retry', async () => {
    const mod = await setup()
    const bodies: string[] = []
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => {
      const request = input instanceof Request ? input : new Request(input)
      if (request.url.endsWith('/auth/login')) return json({ accessToken: 'old' })
      if (request.url.endsWith('/auth/refresh')) return json({ accessToken: 'new' })
      bodies.push(await request.text())
      return new Response(null, { status: request.headers.get('Authorization') === 'Bearer new' ? 200 : 401 })
    }))
    await mod.login('user@example.com', 'password')
    const result = await mod.authFetch(new Request('http://localhost:8081/api/v1/test', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: '{"amount":120}' }))
    expect(result.status).toBe(200)
    expect(bodies).toEqual(['{"amount":120}', '{"amount":120}'])
  })
})
