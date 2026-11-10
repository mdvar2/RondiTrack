// Where to send a person back to after signing in again (Assignment 9.3 Part 3, 401).
//
// src/proxy.ts sets RETURN_PATH_HEADER on every request it lets through,
// overwriting any value the client sent, so Server Components can trust it.
// Only paths on this site pass safeReturnPath: "/x" yes; "//evil", "/\evil",
// "https://evil" and anything else no.
export const RETURN_PATH_HEADER = 'x-ronditrack-return-path'
export const DEFAULT_RETURN_PATH = '/dashboard/member'

export function safeReturnPath(path: string | null | undefined): string {
  return typeof path === 'string' && path.startsWith('/') && !path.startsWith('//') && !path.startsWith('/\\')
    && !/[\u0000-\u001f]/.test(path)
    ? path
    : DEFAULT_RETURN_PATH
}

// The page as the person asked for it, without Next.js's internal RSC marker.
export function returnPathOf(url: URL): string {
  const clean = new URL(url)
  clean.searchParams.delete('_rsc')
  return safeReturnPath(`${clean.pathname}${clean.search}`)
}

export function expiredLoginUrl(returnPath: string | null | undefined): string {
  return `/login?expired=1&next=${encodeURIComponent(safeReturnPath(returnPath))}`
}
