import 'server-only'
import createClient from 'openapi-fetch'
import { notFound, redirect } from 'next/navigation'
import { headers } from 'next/headers'
import type { paths } from './schema'
import { ApiError, type Problem } from './errors'
import { getSession } from './session'
import { baseUrl } from './proxy'
import { apiClientFetch } from './trace'
import { expiredLoginUrl, RETURN_PATH_HEADER } from './return-path'

export { publicApi } from './proxy'

// For everything else: the access token comes from the encrypted session and
// is added here, on the server. No session means sign in first.
// Pages use this, and a Server Component cannot write cookies, so it does not
// refresh: src/proxy.ts has already refreshed a token close to expiry.
export async function api() {
  const session = await getSession()
  if (!session) redirect('/login')
  // Set by src/proxy.ts on every request, never by the client.
  const returnPath = (await headers()).get(RETURN_PATH_HEADER)
  const client = createClient<paths>({
    baseUrl: baseUrl(),
    cache: 'no-store',
    fetch: apiClientFetch,
    headers: { Authorization: `Bearer ${session.accessToken}` },
  })
  // The API refused this session during the render: sign in again, then come
  // back to exactly this page (the proxy had already tried to refresh it).
  client.use({ onResponse({ response }) { if (response.status === 401) redirect(expiredLoginUrl(returnPath)) } })
  return client
}

type Result<T> = { data?: T; error?: unknown; response: Response }

// One place that turns an API result into data, or into the right response:
// 401 -> the session is no longer accepted (the proxy refreshes first), so sign in again
//        (api() has already redirected with the way back; this is the fallback);
// 404 -> the page's not-found.tsx, the same for "not yours" and "no such id";
// anything else -> ApiError, which error.tsx shows with its reference.
export function readOrFail<T>(result: Result<T>): T {
  const { response } = result
  if (response.status === 401) redirect('/login?expired=1')
  if (response.status === 404) notFound()
  if (!response.ok || result.data === undefined) {
    const problem = typeof result.error === 'object' && result.error !== null ? result.error as Problem : null
    throw new ApiError(response.status, problem, response.headers.get('Retry-After'))
  }
  return result.data
}

