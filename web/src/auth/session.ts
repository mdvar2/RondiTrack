/**
 * The backend rotates refresh cookies and rejects reused tokens. A single in-flight
 * refresh promise prevents simultaneous requests from racing the same cookie.
 */
const BASE = (import.meta.env.VITE_API_BASE_URL || 'http://localhost:8081').replace(/\/$/, '')
type Problem = { title?: string; detail?: string; status?: number; correlationId?: string; errors?: Record<string, string[]> }
export class ApiError extends Error {
  readonly status: number
  readonly problem: Problem | null
  readonly retryAfter: string | null
  constructor(status: number, problem: Problem | null, retryAfter: string | null) {
    super(problem?.title || `Request failed (${status})`)
    this.name = 'ApiError'; this.status = status; this.problem = problem; this.retryAfter = retryAfter
  }
}
let token: string | null = null
let pending: Promise<string> | null = null
let signedOut = false
const listeners = new Set<() => void>()
export function onSignedOut(fn: () => void) { listeners.add(fn); return () => { listeners.delete(fn) } }
function clearSession() {
  token = null
  if (!signedOut) { signedOut = true; listeners.forEach(fn => fn()) }
}
async function errorFrom(res: Response) {
  let problem: Problem | null = null
  try { problem = await res.clone().json() as Problem } catch { /* no JSON */ }
  return new ApiError(res.status, problem, res.headers.get('Retry-After'))
}
async function doRefresh(): Promise<string> {
  const res = await fetch(`${BASE}/api/v1/auth/refresh`, { method: 'POST', credentials: 'include' })
  if (!res.ok) throw await errorFrom(res)
  const body = await res.json() as { accessToken: string }
  token = body.accessToken; signedOut = false
  return token
}
export function refreshAccessToken(): Promise<string> {
  if (pending) return pending
  const attempt = doRefresh()
  pending = attempt
  void attempt.then(() => { if (pending === attempt) pending = null }, () => { if (pending === attempt) pending = null })
  return attempt
}
// The user's id is the "sub" claim of the access token.
export function currentUserId(): string | null {
  if (!token) return null
  try { return JSON.parse(atob(token.split('.')[1].replace(/-/g, '+').replace(/_/g, '/'))).sub ?? null } catch { return null }
}
export async function login(email: string, password: string): Promise<void> {
  const res = await fetch(`${BASE}/api/v1/auth/login`, {
    method: 'POST', credentials: 'include', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ email, password }),
  })
  if (!res.ok) throw await errorFrom(res)
  const body = await res.json() as { accessToken: string }
  token = body.accessToken; signedOut = false
}
export async function logout(): Promise<void> {
  clearSession()
  try { await fetch(`${BASE}/api/v1/auth/logout`, { method: 'POST', credentials: 'include' }) } catch { /* offline logout still clears state */ }
}
export async function authFetch(request: Request): Promise<Response> {
  let current = token
  let refreshedBeforeRequest = false
  if (!current) {
    try { current = await refreshAccessToken(); refreshedBeforeRequest = true }
    catch { clearSession(); return new Response(null, { status: 401 }) }
  }
  const send = (bearer: string) => {
    const headers = new Headers(request.headers)
    headers.set('Authorization', `Bearer ${bearer}`)
    return fetch(new Request(request.clone(), { headers, credentials: 'include' }))
  }
  const first = await send(current)
  if (first.status !== 401) return first
  if (token && token !== current) {
    const retry = await send(token)
    if (retry.status === 401) clearSession()
    return retry
  }
  if (refreshedBeforeRequest) { clearSession(); return first }
  try {
    const next = await refreshAccessToken()
    const retry = await send(next)
    if (retry.status === 401) clearSession()
    return retry
  } catch { clearSession(); return first }
}
