import 'server-only'

import { cache } from 'react'
import { redirect } from 'next/navigation'
import { baseUrl } from './proxy'
import { getSession } from './session'
import { apiFetch } from './trace'
import type { InvitePreview } from './join-state'
import type { Problem } from './errors'

export type { InvitePreview } from './join-state'

async function request(path: string, init?: RequestInit): Promise<Response> {
  const session = await getSession()
  if (!session) redirect(`/login?next=${encodeURIComponent(`/join${path}`)}`)
  return apiFetch(`${baseUrl()}${path}`, {
    ...init,
    cache: 'no-store',
    headers: { Authorization: `Bearer ${session.accessToken}`, ...init?.headers },
  })
}

export async function previewInvite(code: string): Promise<{ response: Response; data?: InvitePreview; problem?: Problem }> {
  const response = await request(`/api/v1/invites/${encodeURIComponent(code)}`)
  if (response.ok) return { response, data: await response.json() as InvitePreview }
  const problem = await response.json().catch(() => null) as Problem | null
  return { response, problem: problem ?? undefined }
}

export async function joinInvite(code: string): Promise<Response> {
  return request(`/api/v1/invites/${encodeURIComponent(code)}/join`, { method: 'POST' })
}

export type InviteStatus = 'valid' | 'expired' | 'unknown' | 'unavailable'

// Anonymous on purpose: this is what a signed-out link preview may learn
// about /join?code=…, whether the invite is still valid. The API's status
// endpoint returns nothing else (no stokvel name, no members, no amounts).
// Cached per render, so generateMetadata and the page share one request.
export const inviteStatus = cache(async (code: string): Promise<InviteStatus> => {
  try {
    const response = await apiFetch(`${baseUrl()}/api/v1/invites/${encodeURIComponent(code)}/status`, { cache: 'no-store' })
    if (response.status === 404) return 'unknown'
    if (!response.ok) return 'unavailable'
    const body = await response.json() as { status?: string }
    return body.status === 'valid' || body.status === 'expired' ? body.status : 'unavailable'
  } catch {
    return 'unavailable'
  }
})
