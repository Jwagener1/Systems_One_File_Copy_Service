# Systems One File Copy Service

.NET 8 Windows Service that polls a SQL Server database for new scan records and copies the generated CSV (and optionally image) files to a Windows share folder.

## Requirements

- .NET 8 SDK (build) / .NET 8 Runtime (deploy)
- SQL Server with an `ItemLog` table
- A reachable Windows share (UNC path)

## First run — settings seeding

On first start the service creates:

```
C:\Users\Public\Documents\Systems_One_Settings\
├── upload_settings.json        ← edit this with your DB and share details
└── Profiles\
    ├── Rhenus_Durban.json
    └── PEP_Africa.json
```

Edit `upload_settings.json` before starting the service. The file is **never overwritten** on subsequent starts.

## Configuration (`upload_settings.json`)

| Key | Description |
|-----|-------------|
| `Customer` | Must match a `{Name}.json` in the Profiles folder |
| `Database.*` | SQL Server connection details |
| `WindowsShare.BaseSharePath` | UNC root, e.g. `\\SERVER\Share` |
| `WindowsShare.DataRemoteDirectory` | Sub-folder for CSV files |
| `WindowsShare.ImageRemoteDirectory` | Sub-folder for image files |
| `WindowsShare.ShareUsername/Password/Domain` | Leave empty if the service account already has share access |
| `FileSettings.Data.ArchiveFolder` | Local folder where CSVs are written before copying |
| `FileSettings.Image.SourceFolder` | Folder where the scanner drops `.jpg` files |
| `FileSettings.Image.ArchiveFolder` | Local folder where images are archived before copying |
| `FileSettings.Image.EnableUpload` | Set to `false` to skip image processing entirely |

## Build

```powershell
dotnet build
```

## Publish (self-contained, single file)

```powershell
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o publish\
```

## Install as a Windows Service

```powershell
sc.exe create "Systems One File Copy Service" `
    binPath="C:\path\to\publish\SystemsOneFileCopyService.exe" `
    start=auto

sc.exe start "Systems One File Copy Service"
```

To uninstall:

```powershell
sc.exe stop  "Systems One File Copy Service"
sc.exe delete "Systems One File Copy Service"
```

## Logs

```
C:\Users\Public\Documents\Systems_One_Logs\
└── 2025-02-03\
    ├── upload.log   ← all operational events
    └── files.log    ← CSV filename + full contents per file sent
```

Logs roll over at midnight. Folders older than 30 days are deleted automatically.

## Customer profiles

Customer profiles live in `Systems_One_Settings\Profiles\`. See `Customer_Example_Files\customer.profile.schema.json` for the full schema and the bundled Rhenus_Durban / PEP_Africa profiles for examples.

To add a new customer:
1. Create `{CustomerName}.json` in the Profiles folder.
2. Set `Customer` in `upload_settings.json` to match the file name (without `.json`).
3. Restart the service.
