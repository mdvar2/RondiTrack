'use server'

import { redirect } from 'next/navigation'
import { publicApi } from './proxy'
import { describeError, type ErrorDescription, type Problem } from './errors'
import { readRefreshCookie, REFRESH_COOKIE } from './refresh-cookie'
import { createSession, deleteSession, getSession } from './session'

export type SignInState = (ErrorDescription & { email: string }) | null

export async function signIn(_previous: SignInState, formData: FormData): Promise<SignInState> {
  const email = String(formData.get('email') ?? '').trim()
  const password = String(formData.get('password') ?? '')
  const next = String(formData.get('next') ?? '/dashboard/member')
  if (!email || !password) return { message: 'Enter your email and password.', email }

  const client = publicApi()
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

  const me = await client.GET('/api/v1/me', {
    headers: { Authorization: `Bearer ${login.data.accessToken}` },
  })
  if (!me.data) {
    return { ...describeError({ status: me.response.status, problem: me.error as Problem | undefined }), email }
  }

  const fallbackExpiry = new Date(Date.now() + 7 * 24 * 60 * 60 * 1000)
  await createSession(
    {
      accessToken: login.data.accessToken,
      refreshToken: refresh.token,
      user: { id: me.data.id, name: me.data.name, email: me.data.email },
    },
    refresh.expiresAt ?? fallbackExpiry,
  )

  redirect(next.startsWith('/') && !next.startsWith('//') && !next.startsWith('/\\') ? next : '/dashboard/member')
}

export type RegisterState = (ErrorDescription & { name: string; email: string }) | null

export async function register(_previous: RegisterState, formData: FormData): Promise<RegisterState> {
  const name = String(formData.get('name') ?? '').trim()
  const email = String(formData.get('email') ?? '').trim()
  const password = String(formData.get('password') ?? '')
  const next = String(formData.get('next') ?? '/dashboard/member')
  if (!name || !email || !password) return { message: 'Please fill in all fields.', name, email }

  const client = publicApi()
  const created = await client.POST('/api/v1/auth/register', { body: { name, email, password } }).catch(() => null)
  if (!created) return { ...describeError(null), name, email }
  if (!created.data) {
    return {
      ...describeError({
        status: created.response.status,
        problem: created.error as Problem | undefined,
        retryAfter: created.response.headers.get('Retry-After'),
      }),
      name,
      email,
    }
  }

  const login = await client.POST('/api/v1/auth/login', { body: { email, password } }).catch(() => null)
  if (!login || !login.data) {
    return { ...describeError(login ? { status: login.response.status, problem: login.error as Problem | undefined } : null), name, email }
  }

  const refresh = readRefreshCookie(login.response.headers.getSetCookie())
  if (!refresh) return { ...describeError({ status: 500 }), name, email }

  const me = await client.GET('/api/v1/me', {
    headers: { Authorization: `Bearer ${login.data.accessToken}` },
  })
  if (!me.data) {
    return { ...describeError({ status: me.response.status, problem: me.error as Problem | undefined }), name, email }
  }

  const fallbackExpiry = new Date(Date.now() + 7 * 24 * 60 * 60 * 1000)
  await createSession(
    {
      accessToken: login.data.accessToken,
      refreshToken: refresh.token,
      user: { id: me.data.id, name: me.data.name, email: me.data.email },
    },
    refresh.expiresAt ?? fallbackExpiry,
  )
  redirect(next.startsWith('/') && !next.startsWith('//') && !next.startsWith('/\\') ? next : '/dashboard/member')
}

export async function signOut(): Promise<void> {
  const session = await getSession()
  if (session) {
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
