param(
    [string]$Runtime = "win-x64",
    [switch]$Installer
)

$ErrorActionPreference = "Stop"
$projectRoot = $PSScriptRoot
$publishDirectory = Join-Path $projectRoot "release\$Runtime"

dotnet publish (Join-Path $projectRoot "ReimbursementAssistant.csproj") `
    -c Release `
    -r $Runtime `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:PublishDir="$publishDirectory\"

if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE."
}

Write-Host "Portable build created: $publishDirectory\ReimBox.exe" -ForegroundColor Green

if ($Installer) {
    $compilerCandidates = @(
        "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
        "$env:ProgramFiles\Inno Setup 6\ISCC.exe"
    ) | Where-Object { $_ -and (Test-Path $_) }

    if ($compilerCandidates.Count -eq 0) {
        throw "Inno Setup 6 was not found. Install it before using -Installer."
    }

    & $compilerCandidates[0] (Join-Path $projectRoot "installer\ReimBox.iss")
    if ($LASTEXITCODE -ne 0) {
        throw "Inno Setup failed with exit code $LASTEXITCODE."
    }

    Write-Host "Installer created: $projectRoot\installer\output" -ForegroundColor Green
}
