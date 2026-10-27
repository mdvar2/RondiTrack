'use client'

// Error boundaries must be Client Components: they catch errors in the browser
// and the Try again button needs an onClick.
import { describeError, fromDigest } from '@/lib/errors'
import { Button } from '@/components/ui/button'

export default function AppError({ error, retry }: { error: Error & { digest?: string }; retry: () => void }) {
  // Only the digest survives the trip from the server. readOrFail puts the
  // API's status and correlation id in it; anything else is our own bug.
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
      <Button type="button" onClick={() => retry()}>Try again</Button>
    </div>
  )
}
