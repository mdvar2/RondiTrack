import 'server-only'
import { cookies } from 'next/headers'
import { trace } from '@opentelemetry/api'
import { getSession } from './session'
import { currentTraceId } from './trace'
import { claimDemoFault, DEMO_FAULT_COOKIE, demoFaultConfig, hashKey, isDemoAccount, verifyArmToken } from './demo-fault'

// Thrown by the contribution Server Action instead of returning, so the browser
// sees a rejected action (see demo-fault.ts). Its message never reaches the browser.
export class SimulatedPostCommitResponseFailure extends Error {
  constructor() {
    super('Simulated post-commit response failure (demonstration only). The contribution was committed.')
    this.name = 'SimulatedPostCommitResponseFailure'
  }
}

// Call ONLY after the API answered 2xx for this Idempotency-Key. True means this
// request must not return its result. Logs and span attributes carry no user id,
// email, amount, key or cookie: only the fact and the trace id.
export async function shouldDropCommittedResponse(idempotencyKey: string): Promise<boolean> {
  const config = demoFaultConfig()
  if (!config) return false
  const session = await getSession()
  if (!session || !isDemoAccount(config, session.user.email)) return false
  const jar = await cookies()
  const armed = verifyArmToken(config, jar.get(DEMO_FAULT_COOKIE)?.value, session.user.id)
  if (!armed) return false
  if (!claimDemoFault(armed.nonce, hashKey(idempotencyKey))) {
    // A spent token: this request returns normally (and refreshes the page), so
    // the cookie can go now. Deleting it in the dropped request itself would make
    // Next.js re-render the page with that answer, which a lost answer can't do.
    // "Once" never depends on this deletion: the ledger has already used it up.
    try {
      jar.delete(DEMO_FAULT_COOKIE)
    } catch {
      // Tidiness only.
    }
    return false
  }
  trace.getActiveSpan()?.setAttribute('ronditrack.demo.fault', 'post_commit_response_dropped')
  console.warn(`[demo-fault] Simulated post-commit response failure (demonstration only). Trace ${currentTraceId() ?? 'unavailable'}.`)
  return true
}
