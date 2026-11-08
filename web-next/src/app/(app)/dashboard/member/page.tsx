import type { Metadata } from 'next'
import Link from 'next/link'
import { Suspense } from 'react'
import { formatCents } from '@/lib/money'
import { formatJohannesburgDate } from '@/lib/dates'
import { loadActivityPage, loadHomeStokvels } from '@/lib/activity'
import { activityFiltersFrom } from '@/lib/activity-filters'
import { StatusBadge } from '@/components/StatusBadge'
import { EmptyState } from '@/components/EmptyState'
import { PageHeading } from '@/components/PageHeading'
import { ActivityFeed } from '@/components/ActivityFeed'
import { DashboardSkeleton } from '@/components/Skeletons'

export const metadata: Metadata = { title: 'Member dashboard' }

// The heading is in the static shell, sent before any API call: it was the
// home screen's LCP element and waited for /me/home. The data streams in below.
export default function MemberDashboardPage({ searchParams }: PageProps<'/dashboard/member'>) {
  return (
    <>
      <PageHeading title="Member dashboard">
        Every stokvel you belong to, whatever your role in it.
      </PageHeading>
      <Suspense fallback={<DashboardSkeleton />}>
        <MemberDashboard searchParams={searchParams} />
      </Suspense>
    </>
  )
}

async function MemberDashboard({ searchParams }: { searchParams: PageProps<'/dashboard/member'>['searchParams'] }) {
  const filters = activityFiltersFrom(await searchParams)
  const stokvels = await loadHomeStokvels()

  if (stokvels.length === 0) {
    return (
      <>
        <EmptyState title="You're not in any stokvels yet">
          When a stokvel adds you as a member, what you&apos;ve paid and what you still owe will show here.
        </EmptyState>
      </>
    )
  }

  // The first page of activity is fetched here, with the rest of the page,
  // so it is in the HTML rather than requested by the browser after the
  // JavaScript has loaded. It was the home screen's LCP element.
  const firstPage = await loadActivityPage(stokvels, filters)
  const totalPaid = stokvels.reduce((sum, stokvel) => sum + stokvel.amountPaid, 0)
  const totalOwed = stokvels.reduce((sum, stokvel) => sum + stokvel.amountOwed, 0)

  return (
    <>
      <dl className="mb-6 grid gap-3 sm:grid-cols-2">
        <div className="rounded-lg bg-secondary p-4">
          <dt className="text-sm text-muted-foreground">Paid this cycle</dt>
          <dd className="text-xl font-semibold tabular-nums">{formatCents(totalPaid)}</dd>
        </div>
        <div className="rounded-lg bg-secondary p-4">
          <dt className="text-sm text-muted-foreground">Still owed this cycle</dt>
          <dd className="text-xl font-semibold tabular-nums">{totalOwed === 0 ? 'All paid up' : formatCents(totalOwed)}</dd>
        </div>
      </dl>

      <div className="mb-6">
        <ActivityFeed initialPage={firstPage} initialFilters={filters} />
      </div>

      <ul className="space-y-3">
        {stokvels.map((stokvel) => (
          <li key={stokvel.stokvelId} className="stokvel-card">
            <div className="stokvel-card__header mb-3!">
              <h2><Link href={`/stokvels/${stokvel.stokvelId}`} className="underline-offset-4 hover:underline">{stokvel.stokvelName}</Link></h2>
              <p className="role-badge">You&apos;re the {stokvel.role}</p>
            </div>
            <dl className="grid gap-x-6 sm:grid-cols-2 lg:grid-cols-4">
              <div className="detail-row"><dt>Cycle</dt><dd>{stokvel.currentCycle ? <>{stokvel.currentCycle.period} <StatusBadge status={stokvel.currentCycle.status} /></> : 'No current cycle'}</dd></div>
              <div className="detail-row"><dt>Paid</dt><dd className="tabular-nums">{formatCents(stokvel.amountPaid)}</dd></div>
              <div className="detail-row"><dt>Still owed</dt><dd className="tabular-nums">{stokvel.amountOwed === 0 ? 'All paid up' : formatCents(stokvel.amountOwed)}</dd></div>
              <div className="detail-row"><dt>Next payout</dt><dd>{stokvel.nextPayoutDate ? formatJohannesburgDate(stokvel.nextPayoutDate) : 'No payout scheduled'}</dd></div>
            </dl>
            {stokvel.arrearsCents > 0 && <StatusBadge arrearsCents={stokvel.arrearsCents} />}
          </li>
        ))}
      </ul>
    </>
  )
}
