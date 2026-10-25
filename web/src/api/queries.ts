import { useInfiniteQuery, useQuery } from '@tanstack/react-query'
import { api, unwrap } from './client'
import type { components } from './schema'
export const queryKeys = {
  me: ['me'] as const,
  home: ['me', 'home'] as const,
  stokvels: ['stokvels'] as const,
  stokvel: (id: string) => ['stokvels', id] as const,
  cycles: (id: string) => ['stokvels', id, 'cycles'] as const,
  contributions: (id: string) => ['stokvels', id, 'contributions'] as const,
  members: (id: string) => ['stokvels', id, 'members'] as const,
}
export function useHome() { return useQuery({ queryKey: queryKeys.home, queryFn: async () => unwrap(await api.GET('/api/v1/me/home')) }) }
// The Week 6 API has /me/home but no /me; member data is provided by /me/home.
export const useMe = useHome
export function useStokvels() { const home = useHome(); return { ...home, data: home.data?.stokvels } }
export function useStokvel(id: string) { const home = useHome(); return { ...home, data: home.data?.stokvels.find(s => s.stokvelId === id) } }
export function useCycles(id: string) {
  return useQuery({ queryKey: queryKeys.cycles(id), enabled: !!id,
    queryFn: async () => unwrap(await api.GET('/api/v1/stokvels/{stokvelId}/cycles', { params: { path: { stokvelId: id } } })) })
}
export function useMembers(id: string, enabled: boolean) {
  return useQuery({ queryKey: queryKeys.members(id), enabled: !!id && enabled,
    queryFn: async () => unwrap(await api.GET('/api/v1/stokvels/{stokvelId}/members', { params: { path: { stokvelId: id }, query: { pageSize: 100 } } })).items })
}
type Page = components['schemas']['PagedResultOfContributionWithMemberResponse']
export function useContributions(id: string) {
  const cycles = useCycles(id)
  const query = useInfiniteQuery({
    queryKey: queryKeys.contributions(id),
    enabled: !!id && cycles.isSuccess,
    initialPageParam: { cycleIndex: 0, pageToken: '' },
    queryFn: async ({ pageParam }): Promise<Page> => {
      const cycle = cycles.data?.[pageParam.cycleIndex]
      if (!cycle) return { items: [], nextPageToken: '', pageSize: 25 }
      
      const result = await api.GET('/api/v1/stokvels/{stokvelId}/cycles/{cycleId}/contributions', {
        params: { path: { stokvelId: id, cycleId: cycle.id }, query: { pageSize: 25, ...(pageParam.pageToken ? { pageToken: pageParam.pageToken } : {}) } },
      })
      const raw: Page = unwrap(result)
      if (!raw || !Array.isArray(raw.items)) throw new Error('Unexpected contribution page response')
      return raw
    },
    getNextPageParam: (page, _all, last) => {
      if (page.nextPageToken) return { cycleIndex: last.cycleIndex, pageToken: page.nextPageToken }
      if (last.cycleIndex + 1 < (cycles.data?.length ?? 0)) return { cycleIndex: last.cycleIndex + 1, pageToken: '' }
      return undefined
    },
  })
  return { ...query, cycles, contributions: query.data?.pages.flatMap(p => p.items) ?? [] }
}
