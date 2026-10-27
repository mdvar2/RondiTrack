export default function Loading() {
  return (
    <div role="status" aria-live="polite" className="space-y-4">
      <div className="h-8 w-56 animate-pulse rounded bg-muted" />
      <div className="stokvel-grid">
        {[0, 1, 2].map((key) => <div key={key} className="h-64 animate-pulse rounded-lg bg-muted" />)}
      </div>
      <span className="sr-only">Loading…</span>
    </div>
  )
}
