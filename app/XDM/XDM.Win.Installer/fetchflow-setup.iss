; © Mayanktaker Computers & Web Development | https://mayanktaker.com
; Inno Setup compiler script for FetchFlow Download Manager (Windows x64)

#ifndef AppVersion
  #define AppVersion "9.1.8"
#endif

#ifndef SourceDir
  #define SourceDir "..\..\..\build_output\xdm-win-x64"
#endif

#ifndef OutputDir
  #define OutputDir "..\..\..\xdm-release"
#endif

#define AppName "FetchFlow Download Manager"
#define AppPublisher "Mayanktaker Computers & Web Development"
#define AppURL "https://mayanktaker.com"
#define AppExeName "xdm-app.exe"

[Setup]
AppId={{D387FE84-569A-404B-9DF7-A3508C69298B}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL={#AppURL}
AppSupportURL={#AppURL}
AppUpdatesURL=https://github.com/Mayanktaker/fetchflow/releases
DefaultDirName={autopf}\FetchFlow
DefaultGroupName=FetchFlow Download Manager
AllowNoIcons=yes
OutputDir={#OutputDir}
OutputBaseFilename=fetchflow-windows-x64-setup
SetupIconFile=..\fetchflow-logo.ico
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
DisableProgramGroupPage=yes

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "startupicon"; Description: "Launch FetchFlow when Windows starts"; GroupDescription: "Startup:"

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExeName}"; IconFilename: "{app}\xdm-logo.ico"
Name: "{group}\{cm:UninstallProgram,{#AppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExeName}"; IconFilename: "{app}\xdm-logo.ico"; Tasks: desktopicon
Name: "{userstartup}\{#AppName}"; Filename: "{app}\{#AppExeName}"; Parameters: "--background"; Tasks: startupicon

[Run]
Filename: "{app}\{#AppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(AppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent
