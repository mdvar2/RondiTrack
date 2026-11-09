import type { Metadata } from 'next'
import { RegisterForm } from '@/components/RegisterForm'

export const metadata: Metadata = {
  title: 'Create account',
  description: 'Create a RondiTrack account to record your stokvel contributions and see what everyone has paid.',
}

// Reads ?next=, like the sign-in page, so it renders per request.
export const instant = false

export default async function RegisterPage({ searchParams }: PageProps<'/register'>) {
  const { next } = await searchParams
  const safeNext = typeof next === 'string' && next.startsWith('/') && !next.startsWith('//') && !next.startsWith('/\\')
    ? next
    : '/dashboard/member'

  return (
    <main className="mx-auto flex min-h-screen max-w-sm flex-col justify-center px-4 py-12">
      <p className="mb-2 text-sm font-semibold tracking-wide text-muted-foreground">RondiTrack</p>
      <h1 className="mb-6 text-2xl font-semibold">Create account</h1>
      <RegisterForm next={safeNext} />
    </main>
  )
}
