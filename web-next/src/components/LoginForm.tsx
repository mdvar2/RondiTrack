'use client'

// useActionState keeps the Server Action's answer (an error message and its
// reference) between submissions, and tells us when sign-in is in progress.
import { useActionState } from 'react'
import { signIn, type SignInState } from '@/lib/auth-actions'
import { Button } from '@/components/ui/button'

const inputClass = 'h-9 w-full rounded-lg border border-input bg-background px-3 text-sm focus-visible:outline-2 focus-visible:outline-ring'

export function LoginForm() {
  const [state, formAction, pending] = useActionState<SignInState, FormData>(signIn, null)

  return (
    <form action={formAction} className="space-y-4" noValidate>
      <div className="space-y-1">
        <label htmlFor="email" className="text-sm font-medium">Email</label>
        <input id="email" name="email" type="email" autoComplete="email" required
          defaultValue={state?.email} className={inputClass} />
      </div>
      <div className="space-y-1">
        <label htmlFor="password" className="text-sm font-medium">Password</label>
        <input id="password" name="password" type="password" autoComplete="current-password" required
          className={inputClass} />
      </div>
      {state && (
        <div role="alert" className="space-y-1 rounded-lg border border-destructive p-3 text-sm">
          <p>{state.message}</p>
          {state.retryAfter !== undefined && <p>Retry after {state.retryAfter} seconds.</p>}
          {state.correlationId && <p className="text-muted-foreground">Reference: <code>{state.correlationId}</code></p>}
        </div>
      )}
      <Button type="submit" size="lg" className="w-full" disabled={pending}>
        {pending ? 'Signing in…' : 'Sign in'}
      </Button>
    </form>
  )
}
