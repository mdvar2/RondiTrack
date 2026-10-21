import { useState } from 'react'
import {
  groupContributionsByCycle,
  type Contribution,
  type Cycle,
} from '../lib/groupContributions'
import { ContributionCard } from './ContributionCard'
import { StatusBadge } from './StatusBadge'
import { DueCountdown } from './DueCountdown'
import { formatCents } from '../lib/money'
import { formatCycleDueDate } from '../lib/dates'

type ContributionListProps = {
  cycles: readonly Cycle[]
  contributions: readonly Contribution[]
}

export function ContributionList({
  cycles,
  contributions,
}: ContributionListProps) {
  const groups = groupContributionsByCycle(
    cycles,
    contributions,
  )

  const [expanded, setExpanded] = useState<
    Record<string, boolean>
  >({})

  function isExpanded(id: string, status?: string) {
    return expanded[id] ?? status === 'Open'
  }

  function toggle(id: string, status?: string) {
    setExpanded((previous) => ({
      ...previous,
      [id]: !(previous[id] ?? status === 'Open'),
    }))
  }

  function setAll(value: boolean) {
    setExpanded(
      Object.fromEntries(
        groups.map((group) => [
          group.cycle?.id ?? 'unknown',
          value,
        ]),
      ),
    )
  }

  return (
    <section aria-label="Contributions by cycle">
      <div className="mb-4 flex flex-wrap gap-2">
        <button
          type="button"
          onClick={() => setAll(true)}
          className="min-h-10 rounded-md border border-border px-3 py-2 text-sm"
        >
          Expand all
        </button>

        <button
          type="button"
          onClick={() => setAll(false)}
          className="min-h-10 rounded-md border border-border px-3 py-2 text-sm"
        >
          Collapse all
        </button>
      </div>

      {groups.length === 0 ? (
        <p className="text-sm text-muted-foreground">
          No contribution cycles available.
        </p>
      ) : (
        <div className="space-y-4">
          {groups.map((group) => {
            const cycle = group.cycle
            const id = cycle?.id ?? 'unknown'
            const open = isExpanded(id, cycle?.status)
            const panelId = `cycle-${id}`

            return (
              <article
                key={id}
                className="rounded-lg border border-border bg-card p-4"
              >
                <button
                  type="button"
                  aria-expanded={open}
                  aria-controls={panelId}
                  onClick={() =>
                    toggle(id, cycle?.status)
                  }
                  className="flex min-h-11 w-full items-center justify-between gap-3 rounded-md text-left"
                >
                  <span className="min-w-0 font-semibold">
                    {cycle?.period ?? 'Unknown cycle'}
                  </span>

                  <span aria-hidden="true">
                    {open ? '−' : '+'}
                  </span>
                </button>

                {cycle && (
                  <div className="mt-2 space-y-2 text-sm">
                    <StatusBadge status={cycle.status} />

                    <p>
                      Due:{' '}
                      {cycle.dueDate
                        ? formatCycleDueDate(cycle.dueDate)
                        : 'Not set'}
                    </p>

                    <DueCountdown
                      dueDate={cycle.dueDate ?? null}
                      status={cycle.status}
                    />

                    <p>
                      Total collected:{' '}
                      <span className="whitespace-nowrap font-medium">
                        {formatCents(
                          cycle.totalCollectedCents,
                        )}
                      </span>
                    </p>

                    <p>
                      Amount due:{' '}
                      <span className="whitespace-nowrap font-medium">
                        {formatCents(cycle.amountDueCents)}
                      </span>
                    </p>
                  </div>
                )}

                <div
                  id={panelId}
                  hidden={!open}
                  className="mt-4"
                >
                  {group.contributions.length === 0 ? (
                    <p className="text-sm text-muted-foreground">
                      No contributions recorded.
                    </p>
                  ) : (
                    <div className="contribution-grid">
                      {group.contributions.map(
                        (contribution) => (
                          <ContributionCard
                            key={contribution.id}
                            contribution={contribution}
                            cycleName={
                              cycle?.period ??
                              'Unknown cycle'
                            }
                          />
                        ),
                      )}
                    </div>
                  )}
                </div>
              </article>
            )
          })}
        </div>
      )}
    </section>
  )
}