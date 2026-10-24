param([string]$OutputDirectory)
$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path -Parent $PSScriptRoot
& (Join-Path $PSScriptRoot 'verify-dependencies.ps1')
$taskVersion = (Get-Content -LiteralPath (Join-Path $taskRoot 'VERSION') -Raw).Trim()
if ($taskVersion -notmatch '^\d+\.\d+\.\d+$') { throw 'VERSION dosyasi major.minor.patch biciminde olmali.' }
$taskGenerated = Join-Path $taskRoot 'artifacts\generated'
New-Item -ItemType Directory -Path $taskGenerated -Force | Out-Null
$taskVersionSource = Join-Path $taskGenerated 'VersionInfo.cs'
@('using System.Reflection;', ('[assembly: AssemblyVersion("' + $taskVersion + '.0")]'), ('[assembly: AssemblyFileVersion("' + $taskVersion + '.0")]')) | Set-Content -LiteralPath $taskVersionSource -Encoding UTF8
$taskManifest = Join-Path $taskGenerated 'app.manifest'
$taskManifestText = [IO.File]::ReadAllText((Join-Path $taskRoot 'src\app.manifest'))
$taskManifestText = $taskManifestText -replace 'version="\d+\.\d+\.\d+\.\d+"',('version="' + $taskVersion + '.0"')
[IO.File]::WriteAllText($taskManifest, $taskManifestText, [Text.UTF8Encoding]::new($true))
if (-not $OutputDirectory) { $OutputDirectory = Join-Path $taskRoot 'artifacts\build' }
$taskOutput = [IO.Path]::GetFullPath($OutputDirectory)
$taskCompiler = Join-Path $env:SystemRoot 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$taskWpf = Join-Path (Split-Path $taskCompiler) 'WPF'
if (-not (Test-Path -LiteralPath $taskCompiler)) { throw '.NET Framework 4.x C# derleyicisi bulunamadi.' }
New-Item -ItemType Directory -Path $taskOutput -Force | Out-Null
$taskArguments = @('/nologo','/target:winexe','/optimize+','/platform:x64','/warn:4','/codepage:65001',
    ('/out:' + (Join-Path $taskOutput 'Relay.exe')),
    ('/win32icon:' + (Join-Path $taskRoot 'assets\relay.ico')),
    ('/win32manifest:' + $taskManifest),
    '/r:System.dll','/r:System.Core.dll','/r:System.Xml.dll','/r:System.Drawing.dll','/r:System.Windows.Forms.dll','/r:System.Xaml.dll',
    ('/r:' + (Join-Path $taskWpf 'WindowsBase.dll')),
    ('/r:' + (Join-Path $taskWpf 'PresentationCore.dll')),
    ('/r:' + (Join-Path $taskWpf 'PresentationFramework.dll')))
$taskSources = @(Get-ChildItem -LiteralPath (Join-Path $taskRoot 'src') -Filter '*.cs' -File | Sort-Object Name | ForEach-Object FullName)
$taskSources += $taskVersionSource
& $taskCompiler @taskArguments @taskSources
if ($LASTEXITCODE -ne 0) { throw 'Derleme basarisiz.' }
Copy-Item -LiteralPath (Join-Path $taskRoot 'src\Relay.exe.config') -Destination (Join-Path $taskOutput 'Relay.exe.config') -Force
New-Item -ItemType Directory -Path (Join-Path $taskOutput 'assets') -Force | Out-Null
foreach ($taskAsset in @('relay.ico')) {
    Copy-Item -LiteralPath (Join-Path $taskRoot ('assets\' + $taskAsset)) -Destination (Join-Path $taskOutput 'assets') -Force
}
foreach ($taskFolder in @('bin','lists','licenses')) {
    Copy-Item -LiteralPath (Join-Path $taskRoot $taskFolder) -Destination $taskOutput -Recurse -Force
}
foreach ($taskFile in @('LICENSE','THIRD_PARTY_NOTICES.md','installer\KULLANIM.txt')) {
    Copy-Item -LiteralPath (Join-Path $taskRoot $taskFile) -Destination $taskOutput -Force
}
Write-Host ('Derlendi: ' + (Join-Path $taskOutput 'Relay.exe'))
