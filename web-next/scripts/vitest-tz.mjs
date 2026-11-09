// Runs Vitest with a given TZ on any OS (`TZ=... vitest` doesn't work in cmd).
//   node scripts/vitest-tz.mjs Africa/Johannesburg
import { spawnSync } from 'node:child_process'

const tz = process.argv[2]
if (!tz) throw new Error('Usage: node scripts/vitest-tz.mjs <IANA time zone>')
const result = spawnSync('npx', ['vitest', 'run', ...process.argv.slice(3)], { stdio: 'inherit', shell: process.platform === 'win32', env: { ...process.env, TZ: tz } })
process.exit(result.status ?? 1)
