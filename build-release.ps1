param(
    [string]$Runtime = "win-x64"
)

$ErrorActionPreference = "Stop"
$project = Join-Path $PSScriptRoot "src\VORTEX.UI\VORTEX.UI.csproj"
$output = Join-Path $PSScriptRoot "artifacts\VORTEX-$Runtime"

dotnet publish $project `
    --configuration Release `
    --runtime $Runtime `
    --self-contained true `
    --output $output `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:DebugType=None `
    -p:DebugSymbols=false

Write-Host "VORTEX publicado em: $output"
