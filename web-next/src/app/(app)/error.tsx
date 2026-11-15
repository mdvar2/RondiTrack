'use client'

// Error boundaries must be Client Components: they catch errors in the browser
// and the Try again button needs an onClick.
import { ErrorPanel } from '@/components/ErrorPanel'

export default function AppError({ error, retry }: { error: Error & { digest?: string }; retry: () => void }) {
  return <ErrorPanel error={error} retry={retry} />
}
