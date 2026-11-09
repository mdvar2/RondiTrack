'use client'

import { ToastProvider } from './ToastProvider'

// What every page needs: the toasts. The query cache and URL-state adapter
// are only for the activity feed, so they live in DataProviders, on the two
// pages that show it. Here in the root they made every page, the stokvel page
// included, download and hydrate TanStack Query and nuqs (INP, Assignment 9.2
// Part 5).
export function Providers({ children }: { children: React.ReactNode }) {
  return <ToastProvider>{children}</ToastProvider>
}
