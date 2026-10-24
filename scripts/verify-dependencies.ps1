$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path -Parent $PSScriptRoot
$taskLock = Get-Content -LiteralPath (Join-Path $taskRoot 'third-party\dependencies.lock.json') -Raw -Encoding UTF8 | ConvertFrom-Json
$taskExpected = @{}
foreach ($taskEntry in $taskLock.files) {
    $taskExpected[$taskEntry.path.Replace('\','/')] = $true
    $taskPath = Join-Path $taskRoot $taskEntry.path
    if (-not (Test-Path -LiteralPath $taskPath -PathType Leaf)) { throw ('Eksik dosya: ' + $taskEntry.path) }
    if ((Get-FileHash -LiteralPath $taskPath -Algorithm SHA256).Hash -ne $taskEntry.sha256) { throw ('SHA-256 eslesmedi: ' + $taskEntry.path) }
}
foreach ($taskFile in Get-ChildItem -LiteralPath (Join-Path $taskRoot 'bin') -Recurse -File) {
    $taskRelative = $taskFile.FullName.Substring($taskRoot.Length + 1).Replace('\','/')
    if (-not $taskExpected.ContainsKey($taskRelative)) { throw ('Kilit dosyasinda bulunmayan dosya: ' + $taskRelative) }
}
Write-Host ('Bagimliliklar dogrulandi: ' + $taskLock.files.Count + ' dosya.')
