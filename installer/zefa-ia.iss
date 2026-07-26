; Zefa IA - Inno Setup script
;
; Build the payload first:
;   pwsh installer/build-installer.ps1
;
; Or manually:
;   dotnet publish src/ZefaIA.App -p:PublishProfile=win-x64
;   iscc installer/zefa-ia.iss

#define AppName "Zefa IA"
#define AppVersion "1.0.0"
#define AppPublisher "Aennson"
#define AppExeName "ZefaIA.App.exe"
#define PublishDir "..\src\ZefaIA.App\bin\publish\win-x64"

[Setup]
AppId={{8F3A2C71-4B6D-4E19-9A2F-1D7E5C8B3A46}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
DefaultDirName={autopf}\ZefaIA
DefaultGroupName={#AppName}
OutputDir=output
OutputBaseFilename=ZefaIA-Setup-{#AppVersion}
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
; The app is self-contained, so no .NET runtime check is needed. It does require
; Windows 10 1903 for WASAPI loopback and SetWindowDisplayAffinity.
MinVersion=10.0.18362
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
UninstallDisplayIcon={app}\{#AppExeName}

[Languages]
Name: "brazilianportuguese"; MessagesFile: "compiler:Languages\BrazilianPortuguese.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "autostart"; Description: "Iniciar o Zefa IA junto com o Windows"; GroupDescription: "Inicializacao:"; Flags: unchecked

[Files]
Source: "{#PublishDir}\{#AppExeName}"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "..\README.md"; DestDir: "{app}"; DestName: "README.md"; Flags: ignoreversion
Source: "..\docs\USAGE.md"; DestDir: "{app}\docs"; Flags: ignoreversion

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExeName}"
Name: "{group}\Guia de Uso"; Filename: "{app}\docs\USAGE.md"
Name: "{group}\{cm:UninstallProgram,{#AppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExeName}"; Tasks: desktopicon

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; \
    ValueType: string; ValueName: "ZefaIA"; ValueData: """{app}\{#AppExeName}"""; \
    Flags: uninsdeletevalue; Tasks: autostart

[Run]
Filename: "{app}\{#AppExeName}"; Description: "{cm:LaunchProgram,{#AppName}}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
; Only the app's own folder. Meeting data in %APPDATA%\ZefaIA is left alone and
; removed separately if the user confirms in CurUninstallStepChanged.
Type: filesandordirs; Name: "{app}"

[Code]
// The Whisper model is downloaded on first use rather than bundled, which keeps
// the installer around 80 MB instead of 220 MB.

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  DataDir: String;
begin
  if CurUninstallStep = usPostUninstall then
  begin
    DataDir := ExpandConstant('{userappdata}\ZefaIA');
    if DirExists(DataDir) then
    begin
      if MsgBox('Remover tambem o historico de reunioes e as configuracoes?' + #13#10 + #13#10 +
                DataDir + #13#10 + #13#10 +
                'Escolha Nao para manter seus dados.',
                mbConfirmation, MB_YESNO or MB_DEFBUTTON2) = IDYES then
        DelTree(DataDir, True, True, True);
    end;
  end;
end;
