// Configuration/Validators/KeyProtectionOptionsValidator.cs

using Microsoft.Extensions.Options;

namespace OpenAcademy.Configuration;

public sealed class KeyProtectionOptionsValidator : IValidateOptions<KeyProtectionOptions>
{
    public ValidateOptionsResult Validate(string? name, KeyProtectionOptions options)
    {
        // [Required] reports missing paths
        if (string.IsNullOrWhiteSpace(options.CertificatePath) ||
            string.IsNullOrWhiteSpace(options.PrivateKeyPath))
            return ValidateOptionsResult.Success;

        var failures = new List<string>();

        TryLoad(options.CertificatePath, options.PrivateKeyPath, failures);

        if (!string.IsNullOrWhiteSpace(options.RetiredDirectory))
        {
            if (!Directory.Exists(options.RetiredDirectory))
            {
                failures.Add(
                    $"{KeyProtectionOptions.SectionName}:{nameof(KeyProtectionOptions.RetiredDirectory)} '{options.RetiredDirectory}' does not exist.");
            }
            else
            {
                foreach (var (crt, key) in KeyProtectionCertificates.RetiredPairs(options.RetiredDirectory))
                {
                    TryLoad(crt, key, failures);
                }
            }
        }

        return failures.Count > 0 ? ValidateOptionsResult.Fail(failures) : ValidateOptionsResult.Success;
    }

    private static void TryLoad(string crt, string key, List<string> failures)
    {
        try
        {
            using var _ = KeyProtectionCertificates.LoadPair(crt, key);
        }
        catch (InvalidOperationException e)
        {
            failures.Add(e.Message);
        }
    }
}