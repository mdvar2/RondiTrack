'use server'

import { redirect } from 'next/navigation'
import { publicApi } from './api'
import { describeError, type ErrorDescription, type Problem } from './errors'
import { readRefreshCookie, REFRESH_COOKIE } from './refresh-cookie'
import { createSession, deleteSession, getSession } from './session'

// What the login form shows. Only safe, readable values: never a token.
export type SignInState = (ErrorDescription & { email: string }) | null

export async function signIn(_previous: SignInState, formData: FormData): Promise<SignInState> {
  const email = String(formData.get('email') ?? '').trim()
  const password = String(formData.get('password') ?? '')
  if (!email || !password) return { message: 'Enter your email and password.', email }

  const client = publicApi()
  // null when the API can't be reached at all.
  const login = await client.POST('/api/v1/auth/login', { body: { email, password } }).catch(() => null)
  if (!login) return { ...describeError(null), email }
  if (!login.data) {
    return {
      ...describeError({
        status: login.response.status,
        problem: login.error as Problem | undefined,
        retryAfter: login.response.headers.get('Retry-After'),
      }),
      email,
    }
  }

  const refresh = readRefreshCookie(login.response.headers.getSetCookie())
  if (!refresh) return { ...describeError({ status: 500 }), email }

  // Who signed in. Name and email only: roles are per stokvel and come later.
  const me = await client.GET('/api/v1/me', {
    headers: { Authorization: `Bearer ${login.data.accessToken}` },
  })
  if (!me.data) return { ...describeError({ status: me.response.status, problem: me.error as Problem | undefined }), email }

  const fallbackExpiry = new Date(Date.now() + 7 * 24 * 60 * 60 * 1000)
  await createSession(
    {
      accessToken: login.data.accessToken,
      refreshToken: refresh.token,
      user: { id: me.data.id, name: me.data.name, email: me.data.email },
    },
    refresh.expiresAt ?? fallbackExpiry,
  )
  redirect('/dashboard/member')
}

export async function signOut(): Promise<void> {
  const session = await getSession()
  if (session) {
    // The API finds the refresh session by its cookie, so send it as one.
    // A failed call must not keep the person signed in here.
    try {
      await publicApi().POST('/api/v1/auth/logout', {
        headers: { Cookie: `${REFRESH_COOKIE}=${session.refreshToken}` },
      })
    } catch {
      // The API is unreachable; the local session is still removed.
    }
  }
  await deleteSession()
  redirect('/login')
}
