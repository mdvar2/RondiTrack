import { render, screen } from '@testing-library/react'
import { describe, expect, it, vi } from 'vitest'
import { StokvelSummary } from './StokvelSummary'
import { ContributionList } from './ContributionList'
import { ToastProvider } from './ToastProvider'
import type { ContributionWithMember, StokvelDetail, StokvelMember } from '@/lib/types'

// Roles belong to a stokvel, not to a person. Sipho is Treasurer of the
// family stokvel and an ordinary Member of the work one. The fixtures use the
// types generated from the API's OpenAPI document, so renaming an API field
// stops this file compiling.
const SIPHO = '11111111-1111-4111-8111-111111111111'
const THANDI = '22222222-2222-4222-8222-222222222222'
const CYCLE = '44444444-4444-4444-8444-444444444444'

function stokvel(id: string, name: string, role: string): StokvelDetail {
  return {
    id, name, role, contributionAmount: 500, contributionAmountCents: 50000, memberCount: 2, version: 1,
    currentCycle: { period: '2026-11', status: 'Open', dueDate: '2026-11-30', membersPaid: 1, collectedCents: 50000, targetCents: 100000 },
  }
}

function member(stokvelId: string, userId: string, userName: string, role: string): StokvelMember {
  return { stokvelId, userId, userName, role, userEmail: `${userName.split(' ')[0].toLowerCase()}@example.test`, joinedAtUtc: '2026-01-01T08:00:00Z', version: 1 }
}

function contribution(stokvelId: string, userId: string, userName: string): ContributionWithMember {
  return {
    id: `${userId.slice(0, 8)}-0000-4000-8000-000000000000`, stokvelId, userId, userName, userEmail: '', memberRole: 'Member',
    contributionCycleId: CYCLE, amount: 50000, status: 'Paid', recordedAtUtc: '2026-11-02T08:00:00Z', version: 1,
  }
}

const family = { stokvel: stokvel('aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa', 'Nkosi Family Stokvel', 'Treasurer') }
const work = { stokvel: stokvel('bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb', 'Office Savings Club', 'Member') }

function renderStokvelFor(view: { stokvel: StokvelDetail }) {
  const id = view.stokvel.id
  const members = [member(id, SIPHO, 'Sipho Nkosi', view.stokvel.role), member(id, THANDI, 'Thandi Dlamini', 'Member')]
  // What the page passes for a Member: only themselves (see stokvels/[id]/page.tsx).
  const visibleMembers = view.stokvel.role === 'Member' ? members.filter((candidate) => candidate.userId === SIPHO) : members
  render(
    <ToastProvider>
      <StokvelSummary stokvel={view.stokvel} cycleId={CYCLE} currentUserId={SIPHO} members={visibleMembers} recordContribution={vi.fn()}>
        <ContributionList
          contributions={[contribution(id, THANDI, 'Thandi Dlamini'), contribution(id, SIPHO, 'Sipho Nkosi')]}
          role={view.stokvel.role}
          currentUserId={SIPHO}
          cycleOpen
        />
      </StokvelSummary>
    </ToastProvider>,
  )
}

describe('the stokvel view for Sipho', () => {
  it('in the family stokvel, where he is Treasurer, shows the treasurer actions', () => {
    renderStokvelFor(family)
    expect(screen.getByRole('heading', { level: 1, name: 'Nkosi Family Stokvel' })).toBeInTheDocument()
    // Record for another member.
    expect(screen.getByRole('option', { name: 'Thandi Dlamini' })).toBeInTheDocument()
    // Close the cycle.
    expect(screen.getByRole('button', { name: 'Close cycle' })).toBeInTheDocument()
    // Check another member's proof of payment.
    expect(screen.getByRole('link', { name: "Check Thandi Dlamini's proof of payment" })).toHaveAttribute('href', expect.stringMatching(/^\/bff\/contributions\/.+\/proof$/))
    expect(screen.getByRole('button', { name: "Upload Thandi Dlamini's proof" })).toBeInTheDocument()
  })

  it('in the work stokvel, where he is a Member, shows none of them, and nothing hints that they exist', () => {
    renderStokvelFor(work)
    expect(screen.getByRole('heading', { level: 1, name: 'Office Savings Club' })).toBeInTheDocument()
    expect(screen.queryByRole('combobox', { name: 'Member' })).not.toBeInTheDocument()
    expect(screen.queryByRole('option', { name: 'Thandi Dlamini' })).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /close/i })).not.toBeInTheDocument()
    expect(screen.queryByRole('link', { name: /Thandi Dlamini's proof/ })).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /Thandi Dlamini's proof/ })).not.toBeInTheDocument()
    expect(screen.queryByText(/treasurer|close the cycle|record for/i)).not.toBeInTheDocument()
  })

  it('in the work stokvel he still records and proves his own contribution', () => {
    renderStokvelFor(work)
    expect(screen.getByRole('button', { name: 'Record contribution' })).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Open your proof of payment' })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Upload your proof' })).toBeInTheDocument()
  })

  it('shows amounts from the API as the cents they are', () => {
    renderStokvelFor(family)
    // 50 000 cents is R 500,00. The old page multiplied by 100 again.
    expect(screen.getAllByText('R 500,00').length).toBeGreaterThan(0)
    expect(screen.queryByText(/50 000,00/)).not.toBeInTheDocument()
  })
})
