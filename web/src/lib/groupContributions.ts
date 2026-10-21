import type {
  ContributionCycleResponse,
  ContributionResponse,
  ContributionWithMemberResponse,
} from '../api/types'

export type Contribution =
  | ContributionResponse
  | ContributionWithMemberResponse

export type Cycle = ContributionCycleResponse

export type ContributionGroup = {
  cycle: Cycle | null
  contributions: Contribution[]
}

export function groupContributionsByCycle(
  cycles: readonly Cycle[],
  contributions: readonly Contribution[],
): ContributionGroup[] {
  const sortedCycles = [...cycles].sort(
    (a, b) =>
      b.period.localeCompare(a.period) ||
      b.id.localeCompare(a.id),
  )

  const groups: ContributionGroup[] = sortedCycles.map(
    (cycle) => ({
      cycle,
      contributions: [],
    }),
  )

  const cycleGroups = new Map(
    groups.map((group) => [group.cycle!.id, group]),
  )

  const unknown: Contribution[] = []

  for (const contribution of contributions) {
    const group = cycleGroups.get(
      contribution.contributionCycleId,
    )

    if (group) {
      group.contributions.push(contribution)
    } else {
      unknown.push(contribution)
    }
  }

  function sortNewestFirst(items: Contribution[]) {
    items.sort(
      (a, b) =>
        Date.parse(b.recordedAtUtc) -
          Date.parse(a.recordedAtUtc) ||
        b.id.localeCompare(a.id),
    )
  }

  for (const group of groups) {
    sortNewestFirst(group.contributions)
  }

  if (unknown.length > 0) {
    sortNewestFirst(unknown)

    groups.push({
      cycle: null,
      contributions: unknown,
    })
  }

  return groups
}