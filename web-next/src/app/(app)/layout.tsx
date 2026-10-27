import { Suspense } from 'react'
import Link from 'next/link'
import { redirect } from 'next/navigation'
import { getSession } from '@/lib/session'
import { signOut } from '@/lib/auth-actions'
import { Button } from '@/components/ui/button'

// All four links, for everyone. Whether someone is Treasurer anywhere is
// answered per stokvel by the API, and the treasurer page handles "nowhere".
const links = [
  { href: '/', label: 'Home' },
  { href: '/dashboard/member', label: 'Member dashboard' },
  { href: '/dashboard/treasurer', label: 'Treasurer dashboard' },
  { href: '/stokvels', label: 'My stokvels' },
] as const

// Reads the session, so it waits inside <Suspense> and the rest of the layout
// can be sent straight away. Only the name and email are rendered.
async function SignedInAs() {
  const session = await getSession()
  if (!session) redirect('/login')
  return (
    <p className="text-sm">
      <span className="block font-medium">{session.user.name}</span>
      <span className="block break-all text-muted-foreground">{session.user.email}</span>
    </p>
  )
}

export default function AppLayout({ children }: LayoutProps<'/'>) {
  return (
    <div className="flex min-h-screen flex-col md:flex-row">
      <aside className="border-b bg-card md:sticky md:top-0 md:h-screen md:w-64 md:shrink-0 md:border-r md:border-b-0">
        <div className="flex h-full flex-col gap-6 p-4">
          <Link href="/dashboard/member" className="flex items-center gap-3">
            <span aria-hidden="true" className="grid size-9 place-items-center rounded-lg bg-primary text-lg font-bold text-primary-foreground">R</span>
            <strong className="text-lg">RondiTrack</strong>
          </Link>
          <nav aria-label="Main">
            <ul className="flex flex-wrap gap-1 md:flex-col">
              {links.map((link) => (
                <li key={link.href}>
                  <Link href={link.href} className="block rounded-lg px-3 py-2 text-sm hover:bg-muted">{link.label}</Link>
                </li>
              ))}
            </ul>
          </nav>
          <div className="mt-auto space-y-3 border-t pt-4">
            <Suspense fallback={<p className="text-sm text-muted-foreground">Loading…</p>}>
              <SignedInAs />
            </Suspense>
            <form action={signOut}>
              <Button type="submit" variant="outline" className="w-full">Sign out</Button>
            </form>
          </div>
        </div>
      </aside>
      <main className="min-w-0 flex-1 px-4 py-6 sm:px-8 sm:py-8">{children}</main>
    </div>
  )
}
