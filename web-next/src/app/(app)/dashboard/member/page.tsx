import type { Metadata } from 'next'
import Link from 'next/link'
import { api, readOrFail } from '@/lib/api'
import { formatCents } from '@/lib/money'
import { formatJohannesburgDate } from '@/lib/dates'
import { StatusBadge } from '@/components/StatusBadge'
import { EmptyState } from '@/components/EmptyState'
import { PageHeading } from '@/components/PageHeading'

export const metadata: Metadata = { title: 'Member dashboard · RondiTrack' }

export default async function MemberDashboardPage() {
  const client = await api()
  const { stokvels } = readOrFail(await client.GET('/api/v1/me/home'))

  if (stokvels.length === 0) {
    return (
      <>
        <PageHeading title="Member dashboard" />
        <EmptyState title="You're not in any stokvels yet">
          When a stokvel adds you as a member, what you&apos;ve paid and what you still owe will show here.
        </EmptyState>
      </>
    )
  }

  const totalPaid = stokvels.reduce((sum, stokvel) => sum + stokvel.amountPaid, 0)
  const totalOwed = stokvels.reduce((sum, stokvel) => sum + stokvel.amountOwed, 0)

  return (
    <>
      <PageHeading title="Member dashboard">
        Every stokvel you belong to, whatever your role in it.
      </PageHeading>

      <dl className="mb-6 grid gap-3 sm:grid-cols-2">
        <div className="rounded-lg bg-secondary p-4">
          <dt className="text-sm text-muted-foreground">Paid this cycle</dt>
          <dd className="text-xl font-semibold">{formatCents(totalPaid)}</dd>
        </div>
        <div className="rounded-lg bg-secondary p-4">
          <dt className="text-sm text-muted-foreground">Still owed this cycle</dt>
          <dd className="text-xl font-semibold">{totalOwed === 0 ? 'All paid up' : formatCents(totalOwed)}</dd>
        </div>
      </dl>

      <ul className="space-y-3">
        {stokvels.map((stokvel) => (
          <li key={stokvel.stokvelId} className="stokvel-card">
            <div className="stokvel-card__header mb-3!">
              <h2><Link href={`/stokvels/${stokvel.stokvelId}`} className="underline-offset-4 hover:underline">{stokvel.stokvelName}</Link></h2>
              <p className="role-badge">You&apos;re the {stokvel.role}</p>
            </div>
            <dl className="grid gap-x-6 sm:grid-cols-2 lg:grid-cols-4">
              <div className="detail-row"><dt>Cycle</dt><dd>{stokvel.currentCycle ? <>{stokvel.currentCycle.period} <StatusBadge status={stokvel.currentCycle.status} /></> : 'No current cycle'}</dd></div>
              <div className="detail-row"><dt>Paid</dt><dd>{formatCents(stokvel.amountPaid)}</dd></div>
              <div className="detail-row"><dt>Still owed</dt><dd>{stokvel.amountOwed === 0 ? 'All paid up' : formatCents(stokvel.amountOwed)}</dd></div>
              <div className="detail-row"><dt>Next payout</dt><dd>{stokvel.nextPayoutDate ? formatJohannesburgDate(stokvel.nextPayoutDate) : 'No payout scheduled'}</dd></div>
            </dl>
            {stokvel.arrearsCents > 0 && <StatusBadge arrearsCents={stokvel.arrearsCents} />}
          </li>
        ))}
      </ul>
    </>
  )
}
