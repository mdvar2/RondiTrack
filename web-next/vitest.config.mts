import { fileURLToPath } from 'node:url'
import { defineConfig } from 'vitest/config'

// No Vite plugins: Vitest resolves the @/ paths below and compiles JSX itself.
export default defineConfig({
  resolve: {
    alias: {
      '@': fileURLToPath(new URL('./src', import.meta.url)),
      // The real package throws outside a React Server environment. In tests
      // the server modules run in plain Node, so the guard becomes a no-op.
      'server-only': fileURLToPath(new URL('./src/test/server-only.ts', import.meta.url)),
    },
  },
  test: {
    projects: [
      // Pure rules: money, dates, filters, roles. Plain Node, milliseconds each.
      { extends: true, test: { name: 'rules', environment: 'node', include: ['src/**/*.test.ts'] } },
      // Client Components in jsdom, with React Testing Library and MSW.
      {
        extends: true,
        test: { name: 'components', environment: 'jsdom', include: ['src/**/*.test.tsx'], setupFiles: ['./src/test/setup.ts'] },
      },
    ],
  },
})
