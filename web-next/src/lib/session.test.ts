import { randomBytes } from 'node:crypto'
import { EncryptJWT } from 'jose'
import { beforeEach, describe, expect, it, vi } from 'vitest'

vi.mock('next/headers', () => ({ cookies: vi.fn() }))

import { decrypt, encrypt, type Session } from './session'

const session: Session = {
  accessToken: 'access-token',
  refreshToken: 'refresh-token',
  user: { id: 'user-1', name: 'Lerato', email: 'lerato@example.com' },
}
const inAnHour = () => new Date(Date.now() + 60 * 60 * 1000)

beforeEach(() => {
  process.env.SESSION_SECRET = randomBytes(32).toString('base64')
})

describe('session cookie', () => {
  it('decrypts what it encrypted, and the cookie does not contain the token in plain text', async () => {
    const value = await encrypt(session, inAnHour())

    expect(value).not.toContain('access-token')
    expect(value.split('.')).toHaveLength(5) // a JWE, not a readable JWT
    expect(await decrypt(value)).toEqual(session)
  })

  it('treats a missing cookie as no session', async () => {
    expect(await decrypt(undefined)).toBeNull()
  })

  it('treats a tampered cookie as no session', async () => {
    const value = await encrypt(session, inAnHour())
    const parts = value.split('.')
    parts[3] = parts[3].slice(0, -2) + (parts[3].endsWith('AA') ? 'BB' : 'AA')

    expect(await decrypt(parts.join('.'))).toBeNull()
  })

  it('treats a cookie made with another secret as no session', async () => {
    const value = await encrypt(session, inAnHour())
    process.env.SESSION_SECRET = randomBytes(32).toString('base64')

    expect(await decrypt(value)).toBeNull()
  })

  it('treats a correctly encrypted cookie with the wrong shape as no session', async () => {
    const key = Buffer.from(process.env.SESSION_SECRET!, 'base64')
    const value = await new EncryptJWT({ accessToken: 'only-this' })
      .setProtectedHeader({ alg: 'dir', enc: 'A256GCM' })
      .setExpirationTime(inAnHour())
      .encrypt(key)

    expect(await decrypt(value)).toBeNull()
  })

  it('treats an expired cookie as no session', async () => {
    const value = await encrypt(session, new Date(Date.now() - 60_000))

    expect(await decrypt(value)).toBeNull()
  })

  it('never stores a role', async () => {
    const value = await encrypt(session, inAnHour())

    expect(Object.keys((await decrypt(value))!)).toEqual(['accessToken', 'refreshToken', 'user'])
  })
})
