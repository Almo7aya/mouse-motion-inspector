$ErrorActionPreference = 'Stop'
Push-Location -LiteralPath $PSScriptRoot
try {
    dotnet build native/MouseMotionInspector.Checks/MouseMotionInspector.Checks.csproj -c Release --configfile native/NuGet.Config --disable-build-servers -p:UseSharedCompilation=false
    if ($LASTEXITCODE -ne 0) { throw 'Native checks did not build.' }
    dotnet native/MouseMotionInspector.Checks/bin/Release/net10.0/MouseMotionInspector.Checks.dll
    if ($LASTEXITCODE -ne 0) { throw 'Native checks failed.' }
    dotnet publish native/MouseMotionInspector.Native/MouseMotionInspector.Native.csproj -c Release -o artifacts/MouseMotionInspector --configfile native/NuGet.Config --disable-build-servers -p:UseSharedCompilation=false
    if ($LASTEXITCODE -ne 0) { throw 'Native app did not publish.' }
    Write-Output ('Ready: ' + (Join-Path $PSScriptRoot 'artifacts\MouseMotionInspector\MouseMotionInspector.exe'))
} finally { Pop-Location }
