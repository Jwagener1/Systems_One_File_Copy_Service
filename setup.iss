#define MyAppName        "Systems One File Copy Service"
; MyAppVersion can be supplied by the build pipeline via /DMyAppVersion=...
; Only fall back to this default when it was not passed on the command line.
#ifndef MyAppVersion
  #define MyAppVersion   "1.0.0"
#endif
#define MyAppPublisher   "Systems One"
#define MyAppExeName     "SystemsOneFileCopyService.exe"
#define MyAppServiceName "SystemsOneFileCopyService"
#define MyAppDescription "Monitors a SQL Server database for new scan records and copies CSV and image files to a configured Windows share folder."

[Setup]
AppId={{A37747BE-A33A-4B8F-A304-FF44FF2EFE19}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
OutputDir=installer_output
OutputBaseFilename=SystemsOneFileCopyService_Setup_{#MyAppVersion}
#if FileExists("Icon\icon.ico")
SetupIconFile=Icon\icon.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
#endif
Compression=lzma
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin
ArchitecturesInstallIn64BitMode=x64compatible

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Files]
; All published application binaries and bundled files
Source: "publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
; Seed config — preserved on upgrade, left behind on uninstall
Source: "appsettings.json"; DestDir: "{app}"; Flags: ignoreversion onlyifdoesntexist uninsneveruninstall

[Dirs]
; Logs folder with full permissions so the service account can write
Name: "{app}\logs"; Permissions: everyone-full

[Messages]
WelcomeLabel2=This will install [name/ver] on your computer.%n%nThis application runs as a Windows Service. It polls a SQL Server database and automatically copies scan data files to a configured Windows share folder.%n%nClick Next to continue.

[Code]
var
  ServiceInstalledPage: TOutputMsgMemoWizardPage;

procedure InitializeWizard;
begin
  ServiceInstalledPage := CreateOutputMsgMemoPage(
    wpFinished,
    'Service Installation',
    'Windows Service Status',
    'The following actions were performed during installation:',
    ''
  );
end;

function IsServiceInstalled: Boolean;
var
  ResultCode: Integer;
begin
  Exec('sc.exe', 'query "' + '{#MyAppServiceName}' + '"', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  Result := (ResultCode = 0);
end;

procedure StopAndDeleteService;
var
  ResultCode: Integer;
begin
  if IsServiceInstalled then
  begin
    Exec('sc.exe', 'stop "' + '{#MyAppServiceName}' + '"',
      '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
    Sleep(2000);
    Exec('sc.exe', 'delete "' + '{#MyAppServiceName}' + '"',
      '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
    Sleep(1000);
  end;
end;

procedure CurStepChanged(CurStep: TSetupStep);
var
  ResultCode: Integer;
  ServiceMsg: String;
  ExePath: String;
begin
  if CurStep = ssPostInstall then
  begin
    StopAndDeleteService;

    ExePath := ExpandConstant('{app}\{#MyAppExeName}');

    // Create the Windows Service
    Exec('sc.exe',
      'create "' + '{#MyAppServiceName}' + '"' +
      ' binPath= "' + ExePath + '"' +
      ' start= auto' +
      ' DisplayName= "' + '{#MyAppName}' + '"',
      '', SW_HIDE, ewWaitUntilTerminated, ResultCode);

    // Set the service description
    Exec('sc.exe',
      'description "' + '{#MyAppServiceName}' + '" "{#MyAppDescription}"',
      '', SW_HIDE, ewWaitUntilTerminated, ResultCode);

    // Configure failure/recovery: restart 3 times with 60-second delays; reset counter after 24 hours
    Exec('sc.exe',
      'failure "' + '{#MyAppServiceName}' + '"' +
      ' reset= 86400' +
      ' actions= restart/60000/restart/60000/restart/60000',
      '', SW_HIDE, ewWaitUntilTerminated, ResultCode);

    // Start the service immediately
    Exec('sc.exe',
      'start "' + '{#MyAppServiceName}' + '"',
      '', SW_HIDE, ewWaitUntilTerminated, ResultCode);

    if ResultCode = 0 then
      ServiceMsg := 'Service "' + '{#MyAppName}' + '" was created and started successfully.'
    else
      ServiceMsg := 'Service "' + '{#MyAppName}' + '" was created but could not be started automatically.' + #13#10 +
                    'Please start it manually from Services (services.msc) after configuring:' + #13#10 +
                    'C:\Users\Public\Documents\Systems_One_Settings\upload_settings.json';

    ServiceInstalledPage.RichEditViewer.Lines.Add(ServiceMsg);
  end;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usUninstall then
    StopAndDeleteService;
end;

[UninstallDelete]
; Remove the logs directory on uninstall (settings and profiles are left behind)
Type: filesandordirs; Name: "{app}\logs"
