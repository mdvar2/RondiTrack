
import { StokvelCard } from './components/StokvelCard'
import { ContributionCard } from './components/ContributionCard'
import {
  sampleStokvels,
  sampleContributions,
} from './data/sample'
import './App.css'

function App() {
  return (
    <div className="app-shell">
      <header className="app-header">
        <div className="app-header__inner">
          <div className="brand">
            <span className="brand__mark" aria-hidden="true">
              R
            </span>

            <div>
              <strong>RondiTrack</strong>
              <span>Community savings, made simple</span>
            </div>
          </div>

          <span className="demo-badge">
            Sample data preview
          </span>
        </div>
      </header>

      <main className="dashboard">
        <div className="dashboard__heading">
          <div>
            <p className="eyebrow">MY SAVINGS GROUPS</p>
            <h1>Stokvel overview</h1>
            <p className="dashboard__description">
              View your contribution cycles, payments and
              upcoming payouts in one place.
            </p>
          </div>

          <div className="dashboard__count">
            <strong>{sampleStokvels.length}</strong>
            <span>Stokvels</span>
          </div>
        </div>

        <section
          className="stokvel-grid"
          aria-label="Your stokvels"
        >
          {sampleStokvels.map((stokvel) => (
            <StokvelCard
              key={stokvel.stokvelId}
              stokvel={stokvel}
            />
          ))}
        </section>

        <section
          className="contributions-section"
          aria-labelledby="contributions-heading"
        >
          <div className="contributions-section__heading">
            <div>
              <p className="eyebrow">
                PAYMENT HISTORY
              </p>

              <h2 id="contributions-heading">
                Recent contributions
              </h2>

              <p className="dashboard__description">
                A preview of recorded contributions
                across your savings groups.
              </p>
            </div>

            <div className="dashboard__count">
              <strong>{sampleContributions.length}</strong>
              <span>Contributions</span>
            </div>
          </div>

          <div className="contribution-grid">
            {sampleContributions.map((contribution) => (
              <ContributionCard
                key={contribution.id}
                contribution={contribution}
                cycleName={sampleStokvels.find((stokvel) => stokvel.stokvelId === contribution.stokvelId)?.currentCycle?.period ?? 'Cycle unavailable'}
              />
            ))}
          </div>
        </section>

        <p className="dashboard__notice">
          Demonstration only — this preview uses typed
          sample data and does not make API requests.
        </p>
      </main>
    </div>
  )
}

export default App
