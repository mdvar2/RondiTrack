import type { MetadataRoute } from 'next'

// Allows everything on purpose. Signed-in pages say noindex in their own
// <head>, and the invite page does too; a crawler that robots.txt kept out
// would never fetch them, never see the noindex, and could still list the
// bare URL. /bff and /api answer with an X-Robots-Tag header instead
// (next.config.ts), and a 401 for anyone without a session.
export default function robots(): MetadataRoute.Robots {
  return { rules: [{ userAgent: '*', allow: '/' }] }
}
