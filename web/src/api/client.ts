import createClient from 'openapi-fetch'
import type { paths } from './schema'
import { authFetch, ApiError } from '../auth/session'
export const api = createClient<paths>({
  baseUrl: (import.meta.env.VITE_API_BASE_URL || 'http://localhost:8081').replace(/\/$/, ''),
  fetch: authFetch,
})
export function unwrap<T>(result: { data?: T; error?: unknown; response: Response }): T {
  if (!result.response.ok || result.error !== undefined) {
    const p = result.error && typeof result.error === 'object' ? result.error as { title?: string; detail?: string; correlationId?: string } : null
    throw new ApiError(result.response.status, p, result.response.headers.get('Retry-After'))
  }
  if (result.data === undefined) throw new Error('API returned no data')
  return result.data
}
