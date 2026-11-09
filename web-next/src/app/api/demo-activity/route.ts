import { NextResponse } from 'next/server'
import { getSession } from '@/lib/session'
import { loadActivityPage, loadHomeStokvels } from '@/lib/activity'

export async function GET(request: Request) {
  const session = await getSession()
  if (!session) {
    return NextResponse.json({ title: 'Authentication required.' }, { status: 401 })
  }

  const { searchParams } = new URL(request.url)
  const status = searchParams.get('status')
  const page = await loadActivityPage(await loadHomeStokvels(), {
    q: searchParams.get('q') ?? '',
    status: status === 'paid' || status === 'pending' ? status : 'all',
    from: searchParams.get('from') ?? '',
    to: searchParams.get('to') ?? '',
    page: Number(searchParams.get('page') ?? '1'),
  })
  return NextResponse.json(page, { headers: { 'Cache-Control': 'private, no-store' } })
}
