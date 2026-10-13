param([switch]$SelfContained)
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
Push-Location -LiteralPath $repoRoot
try {
    & .\build-native.ps1
    if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
    $flavor = if ($SelfContained) { 'self-contained' } else { 'framework-dependent' }
    [xml]$properties = Get-Content -Raw native/Directory.Build.props
    $version = $properties.Project.PropertyGroup.Version
    $name = "MouseMotionInspector-$version-win-x64-$flavor"
    $stage = Join-Path $repoRoot ('artifacts\package-' + [guid]::NewGuid().ToString('N'))
    $publishArgs = @('publish', 'native/MouseMotionInspector.Native/MouseMotionInspector.Native.csproj', '-c', 'Release', '-r', 'win-x64', '-o', $stage, '--disable-build-servers', '-p:UseSharedCompilation=false')
    if ($SelfContained) { $publishArgs += @('--self-contained', 'true', '--source', 'https://api.nuget.org/v3/index.json') }
    else { $publishArgs += @('--self-contained', 'false', '--configfile', 'native/NuGet.Config') }
    & dotnet @publishArgs
    if ($LASTEXITCODE -ne 0) { throw 'Release publish failed.' }
    Copy-Item -LiteralPath LICENSE, README.md, SECURITY.md, CONTRIBUTING.md, index.html -Destination $stage
    Copy-Item -LiteralPath docs -Destination $stage -Recurse
    $releaseDirectory = Join-Path $repoRoot 'artifacts\releases'
    New-Item -ItemType Directory -Force -Path $releaseDirectory | Out-Null
    $archive = Join-Path $releaseDirectory "$name.zip"
    Compress-Archive -Path (Join-Path $stage '*') -DestinationPath $archive -Force
    $hash = (Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash.ToLowerInvariant()
    [IO.File]::WriteAllText("$archive.sha256", "$hash  $name.zip`n")
    Write-Output "Release package: $archive"
} finally { Pop-Location }
