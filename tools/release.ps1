# Builds an installer and update packages with Velopack.
#   .\tools\release.ps1 -Version 1.0.1                   build only (output in artifacts/releases)
#   .\tools\release.ps1 -Version 1.0.1 -Upload -Token ...  also publish to GitHub Releases
# Requires: dotnet tool install -g vpk --version 1.2.161
param(
    [Parameter(Mandatory)][string]$Version,
    [string]$Repo = 'https://github.com/skorjin/Cassandra',
    [switch]$Upload,
    [string]$Token
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$publish = Join-Path $root 'artifacts/publish'
$releases = Join-Path $root 'artifacts/releases'

Remove-Item $publish -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force $releases | Out-Null

if ($Upload) {
    # previous releases are needed to build small delta updates; the first release has none
    vpk download github --repoUrl $Repo --outputDir $releases --token $Token
    if ($LASTEXITCODE -ne 0) { Write-Host 'No previous release found, building a full package only.' }
}

dotnet publish (Join-Path $root 'src/Cassandra.App') -c Release -r win-x64 --self-contained false -p:Version=$Version -o $publish
if ($LASTEXITCODE -ne 0) { throw 'publish failed' }

vpk pack `
    --packId Cassandra `
    --packTitle Cassandra `
    --packVersion $Version `
    --packDir $publish `
    --mainExe Cassandra.exe `
    --outputDir $releases `
    --runtime win-x64 `
    --framework net10.0-x64-desktop
if ($LASTEXITCODE -ne 0) { throw 'pack failed' }

if ($Upload) {
    vpk upload github --repoUrl $Repo --outputDir $releases --publish --releaseName "Cassandra $Version" --tag "v$Version" --token $Token
    if ($LASTEXITCODE -ne 0) { throw 'upload failed' }
}

Write-Host "Done. Output: $releases"
