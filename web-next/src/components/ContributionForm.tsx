'use client'

import { useActionState, useId, useState } from 'react'
import { recordContribution, type ContributionState } from '@/lib/auth-actions'
import { getEligibleMembersForContribution } from '@/lib/contribution-permissions'
import { Button } from './ui/button'

type ContributionFormProps = {
  stokvelId: string
  cycleId: string | null
  role: string
  currentUserId: string
  members: Array<{ userId: string; userName: string; role: string }>
}

export function ContributionForm({ stokvelId, cycleId, role, currentUserId, members }: ContributionFormProps) {
  const [state, formAction, pending] = useActionState<ContributionState, FormData>(recordContribution, null)
  const eligibleMembers = getEligibleMembersForContribution(role, currentUserId, members)
  const defaultSelectedUserId = eligibleMembers[0]?.userId ?? ''
  const [selectedUserId, setSelectedUserId] = useState(defaultSelectedUserId)
  const [localAmount, setLocalAmount] = useState('')
  const idempotencyKey = useId().replace(/:/g, '')
  const amountValue = state?.amount ?? localAmount
  const currentSelectedUserId = eligibleMembers.some((member) => member.userId === selectedUserId)
    ? selectedUserId
    : defaultSelectedUserId

  if (!['Member', 'Chairperson', 'Treasurer'].includes(role)) {
    return (
      <div className="rounded-lg border bg-card p-4 text-sm text-muted-foreground">
        You do not have permission to record contributions for this stokvel.
      </div>
    )
  }

  if (!cycleId) {
    return (
      <div className="rounded-lg border bg-card p-4 text-sm text-muted-foreground">
        There is no current contribution cycle to record against yet.
      </div>
    )
  }

  if (eligibleMembers.length === 0) {
    return (
      <div className="rounded-lg border bg-card p-4 text-sm text-muted-foreground">
        You can only record contributions for yourself as a member of this stokvel.
      </div>
    )
  }

  return (
    <form action={formAction} className="space-y-4 rounded-lg border bg-card p-4">
      <input type="hidden" name="stokvelId" value={stokvelId} />
      <input type="hidden" name="cycleId" value={cycleId} />
      <input type="hidden" name="idempotencyKey" value={idempotencyKey} />

      <div className="space-y-2">
        <label htmlFor="member" className="text-sm font-medium">Member</label>
        <select
          id="member"
          name="userId"
          value={currentSelectedUserId}
          onChange={(event) => setSelectedUserId(event.target.value)}
          className="w-full rounded-md border bg-background px-3 py-2 text-sm"
        >
          {eligibleMembers.map((member) => (
            <option key={member.userId} value={member.userId}>{member.userName} ({member.role})</option>
          ))}
        </select>
      </div>

      <div className="space-y-2">
        <label htmlFor="amount" className="text-sm font-medium">Contribution amount (cents)</label>
        <input
          id="amount"
          name="amount"
          type="number"
          min="1"
          step="100"
          value={amountValue}
          onChange={(event) => setLocalAmount(event.target.value)}
          placeholder="2500"
          className="w-full rounded-md border bg-background px-3 py-2 text-sm"
        />
      </div>

      {state && !state.success && (
        <p role="alert" className="text-sm text-red-600">{state.message}</p>
      )}
      {state?.success && (
        <p className="text-sm text-emerald-600">{state.message}</p>
      )}

      <Button type="submit" disabled={pending} aria-disabled={pending} className="w-full">
        {pending ? 'Saving…' : 'Record contribution'}
      </Button>
    </form>
  )
}
