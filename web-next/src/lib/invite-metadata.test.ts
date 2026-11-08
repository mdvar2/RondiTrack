import { describe, expect, it } from 'vitest'
import { inviteMetadata } from './invite-metadata'

describe('invite link preview metadata', () => {
  it.each(['valid', 'expired', 'unknown', 'unavailable', null] as const)('for a %s invite is never indexed', (status) => {
    expect(inviteMetadata(status).robots).toEqual({ index: false, follow: false })
  })

  it('tells a valid invite from an expired one', () => {
    expect(inviteMetadata('valid').title).toBe('You’re invited to join a stokvel')
    expect(inviteMetadata('expired').title).toBe('This stokvel invite has expired')
  })

  it('has no input that could carry a name or an amount', () => {
    // The function takes only the status. This keeps it that way: a second
    // parameter (a preview with a name in it) fails the type check and this test.
    expect(inviteMetadata.length).toBe(1)
    const text = JSON.stringify(inviteMetadata('valid'))
    expect(text).not.toMatch(/R\s?\d|\d+,\d{2}/)
  })
})
