import { NextResponse } from 'next/server'
import type { NextRequest } from 'next/server'
import { COOKIE_NAME, decrypt, encrypt, sessionCookieOptions } from '@/lib/session'
import { isAccessTokenCloseToExpiry, rotateSession } from '@/lib/proxy'

function isProtectedPath(pathname: string) {
  return pathname === '/' || pathname.startsWith('/dashboard') || pathname.startsWith('/stokvels')
}

function safePath(path: string | null) {
  return path && path.startsWith('/') && !path.startsWith('//') && !path.startsWith('/\\')
    ? path
    : '/dashboard/member'
}

function toLogin(request: NextRequest, expired: boolean) {
  const { pathname, search } = request.nextUrl
  const loginUrl = new URL('/login', request.url)
  if (expired) loginUrl.searchParams.set('expired', '1')
  loginUrl.searchParams.set('next', safePath(`${pathname}${search}`))
  const response = NextResponse.redirect(loginUrl)
  if (request.cookies.has(COOKIE_NAME)) response.cookies.delete(COOKIE_NAME)
  return response
}

export async function proxy(request: NextRequest) {
  const { pathname } = request.nextUrl
  const hasCookie = request.cookies.has(COOKIE_NAME)
  // A cookie that does not decrypt (expired, other secret) counts as no session.
  const session = await decrypt(request.cookies.get(COOKIE_NAME)?.value)

  if (pathname === '/login' || pathname === '/register') {
    // ?expired=1 means the API refused this session, so forget it here
    // instead of sending the person straight back to the dashboard.
    const expired = request.nextUrl.searchParams.get('expired') === '1'
    if (session && !expired) {
      return NextResponse.redirect(new URL(safePath(request.nextUrl.searchParams.get('next')), request.url))
    }
    const response = NextResponse.next()
    if (hasCookie) response.cookies.delete(COOKIE_NAME)
    return response
  }

  if (!session) {
    if (isProtectedPath(pathname)) return toLogin(request, false)
    return NextResponse.next()
  }

  if (!isAccessTokenCloseToExpiry(session.accessToken)) return NextResponse.next()

  // Pages cannot write cookies, so the access token is refreshed here, before
  // the page runs, and the rotated refresh token is saved on the response.
  let rotated
  try {
    rotated = await rotateSession(session)
  } catch {
    // The API is unreachable. Keep the session; the page shows the error.
    return NextResponse.next()
  }
  if (!rotated) return toLogin(request, true)

  const value = await encrypt(rotated.session, rotated.expiresAt)
  // The page in this same request reads the new cookie, not the old one.
  request.cookies.set(COOKIE_NAME, value)
  const response = NextResponse.next({ request: { headers: request.headers } })
  response.cookies.set(COOKIE_NAME, value, sessionCookieOptions(rotated.expiresAt))
  return response
}

export const config = {
  matcher: ['/((?!api|_next/static|_next/image|favicon.ico).*)'],
}
