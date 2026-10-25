import { useRef } from 'react'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { api, unwrap } from '../api/client'
import { queryKeys } from '../api/queries'

export type ContributionInput = {
  userId: string
  cycleId: string
  amountInCents: number
}

export function useRecordContribution(stokvelId: string) {
  const qc = useQueryClient()

  // One key per intent to pay. It is made when the person submits, so a
  // StrictMode double render can never make two keys for one intent.
  const intent = useRef<{ what: string; key: string } | null>(null)

  const mutation = useMutation({
    mutationFn: async (input: ContributionInput & { key: string }) =>
      unwrap(await api.POST('/api/v1/stokvels/{stokvelId}/members/{userId}/contributions', {
        params: {
          path: { stokvelId, userId: input.userId },
          header: { 'Idempotency-Key': input.key },
        },
        body: { amountInCents: input.amountInCents, contributionCycleId: input.cycleId },
      })),
    // Returning the promise keeps the button disabled until the queries have refetched.
    onSuccess: () => Promise.all([
      qc.invalidateQueries({ queryKey: queryKeys.home }),
      qc.invalidateQueries({ queryKey: queryKeys.cycles(stokvelId) }),
      qc.invalidateQueries({ queryKey: queryKeys.contributions(stokvelId) }),
    ]),
  })

  async function record(input: ContributionInput) {
    // A different member, cycle or amount is a different payment, so it gets a new key.
    const what = `${input.userId}|${input.cycleId}|${input.amountInCents}`
    if (intent.current?.what !== what) intent.current = { what, key: crypto.randomUUID() }

    // After a failure the same key is used again, because the payment may have landed.
    const recorded = await mutation.mutateAsync({ ...input, key: intent.current.key })

    intent.current = null
    return recorded
  }

  return { record, isPending: mutation.isPending }
}
