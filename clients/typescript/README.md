# RondiTrack TypeScript contracts

Run from this directory with Node/npm, .NET 10 and Docker running:

```powershell
npm ci --ignore-scripts
npm run regenerate
npm run test:money
```

`regenerate` exports `/openapi/v1.json` from the real API using the existing PostgreSQL Testcontainers fixture, generates `src/schema.d.ts` with openapi-typescript 7.13.0, and runs TypeScript 5.9.3 with `tsc --noEmit --strict`. The production/development database is never used. No NuGet dependency was added. The console exporter explicitly sets the API content root because test-runner content-root discovery does not apply to a console executable.

To generate from an already running Development API instead:

```powershell
Invoke-WebRequest https://localhost:7000/openapi/v1.json -OutFile openapi.json
npm run generate
npm run check
```

Replace the port with the API's actual HTTPS address. Trust the development certificate instead of disabling certificate validation.

`home.ts` derives its input from the generated endpoint response. `totalOutstanding` adds decimal strings using BigInt and a shared base-10 scale, returning an exact decimal string. It never converts money to JavaScript Number. ES2020 is required for BigInt. The runtime checks cover floating-point-sensitive decimals, mixed precision and values beyond JavaScript's safe integer range.

## Actual breaking-change experiment (14 October 2026)

Before rename: `npm run generate` followed by `npm run check` succeeded, exit 0.

Temporarily renamed the C# positional field `AmountOwed` to `OutstandingBalance` in `API/DTOs/Home/HomeResponse.cs`, built the API, exported its actual renamed OpenAPI and regenerated. Kept the already-built test assembly unchanged with `-p:BuildProjectReferences=false` for this export, because rebuilding the home tests would independently reject the C# property rename. No test assertion was edited.

```powershell
# Run these experiment commands from the repository root after the temporary rename.
dotnet build API/RondiTrack.csproj --no-restore -m:1
dotnet run --project scripts/contracts/ExportContracts.csproj --no-restore -p:BuildProjectReferences=false -- clients/typescript/openapi.json
cd clients/typescript
npm run generate
npm run check
```

Actual compiler output, exit 2:

```text
src/home.ts(7,56): error TS2339: Property 'amountOwed' does not exist on type '{ stokvelName: string; role: string; currentCycle: { period: string; status: string; } | null; amountPaid: string; outstandingBalance: string; nextPayoutDate: string | null; }'.
```

Restored `AmountOwed`. The first restore retained the older saved-file timestamp, so the incremental .NET build reused the renamed assembly and home tests reported CS1061. Refreshing the source timestamp rebuilt the correct API. Then `npm run regenerate` re-exported the actual restored contract and `tsc --noEmit --strict` succeeded, exit 0. The committed OpenAPI/types contain `amountOwed`, with no `outstandingBalance`.
