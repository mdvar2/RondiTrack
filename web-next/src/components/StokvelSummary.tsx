import type { ReactNode } from 'react'
import { formatCents } from '@/lib/money'
import { formatCycleDueDate } from '@/lib/dates'
import { canManageStokvel, getEligibleMembersForContribution } from '@/lib/contribution-permissions'
import type { ContributionInput, ContributionResult } from '@/lib/contribution-feedback'
import type { StokvelDetail, StokvelMember } from '@/lib/types'
import { StatusBadge } from './StatusBadge'
import { DueCountdown } from './DueCountdown'
import { ContributionForm } from './ContributionForm'
import { CloseCycleDialog } from './CloseCycleDialog'

type StokvelSummaryProps = {
  stokvel: StokvelDetail
  cycleId: string | null
  currentUserId: string
  members: readonly StokvelMember[]
  recordContribution: (input: ContributionInput) => Promise<ContributionResult>
  // The contribution list, streamed in its own Suspense boundary below.
  children?: ReactNode
}

// The stokvel view for one person in one stokvel. Synchronous and fed with
// data, so a component test can render it for each role (Part 3). What a
// Member doesn't see isn't hidden: it isn't rendered at all.
export function StokvelSummary({ stokvel, cycleId, currentUserId, members, recordContribution, children }: StokvelSummaryProps) {
  const cycle = stokvel.currentCycle
  const manages = canManageStokvel(stokvel.role)
  const eligible = getEligibleMembersForContribution(stokvel.role, currentUserId, members)

  return (
    <>
      <article className="stokvel-card max-w-2xl">
        <header className="stokvel-card__header">
          <h1 className="text-2xl font-semibold">{stokvel.name}</h1>
          <p className="role-badge">You&apos;re the {stokvel.role}</p>
        </header>
        <dl>
          <div className="detail-row"><dt>Contribution</dt><dd className="tabular-nums">{formatCents(stokvel.contributionAmountCents)} a month</dd></div>
          <div className="detail-row"><dt>Members</dt><dd className="tabular-nums">{stokvel.memberCount}</dd></div>
        </dl>
        <section className="stokvel-card__section" aria-labelledby="current-cycle-heading">
          <h2 id="current-cycle-heading" className="mb-4 text-sm font-semibold">Current contribution cycle</h2>
          {cycle ? (
            <>
              <dl>
                <div className="detail-row"><dt>Cycle</dt><dd>{cycle.period}</dd></div>
                <div className="detail-row"><dt>Status</dt><dd><StatusBadge status={cycle.status} /></dd></div>
                <div className="detail-row"><dt>Due date</dt><dd>{cycle.dueDate ? formatCycleDueDate(cycle.dueDate) : 'No due date set'}</dd></div>
              </dl>
              <DueCountdown dueDate={cycle.dueDate} status={cycle.status} />
              <dl className="contribution-summary">
                <div><dt>Members paid</dt><dd>{cycle.membersPaid} of {stokvel.memberCount}</dd></div>
                <div><dt>Collected</dt><dd>{formatCents(cycle.collectedCents)} of {formatCents(cycle.targetCents)}</dd></div>
              </dl>
              {manages && cycleId && cycle.status === 'Open' && (
                <div className="mt-4"><CloseCycleDialog stokvelId={stokvel.id} cycleId={cycleId} /></div>
              )}
            </>
          ) : <p className="empty-state">No current contribution cycle</p>}
        </section>
      </article>

      <section className="mt-6 max-w-2xl space-y-4" aria-labelledby="contribution-tools-heading">
        <h2 id="contribution-tools-heading" className="text-lg font-semibold">Contributions</h2>
        {!cycleId || cycle?.status !== 'Open' ? (
          <p className="rounded-lg border bg-card p-4 text-sm text-muted-foreground">There is no open contribution cycle to record against.</p>
        ) : eligible.length === 0 ? (
          <p className="rounded-lg border bg-card p-4 text-sm text-muted-foreground">You can&apos;t record contributions in this stokvel.</p>
        ) : (
          <ContributionForm
            stokvelId={stokvel.id}
            cycleId={cycleId}
            currentUserId={currentUserId}
            members={eligible}
            action={recordContribution}
          />
        )}
        {children}
      </section>
    </>
  )
}
