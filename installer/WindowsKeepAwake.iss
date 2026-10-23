#ifndef AppPublishDir
  #error AppPublishDir must point to the win-x64 publish directory
#endif
#ifndef ProjectRoot
  #error ProjectRoot must point to the project root
#endif
#ifndef AppVersion
  #error AppVersion must match the application project version
#endif

[Setup]
AppId={{3C9D1D70-09D4-4D18-B272-3BCF2A8CFB51}
AppName=Windows Keep Awake
AppVersion={#AppVersion}
AppVerName=Windows Keep Awake {#AppVersion}
AppPublisher=Windows Keep Awake contributors
AppPublisherURL=https://github.com/lsfaht
AppReadmeFile={app}\README.md
DefaultDirName={localappdata}\Programs\WindowsKeepAwake
DefaultGroupName=Windows Keep Awake
UsePreviousAppDir=yes
UsePreviousGroup=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
AppMutex=Local\WindowsKeepAwake-{username}
SetupIconFile={#ProjectRoot}\src\KeepAwake.Windows\Assets\WindowsKeepAwake.ico
UninstallDisplayIcon={app}\KeepAwake.Windows.exe
CloseApplications=no
Uninstallable=yes
OutputBaseFilename=WindowsKeepAwake-Setup-{#AppVersion}-win-x64
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
SetupLogging=yes
UninstallDisplayName=Windows Keep Awake

[Tasks]
Name: "desktopicon"; Description: "Criar um atalho na área de trabalho"; GroupDescription: "Atalhos:"; Flags: unchecked

[Files]
Source: "{#AppPublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#ProjectRoot}\README.md"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\Windows Keep Awake"; Filename: "{app}\KeepAwake.Windows.exe"; WorkingDir: "{app}"; IconFilename: "{app}\KeepAwake.Windows.exe"
Name: "{autodesktop}\Windows Keep Awake"; Filename: "{app}\KeepAwake.Windows.exe"; WorkingDir: "{app}"; IconFilename: "{app}\KeepAwake.Windows.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\KeepAwake.Windows.exe"; Description: "Iniciar Windows Keep Awake agora"; Flags: postinstall nowait skipifsilent unchecked

[UninstallDelete]
; User settings and diagnostics live under LocalAppData\WindowsKeepAwake and are intentionally preserved.

[Code]
const
  StartupRunKey = 'Software\Microsoft\Windows\CurrentVersion\Run';
  StartupValueName = 'WindowsKeepAwake';

procedure CurStepChanged(CurStep: TSetupStep);
var
  ExistingStartup: string;
  ExistingExecutable: string;
  InstalledCommand: string;
  StartupSuffixPosition: Integer;
begin
  if CurStep = ssPostInstall then begin
    { Preserve an existing user-enabled startup choice, updating only its executable path. }
    if RegQueryStringValue(HKCU, StartupRunKey, StartupValueName, ExistingStartup) then begin
      if ExistingStartup <> '' then begin
        StartupSuffixPosition := Pos('" --minimized', ExistingStartup);
        if (Copy(ExistingStartup, 1, 1) = '"') and (StartupSuffixPosition > 2) then begin
          ExistingExecutable := Copy(ExistingStartup, 2, StartupSuffixPosition - 2);
          if (CompareText(ExtractFileName(ExistingExecutable), 'KeepAwake.Windows.exe') = 0) or
             (CompareText(ExtractFileName(ExistingExecutable), 'WindowsKeepAwake.exe') = 0) then begin
            InstalledCommand := '"' + ExpandConstant('{app}\KeepAwake.Windows.exe') + '" --minimized';
            RegWriteStringValue(HKCU, StartupRunKey, StartupValueName, InstalledCommand);
          end;
        end;
      end else
        RegDeleteValue(HKCU, StartupRunKey, StartupValueName);
    end;
  end;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  ExistingStartup: string;
  InstalledCommand: string;
begin
  if CurUninstallStep = usUninstall then begin
    InstalledCommand := '"' + ExpandConstant('{app}\KeepAwake.Windows.exe') + '" --minimized';
    if RegQueryStringValue(HKCU, StartupRunKey, StartupValueName, ExistingStartup) and
       (CompareText(ExistingStartup, InstalledCommand) = 0) then
      RegDeleteValue(HKCU, StartupRunKey, StartupValueName);
  end;
end;
