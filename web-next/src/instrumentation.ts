import { registerOTel } from '@vercel/otel'
import { isSpanContextValid, trace } from '@opentelemetry/api'

// Next.js runs register() once per server instance, before any request.
// Spans are exported over OTLP when OTEL_EXPORTER_OTLP_ENDPOINT is set
// (local Jaeger: http://localhost:4318); otherwise they are still created, so
// the trace id still exists and is still sent to the API and shown as the reference.
export async function register() {
  // Refuse to start with demo faults switched on outside a demo/test environment.
  if (process.env.NEXT_RUNTIME === 'nodejs') {
    const { readDemoFaultConfig } = await import('./lib/demo-fault')
    readDemoFaultConfig()
  }
  const api = process.env.API_BASE_URL?.replace(/\/$/, '')
  registerOTel({
    serviceName: process.env.OTEL_SERVICE_NAME ?? 'ronditrack-web',
    instrumentationConfig: {
      fetch: {
        // traceparent goes to our own API and nowhere else. The trailing slash
        // stops http://api:8080 also matching http://api:80801.
        propagateContextUrls: api ? [`${api}/`] : [],
      },
    },
  })
}

// An error the API didn't cause (a bug in a page or route) has no API
// reference: error.tsx shows Next's digest. To make that digest lead to a trace:
//  - it is recorded on the active span as ronditrack.error.digest (searchable
//    by tag in the trace backend), when a span is active;
//  - one log line pairs it with the trace id, the route template and the kind
//    of request. Never the error's message, the concrete path or any header.
export function onRequestError(
  error: unknown,
  _request: Readonly<{ path: string; method: string }>,
  context: Readonly<{ routePath: string; routeType: string }>,
) {
  const digest = typeof error === 'object' && error !== null && 'digest' in error ? String((error as { digest: unknown }).digest) : undefined
  if (!digest) return
  const span = trace.getActiveSpan()
  const spanContext = span?.spanContext()
  const traceId = spanContext && isSpanContextValid(spanContext) ? spanContext.traceId : undefined
  span?.setAttribute('ronditrack.error.digest', digest)
  span?.setAttribute('ronditrack.error.type', error instanceof Error ? error.name : typeof error)
  console.error(`[request-error] digest ${digest} trace ${traceId ?? 'unavailable'} route ${context.routePath} type ${context.routeType}`)
}
