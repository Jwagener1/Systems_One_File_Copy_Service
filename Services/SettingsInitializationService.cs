namespace SystemsOne.FileCopyService.Services;

public static class SettingsInitializationService
{
    private static readonly string SettingsRoot = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonDocuments),
        "Systems_One_Settings");

    public static void EnsureSettingsFileExists()
    {
        Directory.CreateDirectory(SettingsRoot);
        Directory.CreateDirectory(Path.Combine(SettingsRoot, "Profiles"));

        SeedSettingsFile();
        SeedProfileFiles();
    }

    private static void SeedSettingsFile()
    {
        var target = Path.Combine(SettingsRoot, "upload_settings.json");
        if (File.Exists(target)) return;

        var source = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
        if (File.Exists(source))
            File.Copy(source, target);
    }

    private static void SeedProfileFiles()
    {
        var examplesDir = Path.Combine(AppContext.BaseDirectory, "Customer_Example_Files");
        if (!Directory.Exists(examplesDir)) return;

        var profilesDir = Path.Combine(SettingsRoot, "Profiles");

        foreach (var file in Directory.GetFiles(examplesDir, "*.profile.json", SearchOption.AllDirectories))
        {
            var dest = Path.Combine(profilesDir, Path.GetFileName(file));
            if (!File.Exists(dest))
                File.Copy(file, dest);
        }
    }
}
