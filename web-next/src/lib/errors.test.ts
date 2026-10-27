import { describe, expect, it } from 'vitest'
import { ApiError, describeError, fromDigest } from './errors'

describe('ApiError digest', () => {
  it('carries only the status and correlation id', () => {
    const error = new ApiError(500, { title: 'Internal Server Error', detail: 'stack here', correlationId: 'abc123' })

    expect(error.digest).toBe('RONDITRACK_API;500;abc123')
    expect(fromDigest(error.digest)).toEqual({ status: 500, correlationId: 'abc123' })
  })

  it('ignores digests Next.js generated for other errors', () => {
    expect(fromDigest('2817390421')).toBeNull()
    expect(fromDigest(undefined)).toBeNull()
  })
})

describe('describeError', () => {
  it('shows the 7.3 message and reference for a 403 and a 500', () => {
    expect(describeError({ status: 403, correlationId: 'r1' })).toEqual({
      message: "You don't have permission to do that.", correlationId: 'r1',
    })
    expect(describeError({ status: 500, correlationId: 'r2' })).toEqual({
      message: 'Something went wrong. Please try again.', correlationId: 'r2',
    })
  })

  it('gives a wrong password the 7.3 message with its correlation id', () => {
    expect(describeError({ status: 401, problem: { correlationId: 'c9' } })).toEqual({
      message: 'Email or password is incorrect.', correlationId: 'c9',
    })
  })

  it('reads Retry-After for a 429', () => {
    expect(describeError({ status: 429, retryAfter: '60' }).retryAfter).toBe(60)
  })
})
