// Which cycle the stokvel page records against. GET /cycles lists cycles
// newest first, so the old `cycles.at(-1)` picked the OLDEST cycle: with a
// year of history the form recorded against a closed cycle and the API
// refused every contribution (found by the Part 5 performance seed).
export type CycleSummary = { id: string; period: string; status: string }

export function pickCurrentCycle<T extends CycleSummary>(cycles: readonly T[], currentPeriod?: string | null): T | null {
  if (cycles.length === 0) return null
  const inPeriod = currentPeriod ? cycles.filter((cycle) => cycle.period === currentPeriod) : []
  return inPeriod.find((cycle) => cycle.status === 'Open')
    ?? inPeriod[0]
    ?? cycles.find((cycle) => cycle.status === 'Open')
    ?? [...cycles].sort((a, b) => b.period.localeCompare(a.period))[0]
}
