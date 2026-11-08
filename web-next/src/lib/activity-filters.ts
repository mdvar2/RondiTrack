// The activity filters as nuqs reads them from the URL on the client, so the
// server can render the same first page the client would ask for.
export type ActivityFilters = { q: string; status: 'all' | 'paid' | 'pending'; from: string; to: string }

type SearchParams = Record<string, string | string[] | undefined>

export function activityFiltersFrom(searchParams: SearchParams): ActivityFilters {
  const one = (name: string) => {
    const value = searchParams[name]
    return typeof value === 'string' ? value : ''
  }
  const status = one('status')
  return { q: one('q'), status: status === 'paid' || status === 'pending' ? status : 'all', from: one('from'), to: one('to') }
}
