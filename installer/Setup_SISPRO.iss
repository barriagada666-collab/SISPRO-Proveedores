#define MyAppName "SISPRO — Gestión de Proveedores y Órdenes de Trabajo"
#define MyAppShortName "SISPRO"
#define MyAppVersion "4.2.1"
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
AppSupportURL={#MyAppURL}/issues
AppUpdatesURL={#MyAppURL}/releases
AppCopyright=Copyright © 2026 {#MyAppPublisher} — Departamento Mantención
AppComments=Sistema de Gestión de Proveedores, Órdenes de Trabajo Externas, Solicitantes con Firmas Digitales, Usuarios y Grupos (.NET 10)
DefaultDirName=C:\Temp
DisableDirPage=no
DirExistsWarning=no
UsePreviousAppDir=yes
DefaultGroupName=Cambiaso\SISPRO
AllowNoIcons=yes
LicenseFile=..\LICENSE
OutputDir=Output
OutputBaseFilename=SISPRO_Setup_v{#MyAppVersion}_win-x64
SetupIconFile=..\src\Sispro.UI\Assets\sispro.ico
WizardImageFile=Assets\wizard_banner.bmp
WizardSmallImageFile=Assets\wizard_small.bmp
WizardImageStretch=yes
UninstallDisplayName={#MyAppShortName} {#MyAppVersion} — {#MyAppPublisher}
UninstallDisplayIcon={app}\Assets\sispro.ico
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
VersionInfoDescription=Instalador Oficial de SISPRO v{#MyAppVersion} (.NET 10)
VersionInfoCopyright=Copyright © 2026 {#MyAppPublisher}

[Languages]
Name: "spanish"; MessagesFile: "compiler:Languages\Spanish.isl"

[Messages]
WelcomeLabel1=Bienvenido al Asistente de Instalación de [name]
WelcomeLabel2=Este asistente instalará [name/ver] en su equipo.%n%nRuta predeterminada: C:\Temp (modificable durante la instalación).%nIncluye base de datos SQLite migrada, gestión de solicitantes con firma digital, aprobadores VB, usuarios/grupos y exportaciones corporativas.

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "..\publish\win-x64\*"; DestDir: "{app}"; Excludes: "Data\sispro.db"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "..\publish\win-x64\Data\sispro.db"; DestDir: "{app}\Data"; Flags: onlyifdoesntexist uninsneveruninstall

[Icons]
Name: "{group}\{#MyAppShortName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\Assets\sispro.ico"; Comment: "Abrir SISPRO — Cambiaso Hnos S.A.C."
Name: "{group}\{cm:UninstallProgram,{#MyAppShortName}}"; Filename: "{uninstallexe}"; IconFilename: "{app}\Assets\sispro.ico"
Name: "{autodesktop}\{#MyAppShortName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\Assets\sispro.ico"; Comment: "SISPRO — Gestión de Proveedores y Órdenes de Trabajo"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppShortName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent
