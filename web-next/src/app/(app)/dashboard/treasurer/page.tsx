import type { Metadata } from 'next'
import Link from 'next/link'
import { api, readOrFail } from '@/lib/api'
import { stokvelsWhereTreasurer } from '@/lib/treasurer'
import { StatusBadge } from '@/components/StatusBadge'
import { EmptyState } from '@/components/EmptyState'
import { PageHeading } from '@/components/PageHeading'

export const metadata: Metadata = { title: 'Treasurer dashboard · RondiTrack' }

// Not a page some people may open: everyone may. It is a filtered view of
// the same /me/home list, using the role the API gives for each stokvel.
export default async function TreasurerDashboardPage() {
  const client = await api()
  const { stokvels } = readOrFail(await client.GET('/api/v1/me/home'))
  const treasurerOf = stokvelsWhereTreasurer(stokvels)

  if (treasurerOf.length === 0) {
    return (
      <>
        <PageHeading title="Treasurer dashboard" />
        <EmptyState
          title="You don't run any stokvels yet"
          action={{ href: '/dashboard/member', label: 'Go to your member dashboard' }}
        >
          Stokvels where you&apos;re the Treasurer will show here.
        </EmptyState>
      </>
    )
  }

  return (
    <>
      <PageHeading title="Treasurer dashboard">
        The stokvels where you&apos;re the Treasurer.
      </PageHeading>
      <ul className="stokvel-grid">
        {treasurerOf.map((stokvel) => (
          <li key={stokvel.stokvelId} className="stokvel-card">
            <div className="stokvel-card__header">
              <h2>{stokvel.stokvelName}</h2>
              <p className="role-badge">Treasurer</p>
            </div>
            <div className="detail-row">
              <span>Current cycle</span>
              {stokvel.currentCycle
                ? <strong>{stokvel.currentCycle.period} <StatusBadge status={stokvel.currentCycle.status} /></strong>
                : <strong>No current cycle</strong>}
            </div>
            <Link href={`/stokvels/${stokvel.stokvelId}`} className="text-sm font-medium underline underline-offset-4">
              Open {stokvel.stokvelName}
            </Link>
          </li>
        ))}
      </ul>
    </>
  )
}
