'use client'

import { useEffect } from 'react'

// Moves focus to a step's heading when the step appears, so a screen reader
// reads "Step 2 of 3: Review the rules" instead of staying silent.
export function FocusOnMount({ targetId }: { targetId: string }) {
  useEffect(() => {
    document.getElementById(targetId)?.focus()
  }, [targetId])
  return null
}
