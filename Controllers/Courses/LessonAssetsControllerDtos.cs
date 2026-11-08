using OpenAcademy.Entities;
using OpenAcademy.Entities.Courses;

namespace OpenAcademy.Controllers;

public sealed record LessonAssetDto(
    Guid Id,
    AssetType Type,
    string ContentType,
    long SizeBytes,
    int? DurationSeconds,
    int Order,
    string Url,
    DateTimeOffset UrlExpiresAt);

public sealed class UploadLessonAssetRequest
{
    public IFormFile File { get; set; } = null!;
}
