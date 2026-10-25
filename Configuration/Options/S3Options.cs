using System.ComponentModel.DataAnnotations;

namespace OpenAcademy.Configuration;

public sealed class S3Options
{
    public const string SectionName = "S3";

    [Required, Url]
    public string Endpoint { get; init; } = string.Empty;

    [Required, RegularExpression(@"^[a-z0-9-]+\z", ErrorMessage = "S3:Region must be lowercase letters, digits and '-'.")]
    public string Region { get; init; } = "us-east-1";

    [Required]
    public string AccessKey { get; init; } = string.Empty;

    [Required]
    public string SecretAccessKey { get; init; } = string.Empty;

    [Required, RegularExpression(@"^[a-z0-9][a-z0-9.-]{1,61}[a-z0-9]\z", ErrorMessage = "S3:BucketName is not a valid bucket name.")]
    public string BucketName { get; init; } = string.Empty;

    /// <summary>Needed by SeaweedFS and some other S3-compatible stores.</summary>
    public bool ForcePathStyle { get; init; }
}
