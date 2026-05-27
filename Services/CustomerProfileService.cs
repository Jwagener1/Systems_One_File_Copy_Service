using System.Text.Json;
using SystemsOne.FileCopyService.Helpers;
using SystemsOne.FileCopyService.Models;

namespace SystemsOne.FileCopyService.Services;

public interface ICustomerProfileService
{
    CustomerProfile GetProfile();
}

public class CustomerProfileService : ICustomerProfileService
{
    private readonly AppSettings _settings;
    private CustomerProfile? _profile;
    private readonly object _lock = new();

    public CustomerProfileService(AppSettings settings)
    {
        _settings = settings;
    }

    public CustomerProfile GetProfile()
    {
        if (_profile is not null) return _profile;
        lock (_lock)
        {
            _profile ??= LoadAndValidate();
        }
        return _profile;
    }

    private CustomerProfile LoadAndValidate()
    {
        var profilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonDocuments),
            "Systems_One_Settings",
            "Profiles",
            $"{_settings.Customer}.profile.json");

        if (!File.Exists(profilePath))
            throw new FileNotFoundException($"Customer profile not found: {profilePath}");

        var profile = JsonSerializer.Deserialize<CustomerProfile>(
            File.ReadAllText(profilePath),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidOperationException("Failed to deserialize customer profile.");

        Validate(profile);

        LoggingService.Upload.Information("Loaded customer profile: {Customer}", profile.CustomerName);
        return profile;
    }

    private void Validate(CustomerProfile profile)
    {
        var errors = new List<string>();

        if (profile.ProfileVersion != 1)
            errors.Add($"ProfileVersion must be 1, got {profile.ProfileVersion}.");

        if (!string.Equals(profile.CustomerName, _settings.Customer, StringComparison.OrdinalIgnoreCase))
            errors.Add($"Profile CustomerName '{profile.CustomerName}' does not match configured Customer '{_settings.Customer}'.");

        if (profile.Columns.Count == 0)
            errors.Add("Profile must define at least one column.");

        foreach (var col in profile.Columns)
        {
            if (string.IsNullOrWhiteSpace(col.Name))
                errors.Add("All columns must have a Name.");

            if (string.Equals(col.Source, nameof(ColumnSource.Constant), StringComparison.OrdinalIgnoreCase)
                && string.IsNullOrEmpty(col.Constant))
                errors.Add($"Column '{col.Name}' has Source=Constant but no Constant value.");
        }

        if (errors.Count > 0)
            throw new InvalidOperationException("Profile validation errors:\n" + string.Join("\n", errors));
    }
}
