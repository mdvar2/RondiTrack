'use client'

import { useId, useRef, useState } from 'react'
import { parseRandsToCents } from '@/lib/money'
import {
  AMOUNT_HINT,
  amountError,
  feedbackFor,
  NETWORK_FEEDBACK,
  type ContributionFeedback,
  type ContributionInput,
  type ContributionResult,
} from '@/lib/contribution-feedback'
import type { ContributionMember } from '@/lib/contribution-permissions'
import { Button } from './ui/button'

type ContributionFormProps = {
  stokvelId: string
  cycleId: string
  currentUserId: string
  // Everyone this person may record for: only themselves for a Member,
  // anyone in the stokvel for a Treasurer or Chairperson.
  members: readonly ContributionMember[]
  // The form's one edge. The page passes the real Server Action; tests pass a fake.
  action: (input: ContributionInput) => Promise<ContributionResult>
  newIdempotencyKey?: () => string
}

const EMPTY: ContributionFeedback = { fieldErrors: {} }

export function ContributionForm({
  stokvelId,
  cycleId,
  currentUserId,
  members,
  action,
  newIdempotencyKey = () => crypto.randomUUID(),
}: ContributionFormProps) {
  const id = useId()
  const [memberId, setMemberId] = useState(members.find((member) => member.userId === currentUserId)?.userId ?? members[0]?.userId ?? '')
  const [amountText, setAmountText] = useState('')
  const [feedback, setFeedback] = useState<ContributionFeedback>(EMPTY)
  const [pending, setPending] = useState(false)
  // One key per contribution: made on the first submit, sent again on every
  // retry of that contribution, and replaced only after the API accepts it.
  const keyRef = useRef<string | null>(null)
  // Set synchronously, so a second click before React re-renders sends nothing.
  const inFlightRef = useRef(false)
  const amountRef = useRef<HTMLInputElement>(null)
  const memberRef = useRef<HTMLSelectElement>(null)

  const forOthers = members.length > 1 || members.some((member) => member.userId !== currentUserId)
  const member = members.find((candidate) => candidate.userId === memberId)
  const amountProblem = feedback.fieldErrors.amount
  const memberProblem = feedback.fieldErrors.member

  async function submit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (inFlightRef.current) return

    const localError = amountError(amountText)
    const amountCents = parseRandsToCents(amountText)
    if (localError || amountCents === null || !member) {
      setFeedback({ fieldErrors: { amount: localError ?? undefined, member: member ? undefined : 'Choose a member.' } })
      ;(localError ? amountRef : memberRef).current?.focus()
      return
    }

    inFlightRef.current = true
    keyRef.current ??= newIdempotencyKey()
    const input: ContributionInput = { stokvelId, cycleId, userId: member.userId, amountCents, idempotencyKey: keyRef.current }
    setFeedback(EMPTY)
    setPending(true)
    let next: ContributionFeedback
    let succeeded = false
    try {
      const result = await action(input)
      next = feedbackFor(result, member.userId === currentUserId ? 'you' : member.userName)
      succeeded = result.ok
      if (succeeded) keyRef.current = null
    } catch {
      // The connection dropped: the key is kept, so trying again is safe.
      next = NETWORK_FEEDBACK
    } finally {
      inFlightRef.current = false
    }
    // Set together after the same await, so React renders them in one
    // commit: the message and the restored button appear at once.
    setFeedback(next)
    setPending(false)
    if (succeeded) setAmountText('')
    if (next.fieldErrors.amount) amountRef.current?.focus()
    else if (next.fieldErrors.member) memberRef.current?.focus()
  }

  return (
    <form onSubmit={submit} noValidate aria-labelledby={`${id}-heading`} className="space-y-4 rounded-lg border bg-card p-4">
      <h3 id={`${id}-heading`} className="text-sm font-semibold">Record a contribution</h3>

      {/* Always in the page, so screen readers announce what appears in it. */}
      <div role="alert">
        {feedback.formError && (
          <div className="space-y-1 rounded-lg border border-destructive p-3 text-sm">
            <p>{feedback.formError.message}</p>
            {feedback.formError.reference && <p className="text-muted-foreground">Reference: <code>{feedback.formError.reference}</code></p>}
          </div>
        )}
      </div>

      {forOthers ? (
        <div className="space-y-1">
          <label htmlFor={`${id}-member`} className="block text-sm font-medium">Member</label>
          <select
            ref={memberRef}
            id={`${id}-member`}
            value={memberId}
            onChange={(event) => setMemberId(event.target.value)}
            aria-invalid={memberProblem ? true : undefined}
            aria-describedby={memberProblem ? `${id}-member-error` : undefined}
            className="h-10 w-full rounded-md border border-input bg-background px-3 text-sm"
          >
            {members.map((option) => (
              <option key={option.userId} value={option.userId}>
                {option.userId === currentUserId ? `${option.userName} (you)` : option.userName}
              </option>
            ))}
          </select>
          {memberProblem && <p id={`${id}-member-error`} className="text-sm text-destructive">{memberProblem}</p>}
        </div>
      ) : (
        <p className="text-sm text-muted-foreground">You&apos;re recording your own contribution.</p>
      )}

      <div className="space-y-1">
        <label htmlFor={`${id}-amount`} className="block text-sm font-medium">Amount</label>
        <p id={`${id}-amount-hint`} className="text-xs text-muted-foreground">{AMOUNT_HINT}</p>
        <div className="flex items-center rounded-md border border-input bg-background focus-within:outline-2 focus-within:outline-ring has-[[aria-invalid=true]]:border-destructive">
          <span aria-hidden="true" className="pl-3 text-sm text-muted-foreground">R</span>
          <input
            ref={amountRef}
            id={`${id}-amount`}
            type="text"
            inputMode="decimal"
            autoComplete="off"
            value={amountText}
            onChange={(event) => setAmountText(event.target.value)}
            aria-invalid={amountProblem ? true : undefined}
            aria-describedby={amountProblem ? `${id}-amount-hint ${id}-amount-error` : `${id}-amount-hint`}
            className="h-10 w-full rounded-md bg-transparent px-2 text-sm tabular-nums outline-none"
          />
        </div>
        {amountProblem && <p id={`${id}-amount-error`} className="text-sm text-destructive">{amountProblem}</p>}
      </div>

      {/* aria-disabled, not disabled: a disabled button drops keyboard focus to <body>. */}
      <Button type="submit" aria-disabled={pending} className="h-10 w-full aria-disabled:opacity-60">
        {pending ? 'Recording…' : 'Record contribution'}
      </Button>

      <p role="status" className="text-sm text-emerald-700 dark:text-emerald-300">{feedback.success}</p>
    </form>
  )
}
