'use client'

import { createContext, useCallback, useContext, useMemo, useState } from 'react'

type Toast = { id: number; message: string; correlationId?: string; persistent?: boolean }
type ToastContextValue = { showToast: (toast: Omit<Toast, 'id'>) => void }

const ToastContext = createContext<ToastContextValue | null>(null)

export function useToast(): ToastContextValue {
  const context = useContext(ToastContext)
  if (!context) throw new Error('useToast must be used within ToastProvider')
  return context
}

export function ToastProvider({ children }: { children: React.ReactNode }) {
  const [toasts, setToasts] = useState<Toast[]>([])
  const showToast = useCallback((toast: Omit<Toast, 'id'>) => {
    const id = Date.now() + Math.random()
    setToasts(current => [...current, { ...toast, id }])
    if (!toast.persistent) {
      window.setTimeout(() => setToasts(current => current.filter(item => item.id !== id)), 4000)
    }
  }, [])
  const value = useMemo(() => ({ showToast }), [showToast])

  return (
    <ToastContext.Provider value={value}>
      {children}
      <div aria-live="polite" aria-label="Notifications" className="fixed bottom-4 right-4 z-50 grid max-w-sm gap-2">
        {toasts.map(toast => (
          <div key={toast.id} role={toast.persistent ? 'alert' : 'status'} className="rounded-lg border bg-background p-3 text-sm shadow-lg">
            <p>{toast.message}</p>
            {toast.correlationId && <p className="mt-1 text-xs text-muted-foreground">Reference: <code>{toast.correlationId}</code></p>}
            {toast.persistent && !toast.correlationId && <p className="mt-1 text-xs text-muted-foreground">Reference unavailable.</p>}
            <button type="button" className="mt-2 text-xs underline" onClick={() => setToasts(current => current.filter(item => item.id !== toast.id))}>Close</button>
          </div>
        ))}
      </div>
    </ToastContext.Provider>
  )
}
