'use client'

import { useEffect, useRef } from 'react'
import { useRouter } from 'next/navigation'

const KEY = 'ronditrack:join-code'

// Step 1 of joining. Without JavaScript it is a plain GET form. With it, the
// code last checked in this tab is remembered, so pressing Back from step 2
// shows it still filled in (WCAG 3.3.7: never asked to type it again). The
// App Router renders /join afresh on Back, so the typed value alone is lost;
// e2e/keyboard.spec.ts found that. The code is already in the URL, so
// keeping it in this tab's sessionStorage reveals nothing new.
export function JoinCodeForm({ defaultCode }: { defaultCode: string }) {
  const router = useRouter()
  const inputRef = useRef<HTMLInputElement>(null)

  useEffect(() => {
    if (defaultCode || !inputRef.current || inputRef.current.value) return
    try {
      inputRef.current.value = window.sessionStorage.getItem(KEY) ?? ''
    } catch {
      // Storage blocked: the field just starts empty.
    }
  }, [defaultCode])

  function submit(event: React.FormEvent<HTMLFormElement>) {
    const code = String(new FormData(event.currentTarget).get('code') ?? '').trim()
    if (!code) return
    event.preventDefault()
    try {
      window.sessionStorage.setItem(KEY, code)
    } catch {
      // Storage blocked: Back shows an empty field, nothing worse.
    }
    router.push(`/join?code=${encodeURIComponent(code)}&step=rules`)
  }

  return (
    <form action="/join" method="get" onSubmit={submit} className="space-y-4">
      <div className="space-y-1">
        <label htmlFor="code" className="block text-sm font-medium">Invite code</label>
        <p id="code-hint" className="text-xs text-muted-foreground">Paste the code from your invite message, or type it.</p>
        <input
          ref={inputRef}
          id="code"
          name="code"
          defaultValue={defaultCode}
          required
          autoComplete="off"
          autoCapitalize="none"
          spellCheck={false}
          aria-describedby="code-hint"
          className="h-10 w-full rounded-lg border border-input bg-background px-3"
        />
      </div>
      <input type="hidden" name="step" value="rules" />
      <button type="submit" className="inline-flex h-10 items-center rounded-lg bg-primary px-4 text-sm font-medium text-primary-foreground">Check code</button>
    </form>
  )
}
