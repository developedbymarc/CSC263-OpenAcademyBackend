using Microsoft.Extensions.Options;
using OpenAcademy.Configuration.Options;

namespace OpenAcademy.Configuration.Validators;

public sealed class S3OptionsValidator : IValidateOptions<S3Options>
{
    public ValidateOptionsResult Validate(string? name, S3Options o)
    {
        var errors = new List<string>();

        if (string.Equals(o.BucketName, o.PublicBucketName, StringComparison.Ordinal))
            errors.Add("S3:BucketName and S3:PublicBucketName must be different buckets.");

        // Public object URLs are path-style ({endpoint}/{bucket}/{key}).
        if (!o.ForcePathStyle)
            errors.Add("S3:ForcePathStyle must be true: public image URLs are built path-style.");

        var publicEndpoint = o.PublicEndpoint ?? o.Endpoint;
        if (Uri.TryCreate(publicEndpoint, UriKind.Absolute, out var uri)
            && uri.Scheme != Uri.UriSchemeHttps && uri.Host is not ("localhost" or "127.0.0.1"))
            errors.Add("S3:PublicEndpoint must be https (except on localhost).");

        return errors.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(errors);
    }
}