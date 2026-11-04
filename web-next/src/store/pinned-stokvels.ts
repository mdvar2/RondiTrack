import { create } from 'zustand'
import { createJSONStorage, persist } from 'zustand/middleware'

type PinnedStokvelsState = {
  pinnedIds: string[]
  togglePinned: (id: string) => void
  isPinned: (id: string) => boolean
  clearPinned: () => void
}

const noopStorage = {
  getItem: () => null,
  setItem: () => undefined,
  removeItem: () => undefined,
}

export const usePinnedStokvels = create<PinnedStokvelsState>()(
  persist(
    (set, get) => ({
      pinnedIds: [],
      togglePinned: (id: string) => {
        const next = new Set(get().pinnedIds)
        if (next.has(id)) {
          next.delete(id)
        } else {
          next.add(id)
        }
        set({ pinnedIds: [...next] })
      },
      isPinned: (id: string) => get().pinnedIds.includes(id),
      clearPinned: () => set({ pinnedIds: [] }),
    }),
    {
      name: 'ronditrack-pinned-stokvels',
      storage: createJSONStorage(() =>
        typeof window === 'undefined' ? noopStorage : localStorage,
      ),
    },
  ),
)
