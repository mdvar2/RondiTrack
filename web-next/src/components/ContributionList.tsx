import { formatCents } from '@/lib/money'
import { formatJohannesburgDateTime } from '@/lib/dates'
import { canSeeProof } from '@/lib/contribution-permissions'
import type { ContributionWithMember } from '@/lib/types'
import { ProofUpload } from './ProofUpload'

type ContributionListProps = {
  contributions: readonly ContributionWithMember[]
  role: string
  currentUserId: string
  cycleOpen: boolean
}

// This cycle's contributions. It streams in its own Suspense boundary, so a
// click on the form above hydrates the form only, not 40 upload forms (INP).
export function ContributionList({ contributions, role, currentUserId, cycleOpen }: ContributionListProps) {
  return (
    <section aria-labelledby="this-cycle-heading" className="rounded-lg border bg-card p-4">
      <h3 id="this-cycle-heading" className="mb-3 text-sm font-semibold">This cycle&apos;s contributions</h3>
      {contributions.length === 0 ? (
        <p className="text-sm text-muted-foreground">No contributions recorded yet.</p>
      ) : (
        <ul className="divide-y">
          {contributions.map((contribution) => {
            const own = contribution.userId === currentUserId
            const who = own ? 'your' : `${contribution.userName}'s`
            return (
              <li key={contribution.id} className="space-y-2 py-3 first:pt-0 last:pb-0">
                <p className="flex flex-wrap items-baseline justify-between gap-x-3 text-sm">
                  <span className="font-medium">{own ? `${contribution.userName} (you)` : contribution.userName}</span>
                  <span className="font-semibold tabular-nums">{formatCents(contribution.amount)}</span>
                </p>
                <p className="text-xs text-muted-foreground">Recorded {formatJohannesburgDateTime(contribution.recordedAtUtc)}</p>
                {canSeeProof(role, currentUserId, contribution.userId) && (
                  <div className="flex flex-wrap items-end gap-x-4 gap-y-2">
                    {cycleOpen && <ProofUpload contributionId={contribution.id} ownerLabel={who} />}
                    {/* Proofs are private: a plain link through /bff, never the shared next/image cache. */}
                    <a
                      href={`/bff/contributions/${encodeURIComponent(contribution.id)}/proof`}
                      className="inline-flex min-h-6 items-center text-sm underline underline-offset-4"
                    >
                      {own ? 'Open your proof of payment' : `Check ${who} proof of payment`}
                    </a>
                  </div>
                )}
              </li>
            )
          })}
        </ul>
      )}
    </section>
  )
}
