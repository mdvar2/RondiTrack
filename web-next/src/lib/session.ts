import 'server-only'
import { cookies } from 'next/headers'
import { EncryptJWT, jwtDecrypt } from 'jose'

// Everything the server needs to call the API for this person. There is no
// role here on purpose: a person is Treasurer of some stokvels and Member of
// others, so a role only means something next to one stokvel, from the API.
export type Session = {
  accessToken: string
  refreshToken: string
  user: { id: string; name: string; email: string }
}

const COOKIE_NAME = 'session'

function secretKey(): Uint8Array {
  const key = Buffer.from(process.env.SESSION_SECRET ?? '', 'base64')
  if (key.length !== 32) {
    throw new Error('SESSION_SECRET must be 32 random bytes, base64-encoded. See .env.example.')
  }
  return key
}

// dir + A256GCM: the cookie is encrypted with SESSION_SECRET itself, so the
// browser holds it but can neither read the tokens inside nor change them.
export async function encrypt(session: Session, expiresAt: Date): Promise<string> {
  return new EncryptJWT({ ...session })
    .setProtectedHeader({ alg: 'dir', enc: 'A256GCM' })
    .setIssuedAt()
    .setExpirationTime(expiresAt)
    .encrypt(secretKey())
}

// Anything that fails to decrypt, has expired or has the wrong shape is
// treated exactly like no cookie at all.
export async function decrypt(value: string | undefined): Promise<Session | null> {
  if (!value) return null
  try {
    const { payload } = await jwtDecrypt(value, secretKey(), {
      keyManagementAlgorithms: ['dir'],
      contentEncryptionAlgorithms: ['A256GCM'],
    })
    return isSession(payload) ? { accessToken: payload.accessToken, refreshToken: payload.refreshToken, user: payload.user } : null
  } catch {
    return null
  }
}

function isSession(value: Record<string, unknown>): value is Session & Record<string, unknown> {
  const user = value.user as Record<string, unknown> | null | undefined
  return typeof value.accessToken === 'string' && value.accessToken !== ''
    && typeof value.refreshToken === 'string' && value.refreshToken !== ''
    && typeof user === 'object' && user !== null
    && typeof user.id === 'string' && typeof user.name === 'string' && typeof user.email === 'string'
}

export async function createSession(session: Session, expiresAt: Date): Promise<void> {
  const cookieStore = await cookies()
  cookieStore.set(COOKIE_NAME, await encrypt(session, expiresAt), {
    httpOnly: true,
    // Browsers treat http://localhost as secure, so this also works in development.
    secure: true,
    sameSite: 'lax',
    path: '/',
    expires: expiresAt,
  })
}

export async function getSession(): Promise<Session | null> {
  const cookieStore = await cookies()
  return decrypt(cookieStore.get(COOKIE_NAME)?.value)
}

export async function deleteSession(): Promise<void> {
  const cookieStore = await cookies()
  cookieStore.delete(COOKIE_NAME)
}
