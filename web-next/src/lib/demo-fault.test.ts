import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'

// --- Next.js and session fakes -------------------------------------------
const jar = new Map<string, string>()
const deleted: string[] = []
vi.mock('next/headers', () => ({
  cookies: vi.fn(async () => ({
    get: (name: string) => (jar.has(name) ? { name, value: jar.get(name) } : undefined),
    delete: (name: string) => { deleted.push(name); jar.delete(name) },
  })),
}))
const revalidatePath = vi.fn()
vi.mock('next/cache', () => ({ revalidatePath: (path: string) => revalidatePath(path) }))
vi.mock('next/navigation', () => ({ redirect: vi.fn((url: string) => { throw new Error(`redirect:${url}`) }) }))
let session: { user: { id: string; name: string; email: string } } | null = null
vi.mock('./session', () => ({ getSession: vi.fn(async () => session) }))
const post = vi.fn()
vi.mock('./proxy', () => ({ refreshedApi: vi.fn(async () => ({ POST: post })) }))
vi.mock('@vercel/otel', () => ({ registerOTel: vi.fn() }))

import {
  claimDemoFault, createArmToken, DEMO_FAULT_COOKIE, demoFaultConfig, hashKey, isDemoAccount,
  readDemoFaultConfig, resetDemoFaultLedgerForTests, verifyArmToken,
} from './demo-fault'
import { shouldDropCommittedResponse, SimulatedPostCommitResponseFailure } from './demo-fault-action'
import { recordContribution } from './contribution-actions'

const SECRET = Buffer.alloc(32, 7).toString('base64')
const DEMO = { id: '11111111-1111-4111-8111-111111111111', name: 'Demo Treasurer', email: 'demo.treasurer@example.test' }
const OTHER = { id: '22222222-2222-4222-8222-222222222222', name: 'Ordinary Member', email: 'member@example.test' }
const VALID_ENV = {
  RONDITRACK_DEMO_FAULTS: 'enabled', RONDITRACK_ENVIRONMENT: 'demo',
  RONDITRACK_DEMO_FAULT_SECRET: SECRET, RONDITRACK_DEMO_FAULT_ACCOUNTS: DEMO.email,
}
const config = () => readDemoFaultConfig(VALID_ENV)!

function useEnv(env: Record<string, string>) {
  for (const [name, value] of Object.entries(env)) vi.stubEnv(name, value)
}

beforeEach(() => {
  jar.clear()
  deleted.length = 0
  session = null
  post.mockReset()
  revalidatePath.mockReset()
  resetDemoFaultLedgerForTests()
})
afterEach(() => vi.unstubAllEnvs())

describe('configuration: only a demo or test environment can switch it on', () => {
  it('is off when the flag is unset (the normal production deployment)', () => {
    expect(readDemoFaultConfig({ RONDITRACK_ENVIRONMENT: 'production' })).toBeNull()
    expect(readDemoFaultConfig({})).toBeNull()
  })

  it.each([
    ['production', { ...VALID_ENV, RONDITRACK_ENVIRONMENT: 'production' }],
    ['an unset environment', { ...VALID_ENV, RONDITRACK_ENVIRONMENT: '' }],
    ['a value other than "enabled"', { ...VALID_ENV, RONDITRACK_DEMO_FAULTS: 'true' }],
    ['a short secret', { ...VALID_ENV, RONDITRACK_DEMO_FAULT_SECRET: Buffer.alloc(8).toString('base64') }],
    ['no allowed accounts', { ...VALID_ENV, RONDITRACK_DEMO_FAULT_ACCOUNTS: '' }],
    ['a real-looking address', { ...VALID_ENV, RONDITRACK_DEMO_FAULT_ACCOUNTS: 'thandiwe@gmail.com' }],
  ])('refuses to start with %s', (_, env) => {
    expect(() => readDemoFaultConfig(env)).toThrow()
    // At request time the same configuration is simply off, never an error.
    expect(demoFaultConfig(env)).toBeNull()
  })

  it('the server refuses to start when production has the flag set', async () => {
    useEnv({ ...VALID_ENV, RONDITRACK_ENVIRONMENT: 'production', NEXT_RUNTIME: 'nodejs' })
    const { register } = await import('../instrumentation')
    await expect(register()).rejects.toThrow('RONDITRACK_ENVIRONMENT is "production"')
  })

  it('the server starts normally without the flag', async () => {
    useEnv({ RONDITRACK_ENVIRONMENT: 'production', NEXT_RUNTIME: 'nodejs' })
    const { register } = await import('../instrumentation')
    await expect(register()).resolves.toBeUndefined()
  })

  it('only listed synthetic accounts are demo accounts', () => {
    expect(isDemoAccount(config(), DEMO.email.toUpperCase())).toBe(true)
    expect(isDemoAccount(config(), OTHER.email)).toBe(false)
    expect(isDemoAccount(config(), undefined)).toBe(false)
  })
})

describe('the arm token', () => {
  it('verifies for the account it was made for, until it expires', () => {
    const token = createArmToken(config(), DEMO.id, 1_000)
    expect(verifyArmToken(config(), token, DEMO.id, 2_000)).not.toBeNull()
    expect(verifyArmToken(config(), token, DEMO.id, 1_000 + 5 * 60 * 1000)).toBeNull()
  })

  it('is refused for another account, if altered, or under another secret', () => {
    const token = createArmToken(config(), DEMO.id)
    expect(verifyArmToken(config(), token, OTHER.id)).toBeNull()
    const [nonce, expires, tag, signature] = token.split('.')
    expect(verifyArmToken(config(), [nonce, String(Number(expires) + 60_000), tag, signature].join('.'), DEMO.id)).toBeNull()
    const other = readDemoFaultConfig({ ...VALID_ENV, RONDITRACK_DEMO_FAULT_SECRET: Buffer.alloc(32, 9).toString('base64') })!
    expect(verifyArmToken(other, token, DEMO.id)).toBeNull()
    expect(verifyArmToken(config(), 'not-a-token', DEMO.id)).toBeNull()
  })
})

describe('one fault per arm token and per Idempotency-Key', () => {
  it('of many concurrent claims for one token, exactly one wins', async () => {
    const results = await Promise.all(Array.from({ length: 50 }, (_, i) =>
      Promise.resolve().then(() => claimDemoFault('nonce-1', hashKey(`key-${i}`)))))
    expect(results.filter(Boolean)).toHaveLength(1)
  })

  it('a key that has faulted never faults again, even with a new token', () => {
    expect(claimDemoFault('nonce-a', hashKey('key-x'))).toBe(true)
    expect(claimDemoFault('nonce-b', hashKey('key-x'))).toBe(false)
  })
})

describe('shouldDropCommittedResponse', () => {
  it('never fires when the environment has not switched it on', async () => {
    session = { user: DEMO }
    jar.set(DEMO_FAULT_COOKIE, createArmToken(config(), DEMO.id))
    expect(await shouldDropCommittedResponse('key-1')).toBe(false)
  })

  it('never fires for an account that is not on the list, even holding a valid token', async () => {
    useEnv(VALID_ENV)
    session = { user: OTHER }
    jar.set(DEMO_FAULT_COOKIE, createArmToken(config(), OTHER.id))
    expect(await shouldDropCommittedResponse('key-1')).toBe(false)
  })

  it('never fires unarmed', async () => {
    useEnv(VALID_ENV)
    session = { user: DEMO }
    expect(await shouldDropCommittedResponse('key-1')).toBe(false)
  })

  it('fires once when armed, disarms, and two concurrent requests cannot both take it', async () => {
    useEnv(VALID_ENV)
    session = { user: DEMO }
    jar.set(DEMO_FAULT_COOKIE, createArmToken(config(), DEMO.id))
    const both = await Promise.all([shouldDropCommittedResponse('key-a'), shouldDropCommittedResponse('key-b')])
    expect(both.filter(Boolean)).toHaveLength(1)
    // The loser is a normal answer, so it tidies the spent cookie away.
    expect(deleted).toEqual([DEMO_FAULT_COOKIE])
  })

  it('a surviving cookie still cannot fire twice: the ledger has used up its token', async () => {
    useEnv(VALID_ENV)
    session = { user: DEMO }
    const token = createArmToken(config(), DEMO.id)
    jar.set(DEMO_FAULT_COOKIE, token)
    expect(await shouldDropCommittedResponse('key-1')).toBe(true)
    jar.set(DEMO_FAULT_COOKIE, token) // as if the deletion never reached the browser
    expect(await shouldDropCommittedResponse('key-1')).toBe(false)
    expect(await shouldDropCommittedResponse('key-2')).toBe(false)
  })
})

describe('recordContribution with the fault armed', () => {
  const input = {
    stokvelId: '33333333-3333-4333-8333-333333333333', cycleId: '44444444-4444-4444-8444-444444444444',
    userId: '55555555-5555-4555-8555-555555555555', amountCents: 50000, idempotencyKey: 'demo-key-0000000001',
  }
  const created = () => ({ data: { id: 'c1' }, response: new Response(null, { status: 201 }) })

  it('rejects only after the API has committed, and the retry with the same key succeeds', async () => {
    useEnv(VALID_ENV)
    session = { user: DEMO }
    jar.set(DEMO_FAULT_COOKIE, createArmToken(config(), DEMO.id))
    post.mockResolvedValue(created())

    await expect(recordContribution(input)).rejects.toBeInstanceOf(SimulatedPostCommitResponseFailure)
    expect(post).toHaveBeenCalledTimes(1) // the API answered 201 before the rejection
    // No cookie change in the lost answer: Next.js would re-render the page for one.
    expect(deleted).toEqual([])
    expect(post.mock.calls[0][1].params.header['Idempotency-Key']).toBe(input.idempotencyKey)
    // Nothing is refreshed for the lost answer, as with a real loss.
    expect(revalidatePath).not.toHaveBeenCalled()

    await expect(recordContribution(input)).resolves.toEqual({ ok: true, amountCents: 50000 })
    expect(post).toHaveBeenCalledTimes(2)
    expect(post.mock.calls[1][1].params.header['Idempotency-Key']).toBe(input.idempotencyKey)
    expect(revalidatePath).toHaveBeenCalledTimes(1)
    expect(deleted).toEqual([DEMO_FAULT_COOKIE]) // the retry tidied the spent cookie

    // A new contribution afterwards is not dropped.
    await expect(recordContribution({ ...input, idempotencyKey: 'demo-key-0000000002' })).resolves.toMatchObject({ ok: true })
  })

  it('does not fire when the API refused the contribution', async () => {
    useEnv(VALID_ENV)
    session = { user: DEMO }
    jar.set(DEMO_FAULT_COOKIE, createArmToken(config(), DEMO.id))
    post.mockResolvedValue({ error: { detail: 'No.' }, response: new Response(null, { status: 422 }) })
    await expect(recordContribution(input)).resolves.toMatchObject({ ok: false, status: 422 })
    expect(deleted).toEqual([])
  })
})
