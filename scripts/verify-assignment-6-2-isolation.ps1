param(
    [string]$PsqlPath = 'C:\Program Files\PostgreSQL\17\bin\psql.exe',
    [switch]$FailAfterIsolation
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path $PSScriptRoot -Parent
$runId = [DateTime]::UtcNow.ToString('yyyyMMddTHHmmssfffZ') + '-' + [Guid]::NewGuid().ToString('N')
$resultsPath = Join-Path $repositoryRoot "obj\assignment-6-3-offline-$runId"
$trxName = "tests-$runId.trx"
$rawOutputPath = Join-Path $resultsPath "tests-$runId.log"
$buildOutputPath = Join-Path $resultsPath "build-$runId.log"
$safeEvidencePath = Join-Path $resultsPath "evidence-$runId.txt"
[xml]$apiProject = Get-Content -LiteralPath (Join-Path $repositoryRoot 'API\RondiTrack.csproj')
$secretsId = [string]$apiProject.Project.PropertyGroup.UserSecretsId
$secretsPath = Join-Path $env:APPDATA "Microsoft\UserSecrets\$secretsId\secrets.json"
$secrets = Get-Content -LiteralPath $secretsPath -Raw | ConvertFrom-Json
$connectionString = $secrets.'ConnectionStrings:RondiTrackDb'
if (-not $connectionString) { throw 'Development connection configuration is required for the isolation proof.' }
$parts = New-Object System.Data.Common.DbConnectionStringBuilder
$parts.set_ConnectionString($connectionString)
$databaseName = [string]$parts['Database']
$serverHost = [string]$parts['Host']
$serverPort = if ($parts.ContainsKey('Port')) { [string]$parts['Port'] } else { '5432' }
if ($databaseName -ne 'ronditrack' -or $serverHost -notin @('localhost', '127.0.0.1', '::1')) {
    throw 'Isolation is restricted to the existing local development database.'
}
if (-not (Test-Path -LiteralPath $PsqlPath)) { throw 'The PostgreSQL client is unavailable.' }

$savedPassword = $env:PGPASSWORD
$savedConnectTimeout = $env:PGCONNECT_TIMEOUT
$savedOtlp = $env:OTEL_EXPORTER_OTLP_ENDPOINT
$savedHealthEvidence = $env:RONDITRACK_HEALTH_EVIDENCE_DIR
$admissionMayHaveChanged = $false
$originalAdmission = $null
$testExit = 1
$evidence = [System.Collections.Generic.List[string]]::new()
function Add-Evidence([string]$line) { $evidence.Add($line); Write-Output $line }
function Quote-PowerShellLiteral([string]$value) { return "'" + $value.Replace("'", "''") + "'" }
try {
    New-Item -ItemType Directory -Path $resultsPath -Force | Out-Null
    Add-Evidence "RUN_ID=$runId"
    # Check external prerequisites and rebuild BEFORE any development metadata mutation.
    Get-Command docker -ErrorAction Stop | Out-Null
    Get-Command dotnet -ErrorAction Stop | Out-Null
    & docker info --format '{{.OSType}}' *> (Join-Path $resultsPath "docker-$runId.log")
    if ($LASTEXITCODE -ne 0) { throw 'Docker daemon is unavailable; no isolation change was made.' }
    $dockerOs = Get-Content -LiteralPath (Join-Path $resultsPath "docker-$runId.log") -Raw
    if ($dockerOs.Trim() -ne 'linux') { throw 'Testcontainers requires Linux containers; no isolation change was made.' }
    Push-Location $repositoryRoot
    try {
        & dotnet build 'RondiTrack.sln' --no-incremental -m:1 *> $buildOutputPath
        if ($LASTEXITCODE -ne 0) { throw "Fresh build failed; no isolation change was made. See $buildOutputPath" }
    }
    finally { Pop-Location }
    Add-Evidence 'PREREQUISITES: Docker Linux daemon available; fresh solution build succeeded.'
    $env:PGPASSWORD = [string]$parts['Password']
    $env:PGCONNECT_TIMEOUT = '5'
    $psqlArguments = @('-h', $serverHost, '-p', $serverPort, '-U', [string]$parts['Username'], '-X', '-w', '-v', 'ON_ERROR_STOP=1')
    $allowed = & $PsqlPath @psqlArguments -d postgres -At -c "SELECT datallowconn FROM pg_database WHERE datname = 'ronditrack';" 2>$null
    if ($LASTEXITCODE -ne 0 -or $allowed -notin @('t', 'f')) { throw 'Original development admission state could not be read; no isolation change was made.' }
    $originalAdmission = if ($allowed -eq 't') { 'true' } else { 'false' }
    Add-Evidence "ORIGINAL_ALLOW_CONNECTIONS=$originalAdmission"
    if ($originalAdmission -ne 'true') { throw 'Development must initially accept connections; its original disabled state was left unchanged.' }
    $recoveryArgs = '-h ' + (Quote-PowerShellLiteral $serverHost) + ' -p ' + (Quote-PowerShellLiteral $serverPort) + ' -U ' + (Quote-PowerShellLiteral ([string]$parts['Username'])) + ' -X -W -v ON_ERROR_STOP=1'
    $client = '& ' + (Quote-PowerShellLiteral $PsqlPath) + ' ' + $recoveryArgs
    $recoveryInstructions = @"
Ensure the local PostgreSQL instance is running. In PowerShell, run these commands
and enter the development database password when prompted (no password is stored):
$client -d postgres -c 'ALTER DATABASE ronditrack ALLOW_CONNECTIONS $originalAdmission;'
$client -d postgres -At -c "SELECT datallowconn FROM pg_database WHERE datname = 'ronditrack';"
$client -d ronditrack -At -c 'SELECT 1;'
Require outputs t and 1 before using development.
"@
    # Persist original state and recovery steps before ALTER, including for abrupt process loss.
    $recoveryInstructions | Set-Content -LiteralPath (Join-Path $resultsPath "recovery-$runId.txt") -Encoding UTF8
    $evidence | Set-Content -LiteralPath $safeEvidencePath -Encoding UTF8
    $initialProbe = & $PsqlPath @psqlArguments -d ronditrack -At -c 'SELECT 1;' 2>$null
    if ($LASTEXITCODE -ne 0 -or $initialProbe -ne '1') { throw 'Initial development connectivity failed; no isolation change was made.' }
    $canAlter = & $PsqlPath @psqlArguments -d postgres -At -c "SELECT (r.rolsuper OR pg_has_role(current_user, d.datdba, 'USAGE')) FROM pg_database d CROSS JOIN pg_roles r WHERE d.datname = 'ronditrack' AND r.rolname = current_user;" 2>$null
    if ($LASTEXITCODE -ne 0 -or $canAlter -ne 't') { throw 'Database owner or superuser privileges are required; no isolation change was made.' }
    $active = & $PsqlPath @psqlArguments -d postgres -At -c "SELECT count(*) FROM pg_stat_activity WHERE datname = 'ronditrack';" 2>$null
    if ($LASTEXITCODE -ne 0 -or $active -ne '0') { throw 'Development has active sessions; no sessions will be terminated and no isolation change was made.' }

    # Connection-admission metadata only: never change records, schema or migrations.
    # Set BEFORE invoking psql: a failed/interrupted client may still have committed ALTER.
    $admissionMayHaveChanged = $true
    & $PsqlPath @psqlArguments -d postgres -c 'ALTER DATABASE ronditrack ALLOW_CONNECTIONS false;' *> $null
    if ($LASTEXITCODE -ne 0) { throw 'Development connection admission could not be disabled.' }
    $active = & $PsqlPath @psqlArguments -d postgres -At -c "SELECT count(*) FROM pg_stat_activity WHERE datname = 'ronditrack';" 2>$null
    if ($LASTEXITCODE -ne 0 -or $active -ne '0') { throw 'A development session appeared during preflight; isolation will be restored without terminating sessions.' }
    $previousPreference = $ErrorActionPreference
    try {
        $ErrorActionPreference = 'Continue'
        $probe = & $PsqlPath @psqlArguments -d ronditrack -At -c 'SELECT 1;' 2>&1
        $probeExit = $LASTEXITCODE
    }
    finally { $ErrorActionPreference = $previousPreference }
    if ($probeExit -eq 0 -or ($probe | Out-String) -notmatch 'not currently accepting connections') {
        throw 'A real development connection was not rejected as expected; independence is not proven.'
    }
    Add-Evidence 'DEVELOPMENT_UNAVAILABLE: real connection rejected with ALLOW_CONNECTIONS=false; zero existing development sessions.'
    Add-Evidence 'ISOLATION_METHOD: connection admission disabled for development only; Windows PostgreSQL service remains running.'
    $env:PGPASSWORD = $savedPassword
    $env:OTEL_EXPORTER_OTLP_ENDPOINT = ''
    $env:RONDITRACK_HEALTH_EVIDENCE_DIR = $resultsPath
    Add-Evidence 'OTLP_DISABLED: no exporter endpoint supplied; no Aspire dashboard required.'
    if ($FailAfterIsolation) {
        Add-Evidence 'FAULT_INJECTION: controlled exception after connection rejection; test process intentionally not started.'
        throw 'Synthetic post-isolation failure used to verify finally restoration.'
    }
    Push-Location $repositoryRoot
    $previousPreference = $ErrorActionPreference
    try {
        # Keep raw diagnostics local; publish only verified aggregate counters below.
        $ErrorActionPreference = 'Continue'
        & dotnet test 'RondiTrack.Tests/RondiTrack.Tests.csproj' --no-build --no-restore -m:1 --logger "trx;LogFileName=$trxName" --results-directory $resultsPath *> $rawOutputPath
        $testExit = $LASTEXITCODE
    }
    finally { $ErrorActionPreference = $previousPreference; Pop-Location }
    Add-Evidence "TEST_EXIT_CODE=$testExit"
    [xml]$trx = Get-Content -LiteralPath (Join-Path $resultsPath $trxName) -Raw
    $counters = $trx.TestRun.ResultSummary.Counters
    Add-Evidence "TEST_RESULT total=$($counters.total) passed=$($counters.passed) failed=$($counters.failed) skipped=$($counters.notExecuted)"
    if ($testExit -ne 0) { throw 'The isolated suite failed; inspect the local test artifact without publishing raw diagnostics.' }
    if ($null -eq $counters -or $counters.total -ne '122' -or $counters.executed -ne '122' -or
        $counters.passed -ne '122' -or $counters.failed -ne '0' -or $counters.notExecuted -ne '0' -or
        $trx.TestRun.ResultSummary.outcome -ne 'Completed') {
        throw 'Assignment 6.3 result discrepancy: exactly 122 total/executed/passed, zero failed and zero skipped are required. Review current tests and this run TRX; do not silently update the baseline.'
    }
}
finally {
    try {
        if ($admissionMayHaveChanged) {
            $env:PGPASSWORD = [string]$parts['Password']
            & $PsqlPath @psqlArguments -d postgres -c "ALTER DATABASE ronditrack ALLOW_CONNECTIONS $originalAdmission;" *> $null
            if ($LASTEXITCODE -ne 0) { throw 'RESTORATION FAILED: development connection admission must be restored before use.' }
            $restoredAdmission = & $PsqlPath @psqlArguments -d postgres -At -c "SELECT datallowconn FROM pg_database WHERE datname = 'ronditrack';" 2>$null
            if ($LASTEXITCODE -ne 0 -or $restoredAdmission -ne 't') { throw 'Restored admission metadata could not be verified.' }
            $restored = & $PsqlPath @psqlArguments -d ronditrack -At -c 'SELECT 1;' 2>$null
            if ($LASTEXITCODE -ne 0 -or $restored -ne '1') { throw 'Development connectivity restoration could not be verified.' }
            Add-Evidence 'DEVELOPMENT_RESTORED: real SELECT 1 succeeded; connection admission restored; no business records or migrations changed.'
        }
    }
    catch {
        $testExit = 1
        Add-Evidence 'RESTORATION_FAILED: VERIFICATION UNSUCCESSFUL. Development access requires immediate recovery.'
        Write-Warning '*** RESTORATION FAILED OR COULD NOT BE VERIFIED. DO NOT TREAT THIS RUN AS SUCCESSFUL. ***' -WarningAction Continue
        Write-Warning $recoveryInstructions -WarningAction Continue
        throw
    }
    finally {
        $env:PGPASSWORD = $savedPassword
        $env:PGCONNECT_TIMEOUT = $savedConnectTimeout
        $env:OTEL_EXPORTER_OTLP_ENDPOINT = $savedOtlp
        $env:RONDITRACK_HEALTH_EVIDENCE_DIR = $savedHealthEvidence
        $evidence | Set-Content -LiteralPath $safeEvidencePath -Encoding UTF8
    }
}
Add-Evidence 'VERIFICATION_SUCCESS: exactly 122 passed, zero failed/skipped; development admission and connectivity restored.'
$evidence | Set-Content -LiteralPath $safeEvidencePath -Encoding UTF8
exit $testExit
