import type { Metadata } from 'next'

export type InviteLinkStatus = 'valid' | 'expired' | 'unknown' | 'unavailable'

// What a link preview of /join?code=… shows. Its only input is the invite's
// status, so it cannot name the stokvel, a member or an amount: the preview
// card goes to everyone in a WhatsApp group, members or not. The URL holds a
// code, so the page is never indexed.
export function inviteMetadata(status: InviteLinkStatus | null): Metadata {
  const title = status === 'valid' ? 'You’re invited to join a stokvel'
    : status === 'expired' ? 'This stokvel invite has expired'
      : status === 'unknown' ? 'Stokvel invite not found'
        : 'Join a stokvel'
  const description = status === 'valid'
    ? 'Someone has invited you to join their stokvel on RondiTrack. Sign in to see the details and join.'
    : status === 'expired'
      ? 'This RondiTrack invite link is no longer valid. Ask the stokvel for a new one.'
      : 'Join a stokvel on RondiTrack with an invite code.'
  return {
    title,
    description,
    robots: { index: false, follow: false },
    openGraph: { type: 'website', siteName: 'RondiTrack', title: `${title} · RondiTrack`, description },
    twitter: { card: 'summary', title: `${title} · RondiTrack`, description },
  }
}
