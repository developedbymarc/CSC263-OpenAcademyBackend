// Configuration/DataProtectionServiceCollectionExtensions.cs
using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Options;
using OpenAcademy.Data;

namespace OpenAcademy.Configuration;

public static class DataProtectionServiceCollectionExtensions
{
    /// <summary>Never change after going live, or existing protected payloads become foreign.</summary>
    private const string ApplicationName = "OpenAcademy";

    public static IServiceCollection AddAppDataProtection(
        this IServiceCollection services, IConfiguration configuration)
    {
        var options = BindAndValidate(configuration);

        var current = KeyProtectionCertificates.LoadPair(options.CertificatePath, options.PrivateKeyPath);

        var retired = string.IsNullOrWhiteSpace(options.RetiredDirectory)
            ? []
            : KeyProtectionCertificates.RetiredPairs(options.RetiredDirectory)
                .Select(p => KeyProtectionCertificates.LoadPair(p.Crt, p.Key))
                .ToArray();

        services.AddDataProtection()
            .SetApplicationName(ApplicationName)
            .PersistKeysToDbContext<ApplicationDbContext>()
            .ProtectKeysWithCertificate(current)              // encrypts new keys, also decrypts
            .UnprotectKeysWithAnyCertificate(retired);        // decrypt-only

        return services;
    }

    // Runs the same rules as startup validation, early enough for registration
    private static KeyProtectionOptions BindAndValidate(IConfiguration configuration)
    {
        var options = configuration.GetSection(KeyProtectionOptions.SectionName)
            .Get<KeyProtectionOptions>() ?? new KeyProtectionOptions();

        var failures = new List<string>();

        var results = new List<ValidationResult>();
        Validator.TryValidateObject(options, new ValidationContext(options), results, validateAllProperties: true);
        failures.AddRange(results.Select(r => r.ErrorMessage ?? "Invalid value."));

        var custom = new KeyProtectionOptionsValidator().Validate(null, options);
        if (custom.Failed) failures.AddRange(custom.Failures!);

        return failures.Count > 0
            ? throw new OptionsValidationException(
                KeyProtectionOptions.SectionName, typeof(KeyProtectionOptions), failures)
            : options;
    }
}