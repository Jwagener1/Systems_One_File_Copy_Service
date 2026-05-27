using SystemsOne.FileCopyService;
using SystemsOne.FileCopyService.Helpers;
using SystemsOne.FileCopyService.Models;
using SystemsOne.FileCopyService.Services;

// Logging first so SettingsInitializationService can write to the log
LoggingService.Initialize();
LoggingService.Upload.Information("Service process starting.");

SettingsInitializationService.EnsureSettingsFileExists();

try
{
    var builder = Host.CreateApplicationBuilder(args);

    builder.Services.AddWindowsService(options =>
    {
        // Must match MyAppServiceName in setup.iss and the sc.exe create call
        options.ServiceName = "SystemsOneFileCopyService";
    });

    var settingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonDocuments),
        "Systems_One_Settings",
        "upload_settings.json");

    LoggingService.Upload.Debug("Loading settings from: {Path}", settingsPath);
    builder.Configuration.AddJsonFile(settingsPath, optional: false, reloadOnChange: true);

    var appSettings = builder.Configuration.Get<AppSettings>()
        ?? throw new InvalidOperationException("Failed to load application settings.");

    ValidateSettings(appSettings);

    // Log all resolved paths and values so problems are immediately visible in the log
    LoggingService.Upload.Information(
        "Settings loaded — Customer: {Customer} | DB: {Server}/{Database} | Table: {Table}",
        appSettings.Customer,
        appSettings.Database.Server,
        appSettings.Database.DatabaseName,
        appSettings.Database.TableName);

    LoggingService.Upload.Information(
        "Share config — Base: {Base} | DataDir: {DataDir} | ImageDir: {ImageDir} | Credentials: {Creds}",
        appSettings.WindowsShare.BaseSharePath,
        appSettings.WindowsShare.DataRemoteDirectory,
        appSettings.WindowsShare.ImageRemoteDirectory,
        string.IsNullOrWhiteSpace(appSettings.WindowsShare.ShareUsername) ? "none (service account)" : $"user '{appSettings.WindowsShare.ShareUsername}'");

    LoggingService.Upload.Information(
        "File paths — CSV archive: {CsvArchive} | Image source: {ImgSrc} | Image archive: {ImgArchive} | Image upload: {ImgEnabled}",
        appSettings.FileSettings.Data.ArchiveFolder,
        appSettings.FileSettings.Image.SourceFolder,
        appSettings.FileSettings.Image.ArchiveFolder,
        appSettings.FileSettings.Image.EnableUpload);

    LoggingService.Upload.Information(
        "Timing — Interval: {Interval}ms | MaxRetries: {Retries}",
        appSettings.General.UploadInterval_ms,
        appSettings.WindowsShare.MaxRetries);

    // Register services
    builder.Services.AddSingleton(appSettings);
    builder.Services.AddSingleton<ICustomerProfileService, CustomerProfileService>();
    builder.Services.AddSingleton<IFileTransferService, WindowsShareService>();
    builder.Services.AddScoped<IDatabaseService, DatabaseService>();
    builder.Services.AddScoped<IFileBuilder, FileBuilder>();
    builder.Services.AddScoped<IFileService, FileService>();
    builder.Services.AddScoped<IUploadOrchestrationService, UploadOrchestrationService>();
    builder.Services.AddHostedService<Worker>();

    var host = builder.Build();

    LoggingService.Upload.Debug("Loading and validating customer profile...");
    host.Services.GetRequiredService<ICustomerProfileService>().GetProfile();

    LoggingService.Upload.Information("Startup complete. Starting host.");
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

    if (string.IsNullOrWhiteSpace(s.FileSettings.Data.ArchiveFolder))
        errors.Add("FileSettings.Data.ArchiveFolder must not be empty.");

    if (s.FileSettings.Image.EnableUpload && string.IsNullOrWhiteSpace(s.FileSettings.Image.SourceFolder))
        errors.Add("FileSettings.Image.SourceFolder must not be empty when image upload is enabled.");

    if (errors.Count > 0)
        throw new InvalidOperationException("Startup validation failed:\n" + string.Join("\n", errors));
}
