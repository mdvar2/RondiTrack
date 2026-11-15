'use client'

// For pages outside the signed-in area (sign in, register, join): without this,
// an error there would reach Next.js's default error page.
import { ErrorPanel } from '@/components/ErrorPanel'

export default function RootError({ error, retry }: { error: Error & { digest?: string }; retry: () => void }) {
  return (
    <main className="mx-auto max-w-xl px-4 py-12">
      <ErrorPanel error={error} retry={retry} />
    </main>
  )
}
