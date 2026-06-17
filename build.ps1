param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',

    [switch]$NoRestore
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$solutionPath = Join-Path $repoRoot 'easymode.sln'

if (-not (Test-Path -Path $solutionPath)) {
    Write-Error "Solution not found: $solutionPath"
    exit 1
}

Push-Location $repoRoot
try {
    if (-not $NoRestore) {
        Write-Host 'Restoring packages...'
        dotnet restore $solutionPath
        if ($LASTEXITCODE -ne 0) {
            exit $LASTEXITCODE
        }
    }

    Write-Host "Building solution ($Configuration)..."
    dotnet build $solutionPath -c $Configuration --no-restore
    if ($LASTEXITCODE -ne 0) {
        exit $LASTEXITCODE
    }

    Write-Host 'Build completed successfully.'
    exit 0
}
finally {
    Pop-Location
}
