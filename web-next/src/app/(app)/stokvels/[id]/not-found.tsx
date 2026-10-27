import Link from 'next/link'

// The same page for "not a member" and "no such stokvel", with nothing taken
// from the URL, so it can't confirm that a stokvel exists.
export default function StokvelNotFound() {
  return (
    <section className="max-w-xl space-y-3 rounded-lg border p-6">
      <h1 className="text-xl font-semibold">We couldn&apos;t find that stokvel</h1>
      <p className="text-sm text-muted-foreground">
        It may not exist, or you may not be a member of it.
      </p>
      <Link href="/stokvels" className="inline-block text-sm font-medium underline underline-offset-4">
        Back to my stokvels
      </Link>
    </section>
  )
}
