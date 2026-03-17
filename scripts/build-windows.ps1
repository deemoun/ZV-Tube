$ErrorActionPreference = 'Stop'

$root = Resolve-Path "$PSScriptRoot/.."
$outDir = Join-Path $root 'artifacts/windows-x64'

Push-Location $root

dotnet publish "ZV Player.csproj" -c Release -r win-x64 --self-contained true -o $outDir

Write-Host "Windows binaries published to $outDir"

Pop-Location
