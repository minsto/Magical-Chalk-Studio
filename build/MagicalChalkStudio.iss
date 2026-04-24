; Installeur Windows — compiler avec Inno Setup 6
; 1) Exécuter build\publish-windows.ps1
; 2) Ouvrir ce fichier dans Inno Setup → Build → Compile

#define MyAppName "Magical Chalk Studio"
#define MyAppVersion "1.0.0"
; Dossier de publication (self-contained) — x64 par défaut
#define BuildDir "dist\win-x64"
#define MyAppExe "MagicalChalkStudio.exe"
#define MyAppPublisher "MickDev"
#define OutputDir "output"

[Setup]
AppId={{A1B2C3D4-E5F6-7890-ABCD-EF1234567890}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
OutputDir={#OutputDir}
OutputBaseFilename=MagicalChalkStudio-Setup-{#MyAppVersion}-x64
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin
ArchitecturesInstallIn64BitMode=x64
UninstallDisplayIcon={app}\{#MyAppExe}
SetupIconFile=..\MagicalChalkStudio.Wpf\Assets\magic_chalk.ico
[Languages]
Name: "french"; MessagesFile: "compiler:Languages\French.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
; Publie d'abord avec publish-windows.ps1 — chemins relatifs à ce fichier .iss (dossier build\)
Source: "{#BuildDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExe}"; WorkingDir: "{app}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExe}"; Tasks: desktopicon; WorkingDir: "{app}"

[Run]
Filename: "{app}\{#MyAppExe}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent
