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

// Roles belong to a stokvel, not to a person: the same person is Treasurer
// of one stokvel and Member of another, so every check takes that stokvel's role.
export function canManageStokvel(role: ContributionRole): boolean {
  return role === 'Treasurer' || role === 'Chairperson'
}

// A Member sees the proof for their own contribution only; a Treasurer or
// Chairperson checks everyone's. The API enforces the same rule (404 otherwise).
export function canSeeProof(role: ContributionRole, currentUserId: string, contributionUserId: string): boolean {
  return contributionUserId === currentUserId || canManageStokvel(role)
}
