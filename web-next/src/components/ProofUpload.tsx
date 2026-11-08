'use client'

import { useRef, useState } from 'react'
import { useToast } from './ToastProvider'

const MAX_BYTES = 5 * 1024 * 1024
const ACCEPTED = ['application/pdf', 'image/jpeg', 'image/png']

// ownerLabel is "your" or "Thandi's": with forty rows on a page, each file
// input and button needs a name that says whose proof it is (WCAG 2.4.6).
export function ProofUpload({ contributionId, ownerLabel }: { contributionId: string; ownerLabel: string }) {
  const [error, setError] = useState<string | null>(null)
  const [pending, setPending] = useState(false)
  const inputRef = useRef<HTMLInputElement>(null)
  const { showToast } = useToast()
  const inputId = `proof-${contributionId}`
  const errorId = `proof-error-${contributionId}`

  async function submit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (pending) return
    setError(null)
    const file = inputRef.current?.files?.[0]
    const problem = !file ? 'Choose a PDF, JPEG or PNG file first.'
      : !ACCEPTED.includes(file.type) ? 'Only PDF, JPEG and PNG files are accepted.'
        : file.size > MAX_BYTES ? 'Proof files must be 5 MB or smaller.'
          : null
    if (problem || !file) {
      setError(problem)
      inputRef.current?.focus()
      return
    }
    setPending(true)
    try {
      const body = new FormData()
      body.append('file', file)
      const response = await fetch(`/bff/contributions/${encodeURIComponent(contributionId)}/proof`, { method: 'POST', body })
      if (!response.ok) {
        const details = await response.json().catch(() => null) as { detail?: string; correlationId?: string } | null
        if (response.status >= 500) {
          showToast({ message: 'The proof service failed. Please try again.', correlationId: details?.correlationId, persistent: true })
          return
        }
        setError(details?.detail ?? 'The proof could not be uploaded.')
        inputRef.current?.focus()
        return
      }
      showToast({ message: `Uploaded ${ownerLabel} proof of payment.` })
      if (inputRef.current) inputRef.current.value = ''
    } catch {
      showToast({ message: 'The proof service is unavailable. Please try again.', persistent: true })
    } finally {
      setPending(false)
    }
  }

  return (
    <form onSubmit={submit} noValidate className="flex flex-wrap items-end gap-2">
      <div className="space-y-1">
        <label htmlFor={inputId} className="block text-xs font-medium">Upload {ownerLabel} proof of payment</label>
        <input
          ref={inputRef}
          id={inputId}
          type="file"
          accept=".pdf,.jpg,.jpeg,.png,application/pdf,image/jpeg,image/png"
          aria-invalid={error ? true : undefined}
          aria-describedby={error ? errorId : undefined}
          className="block w-full max-w-64 text-sm file:mr-2 file:min-h-8 file:rounded-md file:border file:bg-background file:px-2"
        />
        <p id={errorId} role="alert" className="text-sm text-destructive">{error}</p>
      </div>
      {/* aria-disabled keeps focus on the button while the upload is in flight. */}
      {/* The name starts with the visible word (WCAG 2.5.3) and says whose proof. */}
      <button type="submit" aria-disabled={pending} aria-label={`${pending ? 'Uploading' : 'Upload'} ${ownerLabel} proof`} className="min-h-8 rounded-md border px-3 text-sm aria-disabled:opacity-60">
        {pending ? 'Uploading…' : 'Upload'}
      </button>
    </form>
  )
}
