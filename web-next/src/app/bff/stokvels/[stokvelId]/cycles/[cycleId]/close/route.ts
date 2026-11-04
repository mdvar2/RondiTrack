import { NextResponse } from 'next/server'
import { baseUrl } from '@/lib/proxy'
import { getSession } from '@/lib/session'

type Context = { params: Promise<{ stokvelId: string; cycleId: string }> }

export async function GET(request: Request, context: Context) {
  return forward(request, context, 'GET')
}

export async function POST(request: Request, context: Context) {
  return forward(request, context, 'POST')
}

async function forward(request: Request, context: Context, method: string) {
  const session = await getSession()
  if (!session) return NextResponse.json({ title: 'Authentication required.' }, { status: 401 })
  const { stokvelId, cycleId } = await context.params
  const response = await fetch(`${baseUrl()}/api/v1/stokvels/${stokvelId}/cycles/${cycleId}/close${method === 'GET' ? '/preview' : ''}`, {
    method,
    body: method === 'POST' ? await request.arrayBuffer() : undefined,
    headers: {
      Authorization: `Bearer ${session.accessToken}`,
      ...(method === 'POST' ? { 'Content-Type': 'application/json', 'Idempotency-Key': request.headers.get('Idempotency-Key') ?? '' } : {}),
    },
    cache: 'no-store',
  })
  const headers = new Headers({ 'Content-Type': response.headers.get('content-type') ?? 'application/json' })
  return new NextResponse(response.body, { status: response.status, headers })
}
