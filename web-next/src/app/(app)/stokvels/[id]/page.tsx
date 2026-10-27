import type { Metadata } from 'next'
import Link from 'next/link'
import { api, readOrFail } from '@/lib/api'
import { formatCents } from '@/lib/money'
import { formatCycleDueDate } from '@/lib/dates'
import { StatusBadge } from '@/components/StatusBadge'
import { DueCountdown } from '@/components/DueCountdown'

// Generic on purpose: a title with the name would tell a non-member that the
// stokvel exists before the 404 arrives.
export const metadata: Metadata = { title: 'Stokvel · RondiTrack' }

export default async function StokvelPage({ params }: PageProps<'/stokvels/[id]'>) {
  const { id } = await params
  const client = await api()
  // Not a member, no such id and not-a-guid are all a 404 from the API,
  // which readOrFail turns into this route's not-found.tsx.
  const stokvel = readOrFail(await client.GET('/api/v1/stokvels/{id}', { params: { path: { id } } }))
  const cycle = stokvel.currentCycle

  return (
    <>
      <p className="mb-4 text-sm"><Link href="/stokvels" className="underline underline-offset-4">← My stokvels</Link></p>
      <article className="stokvel-card max-w-2xl">
        <header className="stokvel-card__header">
          <h1 className="text-2xl font-semibold">{stokvel.name}</h1>
          <p className="role-badge">You&apos;re the {stokvel.role}</p>
        </header>
        <dl>
          <div className="detail-row"><dt>Contribution</dt><dd>{formatCents(stokvel.contributionAmountCents)} a month</dd></div>
          <div className="detail-row"><dt>Members</dt><dd>{stokvel.memberCount}</dd></div>
        </dl>
        <section className="stokvel-card__section">
          <h2 className="mb-4 text-sm font-semibold">Current contribution cycle</h2>
          {cycle ? (
            <>
              <dl>
                <div className="detail-row"><dt>Cycle</dt><dd>{cycle.period}</dd></div>
                <div className="detail-row"><dt>Status</dt><dd><StatusBadge status={cycle.status} /></dd></div>
                <div className="detail-row"><dt>Due date</dt><dd>{cycle.dueDate ? formatCycleDueDate(cycle.dueDate) : 'No due date set'}</dd></div>
              </dl>
              <DueCountdown dueDate={cycle.dueDate} status={cycle.status} />
              <div className="contribution-summary">
                <div><span>Members paid</span><strong>{cycle.membersPaid} of {stokvel.memberCount}</strong></div>
                <div><span>Collected</span><strong>{formatCents(cycle.collectedCents)} of {formatCents(cycle.targetCents)}</strong></div>
              </div>
            </>
          ) : <p className="empty-state">No current contribution cycle</p>}
        </section>
      </article>
    </>
  )
}
