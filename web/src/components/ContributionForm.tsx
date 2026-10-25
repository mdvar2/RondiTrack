import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import { useMembers } from '../api/queries'
import type { ContributionCycleResponse, HomeStokvel } from '../api/types'
import { ApiError, currentUserId } from '../auth/session'
import { useRecordContribution } from '../contributions/useRecordContribution'
import { centsToInput, formatCents, parseRandsToCents } from '../lib/money'
import { ErrorView } from './ErrorView'

const amountMessage = 'Enter an amount in rands, like 150 or 1 500,50.'

const schema = z.object({
  memberId: z.string().min(1, 'Choose a member.'),
  amount: z.string().refine((text) => (parseRandsToCents(text) ?? 0) > 0, amountMessage),
})

type FormValues = z.infer<typeof schema>

type Props = {
  stokvel: HomeStokvel
  cycles: readonly ContributionCycleResponse[]
}

export function ContributionForm({ stokvel, cycles }: Props) {
  const me = currentUserId() ?? ''
  const canPickMember = stokvel.role === 'Treasurer' || stokvel.role === 'Chairperson'
  const owed = stokvel.amountOwed > 0 ? centsToInput(stokvel.amountOwed) : ''

  const members = useMembers(stokvel.stokvelId, canPickMember)
  const { record } = useRecordContribution(stokvel.stokvelId)
  const form = useForm<FormValues>({
    resolver: zodResolver(schema),
    defaultValues: { memberId: me, amount: owed },
  })

  const [error, setError] = useState<unknown>(null)
  const [otherMessages, setOtherMessages] = useState<string[]>([])
  const [saved, setSaved] = useState('')

  const cycle = cycles.find((c) => c.period === stokvel.currentCycle?.period)

  if (!stokvel.currentCycle || !cycle) {
    return <p>There is no contribution cycle for this month yet, so nothing can be recorded.</p>
  }
  if (cycle.status !== 'Open') {
    return <p>The {cycle.period} cycle is {cycle.status}, so contributions can no longer be recorded.</p>
  }

  const { errors, isSubmitting } = form.formState
  // No answer or a server error: the payment may have gone through.
  const unknownOutcome = error !== null && (!(error instanceof ApiError) || error.status >= 500)

  async function submit(values: FormValues) {
    const amountInCents = parseRandsToCents(values.amount)
    if (amountInCents === null || !cycle) return

    setError(null)
    setOtherMessages([])
    setSaved('')

    try {
      await record({ userId: values.memberId, cycleId: cycle.id, amountInCents })
      const who = values.memberId === me
        ? 'you'
        : members.data?.find((m) => m.userId === values.memberId)?.userName ?? 'the member'
      setSaved(`Recorded ${formatCents(amountInCents)} for ${who} in the ${cycle.period} cycle.`)
      form.reset({ memberId: me, amount: '' }, { keepFieldsRef: true })
    } catch (e) {
      setError(e)
      if (e instanceof ApiError && e.status === 400) {
        const other: string[] = []
        for (const [key, messages] of Object.entries(e.problem?.errors ?? {})) {
          if (key === 'amountInCents') form.setError('amount', { message: messages.join(' ') })
          else other.push(...messages)
        }
        setOtherMessages(other)
      }
    }
  }

  return (
    <form onSubmit={form.handleSubmit(submit)} className="space-y-4 rounded-lg border p-4" noValidate>
      <h2>Record a contribution</h2>

      {error !== null && (
        <div className="space-y-2">
          <ErrorView error={error} />
          {otherMessages.map((message) => <p key={message} role="alert">{message}</p>)}
          {unknownOutcome && (
            <p>This payment may already have gone through. Trying again is safe: it will not be recorded twice.</p>
          )}
        </div>
      )}

      {/* Wait for the members, so the dropdown starts on the signed-in user, not the first option. */}
      {canPickMember && members.data && (
        <label className="block space-y-2">
          <span className="block font-medium">Member</span>
          <select
            className="w-full rounded border bg-background p-3"
            aria-invalid={!!errors.memberId}
            {...form.register('memberId', {
              onChange: (event) => form.setValue('amount', event.target.value === me ? owed : ''),
            })}
          >
            {members.data.map((m) => <option key={m.userId} value={m.userId}>{m.userName}</option>)}
          </select>
          {errors.memberId && <span role="alert">{errors.memberId.message}</span>}
        </label>
      )}

      <label className="block space-y-2">
        <span className="block font-medium">Amount (rands)</span>
        <input
          className="w-full rounded border bg-background p-3"
          inputMode="decimal"
          placeholder="e.g. 1 500,50"
          aria-invalid={!!errors.amount}
          {...form.register('amount')}
        />
        {errors.amount && <span role="alert">{errors.amount.message}</span>}
      </label>

      <button type="submit" className="rounded border px-3 py-2" disabled={isSubmitting}>
        {isSubmitting ? 'Recording…' : unknownOutcome ? 'Try again' : 'Record contribution'}
      </button>

      {saved && <p role="status">{saved}</p>}
    </form>
  )
}
