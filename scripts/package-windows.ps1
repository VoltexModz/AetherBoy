param(
    [string]$DotnetExe = 'dotnet',
    [string]$OutputRoot = '',
    [ValidateSet('development', 'stable')][string]$Channel = 'development',
    [string]$BuildId = ''
)

$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
if ([string]::IsNullOrWhiteSpace($OutputRoot)) {
    $OutputRoot = Join-Path $repoRoot 'artifacts/windows-packages'
}
$resolvedOutputRoot = [System.IO.Path]::GetFullPath($OutputRoot)
$revisionOutput = & git -c "safe.directory=$repoRoot" -C $repoRoot rev-parse --short=12 HEAD
if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($revisionOutput)) {
    throw 'Git-Revision konnte nicht ermittelt werden.'
}
$revision = $revisionOutput.Trim()
$dirty = (& git -c "safe.directory=$repoRoot" -C $repoRoot status --porcelain).Count -gt 0
$stamp = [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss')
if ([string]::IsNullOrWhiteSpace($BuildId)) {
    $BuildId = "$revision-$stamp" + $(if ($dirty) { '-local' } else { '' })
}
if ($BuildId -notmatch '^[A-Za-z0-9][A-Za-z0-9.-]{0,95}$') {
    throw 'BuildId muss aus 1 bis 96 Buchstaben, Ziffern, Punkten oder Bindestrichen bestehen.'
}
$packageName = "AetherBoy-Windows-x64-$buildId"
$packageDirectory = Join-Path $resolvedOutputRoot $packageName
$archivePath = Join-Path $resolvedOutputRoot "$packageName.zip"
if ([System.IO.Directory]::Exists($packageDirectory) -or [System.IO.File]::Exists($archivePath)) {
    throw "Das Paketziel existiert bereits: $packageName"
}
[System.IO.Directory]::CreateDirectory($resolvedOutputRoot) | Out-Null

$sourceRoot = Join-Path $repoRoot ("artifacts/windows-package-build/" + [Guid]::NewGuid().ToString('N'))
$listedFiles = & git -c "safe.directory=$repoRoot" -C $repoRoot ls-files --cached --others --exclude-standard
if ($LASTEXITCODE -ne 0) { throw 'Quelldateien konnten nicht ermittelt werden.' }
$rootFiles = @('Directory.Build.props', 'Directory.Build.targets', 'NuGet.config', 'global.json', 'LICENSE', 'THIRD_PARTY_NOTICES.md', 'CHANGELOG.md')
$extensions = @('.cs', '.csproj', '.props', '.targets', '.json', '.config', '.resx', '.settings', '.md', '.txt', '.ps1', '.sh', '.py', '.html', '.js')
$sourceFiles = @(foreach ($relative in ($listedFiles | Sort-Object -Unique)) {
    if ($relative -notin $rootFiles -and $relative -notmatch '^(nanoboy/|third_party/(GBADotnet.Core|online-native-licenses|mgba-cheats|sharpcompress|discord-rpc)/|branding/|scripts/)') { continue }
    if ($relative -match '(^|/)(bin|obj|\.git)/') { continue }
    $extension = [IO.Path]::GetExtension($relative).ToLowerInvariant()
    $branding = $relative -match '^(branding/|nanoboy/Branding/)' -and $extension -in @('.png', '.ico', '.ttf')
    if ($relative -notin $rootFiles -and $extension -notin $extensions -and -not $branding) { continue }
    $source = Join-Path $repoRoot $relative
    if (-not [IO.File]::Exists($source)) { continue }
    if ((Get-Item -LiteralPath $source).Attributes -band [IO.FileAttributes]::ReparsePoint) { throw "Quellverknüpfung nicht erlaubt: $relative" }
    $target = Join-Path $sourceRoot $relative
    [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($target)) | Out-Null
    Copy-Item -LiteralPath $source -Destination $target
    $relative
})
$project = Join-Path $sourceRoot 'nanoboy/nanoboy.csproj'
# Verify the committed dependency graph first. RID-specific restore may extend
# only this private snapshot's lock files; development bin/obj/locks stay intact.
& $DotnetExe restore $project --locked-mode --configfile (Join-Path $sourceRoot 'NuGet.config')
if ($LASTEXITCODE -ne 0) { throw 'Gesperrte Windows-Wiederherstellung fehlgeschlagen.' }
$sourceRevision = (& git -c "safe.directory=$repoRoot" -C $repoRoot rev-parse HEAD).Trim()
& $DotnetExe publish $project -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=false -p:AetherBoyChannel=$Channel `
    -p:ContinuousIntegrationBuild=true "-p:SourceRevisionId=$sourceRevision" "-p:PathMap=$sourceRoot=/_/" `
    "-p:AetherBoyBuildId=$buildId" -o $packageDirectory
if ($LASTEXITCODE -ne 0) { throw 'Windows-Publish fehlgeschlagen.' }

foreach ($required in @('AetherBoy.exe', 'datachannel.dll', 'LICENSE', 'THIRD_PARTY_NOTICES.md')) {
    if (-not [System.IO.File]::Exists((Join-Path $packageDirectory $required))) {
        throw "Im Paket fehlt: $required"
    }
}

$notices = Join-Path $packageDirectory 'licenses'
[System.IO.Directory]::CreateDirectory($notices) | Out-Null
Copy-Item -LiteralPath (Join-Path $repoRoot 'third_party/GBADotnet.Core/LICENSE.md') -Destination (Join-Path $notices 'GBADotnet-LICENSE.md')
$assets = Get-Content -LiteralPath (Join-Path $sourceRoot 'nanoboy/obj/project.assets.json') -Raw | ConvertFrom-Json
$packageCache = @($assets.packageFolders.PSObject.Properties.Name)[0]
$runtimeConfig = Get-Content -LiteralPath (Join-Path $packageDirectory 'AetherBoy.runtimeconfig.json') -Raw | ConvertFrom-Json
foreach ($framework in $runtimeConfig.runtimeOptions.includedFrameworks) {
    $frameworkPackage = switch ($framework.name) {
        'Microsoft.NETCore.App' { 'microsoft.netcore.app.runtime.win-x64' }
        'Microsoft.WindowsDesktop.App' { 'microsoft.windowsdesktop.app.runtime.win-x64' }
        default { throw "Unbekanntes gebündeltes Framework: $($framework.name)" }
    }
    $frameworkNotices = if ($framework.name -eq 'Microsoft.WindowsDesktop.App') { @('LICENSE') } else { @('LICENSE.TXT', 'THIRD-PARTY-NOTICES.TXT') }
    foreach ($notice in $frameworkNotices) {
        $source = Join-Path $packageCache "$frameworkPackage/$($framework.version)/$notice"
        Copy-Item -LiteralPath $source -Destination (Join-Path $notices "$frameworkPackage-$notice")
    }
}

# Include corresponding current source, including local changes, without user data
# or unrelated projects. Only approved build inputs/assets enter this nested ZIP.
$sourceArchive = [IO.Compression.ZipFile]::Open((Join-Path $packageDirectory 'source.zip'), [IO.Compression.ZipArchiveMode]::Create)
try {
    foreach ($relative in $sourceFiles) {
        $source = Join-Path $sourceRoot $relative
        [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($sourceArchive, $source, "source/$relative", [IO.Compression.CompressionLevel]::Optimal) | Out-Null
    }
} finally { $sourceArchive.Dispose() }
Copy-Item -LiteralPath (Join-Path $repoRoot 'docs/TEST_BUILD_CHECKLIST.txt') -Destination $packageDirectory

$startText = @"
AetherBoy für Windows x64
Build: $buildId
Kanal: $Channel

Starte AetherBoy.exe aus diesem Ordner. Alle mitgelieferten Dateien müssen zusammenbleiben.
Öffne ein Spiel über „Spiel öffnen“ oder ziehe eine eigene .gb-, .gbc- oder .gba-Datei ins Fenster.
Spielstände, Einstellungen und Diagnosen liegen in %LOCALAPPDATA%\AetherBoy.
Deine ROM-Dateien und persönlichen Spielstände sind nicht in diesem Paket enthalten.
Bei Problemen: TOOLS > Problem markieren und den lokalen Diagnosebericht öffnen.
Online Link ist ein Entwicklungsstand. Eine Verbindung bestätigt keinen erfolgreichen Tausch.

Lizenz: LICENSE; Drittkomponenten: THIRD_PARTY_NOTICES.md und licenses/.
Zugehöriger Windows-Quellstand: source.zip. Bauen: dotnet publish nanoboy/nanoboy.csproj -c Release -r win-x64 --self-contained true
Kurzer gemeinsamer Testplan: TEST_BUILD_CHECKLIST.txt.
"@
[System.IO.File]::WriteAllText((Join-Path $packageDirectory 'START_HERE.txt'), $startText,
    [System.Text.UTF8Encoding]::new($false))
$packageInfo = [ordered]@{
    buildId = $BuildId
    sourceCommit = (& git -c "safe.directory=$repoRoot" -C $repoRoot rev-parse HEAD).Trim()
    sourceStatus = $(if ($dirty) { 'modified' } else { 'clean' })
    runtimeIdentifier = 'win-x64'
    channel = $Channel
    frameworks = @($runtimeConfig.runtimeOptions.includedFrameworks)
}
[System.IO.File]::WriteAllText((Join-Path $packageDirectory 'package-info.json'),
    ($packageInfo | ConvertTo-Json), [System.Text.UTF8Encoding]::new($false))

$files = Get-ChildItem -LiteralPath $packageDirectory -File -Recurse | Sort-Object FullName
$hashLines = foreach ($item in $files) {
    $relative = [System.IO.Path]::GetRelativePath($packageDirectory, $item.FullName).Replace('\', '/')
    '{0}  {1}' -f (Get-FileHash -LiteralPath $item.FullName -Algorithm SHA256).Hash.ToLowerInvariant(), $relative
}
[System.IO.File]::WriteAllLines((Join-Path $packageDirectory 'SHA256SUMS.txt'), [string[]]$hashLines,
    [System.Text.UTF8Encoding]::new($false))
Compress-Archive -LiteralPath $packageDirectory -DestinationPath $archivePath -CompressionLevel Optimal
Write-Output "Windows-Paket: $archivePath"
Write-Output "Buildkennung: $buildId"
