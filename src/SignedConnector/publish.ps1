# End-to-end demo: keygen -> build+sign -> verify locally.
# Run from this directory after `dotnet tool install -g Kuestenlogik.Surgewave.Cli`.

$ErrorActionPreference = 'Stop'

$publisher = 'demo-publisher'
$keysDir = Join-Path $PSScriptRoot 'keys'
$privateKey = Join-Path $keysDir "$publisher.key"
$publicKey = Join-Path $keysDir "$publisher.pub"
$trustDir = Join-Path $PSScriptRoot 'plugins/trusted-keys'

if (-not (Test-Path $privateKey))
{
    Write-Host "=== Generating key pair ==="
    surgewave plugins keygen $publisher --output $keysDir
}

Write-Host "`n=== Trusting the publisher locally ==="
New-Item -ItemType Directory -Force -Path $trustDir | Out-Null
Copy-Item -Force $publicKey (Join-Path $trustDir "$publisher.pub")

Write-Host "`n=== Publishing + signing ==="
dotnet publish -c Release `
    -p:SurgewavePackPlugin=true `
    -p:SurgewaveSigningKey=$privateKey

$pluginPackage = Get-ChildItem -Recurse (Join-Path $PSScriptRoot 'artifacts/pub') -Filter '*.swpkg' | Select-Object -First 1
if (-not $pluginPackage) { throw "No .swpkg produced under artifacts/pub/" }

Write-Host "`n=== Verifying locally ==="
surgewave plugins verify $pluginPackage.FullName --plugins-dir (Join-Path $PSScriptRoot 'plugins')

Write-Host "`nPackage: $($pluginPackage.FullName)"
Write-Host "Signature: $($pluginPackage.FullName).sig"
Write-Host "SBOM is embedded at sbom.json inside the .swpkg"
