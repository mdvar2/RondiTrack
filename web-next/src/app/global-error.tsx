'use client'

// The last resort, when the root layout itself fails. It replaces the root
// layout, so it brings its own <html>, <body> and styles.
import './globals.css'
import { ErrorPanel } from '@/components/ErrorPanel'

export default function GlobalError({ error, retry }: { error: Error & { digest?: string }; retry: () => void }) {
  return (
    <html lang="en" className="h-full">
      <body className="min-h-full">
        <title>Something went wrong · RondiTrack</title>
        <main className="mx-auto max-w-xl px-4 py-12">
          <ErrorPanel error={error} retry={retry} />
        </main>
      </body>
    </html>
  )
}
