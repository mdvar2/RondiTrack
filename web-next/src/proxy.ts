import { NextResponse } from 'next/server'
import type { NextRequest } from 'next/server'
import { COOKIE_NAME, decrypt, encrypt, sessionCookieOptions } from '@/lib/session'
import { isAccessTokenCloseToExpiry, rotateSession } from '@/lib/proxy'
import { ARM_TTL_MS, createArmToken, DEMO_FAULT_COOKIE, DEMO_FAULT_PARAM, DEMO_FAULT_PARAM_VALUE, demoFaultConfig, isDemoAccount } from '@/lib/demo-fault'

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

  // ?demoFault=drop-next arms the simulated post-commit response failure
  // (src/lib/demo-fault.ts). The parameter is always removed with a redirect;
  // the arm cookie is only set when every server-side check passes, so the
  // answer looks the same to anyone who isn't allowed.
  if (request.nextUrl.searchParams.has(DEMO_FAULT_PARAM)) {
    const clean = request.nextUrl.clone()
    clean.searchParams.delete(DEMO_FAULT_PARAM)
    const response = NextResponse.redirect(clean)
    const config = demoFaultConfig()
    if (config && request.nextUrl.searchParams.get(DEMO_FAULT_PARAM) === DEMO_FAULT_PARAM_VALUE
      && pathname.startsWith('/stokvels/') && isDemoAccount(config, session.user.email)) {
      response.cookies.set(DEMO_FAULT_COOKIE, createArmToken(config, session.user.id), {
        httpOnly: true, sameSite: 'strict', secure: request.nextUrl.protocol === 'https:', path: '/', maxAge: ARM_TTL_MS / 1000,
      })
    }
    return response
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
  // robots.txt and the sign-in page's preview image are public files: no
  // session to read, nothing to redirect.
  matcher: ['/((?!api|_next/static|_next/image|favicon.ico|robots.txt|login/opengraph-image).*)'],
}
