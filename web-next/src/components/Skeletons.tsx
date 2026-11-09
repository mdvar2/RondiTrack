// Loading placeholders shaped like what replaces them. Each is a status
// region with words a screen reader can say (aria-label on a plain div is
// ignored), and each reserves roughly the real content's height on a phone,
// so the page doesn't jump when the content streams in (CLS).
function Bar({ className }: { className: string }) {
  return <div aria-hidden="true" className={`animate-pulse rounded bg-muted motion-reduce:animate-none ${className}`} />
}

export function StokvelSummarySkeleton() {
  return (
    <div role="status">
      <span className="sr-only">Loading the stokvel…</span>
      <div aria-hidden="true" className="stokvel-card max-w-2xl space-y-4">
        <div className="flex justify-between gap-3"><Bar className="h-8 w-56" /><Bar className="h-6 w-28" /></div>
        <Bar className="h-5 w-full" />
        <Bar className="h-5 w-full" />
        <div className="space-y-3 border-t pt-4">
          <Bar className="h-5 w-48" />
          <Bar className="h-5 w-full" />
          <Bar className="h-5 w-full" />
          <Bar className="h-5 w-full" />
          <div className="grid gap-2.5 sm:grid-cols-2"><Bar className="h-16" /><Bar className="h-16" /></div>
          <Bar className="h-10 w-28" />
        </div>
      </div>
      <div aria-hidden="true" className="mt-6 max-w-2xl space-y-4">
        <Bar className="h-7 w-40" />
        <Bar className="h-80 w-full" />
      </div>
    </div>
  )
}

export function ContributionListSkeleton() {
  return (
    <div role="status" className="rounded-lg border bg-card p-4">
      <span className="sr-only">Loading this cycle&apos;s contributions…</span>
      <div aria-hidden="true" className="space-y-4">
        <Bar className="h-5 w-52" />
        {[0, 1, 2, 3].map((row) => (
          <div key={row} className="space-y-2"><div className="flex justify-between"><Bar className="h-5 w-36" /><Bar className="h-5 w-24" /></div><Bar className="h-4 w-44" /></div>
        ))}
      </div>
    </div>
  )
}

export function ActivityFeedSkeleton() {
  return (
    <div role="status" className="rounded-lg border bg-card p-4">
      <span className="sr-only">Loading recent activity…</span>
      <div aria-hidden="true" className="space-y-3">
        <Bar className="h-7 w-40" />
        {[0, 1, 2].map((row) => <Bar key={row} className="h-16 w-full" />)}
      </div>
    </div>
  )
}

export function DashboardSkeleton() {
  return (
    <div role="status">
      <span className="sr-only">Loading your stokvels…</span>
      <div aria-hidden="true" className="space-y-6">
        <div className="grid gap-3 sm:grid-cols-2"><Bar className="h-20" /><Bar className="h-20" /></div>
        <Bar className="h-96 w-full" />
        <Bar className="h-40 w-full" />
      </div>
    </div>
  )
}
