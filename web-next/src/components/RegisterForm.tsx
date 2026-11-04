'use client'

import Link from 'next/link'
import { useActionState } from 'react'
import { register, type RegisterState } from '@/lib/auth-actions'
import { Button } from '@/components/ui/button'

const inputClass = 'h-9 w-full rounded-lg border border-input bg-background px-3 text-sm focus-visible:outline-2 focus-visible:outline-ring'

export function RegisterForm() {
  const [state, formAction, pending] = useActionState<RegisterState, FormData>(register, null)

  return (
    <form action={formAction} className="space-y-4" noValidate>
      <div className="space-y-1">
        <label htmlFor="name" className="text-sm font-medium">Name</label>
        <input id="name" name="name" type="text" autoComplete="name" required defaultValue={state?.name} className={inputClass} />
      </div>
      <div className="space-y-1">
        <label htmlFor="email" className="text-sm font-medium">Email</label>
        <input id="email" name="email" type="email" autoComplete="email" required defaultValue={state?.email} className={inputClass} />
      </div>
      <div className="space-y-1">
        <label htmlFor="password" className="text-sm font-medium">Password</label>
        <input id="password" name="password" type="password" autoComplete="new-password" required className={inputClass} />
      </div>
      {state && (
        <div role="alert" className="space-y-1 rounded-lg border border-destructive p-3 text-sm">
          <p>{state.message}</p>
          {state.retryAfter !== undefined && <p>Retry after {state.retryAfter} seconds.</p>}
          {state.correlationId && <p className="text-muted-foreground">Reference: <code>{state.correlationId}</code></p>}
        </div>
      )}
      <Button type="submit" size="lg" className="w-full" disabled={pending}>
        {pending ? 'Creating account…' : 'Create account'}
      </Button>
      <p className="text-center text-sm text-muted-foreground">
        Already have an account?{' '}
        <Link href="/login" className="font-medium underline underline-offset-4">Sign in</Link>
      </p>
    </form>
  )
}
