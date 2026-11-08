import type { Metadata } from 'next'
import Link from 'next/link'
import { joinStokvel } from './actions'
import { inviteStatus, previewInvite } from '@/lib/join'
import { inviteMetadata } from '@/lib/invite-metadata'
import { stepFromQuery } from '@/lib/join-state'
import { formatCents } from '@/lib/money'
import { getSession } from '@/lib/session'
import { Button } from '@/components/ui/button'
import { FocusOnMount } from '@/components/FocusOnMount'
import { JoinCodeForm } from '@/components/JoinCodeForm'

export const instant = false

const steps = ['Code', 'Rules', 'Confirm'] as const
const stepTitles = { code: 'Step 1 of 3: Enter your invite code', rules: 'Step 2 of 3: Review the rules', confirm: 'Step 3 of 3: Confirm your join' } as const

// The only page with generateMetadata. A link preview bot is signed out, so
// what it sees comes from inviteMetadata(status) alone: "an invite to a
// stokvel on RondiTrack" and whether it's still valid. Never the stokvel's
// name or amount, which the signed-in preview below does show to a person.
export async function generateMetadata({ searchParams }: PageProps<'/join'>): Promise<Metadata> {
  const { code, step } = await searchParams
  const trimmed = typeof code === 'string' ? code.trim() : ''
  if (!trimmed) return inviteMetadata(null)
  const session = await getSession()
  const metadata = inviteMetadata(await inviteStatus(trimmed))
  // Signed in, the title follows the step, so moving on is announced.
  // Still no name in it: titles stay in browser history.
  return session ? { ...metadata, title: `${stepTitles[stepFromQuery(typeof step === 'string' ? step : undefined)]} · Join a stokvel` } : metadata
}

export default async function JoinPage({ searchParams }: PageProps<'/join'>) {
  const query = await searchParams
  const code = typeof query.code === 'string' ? query.code.trim() : ''
  const requested = stepFromQuery(typeof query.step === 'string' ? query.step : undefined)
  const session = await getSession()

  // Signed out with a code: a page, not a redirect. The person (or the
  // preview bot) learns it's an invite and whether it's valid, then signs in
  // and comes straight back here with the code, never retyping it.
  if (!session && code) {
    const status = await inviteStatus(code)
    const back = `/join?code=${encodeURIComponent(code)}&step=rules`
    return (
      <main className="mx-auto max-w-xl px-4 py-12">
        <p className="mb-2 text-sm font-semibold tracking-wide text-muted-foreground">RondiTrack</p>
        <h1 className="mb-4 text-2xl font-semibold">
          {status === 'valid' ? 'You’re invited to join a stokvel' : status === 'expired' ? 'This invite has expired' : status === 'unknown' ? 'We couldn’t find this invite' : 'Join a stokvel'}
        </h1>
        <p className="mb-6 text-sm text-muted-foreground">
          {status === 'valid' ? 'Sign in or create an account to see the stokvel’s details and join it.'
            : status === 'expired' ? 'Ask the person who sent it for a new invite.'
              : 'Check the link, or sign in and enter the code yourself.'}
        </p>
        {status !== 'expired' && (
          <div className="flex flex-wrap gap-3">
            <Link href={`/login?next=${encodeURIComponent(back)}`} className="inline-flex h-10 items-center rounded-lg bg-primary px-4 text-sm font-medium text-primary-foreground">Sign in</Link>
            <Link href={`/register?next=${encodeURIComponent(back)}`} className="inline-flex h-10 items-center rounded-lg border px-4 text-sm font-medium">Create an account</Link>
          </div>
        )}
      </main>
    )
  }

  if (!session) {
    return (
      <main className="mx-auto max-w-xl px-4 py-12">
        <h1 className="mb-4 text-2xl font-semibold">Join a stokvel</h1>
        <p className="text-sm"><Link href={`/login?next=${encodeURIComponent('/join')}`} className="underline underline-offset-4">Sign in</Link> to join a stokvel with an invite code.</p>
      </main>
    )
  }

  const preview = code ? await previewInvite(code) : null
  const step = preview?.data ? requested : 'code'
  const message = preview?.response.status === 404
    ? 'We couldn’t find that code. Check it and try again.'
    : preview?.response.status === 410
      ? 'That invite has expired. Ask for a new one.'
      : preview?.response.status === 429
        ? 'Too many code checks. Please wait a minute and try again.'
        : query.error === '1' ? 'We could not join with that invite. Try again.' : null
  const alreadyMember = query.already === '1' && typeof query.stokvelId === 'string'
  const encodedCode = encodeURIComponent(code)

  return (
    <main className="mx-auto max-w-xl px-4 py-12">
      <p className="mb-4 text-sm"><Link href="/dashboard/member" className="inline-flex min-h-6 items-center underline underline-offset-4">← Dashboard</Link></p>
      <h1 className="mb-6 text-2xl font-semibold">Join a stokvel</h1>
      <ol aria-label="Join progress" className="mb-8 grid grid-cols-3 gap-2 text-sm">
        {steps.map((label, index) => {
          const current = index === ({ code: 0, rules: 1, confirm: 2 }[step])
          return <li key={label} aria-current={current ? 'step' : undefined} className={current ? 'font-semibold' : 'text-muted-foreground'}>Step {index + 1} of 3: {label}</li>
        })}
      </ol>
      <div role="alert">
        {message && <p className="mb-4 rounded-lg border border-destructive p-3 text-sm">{message}</p>}
      </div>
      {step === 'code' && (
        <section aria-labelledby="join-heading" className="space-y-4">
          <h2 id="join-heading" tabIndex={-1} className="text-xl font-semibold">{stepTitles.code}</h2>
          <JoinCodeForm defaultCode={code} />
          {code && <FocusOnMount targetId="join-heading" />}
        </section>
      )}
      {preview?.data && step !== 'code' && (
        <section aria-labelledby="join-heading" className="space-y-5 rounded-lg border p-5">
          <h2 id="join-heading" tabIndex={-1} className="text-xl font-semibold">{stepTitles[step]}</h2>
          {/* Keyed by step, so moving from step 2 to 3 focuses the new heading too. */}
          <FocusOnMount key={step} targetId="join-heading" />
          <dl className="space-y-2 text-sm">
            <div className="detail-row"><dt>Stokvel</dt><dd>{preview.data.name}</dd></div>
            <div className="detail-row"><dt>Contribution</dt><dd className="tabular-nums">{formatCents(preview.data.contributionAmountCents)} {preview.data.schedule.toLowerCase()}</dd></div>
            <div className="detail-row"><dt>Members</dt><dd className="tabular-nums">{preview.data.memberCount}</dd></div>
          </dl>
          {alreadyMember ? (
            <div className="space-y-3">
              <p role="status" className="text-sm">You are already a member of this stokvel.</p>
              <Link href={`/stokvels/${query.stokvelId}`} className="inline-flex h-10 items-center rounded-lg bg-primary px-4 text-sm text-primary-foreground">Open stokvel</Link>
            </div>
          ) : step === 'rules' ? (
            <>
              <p className="text-sm">{preview.data.rules}</p>
              <div className="flex flex-wrap gap-3">
                <Link href={`/join?code=${encodedCode}&step=code`} className="inline-flex h-10 items-center rounded-lg border px-4 text-sm">Back</Link>
                <Link href={`/join?code=${encodedCode}&step=confirm`} className="inline-flex h-10 items-center rounded-lg bg-primary px-4 text-sm text-primary-foreground">Continue</Link>
              </div>
            </>
          ) : (
            <form action={joinStokvel} className="space-y-4">
              <input type="hidden" name="code" value={code} />
              <input type="hidden" name="stokvelId" value={preview.data.stokvelId} />
              <p className="text-sm">You are joining {preview.data.name} for {formatCents(preview.data.contributionAmountCents)} {preview.data.schedule.toLowerCase()}.</p>
              <div className="flex flex-wrap gap-3">
                <Link href={`/join?code=${encodedCode}&step=rules`} className="inline-flex h-10 items-center rounded-lg border px-4 text-sm">Back</Link>
                <Button type="submit" className="h-10 px-4">Join {preview.data.name}</Button>
              </div>
            </form>
          )}
        </section>
      )}
    </main>
  )
}
