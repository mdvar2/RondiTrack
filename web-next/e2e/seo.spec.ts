import { expect, test } from '@playwright/test'
import { api, people, unique } from './support'

// What a link-preview bot sees: Next.js puts metadata in <head> only for
// known bots and streams it for browsers, so ask as one.
const PREVIEW_BOT = 'WhatsApp/2.23.20.0'

function head(html: string) {
  const section = /<head>([\s\S]*?)<\/head>/.exec(html)?.[1] ?? ''
  const meta = [...section.matchAll(/<meta\s+([^>]*?)\/?>/g)].map((match) => match[1])
  const content = (key: string) => meta.find((attributes) => attributes.includes(`"${key}"`))?.match(/content="([^"]*)"/)?.[1]
  const ogValues = meta.filter((attributes) => /property="og:/.test(attributes)).map((attributes) => attributes.match(/content="([^"]*)"/)?.[1] ?? '')
  const title = (/<title>([^<]*)<\/title>/.exec(section)?.[1] ?? '').replace(/&#x27;|&#39;/g, "'")
  return { section, title, content, ogValues }
}

test.describe('signed in', () => {
  test.use({ storageState: people.sipho.state })

  test('every signed-in page is noindex', async ({ page }) => {
    for (const path of ['/dashboard/member', '/dashboard/treasurer', '/stokvels']) {
      await page.goto(path)
      await expect(page.locator('meta[name="robots"]'), path).toHaveAttribute('content', /noindex/)
    }
  })
})

test.describe('signed out', () => {
  test.use({ storageState: { cookies: [], origins: [] } })

  test('a real invite, previewed signed out, names no stokvel and no amount', async ({ request }) => {
    // R 4 321,99 a month: an amount that can't appear by accident.
    const name = unique('Secret Savings Circle')
    const stokvel = await api<{ id: string }>('thandi', 'POST', '/api/v1/stokvels', { name, contributionAmount: 4321.99 })
    const { code } = await api<{ code: string }>('thandi', 'POST', `/api/v1/stokvels/${stokvel.id}/invites`)

    const response = await request.get(`/join?code=${encodeURIComponent(code)}`, { headers: { 'User-Agent': PREVIEW_BOT }, maxRedirects: 0 })
    expect(response.status()).toBe(200)
    const html = await response.text()
    const { section, title, content, ogValues } = head(html)

    expect(title).toBe('You’re invited to join a stokvel · RondiTrack')
    expect(content('description')).toContain('invited you to join their stokvel on RondiTrack')
    expect(content('robots')).toBe('noindex, nofollow')
    expect(ogValues.length).toBeGreaterThan(0)
    for (const text of [title, content('description') ?? '', ...ogValues, section]) {
      expect(text).not.toContain(name)
      expect(text).not.toContain('Secret Savings')
      expect(text).not.toMatch(/4[\s ]?321/)
    }
    // Nor anywhere on the page a signed-out person gets.
    expect(html).not.toContain(name)
  })

  test('an unknown invite says so, still without indexing', async ({ request }) => {
    const html = await (await request.get('/join?code=not-a-real-code', { headers: { 'User-Agent': PREVIEW_BOT } })).text()
    expect(head(html).title).toBe('Stokvel invite not found · RondiTrack')
    expect(head(html).content('robots')).toBe('noindex, nofollow')
  })

  test('the sign-in page may be indexed and previewed: what RondiTrack is', async ({ request }) => {
    const html = await (await request.get('/login', { headers: { 'User-Agent': PREVIEW_BOT } })).text()
    const { content } = head(html)
    expect(content('robots')).toBeUndefined()
    expect(content('og:title')).toBe('RondiTrack: stokvel contributions, tracked')
    expect(content('og:image')).toContain('/login/opengraph-image')
  })

  test("robots.txt allows crawling, so crawlers can see each page's noindex", async ({ request }) => {
    const text = await (await request.get('/robots.txt')).text()
    expect(text).toContain('Allow: /')
    expect(text).not.toMatch(/Disallow:\s*\/\S/)
  })
})
