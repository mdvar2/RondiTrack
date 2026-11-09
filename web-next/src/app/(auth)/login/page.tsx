import type { Metadata } from 'next'
import { LoginForm } from '@/components/LoginForm'

const description = 'RondiTrack keeps a stokvel’s contributions, proofs of payment and payouts in one place, so every member can see what has been paid.'

// The one page a search engine should find, and what a link preview of any
// signed-in URL shows, because signed-out requests for those redirect here.
// It describes the product and nothing about anyone in it.
export const metadata: Metadata = {
  title: 'Sign in',
  description,
  alternates: { canonical: '/login' },
  openGraph: { type: 'website', siteName: 'RondiTrack', title: 'RondiTrack: stokvel contributions, tracked', description, url: '/login' },
  twitter: { card: 'summary_large_image', title: 'RondiTrack: stokvel contributions, tracked', description },
}
export const instant = false

export default async function LoginPage({ searchParams }: PageProps<'/login'>) {
  const { expired, next } = await searchParams
  const safeNext = typeof next === 'string' && next.startsWith('/') && !next.startsWith('//') && !next.startsWith('/\\')
    ? next
    : '/dashboard/member'

  return (
    <main className="mx-auto flex min-h-screen max-w-sm flex-col justify-center px-4 py-12">
      <p className="mb-2 text-sm font-semibold tracking-wide text-muted-foreground">RondiTrack</p>
      <h1 className="mb-6 text-2xl font-semibold">Sign in</h1>
      {expired === '1' && (
        <p role="status" className="mb-4 rounded-lg bg-muted p-3 text-sm">
          Your session has expired. Please sign in again.
        </p>
      )}
      <LoginForm next={safeNext} />
    </main>
  )
}
