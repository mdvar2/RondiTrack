import { ImageResponse } from 'next/og'

// The product's link-preview card: a name and what it does. Built at build
// time, the same for everyone, with nothing about any stokvel or member.
export const alt = 'RondiTrack: stokvel contributions, tracked'
export const size = { width: 1200, height: 630 }
export const contentType = 'image/png'

export default function OpenGraphImage() {
  return new ImageResponse(
    (
      <div style={{ width: '100%', height: '100%', display: 'flex', flexDirection: 'column', justifyContent: 'center', padding: 80, background: '#171717', color: '#fafafa' }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: 24 }}>
          <div style={{ width: 96, height: 96, borderRadius: 20, background: '#fafafa', color: '#171717', display: 'flex', alignItems: 'center', justifyContent: 'center', fontSize: 64, fontWeight: 700 }}>R</div>
          <div style={{ fontSize: 72, fontWeight: 700 }}>RondiTrack</div>
        </div>
        <div style={{ marginTop: 40, fontSize: 40, color: '#d4d4d4' }}>Stokvel contributions, proofs and payouts, tracked.</div>
      </div>
    ),
    size,
  )
}
