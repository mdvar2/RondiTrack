param(
    [string]$PsqlPath = 'C:\Program Files\PostgreSQL\17\bin\psql.exe'
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path $PSScriptRoot -Parent
[xml]$apiProject = Get-Content -LiteralPath (Join-Path $repositoryRoot 'API\RondiTrack.csproj')
$secretsId = [string]$apiProject.Project.PropertyGroup.UserSecretsId
$secretsPath = Join-Path $env:APPDATA "Microsoft\UserSecrets\$secretsId\secrets.json"
$secrets = Get-Content -LiteralPath $secretsPath -Raw | ConvertFrom-Json
$connectionString = $secrets.'ConnectionStrings:RondiTrackDb'
if (-not $connectionString) { throw 'Development connection string is required for the isolation preflight.' }

# Parse without displaying the connection string or credentials.
$connectionParts = New-Object System.Data.Common.DbConnectionStringBuilder
$connectionParts.set_ConnectionString($connectionString)
$databaseName = [string]$connectionParts['Database']
$serverHost = [string]$connectionParts['Host']
$serverPort = if ($connectionParts.ContainsKey('Port')) { [string]$connectionParts['Port'] } else { '5432' }
if ($databaseName -ne 'ronditrack' -or $serverHost -notin @('localhost', '127.0.0.1', '::1')) {
    throw 'This proof only permits the local ronditrack development database.'
}
if (-not (Test-Path -LiteralPath $PsqlPath)) { throw 'PostgreSQL psql was not found.' }

$previousPgPassword = $env:PGPASSWORD
$previousPgConnectTimeout = $env:PGCONNECT_TIMEOUT
$databaseDisabled = $false
$proofExit = 1
try {
    $env:PGPASSWORD = [string]$connectionParts['Password']
    $env:PGCONNECT_TIMEOUT = '5'
    $psqlArguments = @('-h', $serverHost, '-p', $serverPort, '-U', [string]$connectionParts['Username'], '-X', '-w', '-v', 'ON_ERROR_STOP=1')

    # Use postgres for administration so we can restore ronditrack while it rejects connections.
    $allowed = & $PsqlPath @psqlArguments -d postgres -At -c "SELECT datallowconn FROM pg_database WHERE datname = 'ronditrack';"
    if ($LASTEXITCODE -ne 0 -or $allowed -ne 't') { throw 'Development database must initially accept connections.' }
    $active = & $PsqlPath @psqlArguments -d postgres -At -c "SELECT count(*) FROM pg_stat_activity WHERE datname = 'ronditrack';"
    if ($LASTEXITCODE -ne 0 -or $active -ne '0') { throw 'Close development API/database sessions before the isolation proof; no sessions will be terminated.' }

    & $PsqlPath @psqlArguments -d postgres -c 'ALTER DATABASE ronditrack ALLOW_CONNECTIONS false;'
    if ($LASTEXITCODE -ne 0) { throw 'Could not temporarily disable development database connections.' }
    $databaseDisabled = $true
    $active = & $PsqlPath @psqlArguments -d postgres -At -c "SELECT count(*) FROM pg_stat_activity WHERE datname = 'ronditrack';"
    if ($LASTEXITCODE -ne 0 -or $active -ne '0') { throw 'A development session appeared during preflight; restore and retry when idle.' }

    # Windows PowerShell represents expected native stderr as an error record.
    $previousErrorPreference = $ErrorActionPreference
    try {
        $ErrorActionPreference = 'Continue'
        $probe = & $PsqlPath @psqlArguments -d ronditrack -c 'SELECT 1;' 2>&1
        $probeExit = $LASTEXITCODE
    }
    finally { $ErrorActionPreference = $previousErrorPreference }
    if ($probeExit -eq 0) { throw 'Development database unexpectedly accepted a connection.' }
    if (($probe | Out-String) -notmatch 'not currently accepting connections') {
        throw 'Development probe failed for an unexpected reason; independence is not proven.'
    }
    Write-Output 'DEVELOPMENT_DATABASE_UNAVAILABLE: ronditrack rejected a real connection (ALLOW_CONNECTIONS=false); PostgreSQL service remains running.'

    # Remove psql credentials before spawning the test process.
    $env:PGPASSWORD = $previousPgPassword
    Push-Location $repositoryRoot
    try {
        & dotnet test 'RondiTrack.Tests\RondiTrack.Tests.csproj' --no-build --no-restore --logger 'console;verbosity=detailed'
        $proofExit = $LASTEXITCODE
    }
    finally { Pop-Location }
}
finally {
    try {
        if ($databaseDisabled) {
            $env:PGPASSWORD = [string]$connectionParts['Password']
            & $PsqlPath @psqlArguments -d postgres -c 'ALTER DATABASE ronditrack ALLOW_CONNECTIONS true;'
            if ($LASTEXITCODE -ne 0) { throw 'RESTORATION FAILED: restore ALLOW_CONNECTIONS=true for ronditrack before development use.' }
            & $PsqlPath @psqlArguments -d ronditrack -At -c 'SELECT current_database();'
            if ($LASTEXITCODE -ne 0) { throw 'Development database restoration probe failed.' }
            Write-Output 'DEVELOPMENT_DATABASE_RESTORED: ronditrack accepts connections again.'
        }
    }
    finally {
        $env:PGPASSWORD = $previousPgPassword
        $env:PGCONNECT_TIMEOUT = $previousPgConnectTimeout
    }
}
exit $proofExit
