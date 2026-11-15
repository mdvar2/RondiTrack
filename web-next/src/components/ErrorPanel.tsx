'use client'

// What every error boundary shows: one apology, one reference, one way on.
// Used by (app)/error.tsx, app/error.tsx and app/global-error.tsx, so no error
// ever reaches Next.js's default error page.
import { describeError, fromDigest } from '@/lib/errors'
import { Button } from '@/components/ui/button'

export function ErrorPanel({ error, retry }: { error: Error & { digest?: string }; retry?: () => void }) {
  // Only the digest survives the trip from the server. readOrFail and our fetch
  // wrapper put the status and the trace id in it. For any other error the
  // digest is Next.js's own: the trace backend finds it by the span attribute
  // ronditrack.error.digest, and the server log line "[request-error]" pairs it
  // with the trace id (src/instrumentation.ts).
  const api = fromDigest(error.digest)
  const detail = describeError(api ?? { status: 500 })
  const reference = detail.correlationId ?? (api ? undefined : error.digest)

  return (
    <div role="alert" className="max-w-xl space-y-3 rounded-lg border border-destructive p-5">
      <h1 className="text-lg font-semibold">{detail.message}</h1>
      {reference && (
        <p className="text-sm text-muted-foreground">
          Reference: <code>{reference}</code>
          <Button type="button" variant="link" size="sm" onClick={() => void navigator.clipboard?.writeText(reference)}>Copy</Button>
        </p>
      )}
      {retry && <Button type="button" onClick={() => retry()}>Try again</Button>}
    </div>
  )
}
