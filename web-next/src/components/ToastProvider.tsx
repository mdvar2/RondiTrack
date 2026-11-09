'use client'

import { createContext, useCallback, useContext, useEffect, useMemo, useRef, useState } from 'react'

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
  const close = (id: number) => setToasts((current) => current.filter((item) => item.id !== id))
  const regionRef = useRef<HTMLElement>(null)

  // WCAG 2.4.11: while a toast is showing, a control that receives focus
  // under it is scrolled clear, and the page gets room at the bottom so it
  // always can be. (scroll-padding alone wasn't enough: Chrome doesn't
  // scroll an element that is already inside the viewport. Found by
  // e2e/keyboard.spec.ts on a 360 px wide screen.)
  useEffect(() => {
    const region = regionRef.current
    if (!region || toasts.length === 0) return
    const previousPadding = document.body.style.paddingBottom
    document.body.style.paddingBottom = `${region.getBoundingClientRect().height + 32}px`
    function keepClear(event: FocusEvent) {
      const target = event.target
      if (!(target instanceof HTMLElement) || !region || region.contains(target)) return
      const toast = region.getBoundingClientRect()
      const box = target.getBoundingClientRect()
      if (box.bottom > toast.top && box.top < toast.bottom && box.right > toast.left && box.left < toast.right) {
        window.scrollBy({ top: box.bottom - toast.top + 16, behavior: 'instant' })
      }
    }
    document.addEventListener('focusin', keepClear)
    return () => {
      document.removeEventListener('focusin', keepClear)
      document.body.style.paddingBottom = previousPadding
    }
  }, [toasts])

  return (
    <ToastContext.Provider value={value}>
      {children}
      {/* Two live regions that are always in the page, so what is added to
          them is announced: errors interrupt (alert), the rest waits (status).
          The fixed corner never covers a focused control: globals.css keeps
          scroll-padding-bottom clear of it (WCAG 2.4.11). */}
      <section ref={regionRef} aria-label="Notifications" className="pointer-events-none fixed right-4 bottom-4 z-50 grid max-w-sm gap-2">
        <div role="status" className="grid gap-2">
          {toasts.filter((toast) => !toast.persistent).map((toast) => <ToastCard key={toast.id} toast={toast} onClose={close} />)}
        </div>
        <div role="alert" className="grid gap-2">
          {toasts.filter((toast) => toast.persistent).map((toast) => <ToastCard key={toast.id} toast={toast} onClose={close} />)}
        </div>
      </section>
    </ToastContext.Provider>
  )
}

function ToastCard({ toast, onClose }: { toast: Toast; onClose: (id: number) => void }) {
  return (
    <div className="pointer-events-auto rounded-lg border bg-background p-3 text-sm shadow-lg">
      <p>{toast.message}</p>
      {toast.correlationId && <p className="mt-1 text-xs text-muted-foreground">Reference: <code>{toast.correlationId}</code></p>}
      {toast.persistent && !toast.correlationId && <p className="mt-1 text-xs text-muted-foreground">Reference unavailable.</p>}
      <button type="button" aria-label="Close notification" className="mt-2 min-h-6 min-w-6 text-xs underline" onClick={() => onClose(toast.id)}>
        Close
      </button>
    </div>
  )
}
