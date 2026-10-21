import type { ContributionCycleResponse } from '../api/types'

// Illustrative fixtures for Assignment 7.2.
// These are not captured database records.

export const sampleCycles = [
  {
    id: '6e9d6d8e-6c30-4d73-93f0-9f2c61017001',
    stokvelId: 'c9e2e0d5-0b8c-4b0c-b76b-4c4d9c187001',
    period: '2026-10',
    targetAmount: 1000,
    version: 1,
    endDate: '2026-10-31',
    status: 'Open',
    dueDate: '2026-10-31',
    totalCollectedCents: 50000,
    amountDueCents: 50000,
  },
  {
    id: '6e9d6d8e-6c30-4d73-93f0-9f2c61017002',
    stokvelId: 'c9e2e0d5-0b8c-4b0c-b76b-4c4d9c187002',
    period: '2026-10',
    targetAmount: 1000,
    version: 1,
    endDate: '2026-10-25',
    status: 'Open',
    dueDate: '2026-10-25',
    totalCollectedCents: 72510,
    amountDueCents: 27490,
  },
  {
    id: '6e9d6d8e-6c30-4d73-93f0-9f2c61017003',
    stokvelId: 'c9e2e0d5-0b8c-4b0c-b76b-4c4d9c187001',
    period: '2026-09',
    targetAmount: 1000,
    version: 1,
    endDate: '2026-09-30',
    status: 'Closed',
    dueDate: '2026-09-30',
    totalCollectedCents: 100000,
    amountDueCents: 0,
  },
  {
    id: '6e9d6d8e-6c30-4d73-93f0-9f2c61017004',
    stokvelId: 'c9e2e0d5-0b8c-4b0c-b76b-4c4d9c187001',
    period: '2026-08',
    targetAmount: 1000,
    version: 1,
    endDate: '2026-08-31',
    status: 'PaidOut',
    dueDate: '2026-08-31',
    totalCollectedCents: 100000,
    amountDueCents: 0,
  },
] satisfies ContributionCycleResponse[]