import type { HomeStokvel } from '../api/types'
import { formatCents } from '../lib/money'
import {
  formatCycleDueDate,
  formatJohannesburgDate,
} from '../lib/dates'

type StokvelCardProps = {
  stokvel: HomeStokvel
}

export function StokvelCard({ stokvel }: StokvelCardProps) {
  const {
    stokvelName,
    role,
    currentCycle,
    amountPaid,
    amountOwed,
    nextPayoutDate,
  } = stokvel

  const isTreasurer = role === 'Treasurer'

  return (
    <article className="stokvel-card">
      <header className="stokvel-card__header">
        <div>
          <h2>{stokvelName}</h2>
          <p className="stokvel-card__subtitle">
            Community savings group
          </p>
        </div>

        <span className="role-badge">{role}</span>
      </header>

      <section className="stokvel-card__section">
        <h3>Current contribution cycle</h3>

        {currentCycle ? (
          <>
            <div className="detail-row">
              <span>Cycle</span>
              <strong>{currentCycle.period}</strong>
            </div>

            <div className="detail-row">
              <span>Status</span>
              <strong>{currentCycle.status}</strong>
            </div>

            <div className="detail-row">
              <span>Due date</span>
              <strong>
                {currentCycle.dueDate
                  ? formatCycleDueDate(currentCycle.dueDate)
                  : 'No due date set'}
              </strong>
            </div>

            <div className="contribution-summary">
              <div>
                <span>Paid this cycle</span>
                <strong>{formatCents(amountPaid)}</strong>
              </div>

              <div>
                <span>Outstanding</span>
                <strong>
                  {amountOwed === 0
                    ? 'All paid up'
                    : formatCents(amountOwed)}
                </strong>
              </div>
            </div>
          </>
        ) : (
          <p className="empty-state">
            No current contribution cycle
          </p>
        )}
      </section>

      <section className="stokvel-card__section">
        <h3>Next payout</h3>

        <p>
          {nextPayoutDate
            ? formatJohannesburgDate(nextPayoutDate)
            : 'No payout scheduled'}
        </p>
      </section>

      {isTreasurer && (
        <footer className="treasurer-panel">
          <strong>Treasurer tools</strong>
          <p>
            Contribution management will be available
            in a future assignment.
          </p>
        </footer>
      )}
    </article>
  )
}
