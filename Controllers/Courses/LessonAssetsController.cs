using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OpenAcademy.Configuration.Options;
using OpenAcademy.Constants;
using OpenAcademy.Data;
using OpenAcademy.Entities;
using OpenAcademy.Entities.Courses;
using OpenAcademy.Services.ExternalServices.MediaService;

namespace OpenAcademy.Controllers;

/// <remarks>
/// Authorization is declarative: the CanReadLesson / CanEditLesson policies read {lessonId} from the
/// route and run BEFORE the request body is bound, so unauthorized uploads are never buffered.
/// Every action here must keep a {lessonId} route parameter and a lesson policy.
/// </remarks>
[ApiController]
[Route("api/lessons/{lessonId:guid}/assets")]
public sealed class LessonAssetsController(
    ApplicationDbContext db,
    IMediaService media,
    ILogger<LessonAssetsController> logger) : ControllerBase
{
    public sealed record LessonAssetDto(
        Guid Id,
        AssetType Type,
        string ContentType,
        long SizeBytes,
        int? DurationSeconds,
        int Order,
        string Url,
        DateTimeOffset UrlExpiresAt);

    public sealed class UploadRequest
    {
        public IFormFile File { get; set; } = null!;
    }

    // Presigned URLs are bearer credentials: never cache this response.
    [HttpGet]
    [Authorize(Policy = PolicyNames.CanReadLesson)]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<ActionResult<IReadOnlyList<LessonAssetDto>>> List(Guid lessonId, CancellationToken ct)
    {
        var assets = await db.LessonAssets.AsNoTracking()
            .Where(a => a.LessonId == lessonId && a.Status == AssetStatus.Ready)
            .OrderBy(a => a.Order)
            .ToListAsync(ct);

        return assets.Select(a =>
        {
            var link = media.CreateDownloadUrl(a.BlobPath);
            return new LessonAssetDto(a.Id, a.Type, a.ContentType, a.SizeBytes, a.DurationSeconds,
                a.Order, link.Url, link.ExpiresAt);
        }).ToList();
    }

    [HttpPost("images")]
    [Authorize(Policy = PolicyNames.CanEditLesson)]
    [RequestSizeLimit(MediaLimits.ImageCeilingBytes + 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = MediaLimits.ImageCeilingBytes + 1024 * 1024)]
    public Task<ActionResult<LessonAssetDto>> UploadImage(Guid lessonId, [FromForm] UploadRequest req,
        CancellationToken ct)
        => Upload(lessonId, req.File, AssetType.Image, media.StoreImageAsync, ct);

    [HttpPost("videos")]
    [Authorize(Policy = PolicyNames.CanEditLesson)]
    [RequestSizeLimit(MediaLimits.VideoCeilingBytes + 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = MediaLimits.VideoCeilingBytes + 1024 * 1024)]
    public Task<ActionResult<LessonAssetDto>> UploadVideo(Guid lessonId, [FromForm] UploadRequest req,
        CancellationToken ct)
        => Upload(lessonId, req.File, AssetType.Video, media.StoreVideoAsync, ct);

    [HttpDelete("{assetId:guid}")]
    [Authorize(Policy = PolicyNames.CanEditLesson)]
    public async Task<IActionResult> Delete(Guid lessonId, Guid assetId, CancellationToken ct)
    {
        // Scoped by lessonId so the policy's lesson is the one the asset really belongs to.
        var asset = await db.LessonAssets
            .FirstOrDefaultAsync(a => a.Id == assetId && a.LessonId == lessonId, ct);
        if (asset is null) return NotFound();

        db.LessonAssets.Remove(asset);
        await db.SaveChangesAsync(ct);

        try
        {
            await media.DeleteAsync(asset.BlobPath, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Orphaned blob {Path} after asset delete", asset.BlobPath);
        }

        return NoContent();
    }

    // ------------------------------------------------------------------

    private async Task<ActionResult<LessonAssetDto>> Upload(
        Guid lessonId, IFormFile file, AssetType type,
        Func<string, IFormFile, CancellationToken, Task<StoredMedia>> store, CancellationToken ct)
    {
        var assetId = Guid.NewGuid();
        StoredMedia stored;
        try
        {
            stored = await store($"lessons/{lessonId}/{assetId}", file, ct);
        }
        catch (MediaRejectedException ex)
        {
            return Problem(ex.Message, statusCode: ex.StatusCode);
        }

        try
        {
            var nextOrder = (await db.LessonAssets.Where(a => a.LessonId == lessonId)
                .MaxAsync(a => (int?)a.Order, ct) ?? -1) + 1;

            var asset = new LessonAsset
            {
                Id = assetId,
                LessonId = lessonId,
                BlobPath = stored.BlobPath,
                Type = type,
                Status = AssetStatus.Ready,
                ContentType = stored.ContentType,
                SizeBytes = stored.SizeBytes,
                DurationSeconds = stored.DurationSeconds,
                Order = nextOrder,
                CreatedAt = DateTimeOffset.UtcNow,
            };
            db.LessonAssets.Add(asset);
            await db.SaveChangesAsync(ct);

            var link = media.CreateDownloadUrl(asset.BlobPath);
            return CreatedAtAction(nameof(List), new { lessonId },
                new LessonAssetDto(asset.Id, asset.Type, asset.ContentType, asset.SizeBytes,
                    asset.DurationSeconds, asset.Order, link.Url, link.ExpiresAt));
        }
        catch
        {
            // Don't leave a blob behind if the row could not be saved.
            try
            {
                await media.DeleteAsync(stored.BlobPath, CancellationToken.None);
            }
            catch
            {
                /* best effort */
            }

            throw;
        }
    }
}