import { describe, expect, it, vi } from 'vitest'

// Next's redirect() and notFound() throw to stop rendering; mimic that.
vi.mock('next/navigation', () => ({
  redirect: vi.fn((url: string) => { throw new Error(`redirect:${url}`) }),
  notFound: vi.fn(() => { throw new Error('notFound') }),
}))
vi.mock('next/headers', () => ({ cookies: vi.fn() }))

import { readOrFail } from './api'
import { ApiError } from './errors'

const result = (status: number, data?: unknown, error?: unknown) =>
  ({ data, error, response: new Response(null, { status }) })

describe('readOrFail', () => {
  it('returns the data of a successful response', () => {
    expect(readOrFail(result(200, { stokvels: [] }))).toEqual({ stokvels: [] })
  })

  it('sends a 401 (expired token) to /login?expired=1', () => {
    expect(() => readOrFail(result(401, undefined, {}))).toThrow('redirect:/login?expired=1')
  })

  it('sends a 404 to the not-found page', () => {
    expect(() => readOrFail(result(404, undefined, {}))).toThrow('notFound')
  })

  it('throws an ApiError carrying the correlation id for anything else', () => {
    try {
      readOrFail(result(500, undefined, { title: 'Internal Server Error', correlationId: 'abc' }))
      expect.unreachable()
    } catch (error) {
      expect(error).toBeInstanceOf(ApiError)
      expect((error as ApiError).status).toBe(500)
      expect((error as ApiError).digest).toBe('RONDITRACK_API;500;abc')
    }
  })
})
