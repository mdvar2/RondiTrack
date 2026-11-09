import type { Metadata } from 'next'
import Link from 'next/link'
import { cache, Suspense } from 'react'
import { api, readOrFail } from '@/lib/api'
import { getSession } from '@/lib/session'
import { pickCurrentCycle } from '@/lib/cycles'
import { canManageStokvel } from '@/lib/contribution-permissions'
import { recordContribution } from '@/lib/contribution-actions'
import { StokvelSummary } from '@/components/StokvelSummary'
import { ContributionList } from '@/components/ContributionList'
import { ContributionListSkeleton, StokvelSummarySkeleton } from '@/components/Skeletons'

// Generic on purpose: a title with the name would tell a non-member that the
// stokvel exists before the 404 arrives, and titles stay in browser history.
export const metadata: Metadata = { title: 'Stokvel' }

// One request each per page render, shared by both Suspense boundaries.
const getStokvel = cache(async (id: string) =>
  readOrFail(await (await api()).GET('/api/v1/stokvels/{id}', { params: { path: { id } } })))
const getCycles = cache(async (stokvelId: string) =>
  readOrFail(await (await api()).GET('/api/v1/stokvels/{stokvelId}/cycles', { params: { path: { stokvelId } } })))

async function StokvelData({ stokvelId }: { stokvelId: string }) {
  const session = await getSession()
  const currentUser = session?.user ?? { id: '', name: '' }
  const [stokvel, cycles] = await Promise.all([getStokvel(stokvelId), getCycles(stokvelId)])
  const cycle = pickCurrentCycle(cycles, stokvel.currentCycle?.period)
  // Only a Treasurer or Chairperson records for others, so only they need
  // the member list. A Member records for themselves.
  const members = canManageStokvel(stokvel.role)
    ? readOrFail(await (await api()).GET('/api/v1/stokvels/{stokvelId}/members', {
      params: { path: { stokvelId }, query: { pageSize: 100 } },
    })).items ?? []
    : [{ stokvelId, userId: currentUser.id, userName: currentUser.name, userEmail: '', role: stokvel.role, joinedAtUtc: '', version: 0 }]

  return (
    <StokvelSummary
      stokvel={stokvel}
      cycleId={cycle?.id ?? null}
      currentUserId={currentUser.id}
      members={members}
      recordContribution={recordContribution}
    >
      {cycle && (
        // Its own boundary: the list streams in after the form, and a click
        // on the form doesn't have to hydrate forty upload forms first (INP).
        <Suspense fallback={<ContributionListSkeleton />}>
          <ContributionListData stokvelId={stokvelId} cycleId={cycle.id} cycleOpen={cycle.status === 'Open'} role={stokvel.role} currentUserId={currentUser.id} />
        </Suspense>
      )}
    </StokvelSummary>
  )
}

async function ContributionListData({ stokvelId, cycleId, cycleOpen, role, currentUserId }: {
  stokvelId: string; cycleId: string; cycleOpen: boolean; role: string; currentUserId: string
}) {
  const page = readOrFail(await (await api()).GET('/api/v1/stokvels/{stokvelId}/cycles/{cycleId}/contributions', {
    params: { path: { stokvelId, cycleId }, query: { pageSize: 100, sortBy: 'recordedAtUtc', sortDirection: 'desc' } },
  }))
  return <ContributionList contributions={page.items ?? []} role={role} currentUserId={currentUserId} cycleOpen={cycleOpen} />
}

export default async function StokvelPage({ params }: PageProps<'/stokvels/[id]'>) {
  const { id } = await params

  return (
    <>
      <p className="mb-4 text-sm"><Link href="/stokvels" className="inline-flex min-h-6 items-center underline underline-offset-4">← My stokvels</Link></p>
      <Suspense fallback={<StokvelSummarySkeleton />}>
        <StokvelData stokvelId={id} />
      </Suspense>
    </>
  )
}
