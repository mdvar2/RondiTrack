// What the contribution form shows for each answer. Pure functions, so the
// mapping from the API's field names to the form's is tested on its own and
// the component test only has to fake the form's one edge, its Server Action.
import { formatCents, MAX_CONTRIBUTION_CENTS, parseRandsToCents } from './money'

export type ContributionInput = {
  stokvelId: string
  cycleId: string
  userId: string
  amountCents: number
  idempotencyKey: string
}

export type ContributionProblem = {
  title?: string
  detail?: string
  correlationId?: string
  errors?: Record<string, string[]>
}

// What the Server Action resolves to. A dropped connection between browser
// and server is not a result: the action's promise rejects instead.
export type ContributionResult =
  | { ok: true; amountCents: number }
  | { ok: false; status: number; problem?: ContributionProblem; retryAfterSeconds?: number }

export type FormField = 'member' | 'amount'

// One owner per error (Assignment 9.3 Part 3): problems with what was typed or
// with the stokvel's current state (400, 403, 404, 409, 412, 422, 429) are shown
// inline, next to the input that is kept; a failure on our side (5xx) or no
// answer at all is a persistent toast. Never both.
export type ContributionFeedback = {
  fieldErrors: Partial<Record<FormField, string>>
  formError?: { message: string; reference?: string }
  toast?: { message: string; reference?: string }
  success?: string
}

// The API names fields after its JSON body; the form names them after what
// a person sees. "amountInCents" is the one that differs most.
const API_FIELD_TO_FORM: Record<string, FormField> = {
  amountInCents: 'amount',
  userId: 'member',
}

export const AMOUNT_HINT = 'In rands, for example 150,50'

export function amountError(text: string): string | null {
  if (text.trim() === '') return 'Enter the amount that was paid.'
  const cents = parseRandsToCents(text, Number.MAX_SAFE_INTEGER)
  if (cents !== null && cents > MAX_CONTRIBUTION_CENTS) {
    return `A contribution can be at most ${formatCents(MAX_CONTRIBUTION_CENTS)}.`
  }
  if (cents === null) return 'Enter an amount in rands with at most two decimal places, like 150,50.'
  return null
}

export function feedbackFor(result: ContributionResult, memberName: string): ContributionFeedback {
  if (result.ok) {
    return { fieldErrors: {}, success: `Recorded ${formatCents(result.amountCents)} for ${memberName}.` }
  }

  const reference = result.problem?.correlationId
  const detail = result.problem?.detail

  if (result.status === 400) {
    const fieldErrors: ContributionFeedback['fieldErrors'] = {}
    const unplaced: string[] = []
    for (const [apiField, messages] of Object.entries(result.problem?.errors ?? {})) {
      const field = API_FIELD_TO_FORM[apiField]
      const message = messages.join(' ')
      if (field && !fieldErrors[field]) fieldErrors[field] = message
      else unplaced.push(message)
    }
    const nothingPlaced = Object.keys(fieldErrors).length === 0
    return {
      fieldErrors,
      formError: unplaced.length > 0 || nothingPlaced
        ? { message: unplaced.join(' ') || detail || 'Check the details and try again.', reference }
        : undefined,
    }
  }

  if (result.status >= 500) {
    return { fieldErrors: {}, toast: { message: SERVER_FAILURE_MESSAGE, reference } }
  }

  // 409: the stokvel's state refuses it (already paid, cycle closed): look again.
  // 412: what this page showed is out of date: reload it.
  // 422: the request itself breaks a rule: change it.
  const message =
    result.status === 401 ? 'Your session has ended. Sign in again to record this contribution.'
      : result.status === 403 ? "You don't have permission to record a contribution for this member."
        : result.status === 404 ? 'This stokvel or its cycle no longer exists.'
          : result.status === 409 ? `${detail || 'This contribution was refused.'} Reload the page to see the latest, then decide again.`
            : result.status === 412 ? 'This page is out of date. Reload it, then try again.'
              : result.status === 422 ? `${detail || 'This contribution breaks a rule.'} Change the details and try again.`
                : result.status === 429 ? `Too many contributions at once. Try again in ${result.retryAfterSeconds ?? 60} seconds.`
                  : detail || 'This contribution was refused.'
  return { fieldErrors: {}, formError: { message, reference } }
}

export const SERVER_FAILURE_MESSAGE = 'Something went wrong on our side. Trying again is safe: it will not be recorded twice.'

// The browser got no answer, so it has no trace id to show: the toast says
// "Reference unavailable" rather than inventing one.
export const NETWORK_FEEDBACK: ContributionFeedback = {
  fieldErrors: {},
  toast: { message: "We couldn't reach RondiTrack. Check your connection and try again: it will not be recorded twice." },
}
