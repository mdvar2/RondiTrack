import type { Metadata } from 'next'
import { Geist } from 'next/font/google'
import { Providers } from '@/components/Providers'
import './globals.css'

const geistSans = Geist({
  variable: '--font-geist-sans',
  subsets: ['latin'],
})

// Every page names itself ("Sign in · RondiTrack"): a screen reader reads the
// title after each navigation (WCAG 2.4.2). Pages never put a stokvel's or a
// member's name in it: titles stay in browser history on shared phones.
export const metadata: Metadata = {
  metadataBase: new URL(process.env.APP_URL ?? 'http://localhost:3000'),
  title: { default: 'RondiTrack', template: '%s · RondiTrack' },
  description: 'Track your stokvel contributions and payouts.',
  applicationName: 'RondiTrack',
}

export default function RootLayout({ children }: LayoutProps<'/'>) {
  return (
    <html lang="en" className={`${geistSans.variable} h-full`}>
      <body className="min-h-full">
        <Providers>{children}</Providers>
      </body>
    </html>
  )
}
