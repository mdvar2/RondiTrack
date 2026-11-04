import { NextResponse } from 'next/server'
import { baseUrl } from '@/lib/proxy'
import { getSession } from '@/lib/session'

type RouteContext = { params: Promise<{ contributionId: string }> }

export async function GET(request: Request, context: RouteContext) {
  return forward(request, context, 'GET')
}

export async function POST(request: Request, context: RouteContext) {
  return forward(request, context, 'POST')
}

export async function DELETE(request: Request, context: RouteContext) {
  return forward(request, context, 'DELETE')
}

async function forward(request: Request, context: RouteContext, method: string) {
  const session = await getSession()
  if (!session) return NextResponse.json({ title: 'Authentication required.' }, { status: 401 })
  const { contributionId } = await context.params
  const fetchOptions = {
    method,
    body: method === 'POST' ? request.body : undefined,
    duplex: method === 'POST' ? 'half' : undefined,
    headers: {
      Authorization: `Bearer ${session.accessToken}`,
      ...(method === 'POST' ? { 'Content-Type': request.headers.get('content-type') ?? '' } : {}),
    },
    cache: 'no-store',
  } as RequestInit & { duplex?: 'half' }
  const response = await fetch(`${baseUrl()}/api/v1/contributions/${encodeURIComponent(contributionId)}/proof${method === 'GET' ? '/download' : ''}`, fetchOptions)
  const headers = new Headers()
  for (const name of ['content-type', 'content-disposition', 'x-content-type-options', 'cache-control', 'etag']) {
    const value = response.headers.get(name)
    if (value) headers.set(name, value)
  }
  if (method === 'GET') {
    headers.set('X-Content-Type-Options', 'nosniff')
    headers.set('Cache-Control', 'private, no-store')
  }
  return new NextResponse(response.body, { status: response.status, headers })
}
