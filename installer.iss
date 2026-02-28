#define MyAppName "LapLap Auto Tool"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "LapLap Tech"
#define MyAppExeName "LapLapAutoTool.exe"
#define MyAppSourceDir "publish"

[Setup]
AppId={{A1B2C3D4-E5F6-7890-ABCD-EF1234567890}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL=https://laplap.tech
DefaultDirName={autopf}\LapLapAutoTool
DefaultGroupName={#MyAppName}
AllowNoIcons=yes
OutputDir=installer_output
OutputBaseFilename=LapLapAutoTool_Setup_v{#MyAppVersion}
;SetupIconFile=Resources\app_icon.ico  ; Bỏ comment nếu có file .ico
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin
DisableProgramGroupPage=yes
UninstallDisplayName={#MyAppName}
UninstallDisplayIcon={app}\{#MyAppExeName}

; Yêu cầu Windows 10 trở lên
MinVersion=10.0

[Languages]
Name: "vietnamese"; MessagesFile: "compiler:Languages\Vietnamese.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Tạo shortcut trên Desktop"; GroupDescription: "Shortcut:"; Flags: checked
Name: "startmenuicon"; Description: "Tạo shortcut trong Start Menu"; GroupDescription: "Shortcut:"; Flags: checked

[Files]
; File chính
Source: "{#MyAppSourceDir}\{#MyAppExeName}"; DestDir: "{app}"; Flags: ignoreversion

; Resources (config JSON, styles, icons...)
Source: "{#MyAppSourceDir}\Resources\*"; DestDir: "{app}\Resources"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
; Desktop shortcut
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

; Start Menu shortcut
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: startmenuicon
Name: "{group}\Gỡ cài đặt {#MyAppName}"; Filename: "{uninstallexe}"

[Run]
; Mở app sau khi cài xong (tuỳ chọn)
Filename: "{app}\{#MyAppExeName}"; Description: "Mở {#MyAppName} ngay bây giờ"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
; Xoá thư mục Reports và logs khi gỡ cài đặt
Type: filesandordirs; Name: "{app}\Reports"
Type: filesandordirs; Name: "{app}\Logs"
