import { randomUUID } from 'node:crypto'
import { readFileSync } from 'node:fs'
import { jwtDecrypt } from 'jose'

// The two seeded people. Roles belong to a stokvel: each test makes its own
// stokvels and gives them roles there. These accounts are the only shared data.
export const people = {
  sipho: { name: 'Sipho Nkosi', email: 'e2e.sipho@example.test', state: 'e2e/.auth/sipho.json' },
  thandi: { name: 'Thandi Dlamini', email: 'e2e.thandi@example.test', state: 'e2e/.auth/thandi.json' },
} as const
export type Person = keyof typeof people

export const apiBase = (process.env.API_BASE_URL ?? 'http://localhost:5147').replace(/\/$/, '')

export function password(): string {
  const value = process.env.E2E_PASSWORD
  if (!value || value.length < 12) throw new Error('Set E2E_PASSWORD (12+ characters, test-only). See web-next/README.md.')
  return value
}

// Reads the access token out of a saved session, so tests can create their
// own data through the API without signing in again (the login limit is 5 a
// minute). Only possible because the test run knows its own SESSION_SECRET.
export async function accessToken(person: Person): Promise<string> {
  const state = JSON.parse(readFileSync(people[person].state, 'utf8')) as { cookies: Array<{ name: string; value: string }> }
  const cookie = state.cookies.find((candidate) => candidate.name === 'session')
  if (!cookie) throw new Error(`No session saved for ${person}; the setup project signs in first.`)
  const { payload } = await jwtDecrypt(cookie.value, Buffer.from(process.env.SESSION_SECRET ?? '', 'base64'))
  return payload.accessToken as string
}

export async function api<T = unknown>(person: Person, method: string, path: string, body?: unknown): Promise<T> {
  const response = await fetch(`${apiBase}${path}`, {
    method,
    headers: {
      Authorization: `Bearer ${await accessToken(person)}`,
      'Content-Type': 'application/json',
      ...(method === 'POST' && path.endsWith('/contributions') ? { 'Idempotency-Key': randomUUID() } : {}),
    },
    body: body === undefined ? undefined : JSON.stringify(body),
  })
  if (!response.ok) throw new Error(`${method} ${path} -> ${response.status} ${await response.text()}`)
  return (response.status === 204 ? undefined : await response.json()) as T
}

export const unique = (label: string) => `${label} ${randomUUID().slice(0, 8)}`

// The Johannesburg month, which is the API's "current cycle".
export const currentPeriod = () => new Date(Date.now() + 2 * 60 * 60 * 1000).toISOString().slice(0, 7)

// A stokvel this test owns, with an open cycle for this month. Its creator
// is its Chairperson, who has the Treasurer's powers.
export async function stokvelWithCycle(owner: Person, label: string, contributionAmount = 500) {
  const stokvel = await api<{ id: string; name: string }>(owner, 'POST', '/api/v1/stokvels', { name: unique(label), contributionAmount })
  const cycle = await api<{ id: string }>(owner, 'POST', `/api/v1/stokvels/${stokvel.id}/cycles`, { period: currentPeriod(), targetAmount: contributionAmount * 10 })
  return { stokvelId: stokvel.id, name: stokvel.name, cycleId: cycle.id }
}

export async function userId(person: Person): Promise<string> {
  return (await api<{ id: string }>(person, 'GET', '/api/v1/me')).id
}

// "R 1 500,50 of R 5 000,00" -> 150050. Reads cents from the page as
// integers, never as a float.
export function centsIn(text: string): number {
  const match = /R\s?([\d\s ]+),(\d{2})/.exec(text)
  if (!match) throw new Error(`No amount in "${text}"`)
  return Number.parseInt(match[1].replace(/\D/g, ''), 10) * 100 + Number.parseInt(match[2], 10)
}
