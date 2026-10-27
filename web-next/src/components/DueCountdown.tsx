'use client'

// The countdown re-renders at Johannesburg midnight, which needs useState and useEffect.

import { useEffect, useState } from 'react'
import {
  getDueCountdown,
  getNextJohannesburgMidnightDelay,
} from '@/lib/dates'

type DueCountdownProps = {
  dueDate?: string | null
  status?: string | null
}

export function DueCountdown({
  dueDate,
  status,
}: DueCountdownProps) {
  const [now, setNow] = useState(() => new Date())

  useEffect(() => {
    if (!dueDate || status !== 'Open') {
      return
    }

    let timeout: ReturnType<typeof setTimeout>

    function scheduleUpdate() {
      timeout = setTimeout(() => {
        setNow(new Date())
        scheduleUpdate()
      }, getNextJohannesburgMidnightDelay())
    }

    scheduleUpdate()

    return () => clearTimeout(timeout)
  }, [dueDate, status])

  if (!dueDate || status !== 'Open') {
    return null
  }

  return (
    <span className="text-sm font-medium text-foreground">
      {getDueCountdown(dueDate, now)}
    </span>
  )
}
