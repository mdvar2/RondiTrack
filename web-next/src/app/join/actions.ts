'use server'

import { redirect } from 'next/navigation'
import { joinInvite } from '@/lib/join'

export async function joinStokvel(formData: FormData): Promise<void> {
  const code = String(formData.get('code') ?? '').trim()
  const stokvelId = String(formData.get('stokvelId') ?? '')
  if (!code || !stokvelId) redirect('/join')
  const response = await joinInvite(code)
  if (response.ok) redirect(`/stokvels/${stokvelId}`)
  if (response.status === 409) redirect(`/join?code=${encodeURIComponent(code)}&step=confirm&already=1&stokvelId=${encodeURIComponent(stokvelId)}`)
  redirect(`/join?code=${encodeURIComponent(code)}&step=rules&error=1`)
}
