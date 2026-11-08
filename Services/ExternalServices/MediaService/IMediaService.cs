using Amazon.S3.Model;
using Microsoft.AspNetCore.Http;

namespace OpenAcademy.Services.ExternalServices.MediaService;

public interface IMediaService
{
    /// <param name="keyWithoutExtension">e.g. lessons/{lessonId}/{assetId}; ".webp" is appended.</param>
    Task<StoredMedia> StoreImageAsync(string keyWithoutExtension, IFormFile file, CancellationToken ct);

    /// <param name="keyWithoutExtension">e.g. lessons/{lessonId}/{assetId}; ".mp4" is appended.</param>
    Task<StoredMedia> StoreVideoAsync(string keyWithoutExtension, IFormFile file, CancellationToken ct);

    Task DeleteAsync(string blobPath, CancellationToken ct);

    /// <summary>Short-lived presigned GET for the PUBLIC S3 host. Authorize the caller BEFORE calling this.</summary>
    PresignedUrl CreateDownloadUrl(string blobPath);
}

public record StoredMedia(string BlobPath, string ContentType, long SizeBytes, int? DurationSeconds);
public record PresignedUrl(string Url, DateTimeOffset ExpiresAt);

/// <summary>Expected client error; carries the HTTP status to return.</summary>
public sealed class MediaRejectedException(string message, int statusCode = StatusCodes.Status400BadRequest)
    : Exception(message)
{
    public int StatusCode { get; } = statusCode;
}