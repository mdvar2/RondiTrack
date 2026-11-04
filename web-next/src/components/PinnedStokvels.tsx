'use client'

import Link from 'next/link'
import type { HomeStokvel } from '@/lib/types'
import { usePinnedStokvels } from '@/store/pinned-stokvels'

export function PinnedStokvelsList({ stokvels }: { stokvels: HomeStokvel[] }) {
  const pinnedIds = usePinnedStokvels((state) => state.pinnedIds)
  const pinned = stokvels.filter((stokvel) => pinnedIds.includes(String(stokvel.stokvelId)))

  if (pinned.length === 0) {
    return null
  }

  return (
    <section className="mb-6 space-y-3">
      <h2 className="text-lg font-semibold">Pinned stokvels</h2>
      <ul className="grid gap-3 md:grid-cols-2">
        {pinned.map((stokvel) => (
          <li key={stokvel.stokvelId} className="rounded-lg border bg-card p-4">
            <div className="flex items-center justify-between gap-2">
              <Link href={`/stokvels/${stokvel.stokvelId}`} className="font-medium underline-offset-4 hover:underline">
                {stokvel.stokvelName}
              </Link>
              <span className="role-badge">{stokvel.role}</span>
            </div>
            <p className="mt-2 text-sm text-muted-foreground">{stokvel.currentCycle ? stokvel.currentCycle.period : 'No active cycle'}</p>
          </li>
        ))}
      </ul>
    </section>
  )
}

export function PinnedStokvelButton({ stokvelId, label }: { stokvelId: string; label: string }) {
  const togglePinned = usePinnedStokvels((state) => state.togglePinned)
  const isPinned = usePinnedStokvels((state) => state.isPinned(stokvelId))

  return (
    <button
      type="button"
      onClick={() => togglePinned(stokvelId)}
      aria-pressed={isPinned}
      className="rounded border px-2 py-1 text-xs font-medium transition hover:bg-muted"
    >
      {isPinned ? `★ ${label}` : `☆ ${label}`}
    </button>
  )
}
