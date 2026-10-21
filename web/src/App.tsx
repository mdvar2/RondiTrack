import { useEffect, useState } from 'react'
import { StokvelCard } from './components/StokvelCard'
import { ContributionList } from './components/ContributionList'
import {
  sampleStokvels,
  sampleContributions,
} from './data/sample'
import { sampleCycles } from './data/sampleCycles'
import './App.css'

type Theme = 'light' | 'dark' | 'system'

function App() {
  const [theme, setTheme] = useState<Theme>(() => {
    try {
      const saved = localStorage.getItem(
        'ronditrack-theme',
      )

      if (
        saved === 'light' ||
        saved === 'dark' ||
        saved === 'system'
      ) {
        return saved
      }
    } catch {
      return 'system'
    }

    return 'system'
  })

  useEffect(() => {
    const media = window.matchMedia(
      '(prefers-color-scheme: dark)',
    )

    function applyTheme() {
      const dark =
        theme === 'dark' ||
        (theme === 'system' && media.matches)

      document.documentElement.classList.toggle(
        'dark',
        dark,
      )

      document.documentElement.style.colorScheme =
        dark ? 'dark' : 'light'
    }

    applyTheme()
    media.addEventListener('change', applyTheme)

    return () => {
      media.removeEventListener('change', applyTheme)
    }
  }, [theme])

  function changeTheme(value: Theme) {
    setTheme(value)

    try {
      localStorage.setItem(
        'ronditrack-theme',
        value,
      )
    } catch {
      // The selected theme still works for this session.
    }
  }

  return (
    <div className="app-shell">
      <header className="app-header">
        <div className="app-header__inner">
          <div className="brand">
            <span
              className="brand__mark"
              aria-hidden="true"
            >
              R
            </span>
            <strong>RondiTrack</strong>
          </div>

          <label className="theme-control">
            <span>Theme</span>

            <select
              value={theme}
              onChange={(event) =>
                changeTheme(
                  event.target.value as Theme,
                )
              }
            >
              <option value="light">Light</option>
              <option value="dark">Dark</option>
              <option value="system">System</option>
            </select>
          </label>
        </div>
      </header>

      <main className="dashboard">
        <div className="dashboard__heading">
          <h1>My stokvels</h1>

          <div className="dashboard__count">
            <strong>{sampleStokvels.length}</strong>
            <span>Stokvels</span>
          </div>
        </div>

        <p className="mb-4 text-sm text-muted-foreground">
          Demonstration data for Assignment 7.2.
          Values shown are illustrative.
        </p>

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
            <h2 id="contributions-heading">
              Contributions by cycle
            </h2>

            <div className="dashboard__count">
              <strong>
                {sampleContributions.length}
              </strong>
              <span>Contributions</span>
            </div>
          </div>

          <div className="space-y-6">
            {sampleStokvels.map((stokvel) => {
              const cycles = sampleCycles.filter(
                (cycle) =>
                  cycle.stokvelId ===
                  stokvel.stokvelId,
              )

              const contributions =
                sampleContributions.filter(
                  (contribution) =>
                    contribution.stokvelId ===
                    stokvel.stokvelId,
                )

              return (
                <section
                  key={stokvel.stokvelId}
                  aria-label={`${stokvel.stokvelName} contribution history`}
                  className="space-y-3"
                >
                  <h3 className="text-lg font-semibold">
                    {stokvel.stokvelName}
                  </h3>

                  <p className="text-sm text-muted-foreground">
                    Your role: {stokvel.role}
                  </p>

                  <ContributionList
                    cycles={cycles}
                    contributions={contributions}
                  />
                </section>
              )
            })}
          </div>
        </section>
      </main>
    </div>
  )
}

export default App