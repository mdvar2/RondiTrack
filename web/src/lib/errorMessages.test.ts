import { describe, expect, it } from 'vitest'
import { parseRetryAfter } from './errorMessages'
describe('Retry-After', () => {
 it('seconds', () => expect(parseRetryAfter('30')).toBe(30))
 it('HTTP date', () => expect(parseRetryAfter('Thu, 01 Jan 1970 00:00:30 GMT', 0)).toBe(30))
 it('past date', () => expect(parseRetryAfter('Thu, 01 Jan 1970 00:00:01 GMT', 20000)).toBe(0))
 it('garbage', () => expect(parseRetryAfter('not a date')).toBeNull())
})
