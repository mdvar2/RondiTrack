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
    else openerRef.current?.focus()
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
      <button ref={openerRef} type="button" onClick={loadPreview} className="rounded-md border px-3 py-2 text-sm">Close cycle</button>
      {open && (
        <div role="presentation" className="fixed inset-0 z-10 grid place-items-center bg-black/50 p-4" onKeyDown={(event) => {
          if (event.key === 'Escape' && !pending) setOpen(false)
        }}>
          <section role="dialog" aria-modal="true" aria-labelledby="close-cycle-heading" aria-describedby="close-cycle-description" className="w-full max-w-lg space-y-4 rounded-lg bg-background p-6 shadow-xl" onKeyDown={(event) => {
            if (event.key !== 'Tab') return
            const focusable = Array.from(event.currentTarget.querySelectorAll<HTMLElement>('button:not([disabled]), [href], input:not([disabled]), select:not([disabled]), textarea:not([disabled])'))
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
            {!preview && !error && <div aria-label="Loading preview" className="space-y-2"><div className="h-4 animate-pulse rounded bg-muted" /><div className="h-4 animate-pulse rounded bg-muted" /><div className="h-4 animate-pulse rounded bg-muted" /></div>}
            {preview && <div className="space-y-3 text-sm">
              <p>As of South African date: <strong>{preview.asOfDate}</strong></p>
              {preview.arrears.length === 0 ? <p>No members will go into arrears.</p> : <ul>{preview.arrears.map((item) => <li key={item.userId}>{item.userName}: {formatCents(item.amountCents)}</li>)}</ul>}
              <p className="font-semibold">Total arrears: {formatCents(preview.totalCents)}</p>
            </div>}
            {error && <p role="alert" className="text-sm text-red-600">{error}</p>}
            <div className="flex justify-end gap-2">
              <button ref={cancelRef} type="button" onClick={() => setOpen(false)} disabled={pending} className="rounded-md border px-3 py-2 text-sm">Cancel</button>
              <button type="button" onClick={confirm} disabled={!preview || pending} className="rounded-md bg-primary px-3 py-2 text-sm text-primary-foreground">
                {pending ? 'Closing…' : `Close ${preview?.period ?? 'cycle'} and record ${formatCents(preview?.totalCents ?? 0)} in arrears`}
              </button>
            </div>
          </section>
        </div>
      )}
    </>
  )
}
