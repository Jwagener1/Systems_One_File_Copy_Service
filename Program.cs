using SystemsOne.FileCopyService;
using SystemsOne.FileCopyService.Helpers;
using SystemsOne.FileCopyService.Models;
using SystemsOne.FileCopyService.Services;

// Pre-host: seed settings files and start logging before the host is built
SettingsInitializationService.EnsureSettingsFileExists();
LoggingService.Initialize();

try
{
    var builder = Host.CreateApplicationBuilder(args);

    builder.Services.AddWindowsService(options =>
    {
        // Must match MyAppServiceName in setup.iss and the sc.exe create call
        options.ServiceName = "SystemsOneFileCopyService";
    });

    // Load user settings from the seeded file (reloads on change)
    var settingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonDocuments),
        "Systems_One_Settings",
        "upload_settings.json");

    builder.Configuration.AddJsonFile(settingsPath, optional: false, reloadOnChange: true);

    var appSettings = builder.Configuration.Get<AppSettings>()
        ?? throw new InvalidOperationException("Failed to load application settings.");

    ValidateSettings(appSettings);

    // Register services
    builder.Services.AddSingleton(appSettings);
    builder.Services.AddSingleton<ICustomerProfileService, CustomerProfileService>();
    builder.Services.AddSingleton<IFileTransferService, WindowsShareService>();  // singleton: one OS-level share connection
    builder.Services.AddScoped<IDatabaseService, DatabaseService>();
    builder.Services.AddScoped<IFileBuilder, FileBuilder>();
    builder.Services.AddScoped<IFileService, FileService>();
    builder.Services.AddScoped<IUploadOrchestrationService, UploadOrchestrationService>();
    builder.Services.AddHostedService<Worker>();

    var host = builder.Build();

    // Trigger profile load here so any validation error surfaces before the service starts
    host.Services.GetRequiredService<ICustomerProfileService>().GetProfile();

    LoggingService.Upload.Information("Configuration validated. Starting host.");
    host.Run();
}
catch (Exception ex)
{
    LoggingService.Upload.Fatal(ex, "Service failed to start.");
    throw;
}

static void ValidateSettings(AppSettings s)
{
    var errors = new List<string>();

    if (string.IsNullOrWhiteSpace(s.Customer))
        errors.Add("Customer must not be empty.");

    if (string.IsNullOrWhiteSpace(s.Database.Server))
        errors.Add("Database.Server must not be empty.");

    if (string.IsNullOrWhiteSpace(s.Database.DatabaseName))
        errors.Add("Database.DatabaseName must not be empty.");

    if (string.IsNullOrWhiteSpace(s.WindowsShare.BaseSharePath))
        errors.Add("WindowsShare.BaseSharePath must not be empty.");

    if (!string.IsNullOrWhiteSpace(s.WindowsShare.ShareUsername) &&
        string.IsNullOrWhiteSpace(s.WindowsShare.SharePassword))
        errors.Add("WindowsShare.SharePassword must be set when ShareUsername is provided.");

    if (errors.Count > 0)
        throw new InvalidOperationException("Startup validation failed:\n" + string.Join("\n", errors));
}
