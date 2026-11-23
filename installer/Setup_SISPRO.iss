#define MyAppName "SISPRO — Gestión de Proveedores y Órdenes de Trabajo"
#define MyAppShortName "SISPRO"
#define MyAppVersion "3.0.0"
#define MyAppPublisher "Cambiaso Hnos S.A.C."
#define MyAppExeName "SISPRO.exe"
#define MyAppURL "https://github.com/barriagada666-collab/SISPRO-Proveedores"

[Setup]
AppId={{D9C6A34B-7A42-4B9E-8F14-3A61E8C02B19}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppShortName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName={autopf}\Cambiaso\SISPRO
DefaultGroupName=Cambiaso\SISPRO
AllowNoIcons=yes
LicenseFile=..\LICENSE
OutputDir=Output
OutputBaseFilename=SISPRO_Setup_v{#MyAppVersion}_win-x64
SetupIconFile=..\src\Sispro.UI\Assets\sispro.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
CloseApplications=yes
RestartApplications=no
VersionInfoVersion={#MyAppVersion}.0
VersionInfoCompany={#MyAppPublisher}
VersionInfoProductName={#MyAppShortName}
VersionInfoDescription=Instalador Oficial de SISPRO (.NET 10)

[Languages]
Name: "spanish"; MessagesFile: "compiler:Languages\Spanish.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "..\publish\win-x64\*"; DestDir: "{app}"; Excludes: "Data\sispro.db"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "..\publish\win-x64\Data\sispro.db"; DestDir: "{app}\Data"; Flags: onlyifdoesntexist uninsneveruninstall

[Icons]
Name: "{group}\{#MyAppShortName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\Assets\sispro.ico"
Name: "{group}\{cm:UninstallProgram,{#MyAppShortName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppShortName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\Assets\sispro.ico"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppShortName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent
