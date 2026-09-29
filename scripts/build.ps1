param([switch]$Publish)
$ErrorActionPreference = 'Stop'
Set-Location (Split-Path $PSScriptRoot -Parent)
$localSdk = Join-Path $PWD '.tools/dotnet/dotnet.exe'
$sdk = if (Test-Path $localSdk) { $localSdk } else { 'dotnet' }
& $sdk restore VideoGrabber.Tests/VideoGrabber.Tests.csproj --configfile NuGet.Config
if ($LASTEXITCODE -ne 0) { throw 'Restore failed' }
& $sdk build VideoGrabber.Tests/VideoGrabber.Tests.csproj -c Release --no-restore
if ($LASTEXITCODE -ne 0) { throw 'Build failed' }
& $sdk run --project VideoGrabber.Tests -c Release --no-build
if ($LASTEXITCODE -ne 0) { throw 'Tests failed' }
if ($Publish) {
    & $sdk publish VideoGrabber/VideoGrabber.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:RestoreConfigFile="$PWD/NuGet.Config" -o artifacts/VideoGrabber-win-x64
    if ($LASTEXITCODE -ne 0) { throw 'Publish failed' }
    Copy-Item README.md,THIRD-PARTY-NOTICES.md artifacts/VideoGrabber-win-x64
    Compress-Archive -Path artifacts/VideoGrabber-win-x64/* -DestinationPath artifacts/VideoGrabber-win-x64.zip -Force
}
