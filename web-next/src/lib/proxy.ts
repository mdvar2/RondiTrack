import 'server-only'
import createClient from 'openapi-fetch'
import { redirect } from 'next/navigation'
import type { paths } from './schema'
import { readRefreshCookie, REFRESH_COOKIE } from './refresh-cookie'
import { createSession, deleteSession, getSession, type Session } from './session'
import { apiClientFetch } from './trace'

export type RotatedSession = { session: Session; expiresAt: Date }

// Keyed by the refresh token being rotated. The API revokes the whole session
// when a rotated token is replayed, so requests that arrive with the same old
// cookie (parallel tabs, link prefetches) share one rotation for a short while.
const rotations = new Map<string, Promise<RotatedSession | null>>()
const ROTATION_REUSE_MS = 30_000

export function baseUrl(): string {
  const url = process.env.API_BASE_URL
  if (!url) throw new Error('API_BASE_URL is not set. See .env.example.')
  return url.replace(/\/$/, '')
}

export function publicApi() {
  return createClient<paths>({ baseUrl: baseUrl(), cache: 'no-store', fetch: apiClientFetch })
}

export function isAccessTokenCloseToExpiry(token: string): boolean {
  try {
    const parts = token.split('.')
    if (parts.length < 2) return true
    const payload = JSON.parse(Buffer.from(parts[1].replace(/-/g, '+').replace(/_/g, '/'), 'base64').toString('utf8'))
    const exp = typeof payload.exp === 'number' ? payload.exp : Number(payload.exp)
    if (!Number.isFinite(exp)) return false
    return Date.now() >= (exp - 60) * 1000
  } catch {
    return false
  }
}

// Exchanges the refresh token for new tokens. It writes no cookie, so the
// proxy and Server Actions can each store the result the way they are allowed to.
// Null means the API refused the refresh token: the person must sign in again.
// It throws when the API couldn't decide (unreachable, 5xx, 429): callers keep
// the session then, rather than signing a member out over an outage.
export function rotateSession(session: Session): Promise<RotatedSession | null> {
  const existing = rotations.get(session.refreshToken)
  if (existing) return existing

  const rotation = (async () => {
    const result = await publicApi().POST('/api/v1/auth/refresh', {
      headers: { Cookie: `${REFRESH_COOKIE}=${session.refreshToken}` },
    })
    if (!result.data) {
      const status = result.response.status
      if (status >= 500 || status === 429) throw new Error(`Session refresh unavailable (${status}).`)
      return null
    }

    const newRefresh = readRefreshCookie(result.response.headers.getSetCookie())
    return {
      session: {
        ...session,
        accessToken: result.data.accessToken,
        refreshToken: newRefresh?.token ?? session.refreshToken,
      },
      expiresAt: newRefresh?.expiresAt ?? new Date(Date.now() + 7 * 24 * 60 * 60 * 1000),
    }
  })()

  rotations.set(session.refreshToken, rotation)
  // A failed request (API unreachable) is not kept, so the next one can retry.
  rotation.catch(() => rotations.delete(session.refreshToken))
  setTimeout(() => rotations.delete(session.refreshToken), ROTATION_REUSE_MS).unref?.()
  return rotation
}

// For Server Actions only: they may write cookies. Server Components may not,
// so for page renders src/proxy.ts refreshes before the page runs.
export async function refreshSessionIfNeeded(session: Session): Promise<Session> {
  if (!isAccessTokenCloseToExpiry(session.accessToken)) return session

  let rotated: RotatedSession | null
  try {
    rotated = await rotateSession(session)
  } catch {
    // The API couldn't decide (unreachable, 5xx, 429). Keep the session, as the
    // proxy does: the call that follows then fails with a trace reference
    // (lib/trace.ts) instead of the action failing with none.
    return session
  }
  if (!rotated) {
    await deleteSession()
    redirect('/login?expired=1')
  }
  await createSession(rotated.session, rotated.expiresAt)
  return rotated.session
}

export async function refreshedApi() {
  const session = await getSession()
  if (!session) redirect('/login')
  const refreshed = await refreshSessionIfNeeded(session)
  return createClient<paths>({
    baseUrl: baseUrl(),
    cache: 'no-store',
    fetch: apiClientFetch,
    headers: { Authorization: `Bearer ${refreshed.accessToken}` },
  })
}
