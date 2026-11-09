// Signs in through the API once and builds the same encrypted `session`
// cookie that src/lib/session.ts writes, so measurement tools that can't
// fill in a form (Lighthouse's CLI) can load signed-in pages.
// Uses one login, which counts against the API's 5-a-minute limit.
import { EncryptJWT } from 'jose'

const REFRESH_COOKIE = '__Secure-ronditrack_refresh'

export async function sessionCookie({ api, email, password, secret }) {
  const login = await fetch(`${api}/api/v1/auth/login`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ email, password }),
  })
  if (!login.ok) throw new Error(`Login failed: ${login.status} ${await login.text()}`)
  const { accessToken } = await login.json()
  const refreshPair = login.headers.getSetCookie()
    .map((header) => header.split(';')[0])
    .find((pair) => pair.startsWith(`${REFRESH_COOKIE}=`))
  if (!refreshPair) throw new Error('Login response had no refresh cookie')

  const me = await fetch(`${api}/api/v1/me`, { headers: { Authorization: `Bearer ${accessToken}` } })
  if (!me.ok) throw new Error(`/me failed: ${me.status}`)
  const user = await me.json()

  const key = Buffer.from(secret, 'base64')
  if (key.length !== 32) throw new Error('SESSION_SECRET must be 32 bytes, base64-encoded')
  const expiresAt = new Date(Date.now() + 7 * 24 * 60 * 60 * 1000)
  const value = await new EncryptJWT({
    accessToken,
    refreshToken: refreshPair.slice(REFRESH_COOKIE.length + 1),
    user: { id: user.id, name: user.name, email: user.email },
  })
    .setProtectedHeader({ alg: 'dir', enc: 'A256GCM' })
    .setIssuedAt()
    .setExpirationTime(expiresAt)
    .encrypt(key)
  return { cookie: `session=${value}`, accessToken }
}
