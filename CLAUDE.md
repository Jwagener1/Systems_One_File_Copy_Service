# Systems One File Copy Service — Project Notes

.NET 8 Windows Service. Polls the `ItemLog` table in the on-site `Systems_One`
SQL Server database for unsent scan records, builds a per-customer CSV (and
optionally archives/copies a matching image), and copies the files to a configured
**Windows share folder** (this replaced an older SFTP transfer mechanism).

## Database schema — READ BEFORE CHANGING THE MODEL

The database is owned by a **separate scanning application**; we do not control its
schema and must match it exactly. CLR/SQL type mismatches compile fine but throw
`InvalidCastException` at query time.

**`Database/SCHEMA.md` is the authoritative record of the live schema.** Consult it
before editing any of:
- `Models/UploadRecord.cs`
- `Data/ApplicationDbContext.cs`
- `Database/Setup.sql`

Keep those three and `SCHEMA.md` in sync. If the user provides a new schema dump,
update `SCHEMA.md` first (including its "Last verified" date), then the code.

## Build / release

- Build: `dotnet build Systems_One_File_Copy_Service.csproj --configuration Release`
- TFM is `net8.0-windows` (required for Windows-service P/Invoke and `[SupportedOSPlatform]`).
- CI/CD: `.github/workflows/build-and-release.yml` — versions as `yyyy.MM.dd.<run_number>`,
  builds the Inno Setup installer (`setup.iss`), publishes a GitHub release.
- The installer version flows in via `/DMyAppVersion=...`; `setup.iss` guards its
  default with `#ifndef MyAppVersion` so the pipeline value is not overridden.
- The published `.exe` version comes from `/p:Version` / `/p:FileVersion` /
  `/p:AssemblyVersion` passed to `dotnet publish`.

## Runtime layout

- Settings: seeded from `appsettings.json` to
  `C:\Users\Public\Documents\Systems_One_Settings\upload_settings.json` on first run.
- Logs: `upload.log` (Debug+) and `files.log` (Info+) under the app's `logs` folder.
- Images are optional — `FileSettings.Image.EnableUpload = false` skips image
  processing **silently** so logs aren't flooded on sites that don't use images.
