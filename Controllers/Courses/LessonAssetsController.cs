using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using OpenAcademy.Configuration.Options;
using OpenAcademy.Constants;
using OpenAcademy.Data;
using OpenAcademy.Entities;
using OpenAcademy.Entities.Courses;
using OpenAcademy.Responses;
using OpenAcademy.Services.ExternalServices.MediaService;

namespace OpenAcademy.Controllers;

/// <remarks>
/// Authorization is declarative: course-content policies read the most specific resource ID
/// from the route and run BEFORE the request body is bound, so unauthorized uploads are never buffered.
/// Every action here must keep the full ancestor route and a course-content policy.
/// </remarks>
[ApiController]
[Route("Courses/{courseId:guid}/Modules/{moduleId:guid}/Lessons/{lessonId:guid}/Assets")]
public sealed class LessonAssetsController : ControllerBase
{
    // services
    private readonly ApplicationDbContext _dbContext;
    private readonly IMediaService _mediaService;

    // utils
    private readonly ILogger<LessonAssetsController> _logger;

    public LessonAssetsController(
        ApplicationDbContext dbContext,
        IOptions<IMediaService> mediaService,
        ILogger<LessonAssetsController> logger)
    {
        _dbContext = dbContext;
        _mediaService = mediaService.Value;
        _logger = logger;
    }

    // Presigned URLs are bearer credentials: never cache this response.
    [HttpGet]
    [Authorize(Policy = PolicyNames.CanReadCourseContent)]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<LessonAssetDto>>>> List(
        Guid courseId, Guid moduleId, Guid lessonId, CancellationToken ct)
    {
        if (!await LessonBelongsToRouteAsync(courseId, moduleId, lessonId, ct))
            return NotFound(ApiResponse<IReadOnlyList<LessonAssetDto>>.Failed(
                "The lesson was not found.", "LESSON_NOT_FOUND"));

        var assets = await _dbContext.LessonAssets.AsNoTracking()
            .Where(a => a.LessonId == lessonId
                && a.Lesson.ModuleId == moduleId
                && a.Lesson.Module.CourseId == courseId
                && a.Status == AssetStatus.Ready)
            .OrderBy(a => a.Order)
            .ToListAsync(ct);

        var result = assets.Select(a =>
        {
            var link = _mediaService.CreateDownloadUrl(a.BlobPath);
            return new LessonAssetDto(a.Id, a.Type, a.ContentType, a.SizeBytes, a.DurationSeconds,
                a.Order, link.Url, link.ExpiresAt);
        }).ToList();
        return Ok(ApiResponse<IReadOnlyList<LessonAssetDto>>.Succeeded(result));
    }

    [HttpPost("images")]
    [Authorize(Policy = PolicyNames.CanEditCourseContent)]
    [RequestSizeLimit(MediaLimits.ImageCeilingBytes + 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = MediaLimits.ImageCeilingBytes + 1024 * 1024)]
    public Task<ActionResult<ApiResponse<LessonAssetDto>>> UploadImage(
        Guid courseId, Guid moduleId, Guid lessonId, [FromForm] UploadLessonAssetRequest req,
        CancellationToken ct)
        => Upload(courseId, moduleId, lessonId, req.File, AssetType.Image, _mediaService.StoreImageAsync, ct);

    [HttpPost("videos")]
    [Authorize(Policy = PolicyNames.CanEditCourseContent)]
    [RequestSizeLimit(MediaLimits.VideoCeilingBytes + 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = MediaLimits.VideoCeilingBytes + 1024 * 1024)]
    public Task<ActionResult<ApiResponse<LessonAssetDto>>> UploadVideo(
        Guid courseId, Guid moduleId, Guid lessonId, [FromForm] UploadLessonAssetRequest req,
        CancellationToken ct)
        => Upload(courseId, moduleId, lessonId, req.File, AssetType.Video, _mediaService.StoreVideoAsync, ct);

    [HttpDelete("{assetId:guid}")]
    [Authorize(Policy = PolicyNames.CanDeleteCourseContent)]
    public async Task<ActionResult<ApiResponse<object?>>> Delete(
        Guid courseId, Guid moduleId, Guid lessonId, Guid assetId, CancellationToken ct)
    {
        // Scope the asset to every ancestor in the route as well as the authorized asset.
        var asset = await _dbContext.LessonAssets
            .FirstOrDefaultAsync(a =>
                a.Id == assetId
                && a.LessonId == lessonId
                && a.Lesson.ModuleId == moduleId
                && a.Lesson.Module.CourseId == courseId, ct);
        if (asset is null)
            return NotFound(ApiResponse<object?>.Failed("ASSET_NOT_FOUND", "The lesson asset was not found."));

        _dbContext.LessonAssets.Remove(asset);
        await _dbContext.SaveChangesAsync(ct);

        try
        {
            await _mediaService.DeleteAsync(asset.BlobPath, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Orphaned blob {Path} after asset delete", asset.BlobPath);
        }

        return Ok(ApiResponse<object?>.Succeeded(null));
    }

    // ------------------------------------------------------------------

    private async Task<ActionResult<ApiResponse<LessonAssetDto>>> Upload(
        Guid courseId, Guid moduleId, Guid lessonId, IFormFile file, AssetType type,
        Func<string, IFormFile, CancellationToken, Task<StoredMedia>> store, CancellationToken ct)
    {
        if (!await LessonBelongsToRouteAsync(courseId, moduleId, lessonId, ct))
            return NotFound(ApiResponse<LessonAssetDto>.Failed(
                "The lesson was not found.", "LESSON_NOT_FOUND"));

        var assetId = Guid.NewGuid();
        StoredMedia stored;
        try
        {
            stored = await store($"lessons/{lessonId}/{assetId}", file, ct);
        }
        catch (MediaRejectedException ex)
        {
            return StatusCode(ex.StatusCode,
                ApiResponse<LessonAssetDto>.Failed("MEDIA_REJECTED", ex.Message));
        }

        try
        {
            var nextOrder = (await _dbContext.LessonAssets.Where(a => a.LessonId == lessonId)
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
            _dbContext.LessonAssets.Add(asset);
            await _dbContext.SaveChangesAsync(ct);

            var link = _mediaService.CreateDownloadUrl(asset.BlobPath);
            return CreatedAtAction(nameof(List), new { courseId, moduleId, lessonId },
                ApiResponse<LessonAssetDto>.Succeeded(new LessonAssetDto(
                    asset.Id, asset.Type, asset.ContentType, asset.SizeBytes,
                    asset.DurationSeconds, asset.Order, link.Url, link.ExpiresAt)));
        }
        catch
        {
            // Don't leave a blob behind if the row could not be saved.
            try
            {
                await _mediaService.DeleteAsync(stored.BlobPath, CancellationToken.None);
            }
            catch
            {
                /* best effort */
            }

            throw;
        }
    }

    private Task<bool> LessonBelongsToRouteAsync(
        Guid courseId, Guid moduleId, Guid lessonId, CancellationToken ct) =>
        _dbContext.Lessons.AsNoTracking().AnyAsync(lesson =>
            lesson.Id == lessonId
            && lesson.ModuleId == moduleId
            && lesson.Module.CourseId == courseId, ct);
}
