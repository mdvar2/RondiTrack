// Seeds one realistic stokvel for performance measurement (Assignment 9.2, Part 5):
// 40 members and a year of monthly cycles, 11 closed and the current one open.
//
//   API_BASE_URL=http://localhost:5147 COMPOSE_PROJECT=ronditrack-e2e \
//   E2E_PASSWORD=... node scripts/seed-perf.mjs
//
// People are registered through the real API, so their password hashes are
// the API's own. Memberships, roles and the year of history are written with
// SQL inside the Compose database container: the API has no endpoint that
// makes someone Treasurer, and recording 450 contributions through it would
// take 15 minutes against its 30-a-minute limit. Run it on a throwaway
// database only. It refuses to run twice on the same one.
import { execFileSync } from 'node:child_process'
import { randomUUID } from 'node:crypto'

const api = (process.env.API_BASE_URL ?? 'http://localhost:5147').replace(/\/$/, '')
const project = process.env.COMPOSE_PROJECT ?? 'ronditrack-e2e'
const password = process.env.E2E_PASSWORD
if (!password || password.length < 12) {
  console.error('Set E2E_PASSWORD (12+ characters, test-only) before seeding.')
  process.exit(1)
}

const STOKVEL_NAME = 'Masakhane Savings Club'
const MEMBER_COUNT = 40
const CONTRIBUTION_RANDS = 500
const firstNames = ['Thandi', 'Sipho', 'Lerato', 'Bongani', 'Ayanda', 'Kagiso', 'Zanele', 'Mpho', 'Nomvula', 'Tshepo',
  'Lindiwe', 'Themba', 'Palesa', 'Sizwe', 'Naledi', 'Khaya', 'Refilwe', 'Vusi', 'Busisiwe', 'Lwazi']
const surnames = ['Dlamini', 'Nkosi', 'Mokoena', 'Khumalo', 'Ndlovu', 'Mahlangu', 'Molefe', 'Zulu', 'Sithole', 'Mthembu']

function psql(sql) {
  return execFileSync('docker', ['compose', '-p', project, 'exec', '-T', 'db',
    'psql', '-U', 'ronditrack', '-d', 'ronditrack', '-v', 'ON_ERROR_STOP=1', '-At'],
  { input: sql, encoding: 'utf8' }).trim()
}

async function register(name, email) {
  const response = await fetch(`${api}/api/v1/auth/register`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ name, email, password }),
  })
  if (response.status !== 201 && response.status !== 409) {
    throw new Error(`Register ${email} failed: ${response.status} ${await response.text()}`)
  }
}

const quote = (value) => `'${String(value).replace(/'/g, "''")}'`

// Johannesburg is UTC+2 all year, so its current month is UTC + 2 hours.
function periods(count) {
  const now = new Date(Date.now() + 2 * 60 * 60 * 1000)
  const result = []
  for (let back = count - 1; back >= 0; back--) {
    const date = new Date(Date.UTC(now.getUTCFullYear(), now.getUTCMonth() - back, 1))
    result.push(`${date.getUTCFullYear()}-${String(date.getUTCMonth() + 1).padStart(2, '0')}`)
  }
  return result
}

if (psql(`SELECT count(*) FROM "Stokvels" WHERE "Name" = ${quote(STOKVEL_NAME)};`) !== '0') {
  console.log(`"${STOKVEL_NAME}" already exists in ${project}; nothing to do.`)
  process.exit(0)
}

const people = [{ name: 'Nomsa Treasurer', email: 'perf.treasurer@example.test', role: 'Treasurer' },
  { name: 'Ayanda Chair', email: 'perf.chair@example.test', role: 'Chairperson' }]
for (let index = 0; people.length < MEMBER_COUNT; index++) {
  const name = `${firstNames[index % firstNames.length]} ${surnames[Math.floor(index / 2) % surnames.length]}`
  people.push({ name, email: `perf.member${String(index + 1).padStart(2, '0')}@example.test`, role: 'Member' })
}
for (const person of people) await register(person.name, person.email)

const ids = new Map(psql(`SELECT "Email" || '|' || "Id" FROM "Users" WHERE "Email" LIKE 'perf.%@example.test';`)
  .split('\n').map((line) => line.split('|')))

const stokvelId = randomUUID()
const lines = ['BEGIN;',
  `INSERT INTO "Stokvels" ("Id", "Name", "ContributionAmount") VALUES (${quote(stokvelId)}, ${quote(STOKVEL_NAME)}, ${CONTRIBUTION_RANDS});`]
for (const person of people) {
  lines.push(`INSERT INTO "StokvelMembers" ("StokvelId", "UserId", "Role", "JoinedAtUtc") VALUES (${quote(stokvelId)}, ${quote(ids.get(person.email))}, ${quote(person.role)}, now() - interval '13 months');`)
}

const year = periods(12)
let contributionCount = 0
year.forEach((period, monthIndex) => {
  const open = monthIndex === year.length - 1
  const cycleId = randomUUID()
  const [y, m] = period.split('-').map(Number)
  const endDate = new Date(Date.UTC(y, m, 0)).toISOString().slice(0, 10)
  lines.push(`INSERT INTO "ContributionCycles" ("Id", "StokvelId", "Period", "TargetAmount", "EndDate", "Status") VALUES (${quote(cycleId)}, ${quote(stokvelId)}, ${quote(period)}, ${CONTRIBUTION_RANDS * MEMBER_COUNT}, ${quote(endDate)}, ${quote(open ? 'Open' : 'Closed')});`)
  // Most people pay; a few miss a month, and fewer have paid so far this month.
  people.forEach((person, personIndex) => {
    const pays = open ? personIndex % 4 !== 3 : (personIndex + monthIndex) % 13 !== 0
    if (!pays) return
    const day = 1 + ((personIndex * 7 + monthIndex) % 27)
    const amount = personIndex % 9 === 0 ? 250.5 : CONTRIBUTION_RANDS
    const recorded = new Date(Date.UTC(y, m - 1, day, 6 + (personIndex % 12), (personIndex * 13) % 60)).toISOString()
    lines.push(`INSERT INTO "Contributions" ("Id", "StokvelId", "UserId", "ContributionCycleId", "Amount", "RecordedAtUtc", "Status") VALUES (${quote(randomUUID())}, ${quote(stokvelId)}, ${quote(ids.get(person.email))}, ${quote(cycleId)}, ${amount}, ${quote(recorded)}, 'Paid');`)
    contributionCount++
  })
})
lines.push('COMMIT;')
psql(lines.join('\n'))

console.log(JSON.stringify({ project, stokvelId, stokvelName: STOKVEL_NAME, members: people.length, cycles: year.length, contributions: contributionCount, signIn: 'perf.treasurer@example.test' }, null, 2))
