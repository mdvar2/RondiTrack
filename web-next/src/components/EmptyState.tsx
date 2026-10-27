import Link from 'next/link'

type EmptyStateProps = {
  title: string
  children: React.ReactNode
  action?: { href: string; label: string }
}

// "Nothing here yet" is a normal answer, not an error: no alert role, no red.
export function EmptyState({ title, children, action }: EmptyStateProps) {
  return (
    <section className="rounded-lg border border-dashed p-6 text-center">
      <h2 className="text-lg font-semibold">{title}</h2>
      <div className="mt-2 text-sm text-muted-foreground">{children}</div>
      {action && (
        <Link href={action.href} className="mt-4 inline-block text-sm font-medium underline underline-offset-4">
          {action.label}
        </Link>
      )}
    </section>
  )
}
