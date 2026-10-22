
import { useEffect, useState } from 'react'
import { useQueryClient } from '@tanstack/react-query'

import {
  login,
  logout,
  onSignedOut,
  refreshAccessToken,
} from './auth/session'

import {
  useHome,
  useCycles,
  useContributions,
} from './api/queries'

import { ErrorView } from './components/ErrorView'
import { StokvelCard } from './components/StokvelCard'
import { ContributionList } from './components/ContributionList'
import { describeError } from './lib/errorMessages'

import './App.css'

// --------------------------------------------------
// Theme configuration (Assignment 7.2)
// --------------------------------------------------

type Theme = 'light' | 'dark' | 'system'

function readTheme(): Theme {
  try {
    const saved = localStorage.getItem('ronditrack-theme')

    if (
      saved === 'light' ||
      saved === 'dark' ||
      saved === 'system'
    ) {
      return saved
    }
  } catch {
    // Local storage may be unavailable.
  }

  return 'system'
}

// --------------------------------------------------
// Live dashboard (Assignment 7.3)
// --------------------------------------------------

function Dashboard({ onExit }: { onExit: () => void }) {
  const qc = useQueryClient()
  const home = useHome()

  const [selected, setSelected] = useState('')
  const [refreshing, setRefreshing] = useState(false)

  const stokvels = home.data?.stokvels ?? []

  // Ensure that the selected stokvel belongs to
  // the authenticated user's home response.
  const id = stokvels.some(
    (s) => s.stokvelId === selected
  )
    ? selected
    : (stokvels[0]?.stokvelId ?? '')

  const cycles = useCycles(id)
  const contributions = useContributions(id)

  const active = stokvels.find(
    (s) => s.stokvelId === id
  )

  // Refresh active queries without signing out.
  async function refreshDashboard() {
    if (refreshing) return

    setRefreshing(true)

    try {
      await qc.invalidateQueries()
    } finally {
      setRefreshing(false)
    }
  }

  return (
    <main className="dashboard space-y-6">
      {/* Dashboard heading and actions */}
      <div className="flex flex-wrap justify-between gap-3">
        <h1>My stokvels</h1>

        <div className="flex flex-wrap gap-2">
          <button
            type="button"
            className="rounded border px-3 py-2"
            onClick={() => void refreshDashboard()}
            disabled={refreshing}
          >
            {refreshing
              ? 'Refreshing...'
              : 'Refresh dashboard'}
          </button>

          <button
            type="button"
            className="rounded border px-3 py-2"
            onClick={onExit}
          >
            Log out
          </button>
        </div>
      </div>

      {/* Home loading state */}
      {home.isPending && (
        <p role="status">
          Loading your stokvels…
        </p>
      )}

      {/* Home error state */}
      {home.isError && (
        <ErrorView
          error={home.error}
          retry={() => void home.refetch()}
        />
      )}

      {/* Empty stokvel state */}
      {home.isSuccess && stokvels.length === 0 && (
        <p>
          No stokvels are linked to your account.
        </p>
      )}

      {/* Live stokvel selection */}
      {stokvels.length > 0 && (
        <>
          <label className="block space-y-2">
            <span className="block font-medium">
              Choose stokvel
            </span>

            <select
              className="w-full rounded border bg-background p-3"
              value={id}
              onChange={(event) =>
                setSelected(event.target.value)
              }
            >
              {stokvels.map((stokvel) => (
                <option
                  key={stokvel.stokvelId}
                  value={stokvel.stokvelId}
                >
                  {stokvel.stokvelName}
                </option>
              ))}
            </select>
          </label>

          {/* Selected stokvel summary */}
          {active && (
            <StokvelCard stokvel={active} />
          )}

          {/* Live contributions */}
          <section
            key={id}
            className="contributions-section space-y-4"
            aria-label="Selected stokvel contributions"
          >
            <h2>
              Contributions by cycle — {active?.stokvelName}
            </h2>

            {/* Loading state */}
            {(cycles.isPending ||
              contributions.isPending) && (
              <p role="status">
                Loading contributions for this stokvel…
              </p>
            )}

            {/* Cycle errors */}
            {cycles.isError && (
              <ErrorView
                error={cycles.error}
                retry={() => void cycles.refetch()}
              />
            )}

            {/* Contribution errors */}
            {contributions.isError && (
              <ErrorView
                error={contributions.error}
                retry={() =>
                  void contributions.refetch()
                }
              />
            )}

            {/* Successful contribution data */}
            {cycles.isSuccess &&
              contributions.isSuccess && (
                <>
                  <ContributionList
                    cycles={cycles.data}
                    contributions={
                      contributions.contributions
                    }
                  />

                  {/* Load additional pages */}
                  {contributions.hasNextPage && (
                    <button
                      type="button"
                      className="rounded border px-3 py-2"
                      disabled={
                        contributions.isFetchingNextPage
                      }
                      onClick={() =>
                        void contributions.fetchNextPage()
                      }
                    >
                      {contributions.isFetchingNextPage
                        ? 'Loading…'
                        : 'Load more contributions'}
                    </button>
                  )}
                </>
              )}
          </section>
        </>
      )}
    </main>
  )
}

// --------------------------------------------------
// Application root
// --------------------------------------------------

export default function App() {
  const qc = useQueryClient()

  // Theme state
  const [theme, setTheme] = useState<Theme>(readTheme)

  // Authentication state
  const [authenticated, setAuthenticated] =
    useState<boolean | null>(null)

  const [formError, setFormError] =
    useState<unknown>(null)

  const [busy, setBusy] = useState(false)

  // ------------------------------------------------
  // Apply light, dark, or system theme
  // ------------------------------------------------

  useEffect(() => {
    const media = window.matchMedia(
      '(prefers-color-scheme: dark)'
    )

    function applyTheme() {
      const dark =
        theme === 'dark' ||
        (theme === 'system' && media.matches)

      document.documentElement.classList.toggle(
        'dark',
        dark
      )

      document.documentElement.style.colorScheme =
        dark ? 'dark' : 'light'
    }

    applyTheme()

    media.addEventListener('change', applyTheme)

    return () => {
      media.removeEventListener(
        'change',
        applyTheme
      )
    }
  }, [theme])

  function changeTheme(value: Theme) {
    setTheme(value)

    try {
      localStorage.setItem(
        'ronditrack-theme',
        value
      )
    } catch {
      // Theme still works for this session.
    }
  }

  // ------------------------------------------------
  // Restore existing authenticated session
  // ------------------------------------------------

  useEffect(() => {
    let active = true

    const off = onSignedOut(() => {
      qc.clear()

      if (active) {
        setAuthenticated(false)
      }
    })

    void refreshAccessToken().then(
      () => {
        if (active) {
          setAuthenticated(true)
        }
      },
      () => {
        if (active) {
          setAuthenticated(false)
        }
      }
    )

    return () => {
      active = false
      off()
    }
  }, [qc])

  // ------------------------------------------------
  // Sign in
  // ------------------------------------------------

  async function signIn(
    event: React.FormEvent<HTMLFormElement>
  ) {
    event.preventDefault()

    setBusy(true)
    setFormError(null)

    const form = new FormData(event.currentTarget)

    try {
      qc.clear()

      await login(
        String(form.get('email')),
        String(form.get('password'))
      )

      setAuthenticated(true)
    } catch (error) {
      setFormError(error)
    } finally {
      setBusy(false)
    }
  }

  // ------------------------------------------------
  // Sign out
  // ------------------------------------------------

  async function signOut() {
    qc.clear()
    setAuthenticated(false)

    await logout()
  }

  // ------------------------------------------------
  // Application interface
  // ------------------------------------------------

  return (
    <div className="app-shell">
      {/* Header and theme selector */}
      <header className="app-header">
        <div className="app-header__inner">
          <div className="brand">
            <span className="brand__mark">
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
                  event.target.value as Theme
                )
              }
            >
              <option value="light">
                Light
              </option>

              <option value="dark">
                Dark
              </option>

              <option value="system">
                System
              </option>
            </select>
          </label>
        </div>
      </header>

      {/* Session restoration */}
      {authenticated === null ? (
        <main
          className="dashboard"
          role="status"
        >
          Checking your session…
        </main>
      ) : authenticated ? (
        // Authenticated dashboard
        <Dashboard
          onExit={() => void signOut()}
        />
      ) : (
        // Sign-in form
        <main className="dashboard">
          <form
            onSubmit={(event) => void signIn(event)}
            className="mx-auto max-w-md space-y-4 rounded-lg border p-6"
          >
            <h1 className="text-2xl font-bold">
              Sign in
            </h1>

            <label className="block">
              Email

              <input
                name="email"
                type="email"
                autoComplete="username"
                required
                className="mt-1 w-full rounded border bg-background p-3"
              />
            </label>

            <label className="block">
              Password

              <input
                name="password"
                type="password"
                autoComplete="current-password"
                required
                className="mt-1 w-full rounded border bg-background p-3"
              />
            </label>

            {/* Safe rendering of unknown errors */}
            {formError !== null && (
              <p
                role="alert"
                className="text-destructive"
              >
                {describeError(formError).message}
              </p>
            )}

            <button
              disabled={busy}
              className="rounded border px-4 py-3"
              type="submit"
            >
              {busy
                ? 'Signing in…'
                : 'Sign in'}
            </button>
          </form>
        </main>
      )}
    </div>
  )
}
