export type JoinStep = 'code' | 'rules' | 'confirm'

export type JoinState =
  | { step: 'code'; code: string }
  | { step: 'rules'; code: string; preview: InvitePreview }
  | { step: 'confirm'; code: string; preview: InvitePreview }

export type InvitePreview = {
  stokvelId: string
  name: string
  contributionAmountCents: number
  schedule: string
  rules: string
  memberCount: number
}

export function transition(state: JoinState, event: 'previewed', preview: InvitePreview): JoinState
export function transition(state: JoinState, event: 'confirmed'): JoinState
export function transition(state: JoinState, event: 'previewed' | 'confirmed', preview?: InvitePreview): JoinState {
  if (event === 'previewed' && state.step === 'code' && preview) {
    return { step: 'rules', code: state.code, preview }
  }
  if (event === 'confirmed' && state.step === 'rules') {
    return { step: 'confirm', code: state.code, preview: state.preview }
  }
  return state
}

export function stepFromQuery(step: string | undefined): JoinStep {
  return step === 'rules' || step === 'confirm' ? step : 'code'
}
