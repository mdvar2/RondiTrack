'use client'

import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { NuqsAdapter } from 'nuqs/adapters/next/app'

function makeQueryClient() {
  return new QueryClient({
    defaultOptions: {
      queries: {
        staleTime: 60_000,
        refetchOnWindowFocus: false,
      },
    },
  })
}

// One client per browser tab, so the activity cache survives moving between
// the two pages that use it. On the server, one per request: a module-level
// client there would be shared by everyone.
let browserClient: QueryClient | undefined
function getQueryClient() {
  if (typeof window === 'undefined') return makeQueryClient()
  return (browserClient ??= makeQueryClient())
}

// The query cache and the URL-state adapter, only where the activity feed
// and its filters are rendered (see Providers).
export function DataProviders({ children }: { children: React.ReactNode }) {
  return (
    <NuqsAdapter>
      <QueryClientProvider client={getQueryClient()}>{children}</QueryClientProvider>
    </NuqsAdapter>
  )
}
