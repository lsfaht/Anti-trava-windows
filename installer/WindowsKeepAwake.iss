#ifndef AppPublishDir
  #error AppPublishDir must point to the win-x64 publish directory
#endif
#ifndef ProjectRoot
  #error ProjectRoot must point to the project root
#endif

[Setup]
AppId={{3C9D1D70-09D4-4D18-B272-3BCF2A8CFB51}
AppName=Windows Keep Awake
AppVersion=1.0.0
AppVerName=Windows Keep Awake 1.0.0
AppPublisher=Windows Keep Awake contributors
AppPublisherURL=https://github.com/lsfaht
AppReadmeFile={app}\README.md
DefaultDirName={localappdata}\Programs\WindowsKeepAwake
DefaultGroupName=Windows Keep Awake
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
AppMutex=Local\WindowsKeepAwake-{username}
SetupIconFile={#ProjectRoot}\src\KeepAwake.Windows\Assets\WindowsKeepAwake.ico
UninstallDisplayIcon={app}\WindowsKeepAwake.exe
CloseApplications=no
Uninstallable=yes
OutputBaseFilename=WindowsKeepAwake-Setup-1.0.0-win-x64
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
SetupLogging=yes
UninstallDisplayName=Windows Keep Awake

[Tasks]
Name: "desktopicon"; Description: "Criar um atalho na área de trabalho"; GroupDescription: "Atalhos:"; Flags: unchecked

[Files]
Source: "{#AppPublishDir}\KeepAwake.Windows.exe"; DestDir: "{app}"; DestName: "WindowsKeepAwake.exe"; Flags: ignoreversion
Source: "{#ProjectRoot}\README.md"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\Windows Keep Awake"; Filename: "{app}\WindowsKeepAwake.exe"; WorkingDir: "{app}"
Name: "{autodesktop}\Windows Keep Awake"; Filename: "{app}\WindowsKeepAwake.exe"; WorkingDir: "{app}"; Tasks: desktopicon

[Run]
Filename: "{app}\WindowsKeepAwake.exe"; Description: "Iniciar Windows Keep Awake agora"; Flags: postinstall nowait skipifsilent unchecked

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueName: "WindowsKeepAwake"; Flags: uninsdeletevalue

[UninstallDelete]
; User settings and diagnostics live under LocalAppData\WindowsKeepAwake and are intentionally preserved.
