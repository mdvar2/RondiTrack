import { describe, expect, it } from 'vitest'
import { canRecordContributionForMember, getEligibleMembersForContribution } from './contribution-permissions'

const members = [
  { userId: 'member-1', userName: 'Asha', role: 'Member' },
  { userId: 'member-2', userName: 'Ben', role: 'Member' },
  { userId: 'member-3', userName: 'Cara', role: 'Chairperson' },
  { userId: 'member-4', userName: 'Dane', role: 'Treasurer' },
]

describe('contribution permissions', () => {
  it('allows ordinary members to record only their own contributions', () => {
    expect(canRecordContributionForMember('Member', 'member-1', 'member-1', members)).toBe(true)
    expect(canRecordContributionForMember('Member', 'member-1', 'member-2', members)).toBe(false)
    expect(getEligibleMembersForContribution('Member', 'member-1', members)).toEqual([
      { userId: 'member-1', userName: 'Asha', role: 'Member' },
    ])
  })

  it('allows chairpersons to record for members they manage in the stokvel', () => {
    expect(canRecordContributionForMember('Chairperson', 'member-3', 'member-1', members)).toBe(true)
    expect(canRecordContributionForMember('Chairperson', 'member-3', 'member-2', members)).toBe(true)
    expect(getEligibleMembersForContribution('Chairperson', 'member-3', members)).toHaveLength(4)
  })

  it('allows treasurers to record for members they manage in the stokvel', () => {
    expect(canRecordContributionForMember('Treasurer', 'member-4', 'member-1', members)).toBe(true)
    expect(canRecordContributionForMember('Treasurer', 'member-4', 'member-2', members)).toBe(true)
    expect(getEligibleMembersForContribution('Treasurer', 'member-4', members)).toHaveLength(4)
  })

  it('rejects unsupported roles without any eligible members', () => {
    expect(getEligibleMembersForContribution('Guest', 'member-1', members)).toEqual([])
    expect(canRecordContributionForMember('Guest', 'member-1', 'member-2', members)).toBe(false)
  })
})
