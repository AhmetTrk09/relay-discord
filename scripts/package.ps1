param([switch]$SkipBuild, [switch]$IncludeInstaller)
$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path -Parent $PSScriptRoot
$taskVersion = (Get-Content -LiteralPath (Join-Path $taskRoot 'VERSION') -Raw).Trim()
if (-not $SkipBuild) { & (Join-Path $PSScriptRoot 'build.ps1') }
& (Join-Path $PSScriptRoot 'verify-dependencies.ps1')
& (Join-Path $PSScriptRoot 'fetch-third-party-sources.ps1')
$taskBuild = Join-Path $taskRoot 'artifacts\build'
$taskExecutable = Join-Path $taskBuild 'Relay.exe'
if (-not (Test-Path -LiteralPath $taskExecutable)) { throw 'Once build.bat ile derle.' }
if ([Diagnostics.FileVersionInfo]::GetVersionInfo($taskExecutable).FileVersion -ne ($taskVersion + '.0')) { throw 'EXE ve paket surumleri farkli.' }
$taskRelease = Join-Path $taskRoot ('artifacts\release\v' + $taskVersion)
$taskStaging = Join-Path $taskRoot ('artifacts\staging-' + [Guid]::NewGuid().ToString('N'))
$taskRuntime = Join-Path $taskStaging 'Relay-Windows-x64'
$taskSourceBundle = Join-Path $taskStaging 'Relay-third-party-sources'
New-Item -ItemType Directory -Path $taskRelease,$taskRuntime,$taskSourceBundle -Force | Out-Null
# Only application files go into the download packages.
foreach ($taskFile in @('Relay.exe','Relay.exe.config','LICENSE','THIRD_PARTY_NOTICES.md','KULLANIM.txt')) {
    Copy-Item -LiteralPath (Join-Path $taskBuild $taskFile) -Destination $taskRuntime -Force
}
foreach ($taskFolder in @('assets','bin','lists','licenses')) {
    Copy-Item -LiteralPath (Join-Path $taskBuild $taskFolder) -Destination $taskRuntime -Recurse -Force
}
# Refuse a stale build containing different dependencies.
$taskLockPath = Join-Path $taskRoot 'third-party\dependencies.lock.json'
$taskLock = Get-Content -LiteralPath $taskLockPath -Raw -Encoding UTF8 | ConvertFrom-Json
foreach ($taskEntry in $taskLock.files) {
    if ((Get-FileHash -LiteralPath (Join-Path $taskRuntime $taskEntry.path) -Algorithm SHA256).Hash -ne $taskEntry.sha256) { throw ('Eski derleme dosyasi: ' + $taskEntry.path) }
}
$taskRuntimeHashes = @(Get-ChildItem -LiteralPath $taskRuntime -Recurse -File | Sort-Object FullName | ForEach-Object {
    (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant() + '  ' + $_.FullName.Substring($taskRuntime.Length + 1).Replace('\','/')
})
$taskVerification = Join-Path $taskRoot ('artifacts\verification\v' + $taskVersion)
New-Item -ItemType Directory -Path $taskVerification -Force | Out-Null
$taskRuntimeHashes | Set-Content -LiteralPath (Join-Path $taskVerification 'runtime-files.txt') -Encoding ASCII
Copy-Item -Path (Join-Path $taskRoot 'third-party\sources\*') -Destination $taskSourceBundle -Force
Copy-Item -LiteralPath $taskLockPath -Destination $taskSourceBundle
Copy-Item -LiteralPath (Join-Path $taskRoot 'THIRD_PARTY_NOTICES.md') -Destination $taskSourceBundle
Copy-Item -LiteralPath (Join-Path $taskRoot 'licenses') -Destination $taskSourceBundle -Recurse
$taskRuntimeZip = Join-Path $taskRelease ('Relay-Windows-x64-v' + $taskVersion + '.zip')
$taskSourcesZip = Join-Path $taskRelease ('Relay-third-party-sources-v' + $taskVersion + '.zip')
Compress-Archive -LiteralPath $taskRuntime -DestinationPath $taskRuntimeZip -CompressionLevel Optimal -Force
Compress-Archive -LiteralPath $taskSourceBundle -DestinationPath $taskSourcesZip -CompressionLevel Optimal -Force
$taskReleaseFiles = @($taskRuntimeZip,$taskSourcesZip)
if ($IncludeInstaller) {
    & (Join-Path $PSScriptRoot 'build-installer.ps1') -BuildDirectory $taskRuntime -OutputDirectory $taskRelease
    $taskReleaseFiles += Join-Path $taskRelease ('Relay-Setup-Windows-x64-v' + $taskVersion + '.exe')
}
$taskReleaseFiles | ForEach-Object {
    (Get-FileHash -LiteralPath $_ -Algorithm SHA256).Hash.ToLowerInvariant() + '  ' + [IO.Path]::GetFileName($_)
} | Set-Content -LiteralPath (Join-Path $taskRelease 'SHA256SUMS.txt') -Encoding ASCII
Write-Host ('Paketler hazir: ' + $taskRelease)
