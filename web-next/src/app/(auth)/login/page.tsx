import type { Metadata } from 'next'
import { LoginForm } from '@/components/LoginForm'

export const metadata: Metadata = { title: 'Sign in · RondiTrack' }

// Deliberately does not redirect a signed-in person away: after an expired
// token (until Day 3 adds refresh) the old cookie is still there, and sending
// them back to the dashboard would loop.
export default async function LoginPage({ searchParams }: PageProps<'/login'>) {
  const { expired } = await searchParams

  return (
    <main className="mx-auto flex min-h-screen max-w-sm flex-col justify-center px-4 py-12">
      <p className="mb-2 text-sm font-semibold tracking-wide text-muted-foreground">RondiTrack</p>
      <h1 className="mb-6 text-2xl font-semibold">Sign in</h1>
      {expired === '1' && (
        <p role="status" className="mb-4 rounded-lg bg-muted p-3 text-sm">
          Your session has expired. Please sign in again.
        </p>
      )}
      <LoginForm />
    </main>
  )
}
