$ErrorActionPreference = 'Stop'

$root = Resolve-Path "$PSScriptRoot/.."
$outDir = Join-Path $root 'artifacts/windows-x64'

Push-Location $root

$publishArgs = @(
  'publish'
  'ZV Player.csproj'
  '-c'
  'Release'
  '-r'
  'win-x64'
  '--self-contained'
  'true'
  '-p:PublishSingleFile=true'
  '-p:IncludeNativeLibrariesForSelfExtract=true'
  '-o'
  $outDir
)

dotnet @publishArgs

Write-Host "Windows single-file binary published to $outDir"

Pop-Location
