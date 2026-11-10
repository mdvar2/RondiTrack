'use server'

import { revalidatePath } from 'next/cache'
import { headers } from 'next/headers'
import { redirect } from 'next/navigation'
import { expiredLoginUrl, RETURN_PATH_HEADER } from './return-path'
import { MAX_CONTRIBUTION_CENTS } from './money'
import { parseRetryAfter } from './errors'
import { refreshedApi } from './proxy'
import { shouldDropCommittedResponse, SimulatedPostCommitResponseFailure } from './demo-fault-action'
import type { ContributionInput, ContributionProblem, ContributionResult } from './contribution-feedback'

const UUID = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i

// The contribution form's only edge. The Idempotency-Key comes from the form,
// which keeps it across a retry; this action never makes one, because a
// fresh key per call would turn a retry into a second contribution.
// It checks everything again: a Server Action can be called by anyone.
export async function recordContribution(input: ContributionInput): Promise<ContributionResult> {
  const { stokvelId, cycleId, userId, amountCents, idempotencyKey } = input ?? {}
  if (![stokvelId, cycleId, userId].every((id) => typeof id === 'string' && UUID.test(id))) {
    return { ok: false, status: 400, problem: { detail: 'Missing stokvel details. Reload the page and try again.' } }
  }
  if (typeof idempotencyKey !== 'string' || idempotencyKey.length < 16 || idempotencyKey.length > 256) {
    return { ok: false, status: 400, problem: { detail: 'Reload the page and try again.' } }
  }
  if (!Number.isSafeInteger(amountCents) || amountCents <= 0 || amountCents > MAX_CONTRIBUTION_CENTS) {
    return { ok: false, status: 400, problem: { errors: { amountInCents: ['Enter an amount between R 0,01 and R 1 000 000,00.'] } } }
  }

  const client = await refreshedApi()
  const result = await client.POST('/api/v1/stokvels/{stokvelId}/members/{userId}/contributions', {
    params: { path: { stokvelId, userId }, header: { 'Idempotency-Key': idempotencyKey } },
    body: { amountInCents: amountCents, contributionCycleId: cycleId },
  }).catch(() => null)

  if (!result) return { ok: false, status: 503 }
  if (result.response.status === 401) {
    // The session ended: sign in again, then back to this page (set by src/proxy.ts).
    redirect(expiredLoginUrl((await headers()).get(RETURN_PATH_HEADER)))
  }
  if (!result.response.ok) {
    return {
      ok: false,
      status: result.response.status,
      problem: result.error as ContributionProblem | undefined,
      retryAfterSeconds: parseRetryAfter(result.response.headers.get('Retry-After')) ?? undefined,
    }
  }

  // Demo/test environments only: the API has committed; simulate losing its answer.
  // Before revalidatePath, so the browser gets nothing back, as with a real loss;
  // the retry revalidates.
  if (await shouldDropCommittedResponse(idempotencyKey)) throw new SimulatedPostCommitResponseFailure()
  revalidatePath(`/stokvels/${stokvelId}`)
  return { ok: true, amountCents }
}
