// The API sends the refresh token as an HttpOnly cookie meant for a browser.
// Now the Next.js server is the API's client, so it reads that cookie from the
// login response's Set-Cookie headers and keeps it in the encrypted session.
export const REFRESH_COOKIE = '__Secure-ronditrack_refresh'

export function readRefreshCookie(setCookies: readonly string[]): { token: string; expiresAt: Date | null } | null {
  for (const header of setCookies) {
    const [pair, ...attributes] = header.split(';').map((part) => part.trim())
    const separator = pair.indexOf('=')
    if (separator < 0 || pair.slice(0, separator) !== REFRESH_COOKIE) continue

    const token = pair.slice(separator + 1)
    if (!token) return null

    const expires = attributes.find((attribute) => attribute.toLowerCase().startsWith('expires='))
    const expiresAt = expires ? new Date(expires.slice('expires='.length)) : null
    return { token, expiresAt: expiresAt && !Number.isNaN(expiresAt.getTime()) ? expiresAt : null }
  }
  return null
}
