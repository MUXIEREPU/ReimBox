#define MyAppName "ReimBox"
#define MyAppVersion "0.2.1"
#define MyAppPublisher "MUXIEREPU"
#define MyAppURL "https://github.com/MUXIEREPU/ReimBox"
#define MyAppExeName "ReimBox.exe"

[Setup]
AppId={{7DBE7D91-F7E8-44F8-9E98-D7A337D46C53}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}/issues
DefaultDirName={autopf}\ReimBox
DefaultGroupName=ReimBox
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
OutputDir=output
OutputBaseFilename=ReimBox-Setup-v{#MyAppVersion}
SetupIconFile=..\logo\app.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible

[Languages]
Name: "chinesesimp"; MessagesFile: "compiler:Languages\ChineseSimplified.isl"

[Tasks]
Name: "desktopicon"; Description: "创建桌面快捷方式"; GroupDescription: "附加任务："; Flags: unchecked

[Files]
Source: "..\release\win-x64\ReimBox.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\release\win-x64\PaddleWorker\*"; DestDir: "{app}\PaddleWorker"; Flags: ignoreversion recursesubdirs createallsubdirs skipifsourcedoesntexist

[Icons]
Name: "{autoprograms}\ReimBox"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\ReimBox"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "启动 ReimBox"; Flags: nowait postinstall skipifsilent
