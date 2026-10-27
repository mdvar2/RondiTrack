// "Treasurer" is a role in one stokvel, not a kind of user. The role comes from
// the API's answer for each stokvel; nothing here reads the session or token.
export function stokvelsWhereTreasurer<T extends { role: string }>(list: readonly T[]): T[] {
  return list.filter((stokvel) => stokvel.role === 'Treasurer')
}
