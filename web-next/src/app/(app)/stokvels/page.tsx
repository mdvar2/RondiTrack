import type { Metadata } from 'next'
import { loadActivityPage, loadHomeStokvels } from '@/lib/activity'
import { activityFiltersFrom } from '@/lib/activity-filters'
import { StokvelCard } from '@/components/StokvelCard'
import { EmptyState } from '@/components/EmptyState'
import { PageHeading } from '@/components/PageHeading'
import { PinnedStokvelsList } from '@/components/PinnedStokvels'
import { ActivityFeed } from '@/components/ActivityFeed'
import { ContributionHistoryFilters } from '@/components/ContributionHistoryFilters'
import { DataProviders } from '@/components/DataProviders'

export const metadata: Metadata = { title: 'My stokvels' }

// GET /api/v1/stokvels lists every stokvel and is for platform admins only
// (403 for everyone else), so "my stokvels, with my role" comes from /me/home.
export default async function StokvelsPage({ searchParams }: PageProps<'/stokvels'>) {
  const filters = activityFiltersFrom(await searchParams)
  const stokvels = await loadHomeStokvels()
  const firstPage = await loadActivityPage(stokvels, filters)

  return (
    <>
      <PageHeading title="My stokvels">Each stokvel you belong to, and your role in it.</PageHeading>
      <PinnedStokvelsList stokvels={stokvels} />
      <DataProviders>
        <ContributionHistoryFilters />
        <div className="mt-6">
          {/* The filter bar above is this page's one set of filter controls. */}
          <ActivityFeed initialPage={firstPage} initialFilters={filters} showFilters={false} />
        </div>
      </DataProviders>
      {stokvels.length === 0 ? (
        <EmptyState title="You're not in any stokvels yet">
          Ask a stokvel&apos;s Chairperson to add you.
        </EmptyState>
      ) : (
        <div className="stokvel-grid mt-6">
          {stokvels.map((stokvel) => (
            <StokvelCard key={stokvel.stokvelId} stokvel={stokvel} href={`/stokvels/${stokvel.stokvelId}`} />
          ))}
        </div>
      )}
    </>
  )
}
