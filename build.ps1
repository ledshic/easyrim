param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',

    [switch]$NoRestore
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$solutionPath = Join-Path $repoRoot 'easymode.sln'
$workshopPath = Join-Path $repoRoot '_workshop'
$workshopItems = @(
    'About',
    'Assemblies',
    'Defs',
    'Languages',
    'Patches',
    'Textures'
)

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

    Write-Host "Syncing workshop structure to: $workshopPath"
    foreach ($item in $workshopItems) {
        $sourcePath = Join-Path $repoRoot $item
        if (-not (Test-Path -Path $sourcePath)) {
            throw "Required workshop item not found: $sourcePath"
        }
    }

    if (Test-Path -Path $workshopPath) {
        Get-ChildItem -Path $workshopPath -Force | Remove-Item -Recurse -Force
    }
    else {
        New-Item -ItemType Directory -Path $workshopPath -Force | Out-Null
    }

    foreach ($item in $workshopItems) {
        $sourcePath = Join-Path $repoRoot $item
        Copy-Item -Path $sourcePath -Destination $workshopPath -Recurse -Force
    }

    Write-Host 'Build completed successfully.'
    exit 0
}
finally {
    Pop-Location
}
