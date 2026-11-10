import type { Metadata } from 'next'
import Link from 'next/link'

export const metadata: Metadata = { title: 'Not found', robots: { index: false } }

// Any path that doesn't exist, and the target for a proof of payment that
// doesn't exist or isn't yours to see (the same page for both, with nothing
// taken from the URL).
export default function NotFound() {
  return (
    <main className="mx-auto max-w-xl px-4 py-12">
      <section className="space-y-3 rounded-lg border p-6">
        <h1 className="text-xl font-semibold">We couldn&apos;t find that page</h1>
        <p className="text-sm text-muted-foreground">It may not exist, or you may not have access to it.</p>
        <Link href="/dashboard/member" className="inline-block text-sm font-medium underline underline-offset-4">
          Go to your dashboard
        </Link>
      </section>
    </main>
  )
}
