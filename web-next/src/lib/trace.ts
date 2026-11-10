// The failure reference shown to a person is a trace id. When the API answered,
// it is the API's correlationId (the same trace, continued from this server).
// When it never answered, it is this server's own trace id, which the trace
// backend has, because this server's spans were exported even though the API's weren't.
import { isSpanContextValid, trace } from '@opentelemetry/api'

export function currentTraceId(): string | undefined {
  const context = trace.getActiveSpan()?.spanContext()
  return context && isSpanContextValid(context) ? context.traceId : undefined
}

export const UNREACHABLE_DETAIL = "RondiTrack's service could not be reached. Please try again in a moment."

// The answer we give ourselves when the API gave none: the same problem shape
// the API uses, so every caller handles it like any other 503.
export function unreachableResponse(correlationId: string | undefined): Response {
  return new Response(
    JSON.stringify({ type: 'about:blank', title: 'Service Unavailable', status: 503, detail: UNREACHABLE_DETAIL, correlationId }),
    { status: 503, headers: { 'Content-Type': 'application/problem+json', 'Cache-Control': 'no-store' } },
  )
}

type Fetch = (input: RequestInfo | URL, init?: RequestInit) => Promise<Response>

// Wraps fetch so a dropped connection becomes a 503 problem carrying the trace id.
// globalThis.fetch is read on every call: the tracing instrumentation patches it.
export function withTraceReference(
  fetchImpl: Fetch = (input, init) => globalThis.fetch(input, init),
  traceId: () => string | undefined = currentTraceId,
): Fetch {
  return async (input, init) => {
    try {
      return await fetchImpl(input, init)
    } catch (error) {
      // A caller that aborted on purpose still sees its own abort.
      if (error instanceof Error && error.name === 'AbortError') throw error
      return unreachableResponse(traceId())
    }
  }
}

export const apiFetch = withTraceReference()

// openapi-fetch's client calls fetch with a single Request.
export const apiClientFetch = (request: Request) => apiFetch(request)
