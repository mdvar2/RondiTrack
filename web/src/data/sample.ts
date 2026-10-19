import type {
  HomeStokvel,
  ContributionWithMemberResponse,
} from '../api/types'

export const sampleStokvels = [
  {
    stokvelName: 'Ubuntu Savings Club',
    role: 'Treasurer',
    currentCycle: {
      period: '2026-10',
      status: 'Open',
      dueDate: '2026-10-31',
    },
    amountPaid: 12510,
    amountOwed: 37490,
    nextPayoutDate: '2026-11-15T08:00:00Z',
  },
  {
    stokvelName: 'Masakhane Community Stokvel',
    role: 'Member',
    currentCycle: {
      period: '2026-10',
      status: 'Open',
      dueDate: '2026-10-25',
    },
    amountPaid: 72500,
    amountOwed: 0,
    nextPayoutDate: null,
  },
  {
    stokvelName: 'Future Builders Stokvel',
    role: 'Member',
    currentCycle: null,
    amountPaid: 0,
    amountOwed: 0,
    nextPayoutDate: null,
  },
] satisfies HomeStokvel[]


export const sampleContributions = [
  {
    id: 'd9c745b4-b631-49cf-ae5d-b9814e719001',
    stokvelId: 'c9e2e0d5-0b8c-4b0c-b76b-4c4d9c187001',
    userId: 'e3c7c15b-7936-47bc-9355-39ac84277001',
    userName: 'Lerato Mokoena',
    userEmail: 'lerato@example.com',
    memberRole: 'Member',
    contributionCycleId: '6e9d6d8e-6c30-4d73-93f0-9f2c61017001',
    amount: 12510,
    recordedAtUtc: '2026-10-31T22:30:00Z',
    version: 1,
  },
  {
    id: 'd9c745b4-b631-49cf-ae5d-b9814e719002',
    stokvelId: 'c9e2e0d5-0b8c-4b0c-b76b-4c4d9c187001',
    userId: 'e3c7c15b-7936-47bc-9355-39ac84277002',
    userName: 'Thabo Dlamini',
    userEmail: 'thabo@example.com',
    memberRole: 'Treasurer',
    contributionCycleId: '6e9d6d8e-6c30-4d73-93f0-9f2c61017001',
    amount: 50000,
    recordedAtUtc: '2026-10-19T08:15:00Z',
    version: 1,
  },
  {
    id: 'd9c745b4-b631-49cf-ae5d-b9814e719003',
    stokvelId: 'c9e2e0d5-0b8c-4b0c-b76b-4c4d9c187002',
    userId: 'e3c7c15b-7936-47bc-9355-39ac84277003',
    userName: 'Nomsa Khumalo',
    userEmail: 'nomsa@example.com',
    memberRole: 'Member',
    contributionCycleId: '6e9d6d8e-6c30-4d73-93f0-9f2c61017002',
    amount: 72500,
    recordedAtUtc: '2026-10-18T14:45:00Z',
    version: 1,
  },
] satisfies ContributionWithMemberResponse[]
