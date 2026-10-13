$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$destination = Join-Path $repoRoot 'artifacts\releases\MouseMotionInspector-source.zip'
New-Item -ItemType Directory -Force -Path (Split-Path -Parent $destination) | Out-Null
$files = [Collections.Generic.List[string]]::new()
foreach ($name in @('.gitignore', '.gitattributes', '.editorconfig', 'global.json', 'README.md', 'LICENSE', 'CONTRIBUTING.md', 'SECURITY.md', 'build-native.ps1', 'index.html')) {
    $files.Add((Join-Path $repoRoot $name))
}
foreach ($folder in @('.github', 'docs', 'tests', 'scripts', 'native')) {
    Get-ChildItem -LiteralPath (Join-Path $repoRoot $folder) -Recurse -File -Force |
        Where-Object { $_.FullName -notmatch '[\\/](bin|obj|recordings)[\\/]' -and $_.Extension -in @('.cs', '.csproj', '.props', '.Config', '.md', '.png', '.cjs', '.ps1', '.yml', '.yaml') } |
        ForEach-Object { $files.Add($_.FullName) }
}
$stream = [IO.File]::Open($destination, [IO.FileMode]::Create)
$zip = [IO.Compression.ZipArchive]::new($stream, [IO.Compression.ZipArchiveMode]::Create)
try {
    foreach ($file in ($files | Sort-Object)) {
        $relative = [IO.Path]::GetRelativePath($repoRoot, $file).Replace('\', '/')
        [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip, $file, $relative, [IO.Compression.CompressionLevel]::Optimal) | Out-Null
    }
} finally { $zip.Dispose(); $stream.Dispose() }
$hash = (Get-FileHash -LiteralPath $destination -Algorithm SHA256).Hash.ToLowerInvariant()
[IO.File]::WriteAllText("$destination.sha256", "$hash  MouseMotionInspector-source.zip`n")
Write-Output "Source package: $destination"
