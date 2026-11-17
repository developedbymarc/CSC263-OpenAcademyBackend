using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OpenAcademy.Constants;
using OpenAcademy.Data;
using OpenAcademy.Entities;
using OpenAcademy.Entities.Courses;
using OpenAcademy.Responses;
using OpenAcademy.Services.ExternalServices.MediaService;

namespace OpenAcademy.Controllers;

[ApiController]
[Route("[controller]")]
public sealed class CoursesController(
    ApplicationDbContext dbContext,
    IMediaService mediaService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ApiResponse<CourseCatalogPageDto>>> Browse(
        [FromQuery, StringLength(128)] string? title,
        [FromQuery, StringLength(256)] string? category,
        [FromQuery, Range(1, 1_000_000)] int page = 1,
        [FromQuery, Range(1, 100)] int pageSize = 20,
        CancellationToken ct = default)
    {
        var query = dbContext.Courses.AsNoTracking()
            .Where(course => course.Status == CourseStatus.Published);

        if (!string.IsNullOrWhiteSpace(title))
        {
            var titleSearch = title.Trim();
            query = query.Where(course => EF.Functions.ILike(course.Title, $"%{titleSearch}%"));
        }

        if (!string.IsNullOrWhiteSpace(category))
        {
            var categorySearch = category.Trim().ToLower();
            query = query.Where(course => course.Categories.Any(courseCategory =>
                courseCategory.Name.ToLower() == categorySearch
                || courseCategory.Slug.ToLower() == categorySearch));
        }

        var totalCount = await query.CountAsync(ct);
        var courses = await query
            .OrderBy(course => course.Title)
            .ThenBy(course => course.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Include(course => course.Categories)
            .ToListAsync(ct);

        var items = courses.Select(course => new CourseCatalogItemDto(
            course.Id,
            course.Title,
            course.Description,
            string.IsNullOrWhiteSpace(course.ThumbnailBlobPath)
                ? null
                : mediaService.GetPublicUrl(course.ThumbnailBlobPath),
            course.Categories
                .OrderBy(courseCategory => courseCategory.Name)
                .Select(courseCategory => new CourseCategoryDto(
                    courseCategory.Id, courseCategory.Name, courseCategory.Slug))
                .ToArray())).ToArray();

        return Ok(ApiResponse<CourseCatalogPageDto>.Succeeded(
            new CourseCatalogPageDto(items, page, pageSize, totalCount)));
    }

    [HttpPost]
    [Authorize(Roles = RoleNames.Instructor)]
    public async Task<ActionResult<ApiResponse<CourseDto>>> Create(
        [FromBody] CreateCourseRequest request, CancellationToken ct)
    {
        if (User.IsInRole(RoleNames.Root) || !TryGetCurrentUserId(out var instructorId))
            return StatusCode(
                StatusCodes.Status403Forbidden,
                ApiResponse.Failed(message: "Forbidden")
            );

        var instructorExists = await dbContext.InstructorProfiles
            .AnyAsync(profile => profile.UserId == instructorId, ct);
        if (!instructorExists)
            return StatusCode(
                StatusCodes.Status403Forbidden,
                ApiResponse.Failed(message: "Forbidden")
            );

        var categoryIds = (request.CategoryIds ?? []).Distinct().ToArray();
        var categories = await dbContext.Categories
            .Where(category => ((IEnumerable<int>)categoryIds).Contains(category.Id))
            .ToListAsync(ct);
        if (categories.Count != categoryIds.Length)
            return BadRequest(ApiResponse.Failed(
                "One or more categories were not found.", "CATEGORY_NOT_FOUND"));

        var course = new Course
        {
            InstructorId = instructorId,
            Title = request.Title,
            Description = request.Description,
            ThumbnailBlobPath = string.Empty,
            Status = CourseStatus.Draft
        };
        foreach (var category in categories)
            course.Categories.Add(category);

        dbContext.Courses.Add(course);
        await dbContext.SaveChangesAsync(ct);

        return StatusCode(StatusCodes.Status201Created,
            ApiResponse<CourseDto>.Succeeded(CourseDto.From(course)));
    }

    [HttpPost("{courseId:guid}/enrollments")]
    [Authorize(Policy = PolicyNames.CanEnrollInCourse)]
    public async Task<ActionResult<ApiResponse<EnrollmentDto>>> Enroll(
        Guid courseId, CancellationToken ct)
    {
        if (!TryGetCurrentUserId(out var studentId))
            return StatusCode(
                StatusCodes.Status403Forbidden,
                ApiResponse.Failed(message: "Forbidden")
            );

        var studentExists = await dbContext.StudentProfiles
            .AnyAsync(profile => profile.UserId == studentId, ct);
        if (!studentExists)
            return StatusCode(
                StatusCodes.Status403Forbidden,
                ApiResponse.Failed(message: "Forbidden")
            );

        var enrollment = await dbContext.Enrollments
            .FirstOrDefaultAsync(e => e.CourseId == courseId && e.StudentId == studentId, ct);
        var wasAlreadyActive = enrollment?.Status == EnrollmentStatus.Active;

        if (enrollment is null)
        {
            enrollment = new Enrollment
            {
                CourseId = courseId,
                StudentId = studentId,
                EnrollmentDate = DateTimeOffset.UtcNow,
                Status = EnrollmentStatus.Active
            };
            dbContext.Enrollments.Add(enrollment);
        }
        else if (!wasAlreadyActive)
        {
            enrollment.EnrollmentDate = DateTimeOffset.UtcNow;
            enrollment.Status = EnrollmentStatus.Active;
        }

        if (!wasAlreadyActive)
            await dbContext.SaveChangesAsync(ct);

        var result = ApiResponse<EnrollmentDto>.Succeeded(EnrollmentDto.From(enrollment));
        return wasAlreadyActive ? Ok(result) : StatusCode(StatusCodes.Status201Created, result);
    }

    private bool TryGetCurrentUserId(out Guid userId)
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return Guid.TryParse(raw, out userId);
    }
}
