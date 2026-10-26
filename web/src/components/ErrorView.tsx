import { describeError } from '../lib/errorMessages'
export function ErrorView({ error, retry }: { error: unknown; retry?: () => void }) {
  const detail = describeError(error)
  return <div role="alert" className="rounded-lg border border-destructive p-4 space-y-2">
    <p>{detail.message}</p>
    {detail.retryAfter !== undefined && <p>Retry after {detail.retryAfter} seconds.</p>}
    {detail.correlationId && <p>Reference: <code>{detail.correlationId}</code> <button type="button" onClick={() => void navigator.clipboard?.writeText(detail.correlationId!)}>Copy</button></p>}
    {retry && <button type="button" className="rounded border px-3 py-2" onClick={retry}>Try again</button>}
  </div>
}
