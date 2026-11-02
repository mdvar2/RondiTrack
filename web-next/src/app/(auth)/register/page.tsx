import type { Metadata } from 'next'
import { RegisterForm } from '@/components/RegisterForm'

export const metadata: Metadata = { title: 'Create account · RondiTrack' }

export default function RegisterPage() {
  return (
    <main className="mx-auto flex min-h-screen max-w-sm flex-col justify-center px-4 py-12">
      <p className="mb-2 text-sm font-semibold tracking-wide text-muted-foreground">RondiTrack</p>
      <h1 className="mb-6 text-2xl font-semibold">Create account</h1>
      <RegisterForm />
    </main>
  )
}
