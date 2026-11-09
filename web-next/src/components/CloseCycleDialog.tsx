'use client'

import { useEffect, useRef, useState } from 'react'
import { formatCents } from '@/lib/money'
import { useToast } from './ToastProvider'

type Preview = {
  cycleId: string
  period: string
  asOfDate: string
  version: string
  arrears: Array<{ userId: string; userName: string; amountCents: number }>
  totalCents: number
}

export function CloseCycleDialog({ stokvelId, cycleId }: { stokvelId: string; cycleId: string }) {
  const [open, setOpen] = useState(false)
  const [preview, setPreview] = useState<Preview | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [pending, setPending] = useState(false)
  const cancelRef = useRef<HTMLButtonElement>(null)
  const openerRef = useRef<HTMLButtonElement>(null)
  const idempotencyKeyRef = useRef<string>('')
  // Focus goes back to the opener only after the dialog has been open. The
  // old effect also ran on page load and pulled focus to this button.
  const wasOpenRef = useRef(false)
  const { showToast } = useToast()

  async function loadPreview() {
    setOpen(true)
    setError(null)
    setPreview(null)
    idempotencyKeyRef.current = crypto.randomUUID()
    try {
      const response = await fetch(`/bff/stokvels/${stokvelId}/cycles/${cycleId}/close`, { cache: 'no-store' })
      if (response.ok) setPreview(await response.json() as Preview)
      else if (response.status === 403) setError("You don't have permission to close this cycle.")
      else {
        const problem = await response.json().catch(() => null) as { correlationId?: string } | null
        showToast({ message: 'The close preview service failed. Please try again.', correlationId: problem?.correlationId, persistent: response.status >= 500 })
        setError('The close preview could not be loaded.')
      }
    } catch {
      showToast({ message: 'The close preview service is unavailable. Please try again.', persistent: true })
      setError('The close preview could not be loaded.')
    }
  }

  useEffect(() => {
    if (open) cancelRef.current?.focus()
    else if (wasOpenRef.current) openerRef.current?.focus()
    wasOpenRef.current = open
  }, [open])

  async function confirm() {
    if (!preview || pending) return
    setPending(true)
    setError(null)
    let response: Response
    try {
      response = await fetch(`/bff/stokvels/${stokvelId}/cycles/${cycleId}/close`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json', 'Idempotency-Key': idempotencyKeyRef.current },
      body: JSON.stringify({ version: preview.version }),
      })
    } catch {
      showToast({ message: 'The close service is unavailable. Please retry with the same confirmation.', persistent: true })
      setPending(false)
      return
    }
    if (response.status === 409) {
      setError('The cycle changed since this preview. Review the new preview before confirming again.')
      try {
        const refreshed = await fetch(`/bff/stokvels/${stokvelId}/cycles/${cycleId}/close`, { cache: 'no-store' })
        if (refreshed.ok) {
          setPreview(await refreshed.json() as Preview)
          idempotencyKeyRef.current = crypto.randomUUID()
        } else {
          const problem = await refreshed.json().catch(() => null) as { correlationId?: string } | null
          showToast({ message: 'The refreshed close preview failed.', correlationId: problem?.correlationId, persistent: refreshed.status >= 500 })
        }
      } catch {
        showToast({ message: 'The refreshed close preview is unavailable.', persistent: true })
      }
    } else if (response.ok) {
      setOpen(false)
      window.location.reload()
    } else if (response.status >= 500) {
      const problem = await response.json().catch(() => null) as { correlationId?: string } | null
      showToast({ message: 'The cycle could not be closed.', correlationId: problem?.correlationId, persistent: true })
      setError('The cycle could not be closed.')
    } else {
      setError('The cycle could not be closed.')
    }
    setPending(false)
  }

  return (
    <>
      <button ref={openerRef} type="button" onClick={loadPreview} aria-haspopup="dialog" className="min-h-10 rounded-md border px-3 text-sm">Close cycle</button>
      {open && (
        <div role="presentation" className="fixed inset-0 z-10 grid place-items-center bg-black/50 p-4" onKeyDown={(event) => {
          if (event.key === 'Escape' && !pending) setOpen(false)
        }}>
          <section role="dialog" aria-modal="true" aria-labelledby="close-cycle-heading" aria-describedby="close-cycle-description" className="w-full max-w-lg space-y-4 rounded-lg bg-background p-6 shadow-xl" onKeyDown={(event) => {
            if (event.key !== 'Tab') return
            const focusable = Array.from(event.currentTarget.querySelectorAll<HTMLElement>('button, [href], input, select, textarea, [tabindex]:not([tabindex="-1"])'))
            if (focusable.length === 0) return
            const first = focusable[0]
            const last = focusable[focusable.length - 1]
            if (event.shiftKey && document.activeElement === first) {
              event.preventDefault()
              last.focus()
            } else if (!event.shiftKey && document.activeElement === last) {
              event.preventDefault()
              first.focus()
            }
          }}>
            <h2 id="close-cycle-heading" tabIndex={-1} className="text-xl font-semibold">Close {preview?.period ?? 'cycle'}</h2>
            <p id="close-cycle-description" className="sr-only">Review the arrears calculated by RondiTrack before confirming this irreversible action.</p>
            {!preview && !error && <div role="status" className="space-y-2"><span className="sr-only">Loading the close preview…</span><div aria-hidden="true" className="h-4 animate-pulse rounded bg-muted motion-reduce:animate-none" /><div aria-hidden="true" className="h-4 animate-pulse rounded bg-muted motion-reduce:animate-none" /><div aria-hidden="true" className="h-4 animate-pulse rounded bg-muted motion-reduce:animate-none" /></div>}
            {preview && <div className="space-y-3 text-sm">
              <p>As of South African date: <strong>{preview.asOfDate}</strong></p>
              {preview.arrears.length === 0 ? <p>No members will go into arrears.</p> : <ul>{preview.arrears.map((item) => <li key={item.userId}>{item.userName}: {formatCents(item.amountCents)}</li>)}</ul>}
              <p className="font-semibold">Total arrears: {formatCents(preview.totalCents)}</p>
            </div>}
            <p role="alert" className="text-sm text-destructive">{error}</p>
            <div className="flex justify-end gap-2">
              <button ref={cancelRef} type="button" onClick={() => { if (!pending) setOpen(false) }} aria-disabled={pending} className="min-h-10 rounded-md border px-3 text-sm aria-disabled:opacity-60">Cancel</button>
              <button type="button" onClick={confirm} aria-disabled={!preview || pending} className="min-h-10 rounded-md bg-primary px-3 text-sm text-primary-foreground aria-disabled:opacity-60">
                {/* No amount until the preview has one: it used to say "record R 0,00
                    in arrears" while loading, and formatted money inside the click. */}
                {pending ? 'Closing…' : preview ? `Close ${preview.period} and record ${formatCents(preview.totalCents)} in arrears` : 'Waiting for the preview'}
              </button>
            </div>
          </section>
        </div>
      )}
    </>
  )
}
