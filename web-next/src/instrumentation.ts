import { registerOTel } from '@vercel/otel'
import { trace } from '@opentelemetry/api'

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

// An error in a page or route that the API didn't cause has no API reference;
// error.tsx shows Next's digest. Recording the digest on the request's span
// makes that digest searchable in the trace backend (tag ronditrack.error.digest).
// Only the digest and the error's type are recorded: never its message.
export function onRequestError(error: unknown) {
  const span = trace.getActiveSpan()
  if (!span) return
  const digest = typeof error === 'object' && error !== null && 'digest' in error ? String((error as { digest: unknown }).digest) : undefined
  if (digest) span.setAttribute('ronditrack.error.digest', digest)
  span.setAttribute('ronditrack.error.type', error instanceof Error ? error.name : typeof error)
}
