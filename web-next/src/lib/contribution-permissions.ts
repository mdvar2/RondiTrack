export type ContributionRole = 'Member' | 'Chairperson' | 'Treasurer' | string

export type ContributionMember = {
  userId: string
  userName: string
  role: string
}

export function getEligibleMembersForContribution(
  role: ContributionRole,
  currentUserId: string,
  members: readonly ContributionMember[],
): ContributionMember[] {
  if (role === 'Treasurer' || role === 'Chairperson') {
    return [...members]
  }

  if (role === 'Member') {
    const self = members.find((member) => member.userId === currentUserId)
    return self ? [self] : []
  }

  return []
}

export function canRecordContributionForMember(
  role: ContributionRole,
  currentUserId: string,
  targetUserId: string,
  members: readonly ContributionMember[],
): boolean {
  return getEligibleMembersForContribution(role, currentUserId, members)
    .some((member) => member.userId === targetUserId)
}
