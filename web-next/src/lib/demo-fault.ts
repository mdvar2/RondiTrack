import 'server-only'
// Simulated post-commit response failure, for demonstrations and tests only
// (Assignment 9.3 Part 2). It is NOT a network disconnection: after the API has
// answered 2xx (the contribution is committed), the Server Action throws instead
// of returning, so the browser handles it exactly like a dropped connection.
// The existing form keeps its Idempotency-Key, the person retries, and the API
// replays the stored answer. The API and its financial logic are untouched.
//
// It can only fire when ALL of these hold, checked on the server every time:
//  1. RONDITRACK_DEMO_FAULTS=enabled
//  2. RONDITRACK_ENVIRONMENT is "demo" or "test"
//  3. the signed-in account's email is in RONDITRACK_DEMO_FAULT_ACCOUNTS, which
//     only accepts reserved example/test domains (no real person's address)
//  4. a signed, unexpired arm cookie bound to that account is present
//     (set by src/proxy.ts for ?demoFault=drop-next, after the same checks)
// and it fires at most once per arm token and once per Idempotency-Key: a
// synchronous claim in this process's ledger, so concurrent requests can't both
// take it, and a retry with the same key never faults. The demo environment must
// therefore run ONE Next.js server process.
import { createHash, createHmac, randomBytes, timingSafeEqual } from 'node:crypto'

export const DEMO_FAULT_PARAM = 'demoFault'
export const DEMO_FAULT_PARAM_VALUE = 'drop-next'
export const DEMO_FAULT_COOKIE = 'rt_demo_fault'
export const ARM_TTL_MS = 5 * 60 * 1000
const KEY_TTL_MS = 60 * 60 * 1000
const ALLOWED_ENVIRONMENTS = new Set(['demo', 'test'])
// RFC 2606 / RFC 6761 reserved names: these can never be a real member's address.
const SYNTHETIC_EMAIL = /@(?:[a-z0-9-]+\.)*(?:example\.(?:test|com|org|net)|example|test|invalid)$/i

export type DemoFaultConfig = { secret: Buffer; accounts: ReadonlySet<string> }
type Env = Record<string, string | undefined>

// Strict: throws on any misconfiguration. Used at startup (instrumentation.ts).
export function readDemoFaultConfig(env: Env = process.env): DemoFaultConfig | null {
  const flag = env.RONDITRACK_DEMO_FAULTS
  if (flag === undefined || flag === '') return null
  if (flag !== 'enabled') throw new Error('RONDITRACK_DEMO_FAULTS must be unset or "enabled".')
  const environment = env.RONDITRACK_ENVIRONMENT ?? ''
  if (!ALLOWED_ENVIRONMENTS.has(environment)) {
    throw new Error(`Demo faults are refused: RONDITRACK_ENVIRONMENT is "${environment || 'unset'}", not demo or test.`)
  }
  const secret = Buffer.from(env.RONDITRACK_DEMO_FAULT_SECRET ?? '', 'base64')
  if (secret.length < 32) throw new Error('RONDITRACK_DEMO_FAULT_SECRET must be at least 32 random bytes, base64-encoded.')
  const accounts = new Set((env.RONDITRACK_DEMO_FAULT_ACCOUNTS ?? '').split(',').map((item) => item.trim().toLowerCase()).filter(Boolean))
  if (accounts.size === 0) throw new Error('RONDITRACK_DEMO_FAULT_ACCOUNTS must list at least one synthetic demo account.')
  for (const account of accounts) {
    if (!SYNTHETIC_EMAIL.test(account)) throw new Error('RONDITRACK_DEMO_FAULT_ACCOUNTS may only contain reserved example/test addresses.')
  }
  return { secret, accounts }
}

// Lenient: at request time a bad configuration means "off", never an error,
// so a misconfigured deployment can't fail real contributions.
export function demoFaultConfig(env: Env = process.env): DemoFaultConfig | null {
  try {
    return readDemoFaultConfig(env)
  } catch {
    return null
  }
}

export function isDemoAccount(config: DemoFaultConfig, email: string | undefined): boolean {
  return !!email && config.accounts.has(email.trim().toLowerCase())
}

const mac = (secret: Buffer, text: string) => createHmac('sha256', secret).update(text).digest('base64url')
const userTag = (secret: Buffer, userId: string) => mac(secret, `user:${userId}`).slice(0, 22)

export function createArmToken(config: DemoFaultConfig, userId: string, now = Date.now()): string {
  const body = `${randomBytes(16).toString('base64url')}.${now + ARM_TTL_MS}.${userTag(config.secret, userId)}`
  return `${body}.${mac(config.secret, body)}`
}

export function verifyArmToken(config: DemoFaultConfig, token: string | undefined, userId: string, now = Date.now()): { nonce: string } | null {
  const parts = token?.split('.') ?? []
  if (parts.length !== 4) return null
  const [nonce, expires, tag, signature] = parts
  const expected = Buffer.from(mac(config.secret, `${nonce}.${expires}.${tag}`))
  const given = Buffer.from(signature)
  if (expected.length !== given.length || !timingSafeEqual(expected, given)) return null
  if (!/^\d+$/.test(expires) || Number(expires) <= now) return null
  if (tag !== userTag(config.secret, userId)) return null
  return { nonce }
}

export const hashKey = (idempotencyKey: string) => createHash('sha256').update(idempotencyKey).digest('hex')

// One ledger per server process, shared by every module copy in it.
type Ledger = { nonces: Map<string, number>; keys: Map<string, number> }
const LEDGER = Symbol.for('ronditrack.demoFaultLedger')
function ledger(): Ledger {
  const holder = globalThis as typeof globalThis & { [LEDGER]?: Ledger }
  holder[LEDGER] ??= { nonces: new Map(), keys: new Map() }
  return holder[LEDGER]
}

// Synchronous on purpose: JavaScript runs it to completion, so of any number of
// concurrent requests exactly one can claim a given arm token or key.
export function claimDemoFault(nonce: string, keyHash: string, now = Date.now()): boolean {
  const { nonces, keys } = ledger()
  for (const [item, expires] of nonces) if (expires <= now) nonces.delete(item)
  for (const [item, expires] of keys) if (expires <= now) keys.delete(item)
  if (nonces.has(nonce) || keys.has(keyHash)) return false
  nonces.set(nonce, now + ARM_TTL_MS)
  keys.set(keyHash, now + KEY_TTL_MS)
  return true
}

export function resetDemoFaultLedgerForTests() {
  ledger().nonces.clear()
  ledger().keys.clear()
}
