'use client'

import { useRef, useState } from 'react'
import { useToast } from './ToastProvider'

const MAX_BYTES = 5 * 1024 * 1024
const ACCEPTED = ['application/pdf', 'image/jpeg', 'image/png']

export function ProofUpload({ contributionId }: { contributionId: string }) {
  const [error, setError] = useState<string | null>(null)
  const [message, setMessage] = useState<string | null>(null)
  const [pending, setPending] = useState(false)
  const inputRef = useRef<HTMLInputElement>(null)
  const { showToast } = useToast()

  async function submit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setError(null)
    setMessage(null)
    const file = inputRef.current?.files?.[0]
    if (!file) return setError('Choose a PDF, JPEG or PNG proof.')
    if (!ACCEPTED.includes(file.type)) return setError('Only PDF, JPEG and PNG files are accepted.')
    if (file.size > MAX_BYTES) return setError('Proof files must be 5 MB or smaller.')
    setPending(true)
    try {
      const body = new FormData()
      body.append('file', file)
      const response = await fetch(`/bff/contributions/${encodeURIComponent(contributionId)}/proof`, { method: 'POST', body })
      if (!response.ok) {
        const problem = await response.json().catch(() => null) as { detail?: string; correlationId?: string } | null
        if (response.status >= 500) {
          showToast({ message: 'The proof service failed. Please try again.', correlationId: problem?.correlationId, persistent: true })
          return
        }
        setError(problem?.detail ?? 'The proof could not be uploaded.')
        return
      }
      showToast({ message: 'Proof uploaded.' })
      if (inputRef.current) inputRef.current.value = ''
    } catch {
      showToast({ message: 'The proof service is unavailable. Please try again.', persistent: true })
    } finally {
      setPending(false)
    }
  }

  return (
    <form onSubmit={submit} className="space-y-2">
      <label htmlFor={`proof-${contributionId}`} className="text-sm font-medium">Proof of payment</label>
      <input
        ref={inputRef}
        id={`proof-${contributionId}`}
        type="file"
        accept=".pdf,.jpg,.jpeg,.png,application/pdf,image/jpeg,image/png"
        aria-describedby={`proof-error-${contributionId}`}
        className="block w-full text-sm"
      />
      {error && <p id={`proof-error-${contributionId}`} role="alert" className="text-sm text-red-600">{error}</p>}
      {message && <p id={`proof-error-${contributionId}`} role="status" className="text-sm text-emerald-600">{message}</p>}
      <button type="submit" disabled={pending} className="rounded-md border px-3 py-2 text-sm">
        {pending ? 'Uploading…' : 'Upload proof'}
      </button>
    </form>
  )
}
