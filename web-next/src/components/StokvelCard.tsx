import Link from 'next/link'
import type { HomeStokvel } from '@/lib/types'
import { StatusBadge } from './StatusBadge'
import { DueCountdown } from './DueCountdown'
import { formatCents } from '@/lib/money'
import { formatCycleDueDate, formatJohannesburgDate } from '@/lib/dates'

type StokvelCardProps = { stokvel: HomeStokvel; href?: string }

// A Server Component: it only displays what /me/home returned. The one
// interactive part, DueCountdown, is its own small Client Component.
export function StokvelCard({ stokvel, href }: StokvelCardProps) {
  const { stokvelName, role, currentCycle, amountPaid, amountOwed, nextPayoutDate, arrearsCents = 0 } = stokvel

  return (
    <article className="stokvel-card">
      <header className="stokvel-card__header">
        <h2>{href ? <Link href={href} className="underline-offset-4 hover:underline">{stokvelName}</Link> : stokvelName}</h2>
        <p className="role-badge">You&apos;re the {role}</p>
      </header>
      {arrearsCents > 0 && (
        <div className="mb-3"><StatusBadge arrearsCents={arrearsCents} /></div>
      )}
      <section className="stokvel-card__section">
        <h3>Current contribution cycle</h3>
        {currentCycle ? (
          <>
            <div className="detail-row"><span>Cycle</span><strong>{currentCycle.period}</strong></div>
            <div className="detail-row"><span>Status</span><StatusBadge status={currentCycle.status} /></div>
            <div className="detail-row"><span>Due date</span><strong>{currentCycle.dueDate ? formatCycleDueDate(currentCycle.dueDate) : 'No due date set'}</strong></div>
            <DueCountdown dueDate={currentCycle.dueDate} status={currentCycle.status} />
            <div className="contribution-summary">
              <div><span>Paid this cycle</span><strong>{formatCents(amountPaid)}</strong></div>
              <div><span>Outstanding</span><strong>{amountOwed === 0 ? 'All paid up' : formatCents(amountOwed)}</strong></div>
            </div>
          </>
        ) : <p className="empty-state">No current contribution cycle</p>}
      </section>
      <section className="stokvel-card__section">
        <h3>Next payout</h3>
        <p>{nextPayoutDate ? formatJohannesburgDate(nextPayoutDate) : 'No payout scheduled'}</p>
      </section>
      {href && (
        <p className="mt-4 text-sm"><Link href={href} className="font-medium underline underline-offset-4">View stokvel</Link></p>
      )}
    </article>
  )
}
