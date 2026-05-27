using SystemsOne.FileCopyService.Helpers;

namespace SystemsOne.FileCopyService.Services;

public static class SettingsInitializationService
{
    private static readonly string SettingsRoot = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonDocuments),
        "Systems_One_Settings");

    public static void EnsureSettingsFileExists()
    {
        LoggingService.Upload.Debug("Settings root: {Path}", SettingsRoot);

        Directory.CreateDirectory(SettingsRoot);
        Directory.CreateDirectory(Path.Combine(SettingsRoot, "Profiles"));

        SeedSettingsFile();
        SeedProfileFiles();
    }

    private static void SeedSettingsFile()
    {
        var target = Path.Combine(SettingsRoot, "upload_settings.json");

        if (File.Exists(target))
        {
            LoggingService.Upload.Debug("Settings file already exists: {Path}", target);
            return;
        }

        var source = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
        LoggingService.Upload.Debug("Settings file not found. Seeding from: {Source}", source);

        if (!File.Exists(source))
        {
            LoggingService.Upload.Warning("Template appsettings.json not found at: {Source} — settings file was not created.", source);
            return;
        }

        File.Copy(source, target);
        LoggingService.Upload.Information("Settings file created: {Path}", target);
    }

    private static void SeedProfileFiles()
    {
        var examplesDir = Path.Combine(AppContext.BaseDirectory, "Customer_Example_Files");
        var profilesDir = Path.Combine(SettingsRoot, "Profiles");

        if (!Directory.Exists(examplesDir))
        {
            LoggingService.Upload.Debug("No Customer_Example_Files folder found at: {Path} — skipping profile seeding.", examplesDir);
            return;
        }

        var files = Directory.GetFiles(examplesDir, "*.profile.json", SearchOption.AllDirectories);
        LoggingService.Upload.Debug("Found {Count} example profile(s) in: {Dir}", files.Length, examplesDir);

        foreach (var file in files)
        {
            var dest = Path.Combine(profilesDir, Path.GetFileName(file));
            if (File.Exists(dest))
            {
                LoggingService.Upload.Debug("Profile already exists, skipping: {Name}", Path.GetFileName(file));
                continue;
            }
            File.Copy(file, dest);
            LoggingService.Upload.Information("Profile seeded: {Name}", Path.GetFileName(file));
        }
    }
}
