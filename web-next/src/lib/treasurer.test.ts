import { describe, expect, it } from 'vitest'
import { stokvelsWhereTreasurer } from './treasurer'

const stokvel = (stokvelName: string, role: string) => ({ stokvelId: stokvelName, stokvelName, role })

describe('stokvelsWhereTreasurer', () => {
  it('keeps only the stokvels where the person is Treasurer, in order', () => {
    const list = [
      stokvel('Family', 'Treasurer'),
      stokvel('Work', 'Member'),
      stokvel('Church', 'Chairperson'),
      stokvel('Burial', 'Treasurer'),
    ]

    expect(stokvelsWhereTreasurer(list).map((s) => s.stokvelName)).toEqual(['Family', 'Burial'])
  })

  it('returns an empty list when the person is Treasurer nowhere', () => {
    const list = [stokvel('Work', 'Member'), stokvel('Church', 'Chairperson')]

    expect(stokvelsWhereTreasurer(list)).toEqual([])
  })

  it('returns an empty list for an empty list', () => {
    expect(stokvelsWhereTreasurer([])).toEqual([])
  })

  it('matches the role exactly, as the API spells it', () => {
    expect(stokvelsWhereTreasurer([stokvel('Lower', 'treasurer')])).toEqual([])
  })
})
