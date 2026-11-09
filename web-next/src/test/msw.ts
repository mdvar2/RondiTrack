import { setupServer } from 'msw/node'

// No default handlers: each test says exactly which requests it expects.
export const server = setupServer()
