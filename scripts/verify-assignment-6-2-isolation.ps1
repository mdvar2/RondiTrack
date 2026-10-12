param(
    [string]$PsqlPath = 'C:\Program Files\PostgreSQL\17\bin\psql.exe',
    [switch]$FailAfterIsolation
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path $PSScriptRoot -Parent
$resultsPath = Join-Path $repositoryRoot 'obj\stage7-test-results'
$rawOutputPath = Join-Path $repositoryRoot 'obj\stage7-isolation-raw.txt'
$safeEvidencePath = Join-Path $repositoryRoot 'obj\stage7-isolation-evidence.txt'
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
$disabled = $false
$testExit = 1
$evidence = [System.Collections.Generic.List[string]]::new()
function Add-Evidence([string]$line) { $evidence.Add($line); Write-Output $line }
try {
    New-Item -ItemType Directory -Path $resultsPath -Force | Out-Null
    $env:PGPASSWORD = [string]$parts['Password']
    $env:PGCONNECT_TIMEOUT = '5'
    $psqlArguments = @('-h', $serverHost, '-p', $serverPort, '-U', [string]$parts['Username'], '-X', '-w', '-v', 'ON_ERROR_STOP=1')
    $allowed = & $PsqlPath @psqlArguments -d postgres -At -c "SELECT datallowconn FROM pg_database WHERE datname = 'ronditrack';" 2>$null
    if ($LASTEXITCODE -ne 0 -or $allowed -ne 't') { throw 'Development must initially accept connections; no isolation change was made.' }
    $active = & $PsqlPath @psqlArguments -d postgres -At -c "SELECT count(*) FROM pg_stat_activity WHERE datname = 'ronditrack';" 2>$null
    if ($LASTEXITCODE -ne 0 -or $active -ne '0') { throw 'Development has active sessions; no sessions will be terminated and no isolation change was made.' }

    # Connection-admission metadata only: never change records, schema or migrations.
    & $PsqlPath @psqlArguments -d postgres -c 'ALTER DATABASE ronditrack ALLOW_CONNECTIONS false;' *> $null
    if ($LASTEXITCODE -ne 0) { throw 'Development connection admission could not be disabled.' }
    $disabled = $true
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
        & dotnet test 'RondiTrack.Tests/RondiTrack.Tests.csproj' --no-build --no-restore -m:1 --logger 'trx;LogFileName=stage7-tests.trx' --results-directory $resultsPath *> $rawOutputPath
        $testExit = $LASTEXITCODE
    }
    finally { $ErrorActionPreference = $previousPreference; Pop-Location }
    Add-Evidence "TEST_EXIT_CODE=$testExit"
    [xml]$trx = Get-Content -LiteralPath (Join-Path $resultsPath 'stage7-tests.trx') -Raw
    $counters = $trx.TestRun.ResultSummary.Counters
    Add-Evidence "TEST_RESULT total=$($counters.total) passed=$($counters.passed) failed=$($counters.failed) skipped=$($counters.notExecuted)"
    if ($testExit -ne 0) { throw 'The isolated suite failed; inspect the local test artifact without publishing raw diagnostics.' }
}
finally {
    try {
        if ($disabled) {
            $env:PGPASSWORD = [string]$parts['Password']
            & $PsqlPath @psqlArguments -d postgres -c 'ALTER DATABASE ronditrack ALLOW_CONNECTIONS true;' *> $null
            if ($LASTEXITCODE -ne 0) { throw 'RESTORATION FAILED: development connection admission must be restored before use.' }
            $restored = & $PsqlPath @psqlArguments -d ronditrack -At -c 'SELECT 1;' 2>$null
            if ($LASTEXITCODE -ne 0 -or $restored -ne '1') { throw 'Development connectivity restoration could not be verified.' }
            Add-Evidence 'DEVELOPMENT_RESTORED: real SELECT 1 succeeded; connection admission restored; no business records or migrations changed.'
        }
    }
    finally {
        $env:PGPASSWORD = $savedPassword
        $env:PGCONNECT_TIMEOUT = $savedConnectTimeout
        $env:OTEL_EXPORTER_OTLP_ENDPOINT = $savedOtlp
        $env:RONDITRACK_HEALTH_EVIDENCE_DIR = $savedHealthEvidence
        $evidence | Set-Content -LiteralPath $safeEvidencePath -Encoding UTF8
    }
}
exit $testExit
