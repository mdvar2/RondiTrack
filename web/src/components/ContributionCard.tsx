import type {
  ContributionResponse,
  ContributionWithMemberResponse,
} from '../api/types'
import { formatCents } from '../lib/money'
import { formatJohannesburgDateTime } from '../lib/dates'

type Contribution = ContributionResponse | ContributionWithMemberResponse

type ContributionCardProps = {
  contribution: Contribution
  cycleName: string
}

export function ContributionCard({
  contribution,
  cycleName,
}: ContributionCardProps) {
  const contributorName =
    'userName' in contribution ? contribution.userName : null

  return (
    <article className="contribution-card">
      <div className="contribution-card__header">
        <div>
          <p className="contribution-card__eyebrow">Contribution</p>
          <h3>{formatCents(contribution.amount)}</h3>
        </div>

        <span className="contribution-card__status">
          Recorded
        </span>
      </div>

      <dl className="contribution-card__details">
        <div className="detail-row">
          <dt>Cycle</dt>
          <dd>{cycleName}</dd>
        </div>

        <div className="detail-row">
          <dt>Recorded</dt>
          <dd>
            <time dateTime={contribution.recordedAtUtc}>
              {formatJohannesburgDateTime(contribution.recordedAtUtc)}
            </time>
          </dd>
        </div>

        {contributorName && (
          <div className="detail-row">
            <dt>Contributor</dt>
            <dd>{contributorName}</dd>
          </div>
        )}
      </dl>
    </article>
  )
}
