$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path -Parent $PSScriptRoot
$taskLock = Get-Content -LiteralPath (Join-Path $taskRoot 'third-party\installer.lock.json') -Raw -Encoding UTF8 | ConvertFrom-Json
$taskCache = Join-Path $taskRoot 'artifacts\vendor'
New-Item -ItemType Directory -Path $taskCache -Force | Out-Null
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
function Get-RelayBuildDependency($taskEntry) {
    $taskPath = Join-Path $taskCache $taskEntry.file
    if (-not (Test-Path -LiteralPath $taskPath)) {
        Invoke-WebRequest -Uri $taskEntry.url -OutFile $taskPath -UseBasicParsing -TimeoutSec 120
    }
    if ((Get-FileHash -LiteralPath $taskPath -Algorithm SHA256).Hash -ne $taskEntry.sha256) { throw ('SHA-256 eslesmedi: ' + $taskEntry.file) }
    $taskSignature = Get-AuthenticodeSignature -LiteralPath $taskPath
    if ($taskSignature.Status -ne 'Valid' -or $taskSignature.SignerCertificate.Subject -notlike ('*' + $taskEntry.signer + '*')) { throw ('Yayinci imzasi dogrulanamadi: ' + $taskEntry.file) }
    return $taskPath
}
$taskCompilerInstaller = Get-RelayBuildDependency $taskLock.compiler
$taskFrameworkInstaller = Get-RelayBuildDependency $taskLock.framework
$taskCompilerDirectory = Join-Path $taskRoot ('artifacts\tools\InnoSetup-' + $taskLock.compiler.version)
$taskCompiler = Join-Path $taskCompilerDirectory 'ISCC.exe'
if (-not (Test-Path -LiteralPath $taskCompiler)) {
    New-Item -ItemType Directory -Path $taskCompilerDirectory -Force | Out-Null
    # Vendor-supported portable mode: no registry, file associations or uninstall entry.
    $taskArguments = @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART','/SP-','/NOICONS','/TASKS=','/PORTABLE=1','/CURRENTUSER',('/DIR="' + $taskCompilerDirectory + '"'))
    $taskProcess = Start-Process -FilePath $taskCompilerInstaller -ArgumentList $taskArguments -WindowStyle Hidden -Wait -PassThru
    if ($taskProcess.ExitCode -ne 0 -or -not (Test-Path -LiteralPath $taskCompiler)) { throw 'Tasinabilir Inno Setup derleyicisi hazirlanamadi.' }
}
return [pscustomobject]@{ Compiler = $taskCompiler; FrameworkInstaller = $taskFrameworkInstaller }
