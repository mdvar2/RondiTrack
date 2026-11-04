import type { Metadata } from 'next'
import Link from 'next/link'
import { redirect } from 'next/navigation'
import { joinStokvel } from './actions'
import { previewInvite } from '@/lib/join'
import { stepFromQuery } from '@/lib/join-state'
import { formatCents } from '@/lib/money'
import { getSession } from '@/lib/session'
import { Button } from '@/components/ui/button'

export const metadata: Metadata = { title: 'Join a stokvel · RondiTrack' }
export const instant = false

const steps = ['Code', 'Rules', 'Confirm'] as const

export default async function JoinPage({ searchParams }: PageProps<'/join'>) {
  const query = await searchParams
  const code = typeof query.code === 'string' ? query.code.trim() : ''
  const requested = stepFromQuery(typeof query.step === 'string' ? query.step : undefined)
  const session = await getSession()
  if (!session && code) redirect(`/login?next=${encodeURIComponent(`/join?code=${code}&step=code`)}`)

  let preview: Awaited<ReturnType<typeof previewInvite>> | null = null
  if (code && session) preview = await previewInvite(code)
  const step = preview?.data ? requested : 'code'
  const message = preview?.response.status === 404
    ? 'Check the code and try again.'
    : preview?.response.status === 410
      ? 'Ask for a new invite.'
      : preview?.response.status === 429
        ? 'Too many code checks. Please wait and try again.'
      : query.error === '1' ? 'We could not join with that invite. Try again.' : null
  const alreadyMember = query.already === '1' && typeof query.stokvelId === 'string'

  return (
    <main className="mx-auto max-w-xl px-4 py-12">
      <p className="mb-4 text-sm"><Link href="/dashboard/member" className="underline">← Dashboard</Link></p>
      <h1 className="mb-6 text-2xl font-semibold">Join a stokvel</h1>
      <ol aria-label="Join progress" className="mb-8 grid grid-cols-3 gap-2 text-sm">
        {steps.map((label, index) => {
          const current = index === ({ code: 0, rules: 1, confirm: 2 }[step])
          return <li key={label} aria-current={current ? 'step' : undefined} className={current ? 'font-semibold' : 'text-muted-foreground'}>Step {index + 1} of 3: {label}</li>
        })}
      </ol>
      {message && <p role="alert" className="mb-4 rounded-lg border border-destructive p-3 text-sm">{message}</p>}
      {step === 'code' && (
        <form action="/join" method="get" className="space-y-4">
          <label htmlFor="code" className="block text-sm font-medium">Invite code</label>
          <input id="code" name="code" defaultValue={code} required autoFocus className="h-10 w-full rounded-lg border bg-background px-3" />
          <input type="hidden" name="step" value="rules" />
          <Button type="submit">Check code</Button>
        </form>
      )}
      {preview?.data && step !== 'code' && (
        <section aria-labelledby="join-heading" className="space-y-5 rounded-lg border p-5">
          <h2 id="join-heading" tabIndex={-1} className="text-xl font-semibold">Step {step === 'rules' ? 2 : 3} of 3: {step === 'rules' ? 'Review the rules' : 'Confirm your join'}</h2>
          <dl className="space-y-2 text-sm">
            <div className="detail-row"><dt>Stokvel</dt><dd>{preview.data.name}</dd></div>
            <div className="detail-row"><dt>Contribution</dt><dd>{formatCents(preview.data.contributionAmountCents)} {preview.data.schedule.toLowerCase()}</dd></div>
            <div className="detail-row"><dt>Members</dt><dd>{preview.data.memberCount}</dd></div>
          </dl>
          {alreadyMember ? (
            <div className="space-y-3">
              <p role="status" className="text-sm">You are already a member of this stokvel.</p>
              <Link href={`/stokvels/${query.stokvelId}`} className="inline-flex rounded-lg bg-primary px-4 py-2 text-sm text-primary-foreground">Open stokvel</Link>
            </div>
          ) : step === 'rules' ? (
            <>
              <p className="text-sm">{preview.data.rules}</p>
              <Link href={`/join?code=${encodeURIComponent(code)}&step=confirm`} className="inline-flex rounded-lg bg-primary px-4 py-2 text-sm text-primary-foreground">Continue</Link>
            </>
          ) : (
            <form action={joinStokvel} className="space-y-4">
              <input type="hidden" name="code" value={code} />
              <input type="hidden" name="stokvelId" value={preview.data.stokvelId} />
              <p className="text-sm">You are joining {preview.data.name} for {formatCents(preview.data.contributionAmountCents)} {preview.data.schedule.toLowerCase()}.</p>
              <Button type="submit">Join {preview.data.name}</Button>
            </form>
          )}
        </section>
      )}
    </main>
  )
}
