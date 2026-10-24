#ifndef AppVersion
  #error AppVersion must be supplied by scripts/build-installer.ps1
#endif
#ifndef BuildDir
  #error BuildDir must be supplied by scripts/build-installer.ps1
#endif
#ifndef OutputDir
  #error OutputDir must be supplied by scripts/build-installer.ps1
#endif
#ifndef FrameworkInstaller
  #error FrameworkInstaller must be supplied by scripts/build-installer.ps1
#endif

[Setup]
AppId=Relay.Discord
AppName=Relay
AppVersion={#AppVersion}
AppPublisher=Relay contributors
AppVerName=Relay {#AppVersion}
DefaultDirName={autopf}\Relay
DefaultGroupName=Relay
DisableProgramGroupPage=yes
PrivilegesRequired=admin
ArchitecturesAllowed=x64os
ArchitecturesInstallIn64BitMode=x64os
MinVersion=10.0
OutputDir={#OutputDir}
OutputBaseFilename=Relay-Setup-Windows-x64-v{#AppVersion}
SetupIconFile=..\assets\relay.ico
UninstallDisplayIcon={app}\Relay.exe
UninstallDisplayName=Relay
VersionInfoVersion={#AppVersion}.0
VersionInfoDescription=Relay kurulum paketi
LicenseFile=..\LICENSE
InfoBeforeFile=Setup-notlari.txt
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
AppMutex=Local\Relay_Discord_SingleInstance
CloseApplications=no
RestartApplications=no
AlwaysRestart=no

[Languages]
Name: "turkish"; MessagesFile: "compiler:Languages\Turkish.isl"

[Tasks]
Name: "desktopicon"; Description: "Masaüstüne Relay kısayolu ekle"; GroupDescription: "Kısayollar:"; Flags: unchecked

[Files]
Source: "{#BuildDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#FrameworkInstaller}"; DestDir: "{tmp}"; DestName: "ndp48-web.exe"; Flags: dontcopy

[Icons]
Name: "{group}\Relay"; Filename: "{app}\Relay.exe"; WorkingDir: "{app}"
Name: "{autodesktop}\Relay"; Filename: "{app}\Relay.exe"; WorkingDir: "{app}"; Tasks: desktopicon

[Run]
Filename: "{app}\Relay.exe"; Description: "Relay'i aç"; Flags: nowait postinstall skipifsilent unchecked runasoriginaluser

[Code]
function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  ExitCode: Integer;
begin
  Result := '';
  if IsDotNetInstalled(net48, 0) then Exit;
  ExtractTemporaryFile('ndp48-web.exe');
  if not Exec(ExpandConstant('{tmp}\ndp48-web.exe'), '/passive /norestart', '', SW_SHOW, ewWaitUntilTerminated, ExitCode) then
  begin
    Result := 'Microsoft .NET Framework kurucusu açılamadı. Tekrar deneyebilir veya Microsoft sitesinden .NET Framework 4.8 kurabilirsin.';
    Exit;
  end;
  if ExitCode = 3010 then
  begin
    NeedsRestart := True;
    Result := '.NET Framework kuruldu. Windows yeniden başladıktan sonra Relay kurulumunu tekrar aç.';
    Exit;
  end;
  if (ExitCode <> 0) or not IsDotNetInstalled(net48, 0) then
    Result := '.NET Framework 4.8 kurulumu tamamlanamadı (kod ' + IntToStr(ExitCode) + '). İnternet bağlantısını kontrol edip tekrar dene.';
end;

function InitializeUninstall(): Boolean;
var
  ExitCode: Integer;
begin
  Result := False;
  if not FileExists(ExpandConstant('{app}\Relay.exe')) then
  begin
    MsgBox('Relay.exe eksik olduğu için ağ kayıtları denetlenemedi. Setup ile dosyaları onardıktan sonra kaldırmayı tekrar dene.', mbError, MB_OK);
    Exit;
  end;
  if not Exec(ExpandConstant('{app}\Relay.exe'), '--uninstall-cleanup', '', SW_HIDE, ewWaitUntilTerminated, ExitCode) then
  begin
    MsgBox('Relay ayarları temizlenemedi. Uygulamayı kapatıp kaldırmayı tekrar dene.', mbError, MB_OK);
    Exit;
  end;
  if ExitCode <> 0 then
  begin
    MsgBox('Relay açık olabilir veya kendi ağ kayıtlarını temizleyemedi. Sistem tepsisinden Durdur ve çık seçeneğini kullanıp tekrar dene. Dosyalar kaldırılmadı.', mbError, MB_OK);
    Exit;
  end;
  Result := True;
end;
