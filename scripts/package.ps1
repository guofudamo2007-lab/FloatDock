$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
Push-Location $repoRoot
try {
    git diff HEAD --quiet
    if ($LASTEXITCODE -ne 0) { throw 'Commit the source before packaging so the source ZIP matches the binary.' }
    $untracked = git ls-files --others --exclude-standard
    if ($untracked) { throw 'Commit or explicitly ignore untracked source before packaging.' }
    [xml]$project = Get-Content src/FloatDock.Windows/FloatDock.Windows.csproj
    $version = $project.Project.PropertyGroup.Version
    $packageRoot = Join-Path $repoRoot ('artifacts/package-' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
    if (Test-Path -LiteralPath $packageRoot) { throw 'Package directory already exists; refusing to overwrite.' }
    $appName = "FloatDock-$version-preview-win-x64"
    $appDir = Join-Path $packageRoot $appName
    $null = New-Item -ItemType Directory -Path $appDir
    dotnet publish src/FloatDock.Windows -c Release -r win-x64 --self-contained false -o $appDir
    if ($LASTEXITCODE -ne 0) { throw 'Publish failed.' }
    Copy-Item scripts/restore-taskbar.cmd,README.md,LICENSE,CHANGELOG.md,THIRD-PARTY-NOTICES.md $appDir
    foreach ($folder in 'docs','integrations','licenses') { Copy-Item -LiteralPath $folder -Destination (Join-Path $appDir $folder) -Recurse }
    $sourceZip = Join-Path $packageRoot "FloatDock-$version-source.zip"
    git archive --format=zip --prefix=FloatDock/ -o $sourceZip HEAD
    if ($LASTEXITCODE -ne 0) { throw 'Corresponding source archive failed.' }
    Copy-Item -LiteralPath $sourceZip -Destination $appDir
    $revision = git rev-parse HEAD
    Set-Content -LiteralPath (Join-Path $appDir 'BUILD.txt') -Value "FloatDock $version`nSource revision: $revision`nFramework-dependent Windows x64. Native integration not desktop-validated in this iteration." -Encoding UTF8
    $appZip = Join-Path $packageRoot "$appName.zip"
    Compress-Archive -LiteralPath $appDir -DestinationPath $appZip
    $hashes = Get-FileHash -LiteralPath $appZip,$sourceZip -Algorithm SHA256
    $hashes | ForEach-Object { "$($_.Hash)  $([IO.Path]::GetFileName($_.Path))" } | Set-Content -LiteralPath (Join-Path $packageRoot 'SHA256SUMS.txt')
    Write-Output "Packaged without launching any app: $appZip"
    $hashes | Select-Object Hash,Path
}
finally { Pop-Location }
