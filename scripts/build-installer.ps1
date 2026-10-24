param([string]$BuildDirectory, [string]$OutputDirectory)
$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path -Parent $PSScriptRoot
$taskVersion = (Get-Content -LiteralPath (Join-Path $taskRoot 'VERSION') -Raw).Trim()
if (-not $BuildDirectory) { $BuildDirectory = Join-Path $taskRoot 'artifacts\build' }
if (-not $OutputDirectory) { $OutputDirectory = Join-Path $taskRoot ('artifacts\release\v' + $taskVersion) }
$taskBuild = [IO.Path]::GetFullPath($BuildDirectory)
$taskExecutable = Join-Path $taskBuild 'Relay.exe'
if (-not (Test-Path -LiteralPath $taskExecutable)) { throw 'Once build.bat ile derle.' }
if ([Diagnostics.FileVersionInfo]::GetVersionInfo($taskExecutable).FileVersion -ne ($taskVersion + '.0')) { throw 'EXE ve VERSION farkli.' }
$taskTools = & (Join-Path $PSScriptRoot 'prepare-installer-tools.ps1')
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$taskArguments = @('/Qp', ('/DAppVersion=' + $taskVersion), ('/DBuildDir=' + $taskBuild), ('/DOutputDir=' + [IO.Path]::GetFullPath($OutputDirectory)), ('/DFrameworkInstaller=' + $taskTools.FrameworkInstaller), (Join-Path $taskRoot 'installer\Relay.iss'))
& $taskTools.Compiler @taskArguments
if ($LASTEXITCODE -ne 0) { throw 'Relay Setup derlenemedi.' }
Write-Host ('Setup hazir: ' + (Join-Path $OutputDirectory ('Relay-Setup-Windows-x64-v' + $taskVersion + '.exe')))
