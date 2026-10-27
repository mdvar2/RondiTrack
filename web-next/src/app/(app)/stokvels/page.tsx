import type { Metadata } from 'next'
import { api, readOrFail } from '@/lib/api'
import { StokvelCard } from '@/components/StokvelCard'
import { EmptyState } from '@/components/EmptyState'
import { PageHeading } from '@/components/PageHeading'

export const metadata: Metadata = { title: 'My stokvels · RondiTrack' }

// GET /api/v1/stokvels lists every stokvel and is for platform admins only
// (403 for everyone else), so "my stokvels, with my role" comes from /me/home.
export default async function StokvelsPage() {
  const client = await api()
  const { stokvels } = readOrFail(await client.GET('/api/v1/me/home'))

  return (
    <>
      <PageHeading title="My stokvels">Each stokvel you belong to, and your role in it.</PageHeading>
      {stokvels.length === 0 ? (
        <EmptyState title="You're not in any stokvels yet">
          Ask a stokvel&apos;s Chairperson to add you.
        </EmptyState>
      ) : (
        <div className="stokvel-grid">
          {stokvels.map((stokvel) => (
            <StokvelCard key={stokvel.stokvelId} stokvel={stokvel} href={`/stokvels/${stokvel.stokvelId}`} />
          ))}
        </div>
      )}
    </>
  )
}
