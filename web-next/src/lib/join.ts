import 'server-only'

import { redirect } from 'next/navigation'
import { baseUrl } from './proxy'
import { getSession } from './session'
import type { InvitePreview } from './join-state'

export type { InvitePreview } from './join-state'

async function request(path: string, init?: RequestInit): Promise<Response> {
  const session = await getSession()
  if (!session) redirect(`/login?next=${encodeURIComponent(`/join${path}`)}`)
  return fetch(`${baseUrl()}${path}`, {
    ...init,
    cache: 'no-store',
    headers: { Authorization: `Bearer ${session.accessToken}`, ...init?.headers },
  })
}

export async function previewInvite(code: string): Promise<{ response: Response; data?: InvitePreview }> {
  const response = await request(`/api/v1/invites/${encodeURIComponent(code)}`)
  return { response, data: response.ok ? await response.json() as InvitePreview : undefined }
}

export async function joinInvite(code: string): Promise<Response> {
  return request(`/api/v1/invites/${encodeURIComponent(code)}/join`, { method: 'POST' })
}
