import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { ReactQueryDevtools } from '@tanstack/react-query-devtools'
import './index.css'
import App from './App.tsx'
import { ApiError } from './auth/session'
const client = new QueryClient({ defaultOptions: { queries: { retry: (count, error) => count < 2 && (!(error instanceof ApiError) || error.status >= 500) } } })
createRoot(document.getElementById('root')!).render(<StrictMode><QueryClientProvider client={client}><App /><ReactQueryDevtools initialIsOpen={false} /></QueryClientProvider></StrictMode>)
