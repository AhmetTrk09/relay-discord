param([string]$SourcesDirectory)
$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path -Parent $PSScriptRoot
if (-not $SourcesDirectory) { $SourcesDirectory = Join-Path $taskRoot 'third-party\sources' }
New-Item -ItemType Directory -Path $SourcesDirectory -Force | Out-Null
$taskLock = Get-Content -LiteralPath (Join-Path $taskRoot 'third-party\dependencies.lock.json') -Raw -Encoding UTF8 | ConvertFrom-Json
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
foreach ($taskEntry in $taskLock.sources) {
    if ([IO.Path]::GetFileName($taskEntry.file) -ne $taskEntry.file) { throw 'Gecersiz kaynak arsivi adi.' }
    $taskTarget = Join-Path $SourcesDirectory $taskEntry.file
    if ((Test-Path -LiteralPath $taskTarget) -and (Get-FileHash -LiteralPath $taskTarget -Algorithm SHA256).Hash -eq $taskEntry.sha256) { continue }
    $taskTemporary = $taskTarget + '.download'
    Invoke-WebRequest -Uri $taskEntry.url -OutFile $taskTemporary -UseBasicParsing
    if ((Get-FileHash -LiteralPath $taskTemporary -Algorithm SHA256).Hash -ne $taskEntry.sha256) { throw ('Kaynak arsivi SHA-256 eslesmedi: ' + $taskEntry.file) }
    Move-Item -LiteralPath $taskTemporary -Destination $taskTarget -Force
}
Write-Host ('Kaynak arsivleri dogrulandi: ' + $taskLock.sources.Count + ' arsiv.')
