import type { Metadata } from 'next'
import Link from 'next/link'
import { Suspense } from 'react'
import { api, readOrFail } from '@/lib/api'
import { formatCents } from '@/lib/money'
import { formatCycleDueDate } from '@/lib/dates'
import { StatusBadge } from '@/components/StatusBadge'
import { DueCountdown } from '@/components/DueCountdown'
import { ContributionForm } from '@/components/ContributionForm'
import { getSession } from '@/lib/session'
import { ProofUpload } from '@/components/ProofUpload'
import { CloseCycleDialog } from '@/components/CloseCycleDialog'

// Generic on purpose: a title with the name would tell a non-member that the
// stokvel exists before the 404 arrives.
export const metadata: Metadata = { title: 'Stokvel · RondiTrack' }

async function StokvelDetailData({ stokvelId }: { stokvelId: string }) {
  const session = await getSession()
  const currentUserId = session?.user.id ?? ''
  const client = await api()

  const [stokvelResult, membersResult, cyclesResult] = await Promise.all([
    client.GET('/api/v1/stokvels/{id}', { params: { path: { id: stokvelId } } }),
    client.GET('/api/v1/stokvels/{stokvelId}/members', {
      params: { path: { stokvelId }, query: { pageSize: 100 } },
    }),
    client.GET('/api/v1/stokvels/{stokvelId}/cycles', {
      params: { path: { stokvelId } },
    }),
  ])

  const stokvel = readOrFail(stokvelResult)
  const membersResponse = readOrFail(membersResult)
  const cycles = readOrFail(cyclesResult)
  const cycle = stokvel.currentCycle
  const cycleId = Array.isArray(cycles) ? cycles.at(-1)?.id ?? null : null
  const contributions = cycleId
    ? readOrFail(await client.GET('/api/v1/stokvels/{stokvelId}/cycles/{cycleId}/contributions', {
      params: { path: { stokvelId, cycleId }, query: { pageSize: 100, sortBy: 'recordedAtUtc', sortDirection: 'desc' },
    } }))
    : { items: [] }

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
              {['Treasurer', 'Chairperson'].includes(stokvel.role) && cycleId && <CloseCycleDialog stokvelId={stokvelId} cycleId={cycleId} />}
              <div className="contribution-summary">
                <div><span>Members paid</span><strong>{cycle.membersPaid} of {stokvel.memberCount}</strong></div>
                <div><span>Collected</span><strong>{formatCents(cycle.collectedCents)} of {formatCents(cycle.targetCents)}</strong></div>
              </div>
            </>
          ) : <p className="empty-state">No current contribution cycle</p>}
        </section>
      </article>

      <section className="mt-6 max-w-2xl space-y-4">
        <div className="flex items-center justify-between gap-3">
          <h2 className="text-lg font-semibold">Contribution tools</h2>
        </div>
        <ContributionForm
          stokvelId={stokvelId}
          cycleId={cycleId}
          role={stokvel.role}
          currentUserId={currentUserId}
          members={membersResponse.items ?? []}
        />
        <section className="space-y-3 rounded-lg border bg-card p-4">
          <h3 className="text-sm font-semibold">Proofs of payment</h3>
          {(contributions.items ?? []).map((contribution) => (
            <div key={contribution.id} className="space-y-2 border-b pb-3 last:border-0 last:pb-0">
              <p className="text-sm">{contribution.userName} · {formatCents(Math.round(Number(contribution.amount) * 100))}</p>
              {(contribution.userId === currentUserId || ['Treasurer', 'Chairperson'].includes(stokvel.role)) && (
                <>
                  <ProofUpload contributionId={contribution.id} />
                  <a href={`/bff/contributions/${encodeURIComponent(contribution.id)}/proof/download`} className="text-sm underline">Download proof</a>
                </>
              )}
            </div>
          ))}
          {(contributions.items ?? []).length === 0 && <p className="text-sm text-muted-foreground">No contributions recorded yet.</p>}
        </section>
      </section>
    </>
  )
}

export default async function StokvelPage({ params }: PageProps<'/stokvels/[id]'>) {
  const { id } = await params

  return (
    <Suspense fallback={<p className="text-sm text-muted-foreground">Loading stokvel details…</p>}>
      <StokvelDetailData stokvelId={id} />
    </Suspense>
  )
}
