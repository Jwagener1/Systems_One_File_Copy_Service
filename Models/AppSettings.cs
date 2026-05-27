namespace SystemsOne.FileCopyService.Models;

public class AppSettings
{
    public string Customer { get; set; } = string.Empty;
    public GeneralSettings General { get; set; } = new();
    public DatabaseSettings Database { get; set; } = new();
    public WindowsShareSettings WindowsShare { get; set; } = new();
    public FileSettingsRoot FileSettings { get; set; } = new();
}

public class GeneralSettings
{
    public int UploadInterval_ms { get; set; } = 2500;
}

public class DatabaseSettings
{
    public string Server { get; set; } = string.Empty;
    public string DatabaseName { get; set; } = string.Empty;
    public string TableName { get; set; } = "ItemLog";
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;

    public string BuildConnectionString() =>
        $"Server={Server};Database={DatabaseName};User Id={Username};Password={Password};TrustServerCertificate=True;";
}

public class WindowsShareSettings
{
    /// <summary>Hub code used when the profile HubCode is empty.</summary>
    public string Hub_Code { get; set; } = string.Empty;

    /// <summary>UNC base path, e.g. \\SERVER\ShareName</summary>
    public string BaseSharePath { get; set; } = string.Empty;

    /// <summary>Sub-directory under BaseSharePath for CSV files.</summary>
    public string DataRemoteDirectory { get; set; } = string.Empty;

    /// <summary>Sub-directory under BaseSharePath for image files.</summary>
    public string ImageRemoteDirectory { get; set; } = string.Empty;

    /// <summary>Leave empty to rely on the Windows service account's existing access.</summary>
    public string? ShareUsername { get; set; }
    public string? SharePassword { get; set; }
    public string? ShareDomain { get; set; }

    public int MaxRetries { get; set; } = 3;
}

public class FileSettingsRoot
{
    public DataFileSettings Data { get; set; } = new();
    public ImageFileSettings Image { get; set; } = new();
}

public class DataFileSettings
{
    public string ArchiveFolder { get; set; } = string.Empty;
}

public class ImageFileSettings
{
    public string SourceFolder { get; set; } = string.Empty;
    public string ArchiveFolder { get; set; } = string.Empty;
    public bool EnableUpload { get; set; } = true;
}
