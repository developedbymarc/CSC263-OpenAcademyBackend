using System.ComponentModel.DataAnnotations;

namespace OpenAcademy.Configuration.Options;

public sealed class S3Options
{
    public const string SectionName = "S3";

    /// <summary>Internal endpoint the backend uses for uploads/deletes (e.g. http://seaweedfs:8333).</summary>
    [Required, Url]
    public string Endpoint { get; init; } = string.Empty;

    /// <summary>
    /// Public HTTPS endpoint browsers use (e.g. https://s3.example.edu). Used to sign presigned URLs
    /// and to build public object URLs. Falls back to <see cref="Endpoint"/> (fine in dev).
    /// </summary>
    [Url]
    public string? PublicEndpoint { get; init; }

    [Required, RegularExpression(@"^[a-z0-9-]+\z", ErrorMessage = "S3:Region must be lowercase letters, digits and '-'.")]
    public string Region { get; init; } = "us-east-1";

    [Required]
    public string AccessKey { get; init; } = string.Empty;

    [Required]
    public string SecretAccessKey { get; init; } = string.Empty;

    /// <summary>PRIVATE bucket: lesson assets, served only through presigned URLs.</summary>
    [Required, RegularExpression(@"^[a-z0-9][a-z0-9.-]{1,61}[a-z0-9]\z", ErrorMessage = "S3:BucketName is not a valid bucket name.")]
    public string BucketName { get; init; } = string.Empty;

    /// <summary>PUBLIC-READ bucket: avatars, course thumbnails. Never put lesson assets here.</summary>
    [Required, RegularExpression(@"^[a-z0-9][a-z0-9.-]{1,61}[a-z0-9]\z", ErrorMessage = "S3:PublicBucketName is not a valid bucket name.")]
    public string PublicBucketName { get; init; } = string.Empty;

    /// <summary>Needed by SeaweedFS and some other S3-compatible stores. Public URLs are built path-style.</summary>
    public bool ForcePathStyle { get; init; }
}